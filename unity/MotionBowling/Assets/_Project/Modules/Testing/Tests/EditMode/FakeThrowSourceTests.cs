using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeThrowSource (ruling R21: state first, then events, and PhaseChanged on every
    // actual phase change). Each event name carries source.Phase as the handler saw it.
    public class FakeThrowSourceTests
    {
        private static readonly ThrowContext Context = new ThrowContext(
            new PlayerProfile(Handedness.Left, 0.6f, 0.4f, 0.5f),
            new DifficultyProfile(Difficulty.Pro, 0.25f, true));

        private static readonly ThrowEvent Throw =
            new ThrowEvent(12.5, Handedness.Left, 6.5f, -1.5f, 0.1f, -0.4f, 0.9f);

        private static List<string> Record(FakeThrowSource source)
        {
            var events = new List<string>();
            source.PhaseChanged += phase => events.Add("PhaseChanged:" + phase + ":" + source.Phase);
            source.Thrown += e => events.Add("Thrown:" + e.SpeedMps + ":" + source.Phase);
            source.Rejected += reason => events.Add("Rejected:" + reason + ":" + source.Phase);
            return events;
        }

        [Test]
        public void StartsDisarmedWithNoCalls()
        {
            var source = new FakeThrowSource();

            Assert.AreEqual(SwingPhase.Disarmed, source.Phase);
            Assert.AreEqual(0, source.ArmCount);
            Assert.AreEqual(0, source.DisarmCount);
        }

        [Test]
        public void ArmWaitsForReadyRecordsTheContextAndRaisesPhaseChanged()
        {
            var source = new FakeThrowSource();
            List<string> events = Record(source);

            source.Arm(Context);

            Assert.AreEqual(SwingPhase.WaitingForReady, source.Phase);
            Assert.AreEqual(1, source.ArmCount);
            Assert.AreEqual(Handedness.Left, source.LastContext.Player.BowlingHand);
            Assert.AreEqual(0.6f, source.LastContext.Player.ArmLengthM);
            Assert.AreEqual(Difficulty.Pro, source.LastContext.Difficulty.Level);
            Assert.AreEqual(0.25f, source.LastContext.Difficulty.Assist);
            Assert.IsTrue(source.LastContext.Difficulty.Bumpers);
            Assert.That(events, Is.EqualTo(new[] { "PhaseChanged:WaitingForReady:WaitingForReady" }),
                "the phase already holds the new value inside the handler");
        }

        [Test]
        public void ArmAgainKeepsTheLatestContextAndRaisesNothingWhenThePhaseIsUnchanged()
        {
            var source = new FakeThrowSource();
            var easier = new ThrowContext(Context.Player, new DifficultyProfile(Difficulty.Casual, 1f, false));
            source.Arm(Context);
            List<string> events = Record(source);

            source.Arm(easier);

            Assert.AreEqual(2, source.ArmCount);
            Assert.AreEqual(Difficulty.Casual, source.LastContext.Difficulty.Level);
            Assert.IsEmpty(events, "WaitingForReady to WaitingForReady is not a change");
        }

        [Test]
        public void DisarmSetsDisarmedCountsAndRaisesPhaseChanged()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            source.SetPhase(SwingPhase.Backswing);
            List<string> events = Record(source);

            source.Disarm();

            Assert.AreEqual(SwingPhase.Disarmed, source.Phase);
            Assert.AreEqual(1, source.DisarmCount);
            Assert.AreEqual(1, source.ArmCount);
            Assert.That(events, Is.EqualTo(new[] { "PhaseChanged:Disarmed:Disarmed" }));
        }

        [Test]
        public void DisarmWhileDisarmedCountsButRaisesNothing()
        {
            var source = new FakeThrowSource();
            List<string> events = Record(source);

            source.Disarm();

            Assert.AreEqual(1, source.DisarmCount);
            Assert.IsEmpty(events);
        }

        [Test]
        public void SetPhaseSetsThePhaseThenRaisesPhaseChanged()
        {
            var source = new FakeThrowSource();
            List<string> events = Record(source);

            source.SetPhase(SwingPhase.Backswing);
            source.SetPhase(SwingPhase.ForwardSwing);

            Assert.AreEqual(SwingPhase.ForwardSwing, source.Phase);
            Assert.That(events, Is.EqualTo(new[]
            {
                "PhaseChanged:Backswing:Backswing",
                "PhaseChanged:ForwardSwing:ForwardSwing",
            }));
        }

        [Test]
        public void SetPhaseRaisesEvenWhenThePhaseIsUnchanged()
        {
            var source = new FakeThrowSource();
            List<string> events = Record(source);

            source.SetPhase(SwingPhase.Ready);
            source.SetPhase(SwingPhase.Ready);

            Assert.AreEqual(2, events.Count, "SetPhase is an explicit test control and always raises");
        }

        [Test]
        public void EmitThrowWhileDisarmedThrowsAndRaisesNothing()
        {
            var source = new FakeThrowSource();
            List<string> events = Record(source);

            Assert.Throws<InvalidOperationException>(() => source.EmitThrow(Throw));
            Assert.AreEqual(SwingPhase.Disarmed, source.Phase);
            Assert.IsEmpty(events);
        }

        [Test]
        public void EmitThrowDisarmsThenRaisesPhaseChangedThenThrown()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            source.SetPhase(SwingPhase.Released);
            List<string> events = Record(source);

            source.EmitThrow(Throw);

            Assert.That(events, Is.EqualTo(new[] { "PhaseChanged:Disarmed:Disarmed", "Thrown:6.5:Disarmed" }),
                "the source is already Disarmed when PhaseChanged and Thrown fire, in that order");
            Assert.AreEqual(SwingPhase.Disarmed, source.Phase);
        }

        [Test]
        public void EmitThrowDeliversTheThrowEvent()
        {
            var source = new FakeThrowSource();
            ThrowEvent received = default;
            source.Thrown += e => received = e;
            source.Arm(Context);

            source.EmitThrow(Throw);

            Assert.AreEqual(12.5, received.Timestamp);
            Assert.AreEqual(Handedness.Left, received.Hand);
            Assert.AreEqual(6.5f, received.SpeedMps);
            Assert.AreEqual(-1.5f, received.LaunchAngleDeg);
            Assert.AreEqual(0.1f, received.StartOffsetM);
            Assert.AreEqual(-0.4f, received.Hook);
            Assert.AreEqual(0.9f, received.Confidence);
        }

        [Test]
        public void ThrownHandlerMayReArmAndTheSourceStaysArmed()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            List<string> events = Record(source);
            source.Thrown += e => source.Arm(Context);

            source.EmitThrow(Throw);

            Assert.AreEqual(SwingPhase.WaitingForReady, source.Phase, "nothing disarms it after the handler");
            Assert.AreEqual(2, source.ArmCount);
            Assert.That(events, Is.EqualTo(new[]
            {
                "PhaseChanged:Disarmed:Disarmed",
                "Thrown:6.5:Disarmed",
                "PhaseChanged:WaitingForReady:WaitingForReady",
            }), "the re-arm inside Thrown is a real change from Disarmed, so it raises PhaseChanged");
            Assert.DoesNotThrow(() => source.EmitThrow(Throw), "and it can throw again");
        }

        [Test]
        public void SecondEmitThrowThrowsBecauseTheFirstDisarmed()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            source.EmitThrow(Throw);

            Assert.Throws<InvalidOperationException>(() => source.EmitThrow(Throw));
        }

        [Test]
        public void RejectWaitsForReadyThenRaisesPhaseChangedThenRejected()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            source.SetPhase(SwingPhase.Backswing);
            List<string> events = Record(source);

            source.Reject(ThrowRejection.SwingTooSmall);

            Assert.That(events, Is.EqualTo(new[]
            {
                "PhaseChanged:WaitingForReady:WaitingForReady",
                "Rejected:SwingTooSmall:WaitingForReady",
            }), "the source is already back in WaitingForReady when both events fire");
            Assert.AreEqual(SwingPhase.WaitingForReady, source.Phase);
        }

        [Test]
        public void RejectFromWaitingForReadyRaisesOnlyRejected()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            List<string> events = Record(source);

            source.Reject(ThrowRejection.PlayerLeft);

            Assert.That(events, Is.EqualTo(new[] { "Rejected:PlayerLeft:WaitingForReady" }),
                "the phase did not change, so there is no PhaseChanged");
        }

        [Test]
        public void RejectedHandlerMayDisarmAndTheSourceStaysDisarmed()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            source.SetPhase(SwingPhase.Backswing);
            source.Rejected += reason => source.Disarm();

            source.Reject(ThrowRejection.LowConfidence);

            Assert.AreEqual(SwingPhase.Disarmed, source.Phase, "nothing re-arms it after the handler");
            Assert.AreEqual(1, source.DisarmCount);
        }

        [Test]
        public void ArmAfterAThrowWorksAgain()
        {
            var source = new FakeThrowSource();
            source.Arm(Context);
            source.EmitThrow(Throw);

            source.Arm(Context);

            Assert.AreEqual(SwingPhase.WaitingForReady, source.Phase);
            Assert.AreEqual(2, source.ArmCount);
            Assert.DoesNotThrow(() => source.EmitThrow(Throw));
        }
    }
}
