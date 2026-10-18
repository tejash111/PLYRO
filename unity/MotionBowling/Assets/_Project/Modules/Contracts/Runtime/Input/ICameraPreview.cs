using UnityEngine;

namespace Bowling.Contracts
{
    public interface ICameraPreview                    // implemented by PoseInput (from TrackingCore's Metal texture)
    {
        Texture Texture { get; }                       // null when unavailable; UNMIRRORED — the UI mirrors it for display
    }
}
