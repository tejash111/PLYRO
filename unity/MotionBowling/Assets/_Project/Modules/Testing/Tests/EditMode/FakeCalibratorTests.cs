using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeCalibrator (ruling R21: state first, then events, and StepChanged on every
    // actual step change). Each event name carries calibrator.Step as the handler saw it.
    public class FakeCalibratorTests
    {
        private static readonly PlayerProfile Profile = new PlayerProfile(Handedness.Left, 0.62f, 0.41f, 0.55f);

        private static List<string> Record(FakeCalibrator calibrator)
        {
            var events = new List<string>();
            calibrator.StepChanged += step => events.Add("StepChanged:" + step + ":" + calibrator.Step);
            calibrator.Completed += profile => events.Add("Completed:" + profile.BowlingHand + ":" + calibrator.Step);
            calibrator.Failed += reason => events.Add("Failed:" + reason + ":" + calibrator.Step);
            return events;
        }

        [Test]
        public void StartsIdleWithNoProgressAndNoBegins()
        {
            var calibrator = new FakeCalibrator();

            Assert.AreEqual(CalibrationStep.Idle, calibrator.Step);
            Assert.AreEqual(0f, calibrator.HoldProgress);
            Assert.AreEqual(0, calibrator.BeginCount);
        }

        [Test]
        public void BeginWaitsForFullBodyAndRaisesStepChangedOnlyWhenTheStepChanges()
        {
            var calibrator = new FakeCalibrator();
            List<string> events = Record(calibrator);

            calibrator.Begin();
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:WaitingForFullBody:WaitingForFullBody" }),
                "the step already holds the new value inside the handler");

            calibrator.Begin();

            Assert.AreEqual(CalibrationStep.WaitingForFullBody, calibrator.Step);
            Assert.AreEqual(2, calibrator.BeginCount);
            Assert.AreEqual(1, events.Count, "WaitingForFullBody to WaitingForFullBody is not a change");
        }

        [Test]
        public void CompleteSetsDoneThenRaisesStepChangedThenCompleted()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            List<string> events = Record(calibrator);

            calibrator.Complete(Profile);

            Assert.AreEqual(CalibrationStep.Done, calibrator.Step);
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:Done:Done", "Completed:Left:Done" }),
                "Step is already Done inside both handlers");
        }

        [Test]
        public void CompleteDeliversTheProfile()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            PlayerProfile received = default;
            calibrator.Completed += profile => received = profile;

            calibrator.Complete(Profile);

            Assert.AreEqual(Handedness.Left, received.BowlingHand);
            Assert.AreEqual(0.62f, received.ArmLengthM);
            Assert.AreEqual(0.41f, received.ShoulderWidthM);
            Assert.AreEqual(0.55f, received.NeutralImageX);
        }

        [Test]
        public void FailSetsFailedThenRaisesStepChangedThenFailed()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            List<string> events = Record(calibrator);

            calibrator.Fail(CalibrationFailure.PlayerLeft);

            Assert.AreEqual(CalibrationStep.Failed, calibrator.Step);
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:Failed:Failed", "Failed:PlayerLeft:Failed" }));
        }

        [Test]
        public void CompletedHandlerMayBeginAgainAndTheNewStepSticks()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Completed += profile => calibrator.Begin();

            calibrator.Complete(Profile);

            Assert.AreEqual(CalibrationStep.WaitingForFullBody, calibrator.Step, "nothing overwrites it after the handler");
            Assert.AreEqual(2, calibrator.BeginCount, "one Begin to start, one from the handler");
        }

        [Test]
        public void FailedHandlerMayBeginAgainAndTheNewStepSticks()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Failed += reason => calibrator.Begin();

            calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(CalibrationStep.WaitingForFullBody, calibrator.Step);
        }

        [Test]
        public void CancelSetsIdleAndRaisesStepChangedOnlyWhenTheStepChanges()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            List<string> events = Record(calibrator);

            calibrator.Cancel();
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:Idle:Idle" }));

            calibrator.Cancel();

            Assert.AreEqual(CalibrationStep.Idle, calibrator.Step);
            Assert.AreEqual(1, events.Count, "Idle to Idle is not a change");
        }

        [Test]
        public void CancelAfterCompleteReturnsToIdleWithAnEvent()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Complete(Profile);
            List<string> events = Record(calibrator);

            calibrator.Cancel();

            Assert.That(events, Is.EqualTo(new[] { "StepChanged:Idle:Idle" }));
        }

        [Test]
        public void HoldProgressIsSettableByTests()
        {
            var calibrator = new FakeCalibrator();

            calibrator.HoldProgress = 0.4f;

            Assert.AreEqual(0.4f, calibrator.HoldProgress);
        }

        [Test]
        public void BeginAfterCompleteRestartsTheFlowWithAnEvent()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Complete(Profile);
            List<string> events = Record(calibrator);

            calibrator.Begin();

            Assert.AreEqual(CalibrationStep.WaitingForFullBody, calibrator.Step);
            Assert.AreEqual(2, calibrator.BeginCount);
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:WaitingForFullBody:WaitingForFullBody" }));
        }

        // Complete and Fail are only valid while a calibration is in progress (Step is not Idle, Done or Failed),
        // like the real calibrator. Without the guard a test could pass by completing a calibration nobody began.
        // A refused call changes nothing: no step change, no event.

        private static void AssertRefused(FakeCalibrator calibrator, List<string> events, CalibrationStep step, TestDelegate call)
        {
            int before = events.Count;

            var exception = Assert.Throws<InvalidOperationException>(call);

            Assert.That(exception.Message, Does.Contain(step.ToString()), "the message names the step");
            Assert.AreEqual(step, calibrator.Step, "a refused call leaves the step alone");
            Assert.AreEqual(before, events.Count, "a refused call raises nothing");
        }

        [Test]
        public void CompleteAndFailAreRefusedBeforeAnyBegin()
        {
            var calibrator = new FakeCalibrator();
            List<string> events = Record(calibrator);

            AssertRefused(calibrator, events, CalibrationStep.Idle, () => calibrator.Complete(Profile));
            AssertRefused(calibrator, events, CalibrationStep.Idle, () => calibrator.Fail(CalibrationFailure.Timeout));
        }

        [Test]
        public void CompleteAndFailAreRefusedAfterCancel()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Cancel();
            List<string> events = Record(calibrator);

            AssertRefused(calibrator, events, CalibrationStep.Idle, () => calibrator.Complete(Profile));
            AssertRefused(calibrator, events, CalibrationStep.Idle, () => calibrator.Fail(CalibrationFailure.PlayerLeft));
        }

        [Test]
        public void CompleteAndFailAreRefusedAfterComplete()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Complete(Profile);
            List<string> events = Record(calibrator);

            AssertRefused(calibrator, events, CalibrationStep.Done, () => calibrator.Complete(Profile));
            AssertRefused(calibrator, events, CalibrationStep.Done, () => calibrator.Fail(CalibrationFailure.TrackingLost));
        }

        [Test]
        public void CompleteAndFailAreRefusedAfterFail()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Fail(CalibrationFailure.Timeout);
            List<string> events = Record(calibrator);

            AssertRefused(calibrator, events, CalibrationStep.Failed, () => calibrator.Complete(Profile));
            AssertRefused(calibrator, events, CalibrationStep.Failed, () => calibrator.Fail(CalibrationFailure.Timeout));
        }

        [Test]
        public void AFinishedCalibrationCanBeBegunAndFinishedAgain()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            calibrator.Fail(CalibrationFailure.Timeout);

            calibrator.Begin();
            calibrator.Complete(Profile);

            Assert.AreEqual(CalibrationStep.Done, calibrator.Step);
            Assert.AreEqual(2, calibrator.BeginCount);
        }

        [Test]
        public void CompleteFromAHandlerOfTheSameCalibrationIsRefused()
        {
            // By the time StepChanged(Done) or Completed runs, the calibration is over: a second outcome is a bug.
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            Exception thrown = null;
            calibrator.Completed += profile =>
            {
                try { calibrator.Fail(CalibrationFailure.Timeout); }
                catch (InvalidOperationException e) { thrown = e; }
            };

            calibrator.Complete(Profile);

            Assert.That(thrown, Is.Not.Null);
            Assert.AreEqual(CalibrationStep.Done, calibrator.Step);
        }
    }
}
