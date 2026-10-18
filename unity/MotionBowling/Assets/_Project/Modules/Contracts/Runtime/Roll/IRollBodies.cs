using System.Collections.Generic;
using UnityEngine;

namespace Bowling.Contracts
{
    public interface IRollBodies                       // read-only access for cameras and replay
    {
        Transform Ball { get; }
        IReadOnlyList<Transform> Pins { get; }         // index 0 = pin 1 … index 9 = pin 10
    }
}
