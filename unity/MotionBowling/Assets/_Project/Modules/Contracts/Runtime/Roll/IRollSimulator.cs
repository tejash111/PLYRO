using System;

namespace Bowling.Contracts
{
    public interface IRollSimulator                    // implemented by BowlingPhysics
    {
        PinMask Standing { get; }
        bool IsRolling { get; }
        void SetBumpers(bool enabled);                 // throws InvalidOperationException if IsRolling (only valid while Idle)
        void SetRack(PinMask standing);                // instant: standing pins on their spots, others removed, ball parked;
                                                       // throws InvalidOperationException if IsRolling (only valid while Idle)
        void Roll(in ThrowEvent throwEvent);           // throws InvalidOperationException if IsRolling
        event Action<RollMilestone> Milestone;
        event Action<ImpactEvent> Impact;
        event Action<RollResult> Completed;
    }
}
