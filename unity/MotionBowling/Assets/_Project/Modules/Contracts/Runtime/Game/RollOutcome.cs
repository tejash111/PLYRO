namespace Bowling.Contracts
{
    public readonly struct RollOutcome
    {
        public readonly int PlayerIndex;
        public readonly RollResult Result;
        public readonly MarkKind Mark;

        public RollOutcome(int playerIndex, RollResult result, MarkKind mark)
        {
            PlayerIndex = playerIndex;
            Result = result;
            Mark = mark;
        }
    }
}
