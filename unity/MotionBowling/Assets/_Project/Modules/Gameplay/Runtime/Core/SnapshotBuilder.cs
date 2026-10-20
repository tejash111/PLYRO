using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Turns a <see cref="GameSession"/> into the <see cref="GameSnapshot"/> that <see cref="IGameEvents.Current"/>
    /// publishes. The UI keeps references to snapshots, so every call builds new objects and nothing a snapshot
    /// holds is shared with, or changed by, the session afterwards.
    /// </summary>
    internal static class SnapshotBuilder
    {
        /// <summary>
        /// A snapshot for the states with no game in progress (P-11): no players, and the position at the
        /// start (player 0, frame 1, ball 1). <paramref name="frameCount"/> and <paramref name="difficulty"/>
        /// are the presets.
        /// </summary>
        public static GameSnapshot Lobby(GameState state, int frameCount, DifficultyProfile difficulty)
        {
            return new GameSnapshot
            {
                State = state,
                Players = Array.Empty<PlayerSnapshot>(),
                CurrentPlayer = 0,
                CurrentFrame = 1,
                CurrentBall = 1,
                FrameCount = frameCount,
                Difficulty = difficulty,
            };
        }

        /// <summary>
        /// A snapshot of <paramref name="session"/> as it is now: every player's frames, marks and totals, and the
        /// session's current position. Throws <see cref="ArgumentNullException"/> for a null session.
        /// </summary>
        public static GameSnapshot InGame(GameState state, GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var players = new PlayerSnapshot[session.Players.Count];
            for (int i = 0; i < players.Length; i++)
            {
                players[i] = BuildPlayer(session.Players[i], session.FrameCount);
            }

            return new GameSnapshot
            {
                State = state,
                Players = Array.AsReadOnly(players),
                CurrentPlayer = session.CurrentPlayer,
                CurrentFrame = session.CurrentFrame,
                CurrentBall = session.CurrentBall,
                FrameCount = session.FrameCount,
                Difficulty = session.Difficulty,
            };
        }

        private static PlayerSnapshot BuildPlayer(PlayerSlot slot, int frameCount)
        {
            ScoreCard card = slot.Card;
            var frames = new FrameView[frameCount];
            for (int frame = 1; frame <= frameCount; frame++)
            {
                frames[frame - 1] = new FrameView
                {
                    FrameNumber = frame,
                    Rolls = card.Marks(frame),          // a new array on every call
                    Cumulative = card.Cumulative(frame),
                };
            }

            return new PlayerSnapshot
            {
                DisplayName = slot.DisplayName,
                ColorIndex = slot.ColorIndex,
                Frames = Array.AsReadOnly(frames),
                RunningTotal = card.RunningTotal,
            };
        }
    }
}
