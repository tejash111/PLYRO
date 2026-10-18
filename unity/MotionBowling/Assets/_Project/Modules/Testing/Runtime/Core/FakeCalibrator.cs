using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IPlayerCalibrator a test drives by hand. Begin and Cancel only move Step and raise no events.
    // Complete and Fail set Step first, then raise StepChanged, then the outcome event.
    public sealed class FakeCalibrator : IPlayerCalibrator
    {
        public CalibrationStep Step { get; private set; } = CalibrationStep.Idle;

        // Tests set this directly to show hold progress; the fake never changes it.
        public float HoldProgress { get; set; }

        public int BeginCount { get; private set; }

        public event Action<CalibrationStep> StepChanged;

        public event Action<PlayerProfile> Completed;

        public event Action<CalibrationFailure> Failed;

        public void Begin()
        {
            BeginCount++;
            Step = CalibrationStep.WaitingForFullBody;
        }

        public void Cancel()
        {
            Step = CalibrationStep.Idle;
        }

        public void Complete(PlayerProfile profile)
        {
            Step = CalibrationStep.Done;
            StepChanged?.Invoke(CalibrationStep.Done);
            Completed?.Invoke(profile);
        }

        public void Fail(CalibrationFailure reason)
        {
            Step = CalibrationStep.Failed;
            StepChanged?.Invoke(CalibrationStep.Failed);
            Failed?.Invoke(reason);
        }
    }
}
