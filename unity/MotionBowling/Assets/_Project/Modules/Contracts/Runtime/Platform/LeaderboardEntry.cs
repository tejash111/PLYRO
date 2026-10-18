using System;

namespace Bowling.Contracts
{
    public sealed class LeaderboardEntry
    {
        public string DisplayName;
        public int Score;
        public int FrameCount;
        public DateTime PlayedAtUtc;
    }
}
