using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IPlayerInput a test drives by hand. Starts Stopped, with no player present and no pose.
    // Presence, status and poses are independent: pushing a pose changes neither of the other two.
    public sealed class FakePlayerInput : IPlayerInput
    {
        private readonly List<PlayerPose> _poses = new List<PlayerPose>();

        public TrackingStatus Status { get; private set; } = TrackingStatus.Stopped;

        public bool IsPlayerPresent { get; private set; }

        public PlayerPose Latest { get; private set; }

        public event Action<bool> PresenceChanged;

        public event Action<TrackingStatus> StatusChanged;

        // Sets the value, then raises PresenceChanged. Raises nothing when the value is unchanged.
        public void SetPresent(bool present)
        {
            if (IsPlayerPresent == present)
            {
                return;
            }

            IsPlayerPresent = present;
            PresenceChanged?.Invoke(present);
        }

        // Sets the value, then raises StatusChanged. Raises nothing when the value is unchanged.
        public void SetStatus(TrackingStatus status)
        {
            if (Status == status)
            {
                return;
            }

            Status = status;
            StatusChanged?.Invoke(status);
        }

        // Makes the pose Latest and queues it for ReadPosesSince. The fake keeps this very instance, so
        // push a new object per frame and push in non-decreasing Timestamp order.
        public void PushPose(PlayerPose pose)
        {
            if (pose == null)
            {
                throw new ArgumentNullException(nameof(pose));
            }

            _poses.Add(pose);
            Latest = pose;
        }

        // Appends every pushed pose with Timestamp > timestamp, oldest first, and returns how many.
        // Reading does not consume.
        public int ReadPosesSince(double timestamp, List<PlayerPose> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            int count = 0;
            for (int i = 0; i < _poses.Count; i++)
            {
                if (_poses[i].Timestamp > timestamp)
                {
                    into.Add(_poses[i]);
                    count++;
                }
            }

            return count;
        }
    }
}
