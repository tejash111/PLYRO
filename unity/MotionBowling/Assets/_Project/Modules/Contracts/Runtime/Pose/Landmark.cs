using UnityEngine;

namespace Bowling.Contracts
{
    public readonly struct Landmark
    {
        public readonly Vector3 Position;
        public readonly float Visibility;   // 0..1
        public readonly float Presence;     // 0..1

        public Landmark(Vector3 position, float visibility, float presence)
        {
            Position = position;
            Visibility = visibility;
            Presence = presence;
        }
    }
}
