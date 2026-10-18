namespace Bowling.Contracts
{
    public readonly struct RollResult
    {
        public readonly PinMask StandingBefore;
        public readonly PinMask StandingAfter;
        public readonly bool Gutter;
        public readonly RollEndReason EndReason;

        public RollResult(PinMask standingBefore, PinMask standingAfter, bool gutter, RollEndReason endReason)
        {
            StandingBefore = standingBefore;
            StandingAfter = standingAfter;
            Gutter = gutter;
            EndReason = endReason;
        }

        public int PinsKnocked => (StandingBefore & ~StandingAfter).Count();
    }
}
