using System;

namespace Bowling.Contracts
{
    [Flags]
    public enum PinMask : ushort
    {
        None = 0, P1 = 1, P2 = 1 << 1, P3 = 1 << 2, P4 = 1 << 3, P5 = 1 << 4,
        P6 = 1 << 5, P7 = 1 << 6, P8 = 1 << 7, P9 = 1 << 8, P10 = 1 << 9, All = 0x3FF
    }
}
