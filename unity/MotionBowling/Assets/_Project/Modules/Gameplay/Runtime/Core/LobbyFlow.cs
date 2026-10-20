using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The lobby rules (spec section 4, P-3): Attract waits for a player, Join greets them, and ModeSelect starts a game
    /// from the player's choice or the preset, or gives up on an absent player. Every transition goes through the
    /// <see cref="GameStateMachine"/> that owns it; the flow keeps no state of its own.
    /// </summary>
    internal sealed class LobbyFlow
    {
        private readonly GameStateMachine _host;
        private readonly PresenceTracker _presence;
        private readonly GameplayTuning _tuning;

        /// <summary>Only keeps its arguments: the host is still being built.</summary>
        public LobbyFlow(GameStateMachine host, PresenceTracker presence, GameplayTuning tuning)
        {
            _host = host;
            _presence = presence;
            _tuning = tuning;
        }

        /// <summary>The host's Tick in Attract, Join or ModeSelect: at most one transition.</summary>
        public void Tick(double now)
        {
            switch (_host.State)
            {
                case GameState.Attract:
                    TickAttract();
                    break;
                case GameState.Join:
                    TickJoin(now);
                    break;
                case GameState.ModeSelect:
                    TickModeSelect(now);
                    break;
            }
        }

        /// <summary>ModeSelect: the player's choice starts the game. The host has already resolved the difficulty.</summary>
        public void SelectMode(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            _host.StartGame(playerCount, frameCount, difficulty);
        }

        private void TickAttract()
        {
            if (_presence.IsPresent)
            {
                _host.Enter(GameState.Join);
            }
        }

        private void TickJoin(double now)
        {
            if (!_presence.IsPresent)
            {
                _host.Enter(GameState.Attract);
            }
            else if (_host.TimeInState(now) >= _tuning.JoinGreeting)
            {
                _host.Enter(GameState.ModeSelect);
            }
        }

        // Absence wins over the timeout; the timeout auto-selects the preset only with the player present.
        private void TickModeSelect(double now)
        {
            if (_presence.AbsentFor(now) >= _tuning.ModeSelectAbsent)
            {
                _host.Enter(GameState.Attract);
            }
            else if (_presence.IsPresent && _host.TimeInState(now) >= _tuning.ModeSelectTimeout)
            {
                _host.StartGame(_tuning.PresetPlayerCount, _tuning.PresetFrameCount, _host.Preset);
            }
        }
    }
}
