using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Paused and the ways in and out of it (spec section 4, P-3 to P-6): the pause rule in Calibrate, AwaitReady and
    /// AwaitThrow (with the longer turn-change wait), resuming when the player returns, abandoning after a long absence,
    /// and the operator's Pause, Resume and Abandon, with Pause and Abandon deferred while the ball is in play. Every
    /// transition goes through the <see cref="GameStateMachine"/> that owns it; <see cref="PauseTracker"/> holds the
    /// pause's own state.
    /// </summary>
    internal sealed class PauseFlow
    {
        private readonly GameStateMachine _host;
        private readonly PresenceTracker _presence;
        private readonly GameplayTuning _tuning;
        private readonly StateEntryActions _entry;
        private readonly CalibrationRun _calibration;
        private readonly PauseTracker _tracker = new PauseTracker();

        /// <summary>Only keeps its arguments: the host is still being built. The entry actions and calibration run are the host's.</summary>
        public PauseFlow(GameStateMachine host, PresenceTracker presence, GameplayTuning tuning,
            StateEntryActions entry, CalibrationRun calibration)
        {
            _host = host;
            _presence = presence;
            _tuning = tuning;
            _entry = entry;
            _calibration = calibration;
        }

        /// <summary>An operator <c>Abandon()</c> during Rolling, waiting for the roll to complete (P-6). The host reads it.</summary>
        public bool AbandonDeferred { get; private set; }

        /// <summary>
        /// The host's Tick, after the presence poll and before any other rule: a deferred <c>Pause()</c> applies on the
        /// first Tick after Rolling and Scoring are over (P-6). True when it entered Paused, which is then this Tick's
        /// one transition (P-3).
        /// </summary>
        public bool ApplyDeferredPause()
        {
            if (!_tracker.Deferred || _host.State == GameState.Rolling || _host.State == GameState.Scoring)
            {
                return false;
            }

            _tracker.Deferred = false;
            _host.Enter(GameState.Paused);
            return true;
        }

        /// <summary>
        /// The host's Tick in Calibrate, AwaitReady or AwaitThrow: absence pauses (P-3). A turn-change Calibrate waits
        /// longer, because the zone is empty while the players swap (P-4).
        /// </summary>
        public void TickPausable(double now)
        {
            bool turnChange = _host.State == GameState.Calibrate && _host.IsTurnChange;
            if (_presence.AbsentFor(now) >= (turnChange ? _tuning.TurnChangeWait : _tuning.PauseAfterAbsent))
            {
                _host.Enter(GameState.Paused);
            }
        }

        /// <summary>Paused on enter (P-5): disarms, stops any calibration, then remembers where to resume.</summary>
        public void EnterPaused(GameState from, bool present)
        {
            _entry.EnterPaused();
            _calibration.Cancel();
            _tracker.Begin(from, present);
        }

        /// <summary>
        /// The host's Tick in Paused (P-5): the player present after an absence resumes. Otherwise absence abandons the
        /// game, counted from the later of the pause entry and the start of the absence, so an operator pause with the
        /// player present lasts until <c>Resume()</c>.
        /// </summary>
        public void TickPaused(double now)
        {
            if (_tracker.PlayerReturned(_presence.IsPresent))
            {
                _host.Enter(_tracker.ResumeState);
            }
            else if (Math.Min(_presence.AbsentFor(now), _host.TimeInState(now)) >= _tuning.AbandonAfterPause)
            {
                _host.Enter(GameState.Attract);
            }
        }

        /// <summary>
        /// Pauses now in the lobby, the turn states and GameOver; waits for the ball to finish in Rolling and Scoring;
        /// and is ignored in Attract, Results and Paused (P-6).
        /// </summary>
        public void Pause()
        {
            switch (_host.State)
            {
                case GameState.Join:
                case GameState.ModeSelect:
                case GameState.Calibrate:
                case GameState.AwaitReady:
                case GameState.AwaitThrow:
                case GameState.GameOver:
                    _host.Enter(GameState.Paused);
                    break;
                case GameState.Rolling:
                case GameState.Scoring:
                    _tracker.Deferred = true;
                    break;
                default:
                    _host.Ignore(nameof(Pause));
                    break;
            }
        }

        /// <summary>
        /// Paused: resumes at once, whether or not the player was ever absent (P-5). Rolling and Scoring: cancels a
        /// pending deferred <c>Pause()</c> without a log. Ignored everywhere else (P-6).
        /// </summary>
        public void Resume()
        {
            if (_host.State == GameState.Paused)
            {
                _host.Enter(_tracker.ResumeState);
            }
            else if (_tracker.Deferred && (_host.State == GameState.Rolling || _host.State == GameState.Scoring))
            {
                _tracker.Deferred = false;
            }
            else
            {
                _host.Ignore(nameof(Resume));
            }
        }

        /// <summary>
        /// Ends the game in Attract from any state but Attract, where it is ignored. In Rolling it waits for the roll to
        /// complete, which then goes to Attract unscored (P-6).
        /// </summary>
        public void Abandon()
        {
            switch (_host.State)
            {
                case GameState.Rolling:
                    AbandonDeferred = true;
                    break;
                case GameState.Attract:
                    _host.Ignore(nameof(Abandon));
                    break;
                default:
                    _host.Enter(GameState.Attract);
                    break;
            }
        }

        /// <summary>Attract on enter: no deferred Pause or Abandon outlives the game.</summary>
        public void Clear()
        {
            _tracker.Deferred = false;
            AbandonDeferred = false;
        }
    }
}
