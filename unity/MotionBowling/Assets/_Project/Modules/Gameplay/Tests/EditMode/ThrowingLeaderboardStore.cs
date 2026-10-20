using System;
using System.Collections.Generic;
using Bowling.Contracts;
using Bowling.Testing;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// An <see cref="ILeaderboardStore"/> whose <c>Submit</c> throws for one display name and keeps every other entry
    /// in an <see cref="InMemoryLeaderboardStore"/> on <see cref="GameplayRig.UtcNow"/>, for the P-12 failure tests.
    /// </summary>
    internal sealed class ThrowingLeaderboardStore : ILeaderboardStore
    {
        private readonly string _failFor;
        private readonly InMemoryLeaderboardStore _inner =
            new InMemoryLeaderboardStore(() => GameplayRig.UtcNow, TimeZoneInfo.Utc);

        public ThrowingLeaderboardStore(string failFor)
        {
            _failFor = failFor;
        }

        /// <summary>The exception the last failing <c>Submit</c> threw. Null until one fails.</summary>
        public Exception Thrown { get; private set; }

        public IReadOnlyList<LeaderboardEntry> Top(int frameCount, LeaderboardScope scope, int max)
        {
            return _inner.Top(frameCount, scope, max);
        }

        public void Submit(LeaderboardEntry entry)
        {
            if (entry.DisplayName == _failFor)
            {
                Thrown = new InvalidOperationException("The store refused " + entry.DisplayName + ".");
                throw Thrown;
            }

            _inner.Submit(entry);
        }

        public void Reset()
        {
            _inner.Reset();
        }
    }
}
