using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The game states, their transitions and timers (spec section 4, P-3 to P-6, P-11). It hosts the per-phase flows
    /// (<see cref="LobbyFlow"/>, <see cref="TurnFlow"/>, <see cref="GameEndFlow"/>), and runs the Scoring hold and the
    /// turn changes itself. Every transition sets the state and its entry time, then publishes;
    /// <see cref="CalibrationRun"/> and <see cref="StateEntryActions"/> only run its bookkeeping and side effects. Not
    /// re-entrant: <see cref="GameplayService"/> runs each call to completion (P-7).
    /// </summary>
    internal sealed class GameStateMachine
    {
        private const string LogCategory = "Gameplay";

        private readonly IPlayerInput _input;
        private readonly ISettingsStore _settings;
        private readonly IClock _clock;
        private readonly IGameLog _log;
        private readonly GameplayTuning _tuning;
        private readonly DifficultyCatalog _difficulties;
        private readonly GamePublisher _publisher;
        private readonly PresenceTracker _presence;
        private readonly CalibrationRun _calibration;
        private readonly StateEntryActions _entry;
        private readonly LobbyFlow _lobby;
        private readonly GameEndFlow _end;
        private readonly TurnFlow _turn;

        private double _enteredAt;              // clock time at which State was entered
        private MarkKind _scoredMark;           // the scored mark: a Strike or Spare holds Scoring longer, for the replay

        /// <summary>
        /// Enters Attract quietly (P-11): runs its on-enter actions and seeds <c>Current</c>, raising nothing.
        /// <see cref="GameplayService"/> has already null-checked every argument.
        /// </summary>
        public GameStateMachine(IPlayerInput input, IPlayerCalibrator calibrator, IThrowSource throwSource,
            IRollSimulator rollSimulator, ISettingsStore settings, IClock clock, IGameLog log,
            GameplayTuning tuning, DifficultyCatalog difficulties, GamePublisher publisher, LeaderboardWriter leaderboard)
        {
            _input = input;
            _settings = settings;
            _clock = clock;
            _log = log;
            _tuning = tuning;
            _difficulties = difficulties;
            _publisher = publisher;

            _presence = new PresenceTracker(input.IsPlayerPresent, clock.Now);
            _calibration = new CalibrationRun(calibrator);
            _entry = new StateEntryActions(throwSource, rollSimulator, tuning);
            _lobby = new LobbyFlow(this, _presence, tuning);
            _end = new GameEndFlow(this, _presence, tuning, leaderboard);
            _turn = new TurnFlow(this, _presence, tuning, rollSimulator, _calibration);
            Preset = ReadPreset();
            BeginState(GameState.Attract);
            _publisher.Seed(BuildSnapshot());
        }

        public GameState State { get; private set; }

        /// <summary>The game in progress, for the flows. Null in the lobby states.</summary>
        internal GameSession Session { get; private set; }

        /// <summary>The settings' default difficulty (P-11), for the flows.</summary>
        internal DifficultyProfile Preset { get; private set; }

        /// <summary>This Calibrate is a turn change (P-4). Entering AwaitReady or Attract, and a new game, clear it.</summary>
        internal bool IsTurnChange { get; private set; }

        /// <summary>Polls presence (the P-3 backstop), then makes at most one transition.</summary>
        public void Tick()
        {
            double now = _clock.Now;
            _presence.Observe(_input.IsPlayerPresent, now);

            switch (State)
            {
                case GameState.Attract:
                case GameState.Join:
                case GameState.ModeSelect:
                    _lobby.Tick(now);
                    break;
                case GameState.Calibrate:
                    _calibration.BeginIfIdle(_presence.IsPresent);    // P-4
                    break;
                case GameState.Scoring:
                    TickScoring(now);
                    break;
                case GameState.GameOver:
                case GameState.Results:
                    _end.Tick(now);
                    break;
            }
        }

        public void OnPresenceChanged(bool present) => _presence.Observe(present, _clock.Now);

        // The dependency events of a turn, all handled by TurnFlow.
        public void OnCalibrationCompleted(PlayerProfile profile) => _turn.OnCalibrationCompleted(profile);

        public void OnCalibrationFailed(CalibrationFailure reason) => _turn.OnCalibrationFailed(reason);

        public void OnPhaseChanged(SwingPhase phase) => _turn.OnPhaseChanged(phase);

        public void OnThrown(ThrowEvent throwEvent) => _turn.OnThrown(throwEvent);

        public void OnRejected(ThrowRejection reason) => _turn.OnRejected(reason);

        public void OnRollCompleted(RollResult result) => _turn.OnRollCompleted(result);

        /// <summary>
        /// ModeSelect hands the choice to <see cref="LobbyFlow"/>, and Results ("play again") to <see cref="GameEndFlow"/>;
        /// every other state ignores it (P-6). The service has already validated the arguments.
        /// </summary>
        public void SelectMode(int playerCount, int frameCount, Difficulty difficulty)
        {
            switch (State)
            {
                case GameState.ModeSelect:
                    _lobby.SelectMode(playerCount, frameCount, _difficulties.For(difficulty));
                    break;
                case GameState.Results:
                    _end.PlayAgain(playerCount, frameCount, _difficulties.For(difficulty));
                    break;
                default:
                    Ignore(nameof(SelectMode));
                    break;
            }
        }

        // No state takes these yet, so each is ignored with the P-6 log.
        public void Pause() => Ignore(nameof(Pause));

        public void Resume() => Ignore(nameof(Resume));

        public void Abandon() => Ignore(nameof(Abandon));

        /// <summary>Seconds since <see cref="State"/> was entered, at <paramref name="now"/>.</summary>
        internal double TimeInState(double now) => now - _enteredAt;

        /// <summary>A transition: the state, its entry time and on-enter actions, then the R21 events.</summary>
        internal void Enter(GameState state)
        {
            BeginState(state);
            _publisher.PublishState(BuildSnapshot());
        }

        /// <summary>
        /// Enters Scoring without <see cref="Enter"/>, because a scored roll publishes its own event order (R22). The
        /// snapshot still names the roll just scored (P-11), and the outcome's mark sets the hold.
        /// </summary>
        internal void EnterScoring(RollOutcome outcome)
        {
            State = GameState.Scoring;
            _enteredAt = _clock.Now;
            _scoredMark = outcome.Mark;
            _publisher.PublishScoredRoll(SnapshotBuilder.InGame(GameState.Scoring, Session), outcome);
        }

        /// <summary>A new game of the given mode, which starts with the first player's Calibrate.</summary>
        internal void StartGame(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            Session = new GameSession(playerCount, frameCount, difficulty);
            IsTurnChange = false;
            Enter(GameState.Calibrate);
        }

        // Everything a transition does except publishing, which the constructor replaces with a quiet Seed.
        private void BeginState(GameState state)
        {
            State = state;
            _enteredAt = _clock.Now;

            switch (state)
            {
                case GameState.Attract:
                    // Disarm and the Attract guard, then the calibrator stops (P-6). Attract drops the session.
                    _entry.EnterAttract();
                    _calibration.Cancel();
                    Session = null;
                    IsTurnChange = false;
                    break;
                case GameState.ModeSelect:
                    Preset = ReadPreset();
                    break;
                case GameState.Calibrate:
                    _calibration.Restart(_presence.IsPresent);
                    break;
                case GameState.AwaitReady:
                    IsTurnChange = false;
                    _entry.EnterAwaitReady(Session);
                    break;
                case GameState.GameOver:
                    _end.EnterGameOver();
                    break;
            }
        }

        private void TickScoring(double now)
        {
            bool replay = _scoredMark == MarkKind.Strike || _scoredMark == MarkKind.Spare;
            if (TimeInState(now) < (replay ? _tuning.ScoringHoldReplay : _tuning.ScoringHold))
            {
                return;
            }

            if (Session.IsOver)
            {
                Enter(GameState.GameOver);      // without Advance, which throws once every card is complete
                return;
            }

            // A new turn with 2 or more players hands the lane over: the next player steps in and calibrates (P-4).
            IsTurnChange = Session.Advance() && Session.Players.Count > 1;
            Enter(IsTurnChange ? GameState.Calibrate : GameState.AwaitReady);
        }

        private GameSnapshot BuildSnapshot() => Session != null
            ? SnapshotBuilder.InGame(State, Session)
            : SnapshotBuilder.Lobby(State, _tuning.PresetFrameCount, Preset);

        private DifficultyProfile ReadPreset() => _difficulties.For(_settings.Load().DefaultDifficulty);

        /// <summary>P-6: a command where it doesn't apply, logged for the flows too.</summary>
        internal void Ignore(string command) => _log.Info(LogCategory, command + " ignored in " + State);
    }
}
