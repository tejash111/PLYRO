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

        // Replaces Current with a new snapshot that carries every field of the previous one except State,
        // then raises StateChanged and SnapshotChanged, in that order. It raises even when the state is
        // unchanged (an explicit test control). The previous snapshot object is left untouched.
        public void SetState(GameState state)
        {
            GameSnapshot previous = Current;
            GameSnapshot published = new GameSnapshot
            {
                State = state,
                Players = previous.Players,
                CurrentPlayer = previous.CurrentPlayer,
                CurrentFrame = previous.CurrentFrame,
                CurrentBall = previous.CurrentBall,
                FrameCount = previous.FrameCount,
                Difficulty = previous.Difficulty,
            };
            Current = published;
            StateChanged?.Invoke(state);
            SnapshotChanged?.Invoke(published);
        }

        // Makes the snapshot Current, then raises StateChanged if its State differs from the previous
        // snapshot's, then SnapshotChanged.
        public void Publish(GameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            GameState previousState = Current.State;
            Current = snapshot;
            if (snapshot.State != previousState)
            {
                StateChanged?.Invoke(snapshot.State);
            }

            SnapshotChanged?.Invoke(snapshot);
        }

        // Makes the snapshot Current, then raises StateChanged if its State differs from the previous snapshot's (as
        // Publish does), then RollScored(outcome), then SnapshotChanged: the R22 order of a scored roll.
        public void PublishScoredRoll(GameSnapshot snapshot, RollOutcome outcome)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            GameState previousState = Current.State;
            Current = snapshot;
            if (snapshot.State != previousState)
            {
                StateChanged?.Invoke(snapshot.State);
            }

            RollScored?.Invoke(outcome);
            SnapshotChanged?.Invoke(snapshot);
        }

        public void RaiseRollScored(RollOutcome outcome)
        {
            RollScored?.Invoke(outcome);
        }
    }
}
