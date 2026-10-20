using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The end-of-game rules (spec section 4, P-3, P-6, P-12): GameOver offers the final scores to the leaderboard once
    /// per game and holds, and Results holds, gives up on an absent player, or starts a new game ("play again"). Every
    /// transition goes through the <see cref="GameStateMachine"/> that owns it; the flow keeps no state of its own.
    /// </summary>
    internal sealed class GameEndFlow
    {
        private readonly GameStateMachine _host;
        private readonly PresenceTracker _presence;
        private readonly GameplayTuning _tuning;
        private readonly LeaderboardWriter _leaderboard;

        /// <summary>Only keeps its arguments: the host is still being built.</summary>
        public GameEndFlow(GameStateMachine host, PresenceTracker presence, GameplayTuning tuning,
            LeaderboardWriter leaderboard)
        {
            _host = host;
            _presence = presence;
            _tuning = tuning;
            _leaderboard = leaderboard;
        }

        /// <summary>GameOver on enter: submits the scores, unless this game already has (one submission per game, P-6).</summary>
        public void EnterGameOver()
        {
            GameSession session = _host.Session;
            if (!session.LeaderboardSubmitted)
            {
                _leaderboard.SubmitAll(session);
                session.LeaderboardSubmitted = true;
            }
        }

        /// <summary>The host's Tick in GameOver or Results: at most one transition.</summary>
        public void Tick(double now)
        {
            switch (_host.State)
            {
                case GameState.GameOver:
                    if (_host.TimeInState(now) >= _tuning.GameOverHold)
                    {
                        _host.Enter(GameState.Results);
                    }

                    break;
                case GameState.Results:
                    // Absence (counted from when presence went false, P-3) and the hold both end in Attract.
                    if (_presence.AbsentFor(now) >= _tuning.ResultsAbsent || _host.TimeInState(now) >= _tuning.ResultsHold)
                    {
                        _host.Enter(GameState.Attract);
                    }

                    break;
            }
        }

        /// <summary>Results: "play again" starts a new game with whatever mode the UI passes (P-6).</summary>
        public void PlayAgain(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            _host.StartGame(playerCount, frameCount, difficulty);
        }
    }
}
