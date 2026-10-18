using System;

namespace Bowling.Contracts
{
    public interface IGameEvents                        // implemented by Gameplay
    {
        GameSnapshot Current { get; }
        event Action<GameState> StateChanged;
        event Action<GameSnapshot> SnapshotChanged;
        event Action<RollOutcome> RollScored;
    }
}
