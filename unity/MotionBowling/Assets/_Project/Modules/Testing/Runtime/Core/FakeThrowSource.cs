using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IThrowSource a test drives by hand. It starts Disarmed.
    //
    // Every method sets Phase first and then raises events, so a handler sees the new phase and may call
    // back into the source. Arm, Disarm, EmitThrow and Reject raise PhaseChanged only when the phase
    // actually changes. SetPhase is an explicit test control and always raises.
    public sealed class FakeThrowSource : IThrowSource
    {
        public SwingPhase Phase { get; private set; } = SwingPhase.Disarmed;

        // The context of the latest Arm call. Meaningful only when ArmCount > 0.
        public ThrowContext LastContext { get; private set; }

        public int ArmCount { get; private set; }

        public int DisarmCount { get; private set; }

        public event Action<SwingPhase> PhaseChanged;

        public event Action<ThrowEvent> Thrown;

        public event Action<ThrowRejection> Rejected;

        public void Arm(in ThrowContext context)
        {
            LastContext = context;
            ArmCount++;
            ChangePhase(SwingPhase.WaitingForReady);
        }

        public void Disarm()
        {
            DisarmCount++;
            ChangePhase(SwingPhase.Disarmed);
        }

        // Sets the phase, then raises PhaseChanged. It raises even when the phase is unchanged.
        public void SetPhase(SwingPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        // Disarms (PhaseChanged if it was armed), then raises Thrown, so the source is already Disarmed when
        // Thrown fires and a handler may re-arm it. Throws InvalidOperationException when disarmed.
        public void EmitThrow(ThrowEvent throwEvent)
        {
            if (Phase == SwingPhase.Disarmed)
            {
                throw new InvalidOperationException("EmitThrow needs an armed source: call Arm first.");
            }

            ChangePhase(SwingPhase.Disarmed);
            Thrown?.Invoke(throwEvent);
        }

        // Returns to WaitingForReady (PhaseChanged if the phase changed), then raises Rejected, so the
        // source is already waiting when Rejected fires. Throws InvalidOperationException when disarmed,
        // as EmitThrow does: a disarmed source has no swing to reject.
        public void Reject(ThrowRejection reason)
        {
            if (Phase == SwingPhase.Disarmed)
            {
                throw new InvalidOperationException("Reject needs an armed source: call Arm first.");
            }

            ChangePhase(SwingPhase.WaitingForReady);
            Rejected?.Invoke(reason);
        }

        private void ChangePhase(SwingPhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
