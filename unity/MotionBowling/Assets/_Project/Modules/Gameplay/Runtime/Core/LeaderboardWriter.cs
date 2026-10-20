using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Offers a finished game's scores to the leaderboard (spec section 4, P-12): one entry per player, in player order,
    /// all with the same <c>PlayedAtUtc</c>. A store that throws for one entry is logged, and the rest still go in.
    /// </summary>
    internal sealed class LeaderboardWriter
    {
        private const string LogCategory = "Gameplay";

        private readonly ILeaderboardStore _store;
        private readonly IGameLog _log;
        private readonly Func<DateTime> _utcNow;

        /// <summary>Throws ArgumentNullException for a null argument.</summary>
        public LeaderboardWriter(ILeaderboardStore store, IGameLog log, Func<DateTime> utcNow)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        /// <summary>
        /// Submits <c>{ DisplayName, Score = RunningTotal, FrameCount, PlayedAtUtc }</c> for each player of
        /// <paramref name="session"/>, reading the wall clock once. A <c>Submit</c> that throws is logged as an Error and
        /// the loop goes on.
        /// </summary>
        public void SubmitAll(GameSession session)
        {
            DateTime playedAtUtc = _utcNow();
            for (int i = 0; i < session.Players.Count; i++)
            {
                PlayerSlot player = session.Players[i];
                var entry = new LeaderboardEntry
                {
                    DisplayName = player.DisplayName,
                    Score = player.Card.RunningTotal,
                    FrameCount = session.FrameCount,
                    PlayedAtUtc = playedAtUtc,
                };

                try
                {
                    _store.Submit(entry);
                }
                catch (Exception exception)
                {
                    _log.Error(LogCategory, "Leaderboard submit failed for " + player.DisplayName, exception);
                }
            }
        }
    }
}
