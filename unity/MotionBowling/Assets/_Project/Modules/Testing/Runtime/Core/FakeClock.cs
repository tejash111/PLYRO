using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // A deterministic IClock. Time starts at 0 and only moves when a test calls Advance.
    public sealed class FakeClock : IClock
    {
        public double Now { get; private set; }

        // Moves time forward. Zero is allowed; a negative or NaN amount throws ArgumentOutOfRangeException.
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Time only moves forward by zero or more seconds.");
            }

            Now += seconds;
        }
    }
}
