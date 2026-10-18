using System;

namespace Bowling.Contracts
{
    public interface IPlayerCalibrator                 // implemented by PoseInput
    {
        CalibrationStep Step { get; }
        float HoldProgress { get; }                    // 0..1 while Holding
        void Begin();
        void Cancel();
        event Action<CalibrationStep> StepChanged;
        event Action<PlayerProfile> Completed;
        event Action<CalibrationFailure> Failed;
    }
}
