using UnityEngine;

namespace Bowling.Contracts
{
    public readonly struct ImpactEvent
    {
        public readonly ImpactKind Kind;
        public readonly float ImpulseNs;        // collision impulse magnitude, N·s
        public readonly Vector3 Position;       // lane frame

        public ImpactEvent(ImpactKind kind, float impulseNs, Vector3 position)
        {
            Kind = kind;
            ImpulseNs = impulseNs;
            Position = position;
        }
    }
}
