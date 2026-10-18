using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IThrowSource a test drives by hand. It starts Disarmed.
    // Only SetPhase raises PhaseChanged. Arm, Disarm, EmitThrow and Reject move Phase silently.
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
            Phase = SwingPhase.WaitingForReady;
        }

        public void Disarm()
        {
            DisarmCount++;
            Phase = SwingPhase.Disarmed;
        }

        // Sets the phase, then raises PhaseChanged. It raises even when the phase is unchanged.
        public void SetPhase(SwingPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        // Raises Thrown while Phase is unchanged, then returns the source to Disarmed.
        // Throws InvalidOperationException when the source is disarmed.
        public void EmitThrow(ThrowEvent throwEvent)
        {
            if (Phase == SwingPhase.Disarmed)
            {
                throw new InvalidOperationException("EmitThrow needs an armed source: call Arm first.");
            }

            Thrown?.Invoke(throwEvent);
            Phase = SwingPhase.Disarmed;
        }

        // Raises Rejected while Phase is unchanged, then returns the source to WaitingForReady.
        public void Reject(ThrowRejection reason)
        {
            Rejected?.Invoke(reason);
            Phase = SwingPhase.WaitingForReady;
        }
    }
}
