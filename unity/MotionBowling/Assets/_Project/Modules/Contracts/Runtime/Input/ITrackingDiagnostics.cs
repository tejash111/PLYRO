using System.Collections.Generic;

namespace Bowling.Contracts
{
    public interface ITrackingDiagnostics              // implemented by PoseInput; used by the operator panel
    {
        TrackingStats Stats { get; }
        IReadOnlyList<CameraInfo> Cameras { get; }
    }
}
