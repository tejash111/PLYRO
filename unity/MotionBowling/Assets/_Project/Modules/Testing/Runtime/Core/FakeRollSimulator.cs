using System;
using Bowling.Contracts;

namespace Bowling.Testing
{
    // An IRollSimulator a test drives by hand. The roll finishes only when a test calls CompleteWith.
    //
    // State changes before events (ruling R21): Roll sets IsRolling before Milestone(Released), and
    // CompleteWith sets Standing and IsRolling before Milestone(Settled) and Completed.
    public sealed class FakeRollSimulator : IRollSimulator
    {
        public PinMask Standing { get; private set; } = PinMask.All;

        public bool IsRolling { get; private set; }

        // The argument of the latest SetRack call. Meaningful only when SetRackCount > 0.
        public PinMask LastRack { get; private set; }

        public int SetRackCount { get; private set; }

        // The argument of the latest SetBumpers call. Meaningful only when SetBumpersCount > 0.
        public bool LastBumpers { get; private set; }

        public int SetBumpersCount { get; private set; }

        // The throw of the latest Roll call. Meaningful only when RollCount > 0.
        public ThrowEvent LastThrow { get; private set; }

        public int RollCount { get; private set; }

        public event Action<RollMilestone> Milestone;

        public event Action<ImpactEvent> Impact;

        public event Action<RollResult> Completed;

        public void SetBumpers(bool enabled)
        {
            LastBumpers = enabled;
            SetBumpersCount++;
        }

        // Records the rack and stands exactly those pins, as the contract says. Throws while rolling.
        public void SetRack(PinMask standing)
        {
            if (IsRolling)
            {
                throw new InvalidOperationException("SetRack is not allowed while a roll is in progress.");
            }

            LastRack = standing;
            SetRackCount++;
            Standing = standing;
        }

        // Records the throw, starts the roll, then raises Milestone(Released). Throws if already rolling.
        public void Roll(in ThrowEvent throwEvent)
        {
            if (IsRolling)
            {
                throw new InvalidOperationException("Roll is not allowed while a roll is in progress.");
            }

            LastThrow = throwEvent;
            RollCount++;
            IsRolling = true;
            Milestone?.Invoke(RollMilestone.Released);
        }

        // Ends the roll. It sets Standing = standingAfter and IsRolling = false first, then raises
        // Milestone(Settled), then Completed with a result from the old Standing to standingAfter. A handler
        // may therefore call SetRack or Roll. Throws InvalidOperationException unless a roll is in progress.
        public void CompleteWith(PinMask standingAfter, bool gutter = false, RollEndReason reason = RollEndReason.Settled)
        {
            if (!IsRolling)
            {
                throw new InvalidOperationException("CompleteWith needs a roll in progress: call Roll first.");
            }

            PinMask before = Standing;
            Standing = standingAfter;
            IsRolling = false;
            Milestone?.Invoke(RollMilestone.Settled);
            Completed?.Invoke(new RollResult(before, standingAfter, gutter, reason));
        }

        // Raises an intermediate milestone (EnteredGutter, ReachedPins, EnteredPit). Changes no state.
        public void RaiseMilestone(RollMilestone milestone)
        {
            Milestone?.Invoke(milestone);
        }

        // Raises a collision event. Changes no state.
        public void RaiseImpact(ImpactEvent impact)
        {
            Impact?.Invoke(impact);
        }
    }
}
