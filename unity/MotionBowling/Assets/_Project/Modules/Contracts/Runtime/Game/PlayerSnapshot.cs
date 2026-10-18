using System.Collections.Generic;

namespace Bowling.Contracts
{
    public sealed class PlayerSnapshot
    {
        public string DisplayName;                       // "Player 1".."Player 4"
        public int ColorIndex;                           // 0..3 → palette player colors
        public IReadOnlyList<FrameView> Frames;
        public int RunningTotal;                         // score of all frames whose score is final
    }
}
