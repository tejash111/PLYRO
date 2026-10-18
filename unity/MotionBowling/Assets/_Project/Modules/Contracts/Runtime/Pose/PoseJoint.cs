namespace Bowling.Contracts
{
    // MediaPipe Pose indices (person's own left/right)
    // Arrays always hold all 33 MediaPipe landmarks; indices 1–10 (face detail) have no enum names.
    public enum PoseJoint : byte
    {
        Nose = 0, LeftShoulder = 11, RightShoulder = 12, LeftElbow = 13, RightElbow = 14,
        LeftWrist = 15, RightWrist = 16, LeftPinky = 17, RightPinky = 18, LeftIndex = 19,
        RightIndex = 20, LeftThumb = 21, RightThumb = 22, LeftHip = 23, RightHip = 24,
        LeftKnee = 25, RightKnee = 26, LeftAnkle = 27, RightAnkle = 28,
        LeftHeel = 29, RightHeel = 30, LeftFootIndex = 31, RightFootIndex = 32
    }
}
