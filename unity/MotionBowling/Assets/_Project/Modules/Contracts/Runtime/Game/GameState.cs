namespace Bowling.Contracts
{
    public enum GameState : byte
    {
        Attract, Join, ModeSelect, Calibrate, AwaitReady, AwaitThrow, Rolling, Scoring, Paused, GameOver, Results
    }
}
