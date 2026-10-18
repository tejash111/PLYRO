using System;
using System.Collections.Generic;

namespace Bowling.Contracts
{
    public interface IPlayerInput                      // implemented by PoseInput
    {
        TrackingStatus Status { get; }
        bool IsPlayerPresent { get; }                  // a player is locked and inside the play zone
        PlayerPose Latest { get; }                     // null when no player is locked
        int ReadPosesSince(double timestamp, List<PlayerPose> into);
        event Action<bool> PresenceChanged;
        event Action<TrackingStatus> StatusChanged;
    }
}
