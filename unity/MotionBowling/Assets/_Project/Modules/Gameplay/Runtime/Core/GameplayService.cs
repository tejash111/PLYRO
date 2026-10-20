using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The Gameplay facade: <see cref="IGameEvents"/> for the UI and Presentation, <see cref="IGameCommands"/> for the
    /// UI and the operator panel, and <see cref="Tick"/> for <c>GameplayDriver</c>. Every entry point (Tick, the four
    /// commands and the dependency event handlers) runs to completion (P-7): a call that arrives while another is
    /// running, such as a command from a StateChanged subscriber, is queued and runs after it, in call order.
    /// </summary>
    public sealed class GameplayService : IGameEvents, IGameCommands
    {
        private readonly GamePublisher _publisher;
        private readonly GameStateMachine _machine;
        private readonly Queue<Action> _queued = new Queue<Action>();
        private bool _running;

        // The machine's entry points as delegates, made once, so a call that runs directly allocates nothing.
        private readonly Action _tick;
        private readonly Action<(int Players, int Frames, Difficulty Difficulty)> _selectMode;
        private readonly Action _pause;
        private readonly Action _resume;
        private readonly Action _abandon;
        private readonly Action<bool> _presenceChanged;
        private readonly Action<PlayerProfile> _calibrationCompleted;
        private readonly Action<CalibrationFailure> _calibrationFailed;
        private readonly Action<SwingPhase> _phaseChanged;
        private readonly Action<ThrowEvent> _thrown;
        private readonly Action<ThrowRejection> _rejected;
        private readonly Action<RollResult> _rollCompleted;

        /// <summary>Starts in Attract without raising anything (P-11). Throws ArgumentNullException for a null argument.</summary>
        public GameplayService(IPlayerInput input, IPlayerCalibrator calibrator, IThrowSource throwSource,
            IRollSimulator rollSimulator, ILeaderboardStore leaderboard, ISettingsStore settings, IClock clock,
            IGameLog log, Func<DateTime> utcNow, GameplayTuning tuning, DifficultyCatalog difficulties)
        {
            Require(input, nameof(input));
            Require(calibrator, nameof(calibrator));
            Require(throwSource, nameof(throwSource));
            Require(rollSimulator, nameof(rollSimulator));
            Require(leaderboard, nameof(leaderboard));
            Require(settings, nameof(settings));
            Require(clock, nameof(clock));
            Require(log, nameof(log));
            Require(utcNow, nameof(utcNow));
            Require(tuning, nameof(tuning));
            Require(difficulties, nameof(difficulties));

            _publisher = new GamePublisher(log);
            var leaderboardWriter = new LeaderboardWriter(leaderboard, log, utcNow);
            _machine = new GameStateMachine(input, calibrator, throwSource, rollSimulator, settings, clock, log, tuning,
                difficulties, _publisher, leaderboardWriter);

            _tick = _machine.Tick;
            _selectMode = mode => _machine.SelectMode(mode.Players, mode.Frames, mode.Difficulty);
            _pause = _machine.Pause;
            _resume = _machine.Resume;
            _abandon = _machine.Abandon;
            _presenceChanged = _machine.OnPresenceChanged;
            _calibrationCompleted = _machine.OnCalibrationCompleted;
            _calibrationFailed = _machine.OnCalibrationFailed;
            _phaseChanged = _machine.OnPhaseChanged;
            _thrown = _machine.OnThrown;
            _rejected = _machine.OnRejected;
            _rollCompleted = _machine.OnRollCompleted;

            input.PresenceChanged += OnPresenceChanged;
            calibrator.Completed += OnCalibrationCompleted;
            calibrator.Failed += OnCalibrationFailed;
            throwSource.PhaseChanged += OnPhaseChanged;
            throwSource.Thrown += OnThrown;
            throwSource.Rejected += OnRejected;
            rollSimulator.Completed += OnRollCompleted;
        }

        /// <summary>The latest snapshot. Never null: the constructor seeds it.</summary>
        public GameSnapshot Current => _publisher.Current;

        public event Action<GameState> StateChanged
        {
            add => _publisher.StateChanged += value;
            remove => _publisher.StateChanged -= value;
        }

        public event Action<GameSnapshot> SnapshotChanged
        {
            add => _publisher.SnapshotChanged += value;
            remove => _publisher.SnapshotChanged -= value;
        }

        public event Action<RollOutcome> RollScored
        {
            add => _publisher.RollScored += value;
            remove => _publisher.RollScored -= value;
        }

        /// <summary>Advances timers and polls presence. Called once per frame by <c>GameplayDriver</c>.</summary>
        public void Tick() => Run(_tick);

        /// <summary>
        /// Validates first, in any state (P-6): ArgumentOutOfRangeException unless there are 1 to 4 players, 3, 5 or 10
        /// frames and a defined difficulty, even when the call is then queued or ignored.
        /// </summary>
        public void SelectMode(int playerCount, int frameCount, Difficulty difficulty)
        {
            GameSession.ValidateMode(playerCount, frameCount);
            if (!Enum.IsDefined(typeof(Difficulty), difficulty))
            {
                throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Not a defined difficulty level.");
            }

            Run(_selectMode, (playerCount, frameCount, difficulty));
        }

        public void Pause() => Run(_pause);

        public void Resume() => Run(_resume);

        public void Abandon() => Run(_abandon);

        // The dependency event handlers. Each one goes through the gate like a command.
        private void OnPresenceChanged(bool present) => Run(_presenceChanged, present);

        private void OnCalibrationCompleted(PlayerProfile profile) => Run(_calibrationCompleted, profile);

        private void OnCalibrationFailed(CalibrationFailure reason) => Run(_calibrationFailed, reason);

        private void OnPhaseChanged(SwingPhase phase) => Run(_phaseChanged, phase);

        private void OnThrown(ThrowEvent throwEvent) => Run(_thrown, throwEvent);

        private void OnRejected(ThrowRejection reason) => Run(_rejected, reason);

        private void OnRollCompleted(RollResult result) => Run(_rollCompleted, result);

        // The run-to-completion gate (P-7). If a body throws, the calls it queued are dropped and the gate reopens.
        private void Run(Action body)
        {
            if (_running)
            {
                _queued.Enqueue(body);
                return;
            }

            _running = true;
            try
            {
                body();
                RunQueued();
            }
            finally
            {
                _running = false;
                _queued.Clear();
            }
        }

        // The same gate for a body with an argument. Only a queued call binds the argument into a new delegate.
        private void Run<T>(Action<T> body, T argument)
        {
            if (_running)
            {
                _queued.Enqueue(Bind(body, argument));
                return;
            }

            _running = true;
            try
            {
                body(argument);
                RunQueued();
            }
            finally
            {
                _running = false;
                _queued.Clear();
            }
        }

        private void RunQueued()
        {
            while (_queued.Count > 0)
            {
                _queued.Dequeue()();
            }
        }

        // Its own method on purpose: a lambda inside Run<T> would make the compiler allocate the closure on every call,
        // queued or not, because captured parameters live in a closure object created at method entry.
        private static Action Bind<T>(Action<T> body, T argument)
        {
            return () => body(argument);
        }

        private static void Require(object argument, string parameterName)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        // Unity's == for the assets, so a destroyed asset counts as null too.
        private static void Require(UnityEngine.Object argument, string parameterName)
        {
            if (argument == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }
    }
}
