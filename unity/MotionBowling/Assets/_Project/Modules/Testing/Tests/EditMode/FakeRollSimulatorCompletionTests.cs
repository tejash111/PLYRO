using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeRollSimulator completion and raised events (ruling R21: state first, then
    // events, so handlers may call back): CompleteWith, RaiseMilestone and RaiseImpact.
    public class FakeRollSimulatorCompletionTests
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
        public void CompleteWithWhileNotRollingThrows()
        {
            var simulator = new FakeRollSimulator();
            List<string> events = Record(simulator);

            Assert.Throws<InvalidOperationException>(() => simulator.CompleteWith(PinMask.None));
            Assert.AreEqual(PinMask.All, simulator.Standing);
            Assert.IsEmpty(events);
        }

        [Test]
        public void CompleteWithUpdatesStateThenRaisesSettledThenCompleted()
        {
            var simulator = new FakeRollSimulator();
            simulator.SetRack(SplitLeft);
            simulator.Roll(Throw);
            List<string> events = Record(simulator);

            simulator.CompleteWith(PinMask.P10);

            Assert.That(events, Is.EqualTo(new[]
            {
                "Milestone:Settled:False:P10",
                "Completed:P7, P10>P10:False:P10",
            }), "both handlers already see the new Standing and IsRolling == false, and the result still starts from the old rack");
            Assert.AreEqual(PinMask.P10, simulator.Standing);
            Assert.IsFalse(simulator.IsRolling);
        }

        [Test]
        public void CompletedHandlerMayResetTheRackAndTheRackSticks()
        {
            var simulator = new FakeRollSimulator();
            simulator.Completed += r => simulator.SetRack(PinMask.All);
            simulator.Roll(Throw);

            Assert.DoesNotThrow(() => simulator.CompleteWith(PinMask.None));

            Assert.AreEqual(PinMask.All, simulator.Standing, "the handler's SetRack is not overwritten afterwards");
            Assert.AreEqual(PinMask.All, simulator.LastRack);
            Assert.AreEqual(1, simulator.SetRackCount);
            Assert.IsFalse(simulator.IsRolling);
        }

        [Test]
        public void CompletedHandlerMayRollAgainAndTheNewRollStaysInProgress()
        {
            var simulator = new FakeRollSimulator();
            var results = new List<RollResult>();
            simulator.Completed += r =>
            {
                results.Add(r);
                if (results.Count == 1)
                {
                    simulator.Roll(Throw);
                }
            };
            simulator.Roll(Throw);

            Assert.DoesNotThrow(() => simulator.CompleteWith(SplitLeft));

            Assert.IsTrue(simulator.IsRolling, "the second roll is not cancelled afterwards");
            Assert.AreEqual(2, simulator.RollCount);
            Assert.AreEqual(SplitLeft, simulator.Standing);

            simulator.CompleteWith(PinMask.P10);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(SplitLeft, results[1].StandingBefore);
        }

        [Test]
        public void SettledMilestoneHandlerMayResetTheRack()
        {
            var simulator = new FakeRollSimulator();
            simulator.Milestone += m =>
            {
                if (m == RollMilestone.Settled)
                {
                    simulator.SetRack(SplitLeft);
                }
            };
            simulator.Roll(Throw);

            Assert.DoesNotThrow(() => simulator.CompleteWith(PinMask.None));
            Assert.AreEqual(SplitLeft, simulator.Standing);
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
        public void RaiseMilestoneForwardsEachMidRollMilestoneWhileRollingAndChangesNoState()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);
            List<string> events = Record(simulator);

            simulator.RaiseMilestone(RollMilestone.EnteredGutter);
            simulator.RaiseMilestone(RollMilestone.ReachedPins);
            simulator.RaiseMilestone(RollMilestone.EnteredPit);

            Assert.That(events, Is.EqualTo(new[]
            {
                "Milestone:EnteredGutter:True:All",
                "Milestone:ReachedPins:True:All",
                "Milestone:EnteredPit:True:All",
            }));
            Assert.IsTrue(simulator.IsRolling);
            Assert.AreEqual(PinMask.All, simulator.Standing);
        }

        [TestCase(RollMilestone.Released)]
        [TestCase(RollMilestone.Settled)]
        public void RaiseMilestoneRejectsMilestonesThatRollAndCompleteWithOwn(RollMilestone milestone)
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);
            List<string> events = Record(simulator);

            Assert.Throws<InvalidOperationException>(() => simulator.RaiseMilestone(milestone));
            Assert.IsEmpty(events);
        }

        [TestCase(RollMilestone.EnteredGutter)]
        [TestCase(RollMilestone.ReachedPins)]
        [TestCase(RollMilestone.EnteredPit)]
        public void RaiseMilestoneWhileNotRollingThrows(RollMilestone milestone)
        {
            var simulator = new FakeRollSimulator();
            List<string> events = Record(simulator);

            Assert.Throws<InvalidOperationException>(() => simulator.RaiseMilestone(milestone));
            Assert.IsEmpty(events);
        }

        [Test]
        public void RaiseMilestoneAfterCompletionThrowsAgain()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);
            simulator.CompleteWith(PinMask.None);

            Assert.Throws<InvalidOperationException>(() => simulator.RaiseMilestone(RollMilestone.EnteredPit));
        }

        [Test]
        public void RaiseImpactForwardsTheEventAndChangesNoState()
        {
            var simulator = new FakeRollSimulator();
            List<string> events = Record(simulator);

            simulator.RaiseImpact(new ImpactEvent(ImpactKind.BallPin, 2.5f, Vector3.zero));

            Assert.That(events, Is.EqualTo(new[] { "Impact:BallPin" }));
            Assert.IsFalse(simulator.IsRolling);
            Assert.AreEqual(PinMask.All, simulator.Standing);
        }
    }
}
