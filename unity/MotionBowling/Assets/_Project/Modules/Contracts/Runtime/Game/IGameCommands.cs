namespace Bowling.Contracts
{
    public interface IGameCommands                      // implemented by Gameplay; called by UI (dwell buttons) and operator panel
    {
        void SelectMode(int playerCount, int frameCount, Difficulty difficulty);
        void Pause();
        void Resume();
        void Abandon();                                  // back to Attract
    }
}
