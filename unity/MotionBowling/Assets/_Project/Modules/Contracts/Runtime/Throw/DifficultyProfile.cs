namespace Bowling.Contracts
{
    public readonly struct DifficultyProfile
    {
        public readonly Difficulty Level;
        public readonly float Assist;           // 0..1, blend toward the ideal line
        public readonly bool Bumpers;           // gutter bumpers on/off

        public DifficultyProfile(Difficulty level, float assist, bool bumpers)
        {
            Level = level;
            Assist = assist;
            Bumpers = bumpers;
        }
    }
}
