using Bowling.Contracts;
using UnityEngine;

namespace Bowling.App
{
    /// <summary>The production <see cref="IClock"/>: real seconds since start, unaffected by Time.timeScale.</summary>
    public sealed class UnityClock : IClock
    {
        public double Now => Time.unscaledTimeAsDouble;
    }
}
