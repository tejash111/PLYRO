using Bowling.Contracts;
using UnityEngine;

namespace Bowling.Testing
{
    // An IHandCursor whose state a test sets. Starts inactive at (0, 0).
    public sealed class FakeHandCursor : IHandCursor
    {
        public bool IsActive { get; private set; }

        public Vector2 Position { get; private set; }

        public void Set(bool active, Vector2 position)
        {
            IsActive = active;
            Position = position;
        }
    }
}
