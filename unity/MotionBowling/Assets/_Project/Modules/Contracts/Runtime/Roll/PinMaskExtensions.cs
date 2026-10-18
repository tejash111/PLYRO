namespace Bowling.Contracts
{
    public static class PinMaskExtensions
    {
        // Number of pins set in the low 10 bits.
        public static int Count(this PinMask mask)
        {
            int bits = (int)mask & (int)PinMask.All;
            int count = 0;
            while (bits != 0)
            {
                bits &= bits - 1;
                count++;
            }

            return count;
        }
    }
}
