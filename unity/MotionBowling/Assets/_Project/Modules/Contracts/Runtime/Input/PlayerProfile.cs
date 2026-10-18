namespace Bowling.Contracts
{
    public readonly struct PlayerProfile
    {
        public readonly Handedness BowlingHand;
        public readonly float ArmLengthM;       // shoulder→wrist, bowling side
        public readonly float ShoulderWidthM;
        public readonly float NeutralImageX;    // hip-midpoint image x at calibration (0..1, unmirrored)

        public PlayerProfile(Handedness bowlingHand, float armLengthM, float shoulderWidthM, float neutralImageX)
        {
            BowlingHand = bowlingHand;
            ArmLengthM = armLengthM;
            ShoulderWidthM = shoulderWidthM;
            NeutralImageX = neutralImageX;
        }
    }
}
