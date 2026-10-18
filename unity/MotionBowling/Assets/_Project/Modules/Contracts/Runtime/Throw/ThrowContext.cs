namespace Bowling.Contracts
{
    public readonly struct ThrowContext
    {
        public readonly PlayerProfile Player;
        public readonly DifficultyProfile Difficulty;

        public ThrowContext(PlayerProfile player, DifficultyProfile difficulty)
        {
            Player = player;
            Difficulty = difficulty;
        }
    }
}
