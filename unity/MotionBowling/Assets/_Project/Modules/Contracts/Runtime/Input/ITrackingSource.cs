using System.Collections.Generic;

namespace Bowling.Contracts
{
    public interface ITrackingSource                   // raw samples; implemented by Native, Recorded, Synthetic sources
    {
        TrackingStatus Status { get; }
        void Start();
        void Stop();
        int ReadSamplesSince(double timestamp, List<PoseSample> into); // appends all samples with Timestamp > timestamp, oldest first; returns count
    }
}
