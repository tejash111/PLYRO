using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An ITrackingDiagnostics whose values a test sets. Starts with all-zero stats and no cameras.
    public sealed class FakeTrackingDiagnostics : ITrackingDiagnostics
    {
        public TrackingStats Stats { get; private set; }

        public IReadOnlyList<CameraInfo> Cameras { get; private set; } = Array.Empty<CameraInfo>();

        public void Set(TrackingStats stats)
        {
            Stats = stats;
        }

        // Replaces the camera list with a copy of the arguments, in order. No arguments clears the list.
        public void SetCameras(params CameraInfo[] cameras)
        {
            if (cameras == null)
            {
                throw new ArgumentNullException(nameof(cameras));
            }

            Cameras = (CameraInfo[])cameras.Clone();
        }
    }
}
