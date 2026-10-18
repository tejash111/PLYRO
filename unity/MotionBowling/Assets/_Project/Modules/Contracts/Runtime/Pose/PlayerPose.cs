namespace Bowling.Contracts
{
    // Tracking sources and recordings always carry raw MediaPipe axes. Only PlayerPose.World is in player space.
    //
    // Player space: the frame used for all world landmarks after PoseInput.
    // - The origin is the midpoint of the hips.
    // - +Y is up.
    // - +X is the camera image's right, which is the player's left when they face the camera.
    // - +Z is toward the camera, so a forward throw moves the wrist in +Z.
    // - The units are metres.
    //
    // Converting from MediaPipe's world axes happens in exactly one function, PoseAxes.ToPlayerSpace, in PoseInput.
    public sealed class PlayerPose        // the locked player, filtered; one per PoseSample while a player is locked
    {
        public double Timestamp;
        public readonly Landmark[] Image = new Landmark[PersonPose.LandmarkCount];
        public readonly Landmark[] World = new Landmark[PersonPose.LandmarkCount]; // PLAYER SPACE (above), metres
        public float Confidence;           // mean visibility of shoulders, elbows, wrists, hips
    }
}
