using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IGameEvents a test drives by hand. Current starts as an Attract snapshot with no players,
    // so readers never meet null. The initial snapshot raises no event.
    public sealed class FakeGameEvents : IGameEvents
    {
        public GameSnapshot Current { get; private set; } = new GameSnapshot
        {
            State = GameState.Attract,
            Players = Array.Empty<PlayerSnapshot>(),
            CurrentFrame = 1,
            CurrentBall = 1,
            FrameCount = 10,
        };

        public event Action<GameState> StateChanged;

        public event Action<GameSnapshot> SnapshotChanged;

        public event Action<RollOutcome> RollScored;

        // Publishes a new snapshot that carries every field of the previous one except State, then raises
        // StateChanged and SnapshotChanged, in that order. It raises even when the state is unchanged.
        // The previous snapshot object is left untouched.
        public void SetState(GameState state)
        {
            GameSnapshot previous = Current;
            Current = new GameSnapshot
            {
                State = state,
                Players = previous.Players,
                CurrentPlayer = previous.CurrentPlayer,
                CurrentFrame = previous.CurrentFrame,
                CurrentBall = previous.CurrentBall,
                FrameCount = previous.FrameCount,
                Difficulty = previous.Difficulty,
            };
            StateChanged?.Invoke(state);
            SnapshotChanged?.Invoke(Current);
        }

        // Makes the snapshot Current and raises SnapshotChanged only, even when the state differs.
        public void Publish(GameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            Current = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }

        public void RaiseRollScored(RollOutcome outcome)
        {
            RollScored?.Invoke(outcome);
        }
    }
}
