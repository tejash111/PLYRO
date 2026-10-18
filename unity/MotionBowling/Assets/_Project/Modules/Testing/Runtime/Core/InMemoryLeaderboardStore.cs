using System;
using System.Collections.Generic;
using System.Linq;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An ILeaderboardStore that keeps entries in memory. "Today" is decided by calendar date in the zone
    // the test passes in, using the clock the test passes in, so it never depends on the machine's time.
    //
    // Submit stores a copy and Top returns copies, so a test can reuse one LeaderboardEntry object.
    public sealed class InMemoryLeaderboardStore : ILeaderboardStore
    {
        private readonly Func<DateTime> _utcNow;
        private readonly TimeZoneInfo _zone;
        private readonly List<LeaderboardEntry> _entries = new List<LeaderboardEntry>();

        public InMemoryLeaderboardStore(Func<DateTime> utcNow, TimeZoneInfo zone)
        {
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        }

        // Entries of this frame count, best score first, earlier PlayedAtUtc first on equal scores (then
        // submission order). Today keeps entries whose local date in the zone is the local date of utcNow().
        // A max of 0 returns nothing; a negative max throws ArgumentOutOfRangeException.
        public IReadOnlyList<LeaderboardEntry> Top(int frameCount, LeaderboardScope scope, int max)
        {
            if (max < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be zero or more.");
            }

            DateTime today = LocalDate(_utcNow());
            return _entries
                .Where(e => e.FrameCount == frameCount)
                .Where(e => scope == LeaderboardScope.AllTime || LocalDate(e.PlayedAtUtc) == today)
                .OrderByDescending(e => e.Score)
                .ThenBy(e => e.PlayedAtUtc)
                .Take(max)
                .Select(Copy)
                .ToList();
        }

        public void Submit(LeaderboardEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            _entries.Add(Copy(entry));
        }

        public void Reset()
        {
            _entries.Clear();
        }

        // An Unspecified kind counts as UTC. A Local kind is converted first, because TimeZoneInfo rejects it.
        private DateTime LocalDate(DateTime utc)
        {
            DateTime asUtc = utc.Kind == DateTimeKind.Local
                ? utc.ToUniversalTime()
                : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(asUtc, _zone).Date;
        }

        private static LeaderboardEntry Copy(LeaderboardEntry entry)
        {
            return new LeaderboardEntry
            {
                DisplayName = entry.DisplayName,
                Score = entry.Score,
                FrameCount = entry.FrameCount,
                PlayedAtUtc = entry.PlayedAtUtc,
            };
        }
    }
}
