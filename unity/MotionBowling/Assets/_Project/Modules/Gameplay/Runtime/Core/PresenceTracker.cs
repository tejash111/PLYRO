namespace Bowling.Gameplay
{
    /// <summary>
    /// Whether the player is present, and since when they have been absent (P-3). "Absent for X" means continuous
    /// absence of at least X, counted from the moment presence last became false. It carries across state changes,
    /// and presence returning resets it.
    /// </summary>
    internal sealed class PresenceTracker
    {
        private double _changedAt;      // when IsPresent last changed (construction counts); while absent, when it went false

        public PresenceTracker(bool present, double now)
        {
            IsPresent = present;
            _changedAt = now;
        }

        public bool IsPresent { get; private set; }

        /// <summary>
        /// Takes in the latest presence (from PresenceChanged, or a poll). The time is recorded only when the value
        /// changes, so polling the same value again keeps the original moment.
        /// </summary>
        public void Observe(bool present, double now)
        {
            if (present == IsPresent)
            {
                return;
            }

            IsPresent = present;
            _changedAt = now;
        }

        /// <summary>Seconds of continuous absence at <paramref name="now"/>: 0 while present.</summary>
        public double AbsentFor(double now)
        {
            return IsPresent ? 0.0 : now - _changedAt;
        }
    }
}
