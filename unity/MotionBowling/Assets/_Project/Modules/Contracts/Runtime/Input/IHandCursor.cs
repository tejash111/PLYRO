using UnityEngine;

namespace Bowling.Contracts
{
    public interface IHandCursor                       // implemented by PoseInput
    {
        bool IsActive { get; }                         // a hand is raised into the cursor box
        Vector2 Position { get; }                      // 0..1 screen space, origin top-left, ALREADY MIRRORED for display
    }
}
