using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The game states, their transitions and timers (spec section 4, P-3 to P-6, P-11). It hosts the per-phase flows
    /// (<see cref="LobbyFlow"/>) and dispatches to them, and runs the Calibrate, ball and Scoring loop itself. Every
    /// transition sets the state and its entry time, then publishes; <see cref="CalibrationRun"/> and
    /// <see cref="StateEntryActions"/> only run its bookkeeping and side effects. Not re-entrant:
    /// <see cref="GameplayService"/> runs each call to completion (P-7).
    /// </summary>
    internal sealed class GameStateMachine
    {
        private const string LogCategory = "Gameplay";

        private readonly IPlayerInput _input;
        private readonly IRollSimulator _sim;
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

        private double _enteredAt;              // clock time at which State was entered
        private MarkKind _scoredMark;           // the scored mark: a Strike or Spare holds Scoring longer, for the replay

        /// <summary>
        /// Enters Attract quietly (P-11): runs its on-enter actions and seeds <c>Current</c>, raising nothing.
        /// <see cref="GameplayService"/> has already null-checked every argument.
        /// </summary>
        public GameStateMachine(IPlayerInput input, IPlayerCalibrator calibrator, IThrowSource throwSource,
            IRollSimulator rollSimulator, ISettingsStore settings, IClock clock, IGameLog log,
            GameplayTuning tuning, DifficultyCatalog difficulties, GamePublisher publisher)
        {
            _input = input;
            _sim = rollSimulator;
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
            Preset = ReadPreset();
            BeginState(GameState.Attract);
            _publisher.Seed(BuildSnapshot());
        }

        public GameState State { get; private set; }

        /// <summary>The game in progress, for the flows. Null in the lobby states.</summary>
        internal GameSession Session { get; private set; }

        /// <summary>The settings' default difficulty (P-11), for the flows.</summary>
        internal DifficultyProfile Preset { get; private set; }

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
            }
        }

        public void OnPresenceChanged(bool present) => _presence.Observe(present, _clock.Now);

        /// <summary>Calibrate only: the profile goes to the current player, then the game is ready for the first ball.</summary>
        public void OnCalibrationCompleted(PlayerProfile profile)
        {
            if (State != GameState.Calibrate)
            {
                return;
            }

            _calibration.Completed();
            Session.Current.Profile = profile;
            Enter(GameState.AwaitReady);
        }

        /// <summary>
        /// Calibrate only (P-4). Only a Timeout counts: the first one starts over, the second falls back to the default
        /// profile. PlayerLeft and TrackingLost just end the calibration, and the next Tick with the player present begins
        /// another.
        /// </summary>
        public void OnCalibrationFailed(CalibrationFailure reason)
        {
            if (State != GameState.Calibrate)
            {
                return;
            }

            if (!_calibration.Failed(reason, _presence.IsPresent))
            {
                return;
            }

            Session.Current.Profile = DefaultProfiles.For(_tuning);
            Enter(GameState.AwaitReady);
        }

        /// <summary>
        /// AwaitReady only: the source has left its waiting phase and the swing is under way. Every other phase is
        /// ignored, silently: <c>Arm</c> and <c>Disarm</c> raise <c>PhaseChanged</c> too, and it reaches this method
        /// right after the call that caused it (P-7).
        /// </summary>
        public void OnPhaseChanged(SwingPhase phase)
        {
            if (State != GameState.AwaitReady)
            {
                return;
            }

            if (phase == SwingPhase.Ready || phase == SwingPhase.Backswing || phase == SwingPhase.ForwardSwing)
            {
                Enter(GameState.AwaitThrow);
            }
        }

        /// <summary>
        /// AwaitThrow, or AwaitReady when the source passed Ready within one frame (P-10): rolls the throw as it came,
        /// camera-clock timestamp included, and enters Rolling.
        /// </summary>
        public void OnThrown(ThrowEvent throwEvent)
        {
            if (State == GameState.AwaitThrow || State == GameState.AwaitReady)
            {
                _sim.Roll(throwEvent);
                Enter(GameState.Rolling);
            }
        }

        /// <summary>
        /// AwaitThrow only: the swing was aborted, so AwaitReady is entered again, which re-arms without using the ball.
        /// AwaitReady ignores it: the source is already waiting (P-10).
        /// </summary>
        public void OnRejected(ThrowRejection reason)
        {
            if (State == GameState.AwaitThrow)
            {
                Enter(GameState.AwaitReady);
            }
        }

        /// <summary>
        /// Rolling only: scores the result, then enters Scoring without <see cref="Enter"/>, because a scored roll
        /// publishes its own event order (R22). The snapshot still names the roll just scored (P-11).
        /// </summary>
        public void OnRollCompleted(RollResult result)
        {
            if (State == GameState.Rolling)
            {
                _scoredMark = Session.ScoreRoll(result);
                State = GameState.Scoring;
                _enteredAt = _clock.Now;
                var outcome = new RollOutcome(Session.CurrentPlayer, result, _scoredMark);
                _publisher.PublishScoredRoll(SnapshotBuilder.InGame(GameState.Scoring, Session), outcome);
            }
        }

        /// <summary>
        /// ModeSelect hands the choice to <see cref="LobbyFlow"/>; every other state ignores it (P-6). The service has
        /// already validated the arguments.
        /// </summary>
        public void SelectMode(int playerCount, int frameCount, Difficulty difficulty)
        {
            if (State != GameState.ModeSelect)
            {
                Ignore(nameof(SelectMode));
                return;
            }

            _lobby.SelectMode(playerCount, frameCount, _difficulties.For(difficulty));
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

        /// <summary>A new game of the given mode, which starts with the first player's Calibrate.</summary>
        internal void StartGame(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            Session = new GameSession(playerCount, frameCount, difficulty);
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
                    break;
                case GameState.ModeSelect:
                    Preset = ReadPreset();
                    break;
                case GameState.Calibrate:
                    _calibration.Restart(_presence.IsPresent);
                    break;
                case GameState.AwaitReady:
                    _entry.EnterAwaitReady(Session);
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

            Session.Advance();
            Enter(GameState.AwaitReady);
        }

        private GameSnapshot BuildSnapshot() => Session != null
            ? SnapshotBuilder.InGame(State, Session)
            : SnapshotBuilder.Lobby(State, _tuning.PresetFrameCount, Preset);

        private DifficultyProfile ReadPreset() => _difficulties.For(_settings.Load().DefaultDifficulty);

        // P-6: a command where it doesn't apply.
        private void Ignore(string command) => _log.Info(LogCategory, command + " ignored in " + State);
    }
}
