namespace Bowling.Contracts
{
    public sealed class PersonPose        // one detected person in one camera frame, unfiltered
    {
        public const int LandmarkCount = 33;
        public readonly Landmark[] Image = new Landmark[LandmarkCount]; // x,y in [0,1], origin top-left of the UNMIRRORED camera image; z = MediaPipe relative depth
        public readonly Landmark[] World = new Landmark[LandmarkCount]; // RAW MediaPipe world coordinates as reported: metres, hip-centred, MediaPipe axes
    }
}
