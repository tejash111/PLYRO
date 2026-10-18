using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IPlayerCalibrator a test drives by hand. Every method sets Step first and then raises events, so a
    // handler sees the new step and may call back (for example Begin from Completed).
    //
    // Begin and Cancel raise StepChanged only when the step actually changes. Complete and Fail always
    // raise StepChanged, then the outcome event (Completed or Failed).
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
            ChangeStep(CalibrationStep.WaitingForFullBody);
        }

        public void Cancel()
        {
            ChangeStep(CalibrationStep.Idle);
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

        private void ChangeStep(CalibrationStep step)
        {
            if (Step == step)
            {
                return;
            }

            Step = step;
            StepChanged?.Invoke(step);
        }
    }
}
