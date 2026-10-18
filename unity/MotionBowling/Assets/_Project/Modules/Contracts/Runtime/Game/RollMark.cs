namespace Bowling.Contracts
{
    public readonly struct RollMark
    {
        public readonly int Pins;
        public readonly MarkKind Kind;

        public RollMark(int pins, MarkKind kind)
        {
            Pins = pins;
            Kind = kind;
        }
    }
}
