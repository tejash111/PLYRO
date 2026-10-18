namespace Bowling.Contracts
{
    public sealed class PoseSample        // one camera frame
    {
        public const int MaxPeople = 3;
        public double Timestamp;           // seconds, camera presentation time (monotonic)
        public long FrameIndex;
        public int PersonCount;            // 0..MaxPeople
        public readonly PersonPose[] People = { new PersonPose(), new PersonPose(), new PersonPose() };
    }
}
