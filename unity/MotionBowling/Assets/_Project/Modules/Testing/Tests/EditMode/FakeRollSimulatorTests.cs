using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeRollSimulator.
    public class FakeRollSimulatorTests
    {
        private static readonly ThrowEvent Throw =
            new ThrowEvent(3.0, Handedness.Right, 7.0f, 1.0f, -0.05f, 0.2f, 0.8f);

        private const PinMask SplitLeft = PinMask.P7 | PinMask.P10;

        private static List<string> Record(FakeRollSimulator simulator)
        {
            var events = new List<string>();
            simulator.Milestone += m => events.Add("Milestone:" + m + ":" + simulator.IsRolling + ":" + simulator.Standing);
            simulator.Impact += i => events.Add("Impact:" + i.Kind);
            simulator.Completed += r =>
                events.Add("Completed:" + r.StandingBefore + ">" + r.StandingAfter + ":" + simulator.IsRolling + ":" + simulator.Standing);
            return events;
        }

        [Test]
        public void StartsWithAllPinsStandingAndNotRolling()
        {
            var simulator = new FakeRollSimulator();

            Assert.AreEqual(PinMask.All, simulator.Standing);
            Assert.IsFalse(simulator.IsRolling);
            Assert.AreEqual(0, simulator.RollCount);
            Assert.AreEqual(0, simulator.SetRackCount);
            Assert.AreEqual(0, simulator.SetBumpersCount);
        }

        [Test]
        public void SetRackRecordsItsArgumentAndStandsExactlyThosePins()
        {
            var simulator = new FakeRollSimulator();

            simulator.SetRack(SplitLeft);

            Assert.AreEqual(SplitLeft, simulator.LastRack);
            Assert.AreEqual(1, simulator.SetRackCount);
            Assert.AreEqual(SplitLeft, simulator.Standing);
        }

        [Test]
        public void SetBumpersRecordsItsArgument()
        {
            var simulator = new FakeRollSimulator();

            simulator.SetBumpers(true);
            Assert.IsTrue(simulator.LastBumpers);
            Assert.AreEqual(1, simulator.SetBumpersCount);

            simulator.SetBumpers(false);
            Assert.IsFalse(simulator.LastBumpers);
            Assert.AreEqual(2, simulator.SetBumpersCount);
        }

        [Test]
        public void SetBumpersWhileRollingIsAllowed()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);

            Assert.DoesNotThrow(() => simulator.SetBumpers(true));
        }

        [Test]
        public void RollRecordsTheThrowSetsRollingThenRaisesReleased()
        {
            var simulator = new FakeRollSimulator();
            List<string> events = Record(simulator);

            simulator.Roll(Throw);

            Assert.IsTrue(simulator.IsRolling);
            Assert.AreEqual(1, simulator.RollCount);
            Assert.AreEqual(3.0, simulator.LastThrow.Timestamp);
            Assert.AreEqual(7.0f, simulator.LastThrow.SpeedMps);
            Assert.AreEqual(1.0f, simulator.LastThrow.LaunchAngleDeg);
            Assert.AreEqual(-0.05f, simulator.LastThrow.StartOffsetM);
            Assert.AreEqual(0.2f, simulator.LastThrow.Hook);
            Assert.That(events, Is.EqualTo(new[] { "Milestone:Released:True:All" }),
                "the handler already sees IsRolling == true");
        }

        [Test]
        public void RollWhileRollingThrowsAndKeepsTheFirstThrow()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);
            var second = new ThrowEvent(4.0, Handedness.Left, 5.0f, 0f, 0f, 0f, 1f);
            List<string> events = Record(simulator);

            Assert.Throws<InvalidOperationException>(() => simulator.Roll(second));
            Assert.AreEqual(3.0, simulator.LastThrow.Timestamp);
            Assert.AreEqual(1, simulator.RollCount);
            Assert.IsEmpty(events);
        }

        [Test]
        public void SetRackWhileRollingThrowsAndChangesNothing()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);

            Assert.Throws<InvalidOperationException>(() => simulator.SetRack(PinMask.None));
            Assert.AreEqual(PinMask.All, simulator.Standing);
            Assert.AreEqual(0, simulator.SetRackCount);
        }

        [Test]
        public void CompleteWithWhileNotRollingThrows()
        {
            var simulator = new FakeRollSimulator();
            List<string> events = Record(simulator);

            Assert.Throws<InvalidOperationException>(() => simulator.CompleteWith(PinMask.None));
            Assert.AreEqual(PinMask.All, simulator.Standing);
            Assert.IsEmpty(events);
        }

        [Test]
        public void CompleteWithRaisesSettledThenCompletedThenUpdatesStateAfterBothEvents()
        {
            var simulator = new FakeRollSimulator();
            simulator.SetRack(SplitLeft);
            simulator.Roll(Throw);
            List<string> events = Record(simulator);

            simulator.CompleteWith(PinMask.P10);

            Assert.That(events, Is.EqualTo(new[]
            {
                "Milestone:Settled:True:P7, P10",
                "Completed:P7, P10>P10:True:P7, P10",
            }), "both handlers see the old Standing and IsRolling == true");
            Assert.AreEqual(PinMask.P10, simulator.Standing);
            Assert.IsFalse(simulator.IsRolling);
        }

        [Test]
        public void CompleteWithBuildsTheRollResultFromStandingAndArguments()
        {
            var simulator = new FakeRollSimulator();
            RollResult result = default;
            simulator.Completed += r => result = r;
            simulator.Roll(Throw);

            simulator.CompleteWith(SplitLeft, true, RollEndReason.Timeout);

            Assert.AreEqual(PinMask.All, result.StandingBefore);
            Assert.AreEqual(SplitLeft, result.StandingAfter);
            Assert.IsTrue(result.Gutter);
            Assert.AreEqual(RollEndReason.Timeout, result.EndReason);
            Assert.AreEqual(8, result.PinsKnocked);
        }

        [Test]
        public void CompleteWithDefaultsToNoGutterAndSettled()
        {
            var simulator = new FakeRollSimulator();
            RollResult result = default;
            simulator.Completed += r => result = r;
            simulator.Roll(Throw);

            simulator.CompleteWith(PinMask.None);

            Assert.IsFalse(result.Gutter);
            Assert.AreEqual(RollEndReason.Settled, result.EndReason);
            Assert.AreEqual(10, result.PinsKnocked);
        }

        [Test]
        public void ConsecutiveRollsCarryStandingFromOneToTheNext()
        {
            var simulator = new FakeRollSimulator();
            var results = new List<RollResult>();
            simulator.Completed += results.Add;

            simulator.Roll(Throw);
            simulator.CompleteWith(SplitLeft);
            simulator.Roll(Throw);
            simulator.CompleteWith(PinMask.P10);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(8, results[0].PinsKnocked);
            Assert.AreEqual(SplitLeft, results[1].StandingBefore);
            Assert.AreEqual(1, results[1].PinsKnocked);
            Assert.AreEqual(2, simulator.RollCount);
        }

        [Test]
        public void SetRackWorksAgainOnceTheRollHasCompleted()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);
            simulator.CompleteWith(PinMask.None);

            simulator.SetRack(PinMask.All);

            Assert.AreEqual(PinMask.All, simulator.Standing);
            Assert.AreEqual(1, simulator.SetRackCount);
        }

        [Test]
        public void FullRollEmitsReleasedThenSettledThenCompleted()
        {
            var simulator = new FakeRollSimulator();
            var events = new List<string>();
            simulator.Milestone += m => events.Add("Milestone:" + m);
            simulator.Completed += r => events.Add("Completed");

            simulator.Roll(Throw);
            simulator.CompleteWith(PinMask.None);

            Assert.That(events, Is.EqualTo(new[] { "Milestone:Released", "Milestone:Settled", "Completed" }));
        }

        [Test]
        public void RaiseMilestoneAndRaiseImpactForwardTheirArgumentsOnly()
        {
            var simulator = new FakeRollSimulator();
            List<string> events = Record(simulator);

            simulator.RaiseMilestone(RollMilestone.ReachedPins);
            simulator.RaiseImpact(new ImpactEvent(ImpactKind.BallPin, 2.5f, Vector3.zero));

            Assert.That(events, Is.EqualTo(new[] { "Milestone:ReachedPins:False:All", "Impact:BallPin" }));
            Assert.IsFalse(simulator.IsRolling);
        }
    }
}
