using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>What happens after a ball: whether the frame ended, and the rack the next ball faces.</summary>
    internal readonly struct RackDecision
    {
        /// <summary>The frame is over (the next ball belongs to the next frame, or the game is over).</summary>
        public readonly bool FrameComplete;

        /// <summary>The next ball's rack. <see cref="PinMask.All"/> when <see cref="FrameComplete"/>.</summary>
        public readonly PinMask NextRack;

        /// <summary>The next rack is fresh (P-2). True when <see cref="FrameComplete"/>.</summary>
        public readonly bool NextRackFresh;

        public RackDecision(bool frameComplete, PinMask nextRack, bool nextRackFresh)
        {
            FrameComplete = frameComplete;
            NextRack = nextRack;
            NextRackFresh = nextRackFresh;
        }
    }
}
