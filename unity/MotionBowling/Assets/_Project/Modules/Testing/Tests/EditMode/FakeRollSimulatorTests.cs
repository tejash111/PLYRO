using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeRollSimulator setup and Roll: SetRack, SetBumpers (both only while idle, as
    // physics spec section 7 says) and Roll. Completion has its own file.
    public class FakeRollSimulatorTests
    {
        private static readonly ThrowEvent Throw =
            new ThrowEvent(3.0, Handedness.Right, 7.0f, 1.0f, -0.05f, 0.2f, 0.8f);

        private const PinMask SplitLeft = PinMask.P7 | PinMask.P10;

        private static List<string> Record(FakeRollSimulator simulator)
        {
            var events = new List<string>();
            simulator.Milestone += m => events.Add("Milestone:" + m + ":" + simulator.IsRolling + ":" + simulator.Standing);
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
        public void SetRackWhileRollingThrowsAndChangesNothing()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);

            Assert.Throws<InvalidOperationException>(() => simulator.SetRack(PinMask.None));
            Assert.AreEqual(PinMask.All, simulator.Standing);
            Assert.AreEqual(0, simulator.SetRackCount);
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
        public void SetBumpersWhileRollingThrowsAndRecordsNothing()
        {
            var simulator = new FakeRollSimulator();
            simulator.SetBumpers(false);
            simulator.Roll(Throw);

            Assert.Throws<InvalidOperationException>(() => simulator.SetBumpers(true));
            Assert.IsFalse(simulator.LastBumpers, "the rejected call is not recorded");
            Assert.AreEqual(1, simulator.SetBumpersCount);
        }

        [Test]
        public void SetBumpersWorksAgainOnceTheRollHasCompleted()
        {
            var simulator = new FakeRollSimulator();
            simulator.Roll(Throw);
            simulator.CompleteWith(PinMask.None);

            Assert.DoesNotThrow(() => simulator.SetBumpers(true));
            Assert.IsTrue(simulator.LastBumpers);
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
    }
}
