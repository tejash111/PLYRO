using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An ITrackingSource a test feeds by hand. It starts Stopped, like the real sources before Start.
    public sealed class FakeTrackingSource : ITrackingSource
    {
        private readonly List<PoseSample> _samples = new List<PoseSample>();

        public TrackingStatus Status { get; private set; } = TrackingStatus.Stopped;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public void Start()
        {
            StartCount++;
            Status = TrackingStatus.Running;
        }

        public void Stop()
        {
            StopCount++;
            Status = TrackingStatus.Stopped;
        }

        // Forces any status, for example NoCamera or PermissionDenied.
        public void SetStatus(TrackingStatus status)
        {
            Status = status;
        }

        // Queues a sample. The fake keeps this very instance, so push a new object per frame and
        // push in non-decreasing Timestamp order.
        public void Push(PoseSample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            _samples.Add(sample);
        }

        // Appends every pushed sample with Timestamp > timestamp, oldest first, and returns how many.
        // Reading does not consume: the same call returns the same samples again.
        public int ReadSamplesSince(double timestamp, List<PoseSample> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            int count = 0;
            for (int i = 0; i < _samples.Count; i++)
            {
                if (_samples[i].Timestamp > timestamp)
                {
                    into.Add(_samples[i]);
                    count++;
                }
            }

            return count;
        }
    }
}
