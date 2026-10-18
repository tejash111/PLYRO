using System.Collections.Generic;

namespace Bowling.Contracts
{
    public sealed class GameSnapshot
    {
        public GameState State;
        public IReadOnlyList<PlayerSnapshot> Players;
        public int CurrentPlayer;                        // index into Players
        public int CurrentFrame;                         // 1-based
        public int CurrentBall;                          // 1..3
        public int FrameCount;                           // 3, 5 or 10
        public DifficultyProfile Difficulty;
    }
}
