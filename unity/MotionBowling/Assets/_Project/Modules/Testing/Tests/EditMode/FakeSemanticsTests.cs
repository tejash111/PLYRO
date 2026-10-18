using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of the time and tracking fakes: FakeClock, FakeTrackingSource, FakePlayerInput.
    public class FakeSemanticsTests
    {
        private static PoseSample Sample(double timestamp)
        {
            return new PoseSample { Timestamp = timestamp, FrameIndex = (long)(timestamp * 10) };
        }

        private static PlayerPose Pose(double timestamp)
        {
            return new PlayerPose { Timestamp = timestamp };
        }

        // ---- FakeClock ----

        [Test]
        public void ClockStartsAtZero()
        {
            IClock clock = new FakeClock();

            Assert.AreEqual(0.0, clock.Now);
        }

        [Test]
        public void ClockAdvanceAddsSeconds()
        {
            var clock = new FakeClock();

            clock.Advance(1.5);
            clock.Advance(0.25);
            clock.Advance(0.0);

            Assert.AreEqual(1.75, clock.Now);
        }

        [Test]
        public void ClockAdvanceNegativeThrowsAndLeavesTimeUnchanged()
        {
            var clock = new FakeClock();
            clock.Advance(2.0);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-0.001));
            Assert.AreEqual(2.0, clock.Now);
        }

        [Test]
        public void ClockAdvanceNotANumberThrows()
        {
            var clock = new FakeClock();

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN));
            Assert.AreEqual(0.0, clock.Now);
        }

        // ---- FakeTrackingSource ----

        [Test]
        public void TrackingSourceStartsStoppedWithNoCalls()
        {
            var source = new FakeTrackingSource();

            Assert.AreEqual(TrackingStatus.Stopped, source.Status);
            Assert.AreEqual(0, source.StartCount);
            Assert.AreEqual(0, source.StopCount);
        }

        [Test]
        public void TrackingSourceStartAndStopSetStatusAndCountCalls()
        {
            var source = new FakeTrackingSource();

            source.Start();
            Assert.AreEqual(TrackingStatus.Running, source.Status);
            Assert.AreEqual(1, source.StartCount);
            Assert.AreEqual(0, source.StopCount);

            source.Stop();
            Assert.AreEqual(TrackingStatus.Stopped, source.Status);
            Assert.AreEqual(1, source.StartCount);
            Assert.AreEqual(1, source.StopCount);

            source.Start();
            Assert.AreEqual(TrackingStatus.Running, source.Status);
            Assert.AreEqual(2, source.StartCount);
        }

        [Test]
        public void TrackingSourceSetStatusOverridesStatus()
        {
            var source = new FakeTrackingSource();
            source.Start();

            source.SetStatus(TrackingStatus.NoCamera);

            Assert.AreEqual(TrackingStatus.NoCamera, source.Status);
            Assert.AreEqual(1, source.StartCount);
        }

        [Test]
        public void TrackingSourceReadAppendsOnlyNewerSamplesOldestFirst()
        {
            var source = new FakeTrackingSource();
            PoseSample s1 = Sample(1.0), s2 = Sample(2.0), s3 = Sample(3.0), s4 = Sample(4.0);
            source.Push(s1);
            source.Push(s2);
            source.Push(s3);
            source.Push(s4);
            PoseSample sentinel = Sample(-1.0);
            var into = new List<PoseSample> { sentinel };

            int count = source.ReadSamplesSince(2.0, into);

            Assert.AreEqual(2, count, "Timestamp == t is excluded, so only 3.0 and 4.0 are newer");
            Assert.AreEqual(3, into.Count, "the existing list entry stays, results are appended");
            Assert.AreSame(sentinel, into[0]);
            Assert.AreSame(s3, into[1]);
            Assert.AreSame(s4, into[2]);
        }

        [Test]
        public void TrackingSourceReadBeforeEverySampleReturnsAll()
        {
            var source = new FakeTrackingSource();
            source.Push(Sample(0.5));
            source.Push(Sample(1.5));
            var into = new List<PoseSample>();

            int count = source.ReadSamplesSince(double.NegativeInfinity, into);

            Assert.AreEqual(2, count);
            Assert.AreEqual(2, into.Count);
        }

        [Test]
        public void TrackingSourceReadAfterNewestSampleAppendsNothing()
        {
            var source = new FakeTrackingSource();
            source.Push(Sample(1.0));
            var into = new List<PoseSample>();

            Assert.AreEqual(0, source.ReadSamplesSince(1.0, into));
            Assert.AreEqual(0, source.ReadSamplesSince(9.0, into));
            Assert.AreEqual(0, new FakeTrackingSource().ReadSamplesSince(0.0, into));
            Assert.IsEmpty(into);
        }

        [Test]
        public void TrackingSourceReadDoesNotConsumeSamples()
        {
            var source = new FakeTrackingSource();
            source.Push(Sample(1.0));
            source.Push(Sample(2.0));

            int first = source.ReadSamplesSince(0.0, new List<PoseSample>());
            int second = source.ReadSamplesSince(0.0, new List<PoseSample>());

            Assert.AreEqual(2, first);
            Assert.AreEqual(2, second);
        }

        [Test]
        public void TrackingSourceRejectsNullArguments()
        {
            var source = new FakeTrackingSource();

            Assert.Throws<ArgumentNullException>(() => source.Push(null));
            Assert.Throws<ArgumentNullException>(() => source.ReadSamplesSince(0.0, null));
        }

        // ---- FakePlayerInput ----

        [Test]
        public void PlayerInputStartsStoppedAbsentWithNoPose()
        {
            var input = new FakePlayerInput();

            Assert.AreEqual(TrackingStatus.Stopped, input.Status);
            Assert.IsFalse(input.IsPlayerPresent);
            Assert.IsNull(input.Latest);
        }

        [Test]
        public void PlayerInputSetPresentRaisesOnlyWhenTheValueChanges()
        {
            var input = new FakePlayerInput();
            var events = new List<string>();
            input.PresenceChanged += present => events.Add("Presence:" + present + ":" + input.IsPlayerPresent);

            input.SetPresent(false);
            input.SetPresent(true);
            input.SetPresent(true);
            input.SetPresent(false);
            input.SetPresent(false);

            Assert.That(events, Is.EqualTo(new[] { "Presence:True:True", "Presence:False:False" }),
                "one event per change, and the property already holds the new value inside the handler");
        }

        [Test]
        public void PlayerInputSetStatusRaisesOnlyWhenTheValueChanges()
        {
            var input = new FakePlayerInput();
            var events = new List<string>();
            input.StatusChanged += status => events.Add("Status:" + status + ":" + input.Status);

            input.SetStatus(TrackingStatus.Stopped);
            input.SetStatus(TrackingStatus.Running);
            input.SetStatus(TrackingStatus.Running);
            input.SetStatus(TrackingStatus.NoCamera);

            Assert.That(events, Is.EqualTo(new[] { "Status:Running:Running", "Status:NoCamera:NoCamera" }));
        }

        [Test]
        public void PlayerInputPresenceAndStatusEventsAreIndependent()
        {
            var input = new FakePlayerInput();
            var events = new List<string>();
            input.PresenceChanged += present => events.Add("Presence:" + present);
            input.StatusChanged += status => events.Add("Status:" + status);

            input.SetStatus(TrackingStatus.Running);
            input.SetPresent(true);
            input.PushPose(Pose(1.0));

            Assert.That(events, Is.EqualTo(new[] { "Status:Running", "Presence:True" }),
                "pushing a pose raises no event");
        }

        [Test]
        public void PlayerInputPushPoseSetsLatestToTheNewestPose()
        {
            var input = new FakePlayerInput();
            PlayerPose first = Pose(1.0), second = Pose(2.0);

            input.PushPose(first);
            Assert.AreSame(first, input.Latest);

            input.PushPose(second);
            Assert.AreSame(second, input.Latest);
        }

        [Test]
        public void PlayerInputReadAppendsOnlyNewerPosesOldestFirst()
        {
            var input = new FakePlayerInput();
            PlayerPose p1 = Pose(1.0), p2 = Pose(2.0), p3 = Pose(3.0);
            input.PushPose(p1);
            input.PushPose(p2);
            input.PushPose(p3);
            PlayerPose sentinel = Pose(-1.0);
            var into = new List<PlayerPose> { sentinel };

            int count = input.ReadPosesSince(1.0, into);

            Assert.AreEqual(2, count, "Timestamp == t is excluded");
            Assert.AreEqual(3, into.Count);
            Assert.AreSame(sentinel, into[0]);
            Assert.AreSame(p2, into[1]);
            Assert.AreSame(p3, into[2]);
            Assert.AreEqual(0, input.ReadPosesSince(3.0, new List<PlayerPose>()));
            Assert.AreEqual(3, input.ReadPosesSince(0.0, new List<PlayerPose>()), "reads do not consume");
        }

        [Test]
        public void PlayerInputRejectsNullArguments()
        {
            var input = new FakePlayerInput();

            Assert.Throws<ArgumentNullException>(() => input.PushPose(null));
            Assert.Throws<ArgumentNullException>(() => input.ReadPosesSince(0.0, null));
        }
    }
}
