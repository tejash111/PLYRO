using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The game states, their on-enter actions and their timers (spec section 4, P-3 to P-6, P-11). Every transition
    /// goes through <see cref="Enter"/>, which publishes the new snapshot. Not re-entrant on its own:
    /// <see cref="GameplayService"/> runs every call to it to completion (P-7).
    /// </summary>
    internal sealed class GameStateMachine
    {
        private const string LogCategory = "Gameplay";
        private const int MaxCalibrationTimeouts = 2;   // the second Timeout in one Calibrate gives up (P-4)

        private readonly IPlayerInput _input;
        private readonly IPlayerCalibrator _calibrator;
        private readonly IThrowSource _throwSource;
        private readonly IRollSimulator _sim;
        private readonly ISettingsStore _settings;
        private readonly IClock _clock;
        private readonly IGameLog _log;
        private readonly GameplayTuning _tuning;
        private readonly DifficultyCatalog _difficulties;
        private readonly GamePublisher _publisher;
        private readonly PresenceTracker _presence;

        private GameSession _session;           // null while no game is in progress (the lobby states)
        private DifficultyProfile _preset;      // the settings' default difficulty (P-11)
        private double _enteredAt;              // clock time at which State was entered
        private int _calibrationTimeouts;       // Failed(Timeout) events since Calibrate was entered (P-4)
        private bool _calibrationRunning;       // Begin was called and no Completed or Failed has come since (P-4)

        /// <summary>
        /// Enters Attract quietly (P-11): runs its on-enter actions and seeds <c>Current</c>, raising nothing.
        /// <see cref="GameplayService"/> has already null-checked every argument.
        /// </summary>
        public GameStateMachine(IPlayerInput input, IPlayerCalibrator calibrator, IThrowSource throwSource,
            IRollSimulator rollSimulator, ISettingsStore settings, IClock clock, IGameLog log,
            GameplayTuning tuning, DifficultyCatalog difficulties, GamePublisher publisher)
        {
            _input = input;
            _calibrator = calibrator;
            _throwSource = throwSource;
            _sim = rollSimulator;
            _settings = settings;
            _clock = clock;
            _log = log;
            _tuning = tuning;
            _difficulties = difficulties;
            _publisher = publisher;

            _presence = new PresenceTracker(input.IsPlayerPresent, clock.Now);
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
                    TickCalibrate();
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

            _calibrationRunning = false;
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

            _calibrationRunning = false;
            if (reason != CalibrationFailure.Timeout)
            {
                return;
            }

            _calibrationTimeouts++;
            if (_calibrationTimeouts < MaxCalibrationTimeouts)
            {
                BeginCalibrationIfPresent();
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
                    OnEnterAttract();
                    break;
                case GameState.ModeSelect:
                    _preset = ReadPreset();
                    break;
                case GameState.Calibrate:
                    OnEnterCalibrate();
                    break;
                case GameState.AwaitReady:
                    OnEnterAwaitReady();
                    break;
            }
        }

        // The Attract guard: SetBumpers and SetRack both throw while a roll is in progress. Attract drops the session.
        private void OnEnterAttract()
        {
            _throwSource.Disarm();
            if (!_sim.IsRolling)
            {
                _sim.SetBumpers(false);
                _sim.SetRack(PinMask.All);
            }

            _calibrator.Cancel();
            _session = null;
        }

        // P-4: every entry starts counting again, then Begin runs only if the player is present.
        private void OnEnterCalibrate()
        {
            _calibrationTimeouts = 0;
            _calibrationRunning = false;
            BeginCalibrationIfPresent();
        }

        // The rack and bumpers for this ball, then the throw source armed with the player's profile (or the default).
        private void OnEnterAwaitReady()
        {
            _sim.SetBumpers(_session.Difficulty.Bumpers);
            _sim.SetRack(_session.Rack);
            _throwSource.Arm(new ThrowContext(_session.Current.Profile ?? DefaultProfiles.For(_tuning), _session.Difficulty));
        }

        private void BeginCalibrationIfPresent()
        {
            if (_presence.IsPresent)
            {
                _calibrationRunning = true;
                _calibrator.Begin();
            }
        }

        // P-4: the player is here and no calibration is running. That covers entering while absent and the end of a
        // PlayerLeft or TrackingLost failure.
        private void TickCalibrate()
        {
            if (!_calibrationRunning)
            {
                BeginCalibrationIfPresent();
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

        private void StartGame(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            _session = new GameSession(playerCount, frameCount, difficulty);
            Enter(GameState.Calibrate);
        }

        private GameSnapshot BuildSnapshot()
        {
            return _session != null
                ? SnapshotBuilder.InGame(State, _session)
                : SnapshotBuilder.Lobby(State, _tuning.PresetFrameCount, _preset);
        }

        private DifficultyProfile ReadPreset() => _difficulties.For(_settings.Load().DefaultDifficulty);

        private double Elapsed(double now) => now - _enteredAt;

        // P-6: a command where it doesn't apply.
        private void Ignore(string command) => _log.Info(LogCategory, command + " ignored in " + State);
    }
}
