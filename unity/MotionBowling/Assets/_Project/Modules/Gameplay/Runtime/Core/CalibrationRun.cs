using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The bookkeeping of one Calibrate attempt (P-4): whether a calibration is running, how many Timeouts it has had,
    /// and the <c>Begin</c> and <c>Cancel</c> calls on the calibrator. It makes no transition: the state machine reads
    /// the result of <see cref="Failed"/> and decides.
    /// </summary>
    internal sealed class CalibrationRun
    {
        private const int MaxCalibrationTimeouts = 2;   // the second Timeout in one Calibrate gives up (P-4)

        private readonly IPlayerCalibrator _calibrator;
        private int _timeouts;                          // Failed(Timeout) events since the last Restart

        public CalibrationRun(IPlayerCalibrator calibrator)
        {
            _calibrator = calibrator;
        }

        /// <summary><c>Begin</c> was called and no Completed, Failed or Cancel has come since.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>Calibrate entry: the count starts again, then <c>Begin</c> runs only if the player is present.</summary>
        public void Restart(bool present)
        {
            _timeouts = 0;
            IsRunning = false;
            BeginIfIdle(present);
        }

        /// <summary>
        /// Calls <c>Begin</c> when the player is present and no calibration is running. That covers entering while
        /// absent and the end of a PlayerLeft or TrackingLost failure.
        /// </summary>
        public void BeginIfIdle(bool present)
        {
            if (present && !IsRunning)
            {
                IsRunning = true;
                _calibrator.Begin();
            }
        }

        /// <summary>The calibration ended with a profile.</summary>
        public void Completed() => IsRunning = false;

        /// <summary>
        /// The calibration ended without a profile. Only a Timeout counts: the first one begins again if the player is
        /// present, and the second returns true, so the machine falls back to the default profile. PlayerLeft and
        /// TrackingLost just end the calibration.
        /// </summary>
        public bool Failed(CalibrationFailure reason, bool present)
        {
            IsRunning = false;
            if (reason != CalibrationFailure.Timeout)
            {
                return false;
            }

            _timeouts++;
            if (_timeouts < MaxCalibrationTimeouts)
            {
                BeginIfIdle(present);
                return false;
            }

            return true;
        }

        /// <summary>Stops any calibration: not running, then <c>calibrator.Cancel()</c>.</summary>
        public void Cancel()
        {
            IsRunning = false;
            _calibrator.Cancel();
        }
    }
}
