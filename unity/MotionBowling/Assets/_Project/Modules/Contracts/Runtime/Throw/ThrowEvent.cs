namespace Bowling.Contracts
{
    public readonly struct ThrowEvent
    {
        public readonly double Timestamp;       // release, camera clock (s)
        public readonly Handedness Hand;
        public readonly float SpeedMps;         // clamped to [LaneSpec.MinBallSpeedMps, LaneSpec.MaxBallSpeedMps]
        public readonly float LaunchAngleDeg;   // + toward +X; clamped to ±LaneSpec.MaxLaunchAngleDeg
        public readonly float StartOffsetM;     // lateral release position, + = +X; clamped to ±LaneSpec.MaxStartOffsetM
        public readonly float Hook;             // −1..1; + curves toward +X, − toward −X
        public readonly float Confidence;       // 0..1

        public ThrowEvent(
            double timestamp,
            Handedness hand,
            float speedMps,
            float launchAngleDeg,
            float startOffsetM,
            float hook,
            float confidence)
        {
            Timestamp = timestamp;
            Hand = hand;
            SpeedMps = speedMps;
            LaunchAngleDeg = launchAngleDeg;
            StartOffsetM = startOffsetM;
            Hook = hook;
            Confidence = confidence;
        }
    }
}
