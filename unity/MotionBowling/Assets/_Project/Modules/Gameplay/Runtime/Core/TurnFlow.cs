using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The inputs of a turn (spec section 4, P-4, P-10): the calibrator's result in Calibrate, the swing phases in
    /// AwaitReady, the throw or rejection in AwaitThrow, and the roll result in Rolling. Every transition goes through
    /// the <see cref="GameStateMachine"/> that owns it; the flow keeps no state of its own.
    /// </summary>
    internal sealed class TurnFlow
    {
        private readonly GameStateMachine _host;
        private readonly PresenceTracker _presence;
        private readonly GameplayTuning _tuning;
        private readonly IRollSimulator _sim;
        private readonly CalibrationRun _calibration;

        /// <summary>Only keeps its arguments: the host is still being built. The calibration run is the host's.</summary>
        public TurnFlow(GameStateMachine host, PresenceTracker presence, GameplayTuning tuning,
            IRollSimulator rollSimulator, CalibrationRun calibration)
        {
            _host = host;
            _presence = presence;
            _tuning = tuning;
            _sim = rollSimulator;
            _calibration = calibration;
        }

        /// <summary>Calibrate only: the profile goes to the current player, then the game is ready for the first ball.</summary>
        public void OnCalibrationCompleted(PlayerProfile profile)
        {
            if (_host.State != GameState.Calibrate)
            {
                Ignored("CalibrationCompleted");
                return;
            }

            _calibration.Completed();
            _host.Session.Current.Profile = profile;
            _host.Enter(GameState.AwaitReady);
        }

        /// <summary>
        /// Calibrate only (P-4). Only a Timeout counts: the first one starts over, the second falls back to the default
        /// profile. PlayerLeft and TrackingLost just end the calibration, and the next Tick with the player present begins
        /// another.
        /// </summary>
        public void OnCalibrationFailed(CalibrationFailure reason)
        {
            if (_host.State != GameState.Calibrate)
            {
                Ignored("CalibrationFailed");
                return;
            }

            if (!_calibration.Failed(reason, _presence.IsPresent))    // the bool means "gave up after the second Timeout" (P-4)
            {
                return;
            }

            _host.Session.Current.Profile = DefaultProfiles.For(_tuning);
            _host.Enter(GameState.AwaitReady);
        }

        /// <summary>
        /// AwaitReady only: the source has left its waiting phase and the swing is under way. Every other state and
        /// every other phase is ignored, silently: <c>Arm</c> and <c>Disarm</c> raise <c>PhaseChanged</c> too, and it
        /// reaches this method right after the call that caused it (P-7), so it is not an unexpected input.
        /// </summary>
        public void OnPhaseChanged(SwingPhase phase)
        {
            bool swinging = phase == SwingPhase.Ready || phase == SwingPhase.Backswing || phase == SwingPhase.ForwardSwing;
            if (_host.State != GameState.AwaitReady || !swinging)
            {
                return;
            }

            _host.Enter(GameState.AwaitThrow);
        }

        /// <summary>
        /// AwaitThrow, or AwaitReady when the source passed Ready within one frame (P-10): rolls the throw as it came,
        /// camera-clock timestamp included, and enters Rolling. Any other state warns.
        /// </summary>
        public void OnThrown(ThrowEvent throwEvent)
        {
            if (_host.State != GameState.AwaitThrow && _host.State != GameState.AwaitReady)
            {
                Ignored("Thrown");
                return;
            }

            _sim.Roll(throwEvent);
            _host.Enter(GameState.Rolling);
        }

        /// <summary>
        /// AwaitThrow only: the swing was aborted, so AwaitReady is entered again, which re-arms without using the ball.
        /// AwaitReady ignores it silently: the source is already waiting (P-10). Any other state warns.
        /// </summary>
        public void OnRejected(ThrowRejection reason)
        {
            if (_host.State == GameState.AwaitReady)
            {
                return;
            }

            if (_host.State != GameState.AwaitThrow)
            {
                Ignored("Rejected");
                return;
            }

            _host.Enter(GameState.AwaitReady);
        }

        /// <summary>
        /// Rolling only: scores the result against the session's rack (P-9), then has the host enter Scoring with the
        /// outcome, which publishes the scored-roll event order (R22). The outcome still names the roll just scored
        /// (P-11). A result that started from a different rack than the session's is warned about, and the outcome
        /// keeps it unchanged. Any other state warns.
        /// </summary>
        public void OnRollCompleted(RollResult result)
        {
            if (_host.State != GameState.Rolling)
            {
                Ignored("RollCompleted");
                return;
            }

            GameSession session = _host.Session;
            if (result.StandingBefore != session.Rack)
            {
                _host.Warn("StandingBefore " + result.StandingBefore + " differs from the rack " + session.Rack
                    + "; scoring against the rack");
            }

            MarkKind mark = session.ScoreRoll(result);
            _host.EnterScoring(new RollOutcome(session.CurrentPlayer, result, mark));
        }

        // P-10: an input the current state doesn't take is dropped, and the warning names both.
        private void Ignored(string eventName) => _host.Warn(eventName + " ignored in " + _host.State);
    }
}
