using System.Collections.Generic;

namespace Bowling.Contracts
{
    public interface ILeaderboardStore
    {
        IReadOnlyList<LeaderboardEntry> Top(int frameCount, LeaderboardScope scope, int max);
        void Submit(LeaderboardEntry entry);
        void Reset();
    }
}
