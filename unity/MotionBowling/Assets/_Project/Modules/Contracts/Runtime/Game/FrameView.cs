using System.Collections.Generic;

namespace Bowling.Contracts
{
    public sealed class FrameView
    {
        public int FrameNumber;                          // 1-based
        public IReadOnlyList<RollMark> Rolls;            // 0..3 entries
        public int? Cumulative;                          // null until the frame's score is final
    }
}
