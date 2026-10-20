using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The game states, their transitions and timers (spec section 4, P-3 to P-6, P-11). Every transition sets the state
    /// and its entry time, then publishes; <see cref="CalibrationRun"/> and <see cref="StateEntryActions"/> only run its
    /// bookkeeping and side effects. Not re-entrant: <see cref="GameplayService"/> runs each call to completion (P-7).
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

        private GameSession _session;           // null while no game is in progress (the lobby states)
        private DifficultyProfile _preset;      // the settings' default difficulty (P-11)
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
            _preset = ReadPreset();
            BeginState(GameState.Attract);
            _publisher.Seed(BuildSnapshot());
        }

        public GameState State { get; private set; }

        /// <summary>Polls presence (the P-3 backstop), then makes at most one transition.</summary>
        public void Tick()
        {
            double now = _clock.Now;
            _presence.Observe(_input.IsPlayerPresent, now);

            switch (State)
            {
                case GameState.Attract:
                    TickAttract();
                    break;
                case GameState.Join:
                    TickJoin(now);
                    break;
                case GameState.ModeSelect:
                    TickModeSelect(now);
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
            _session.Current.Profile = profile;
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

            _session.Current.Profile = DefaultProfiles.For(_tuning);
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
                _scoredMark = _session.ScoreRoll(result);
                State = GameState.Scoring;
                _enteredAt = _clock.Now;
                var outcome = new RollOutcome(_session.CurrentPlayer, result, _scoredMark);
                _publisher.PublishScoredRoll(SnapshotBuilder.InGame(GameState.Scoring, _session), outcome);
            }
        }

        /// <summary>Starts a new game in ModeSelect. The service has already validated the arguments (P-6).</summary>
        public void SelectMode(int playerCount, int frameCount, Difficulty difficulty)
        {
            if (State != GameState.ModeSelect)
            {
                Ignore(nameof(SelectMode));
                return;
            }

            StartGame(playerCount, frameCount, _difficulties.For(difficulty));
        }

        // No state takes these yet, so each is ignored with the P-6 log.
        public void Pause() => Ignore(nameof(Pause));

        public void Resume() => Ignore(nameof(Resume));

        public void Abandon() => Ignore(nameof(Abandon));

        // A transition: the state, its entry time and on-enter actions, then the R21 events.
        private void Enter(GameState state)
        {
            BeginState(state);
            _publisher.PublishState(BuildSnapshot());
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
                    _session = null;
                    break;
                case GameState.ModeSelect:
                    _preset = ReadPreset();
                    break;
                case GameState.Calibrate:
                    _calibration.Restart(_presence.IsPresent);
                    break;
                case GameState.AwaitReady:
                    _entry.EnterAwaitReady(_session);
                    break;
            }
        }

        private void TickAttract()
        {
            if (_presence.IsPresent)
            {
                Enter(GameState.Join);
            }
        }

        private void TickJoin(double now)
        {
            if (!_presence.IsPresent)
            {
                Enter(GameState.Attract);
            }
            else if (Elapsed(now) >= _tuning.JoinGreeting)
            {
                Enter(GameState.ModeSelect);
            }
        }

        // Absence wins over the timeout; the timeout auto-selects the preset only with the player present.
        private void TickModeSelect(double now)
        {
            if (_presence.AbsentFor(now) >= _tuning.ModeSelectAbsent)
            {
                Enter(GameState.Attract);
            }
            else if (_presence.IsPresent && Elapsed(now) >= _tuning.ModeSelectTimeout)
            {
                StartGame(_tuning.PresetPlayerCount, _tuning.PresetFrameCount, _preset);
            }
        }

        private void TickScoring(double now)
        {
            bool replay = _scoredMark == MarkKind.Strike || _scoredMark == MarkKind.Spare;
            if (Elapsed(now) < (replay ? _tuning.ScoringHoldReplay : _tuning.ScoringHold))
            {
                return;
            }

            if (_session.IsOver)
            {
                Enter(GameState.GameOver);      // without Advance, which throws once every card is complete
                return;
            }

            _session.Advance();
            Enter(GameState.AwaitReady);
        }

        private void StartGame(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            _session = new GameSession(playerCount, frameCount, difficulty);
            Enter(GameState.Calibrate);
        }

        private GameSnapshot BuildSnapshot() => _session != null
            ? SnapshotBuilder.InGame(State, _session)
            : SnapshotBuilder.Lobby(State, _tuning.PresetFrameCount, _preset);

        private DifficultyProfile ReadPreset() => _difficulties.For(_settings.Load().DefaultDifficulty);

        private double Elapsed(double now) => now - _enteredAt;

        // P-6: a command where it doesn't apply.
        private void Ignore(string command) => _log.Info(LogCategory, command + " ignored in " + State);
    }
}
