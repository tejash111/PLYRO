using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Owns <see cref="Current"/> and the three game events, and keeps the R21/R22 order: <c>Current</c> is
    /// assigned before anything is raised, then StateChanged, [RollScored], SnapshotChanged. Each subscriber runs in
    /// its own try/catch (P-8), so one that throws is logged and the others still run. Not an
    /// <see cref="IGameEvents"/> itself: GameplayService forwards its add/remove accessors to these events.
    /// </summary>
    internal sealed class GamePublisher
    {
        private const string LogCategory = "Gameplay";

        private readonly IGameLog _log;

        public GamePublisher(IGameLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>The latest published snapshot. Null until <see cref="Seed"/>.</summary>
        public GameSnapshot Current { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<GameSnapshot> SnapshotChanged;
        public event Action<RollOutcome> RollScored;

        /// <summary>Sets <see cref="Current"/> without raising anything (the first snapshot, before anyone listens).</summary>
        public void Seed(GameSnapshot snapshot)
        {
            Current = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        /// <summary>A transition: assigns <c>Current</c>, then raises StateChanged, then SnapshotChanged.</summary>
        public void PublishState(GameSnapshot snapshot)
        {
            Seed(snapshot);
            Raise(StateChanged, snapshot.State, nameof(StateChanged));
            Raise(SnapshotChanged, snapshot, nameof(SnapshotChanged));
        }

        /// <summary>
        /// A scored roll (R22): assigns <c>Current</c>, then raises StateChanged, then RollScored, then
        /// SnapshotChanged, so a RollScored handler already sees the new snapshot.
        /// </summary>
        public void PublishScoredRoll(GameSnapshot snapshot, RollOutcome outcome)
        {
            Seed(snapshot);
            Raise(StateChanged, snapshot.State, nameof(StateChanged));
            Raise(RollScored, outcome, nameof(RollScored));
            Raise(SnapshotChanged, snapshot, nameof(SnapshotChanged));
        }

        // Calls each subscriber on its own, so an exception in one is logged and never reaches the rest or the
        // caller. Walking the invocation list allocates an array per raise, which is fine: raises happen only on change.
        private void Raise<T>(Action<T> handlers, T argument, string eventName)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)subscriber)(argument);
                }
                catch (Exception exception)
                {
                    _log.Error(LogCategory, eventName + " subscriber failed", exception);
                }
            }
        }
    }
}
