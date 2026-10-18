using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of FakeCalibrator.
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
        public void BeginWaitsForFullBodyCountsAndRaisesNothing()
        {
            var calibrator = new FakeCalibrator();
            List<string> events = Record(calibrator);

            calibrator.Begin();
            calibrator.Begin();

            Assert.AreEqual(CalibrationStep.WaitingForFullBody, calibrator.Step);
            Assert.AreEqual(2, calibrator.BeginCount);
            Assert.IsEmpty(events);
        }

        [Test]
        public void CompleteSetsDoneThenRaisesStepChangedThenCompleted()
        {
            var calibrator = new FakeCalibrator();
            List<string> events = Record(calibrator);
            calibrator.Begin();

            calibrator.Complete(Profile);

            Assert.AreEqual(CalibrationStep.Done, calibrator.Step);
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:Done:Done", "Completed:Left:Done" }),
                "Step is already Done inside both handlers");
        }

        [Test]
        public void CompleteDeliversTheProfile()
        {
            var calibrator = new FakeCalibrator();
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
            List<string> events = Record(calibrator);
            calibrator.Begin();

            calibrator.Fail(CalibrationFailure.PlayerLeft);

            Assert.AreEqual(CalibrationStep.Failed, calibrator.Step);
            Assert.That(events, Is.EqualTo(new[] { "StepChanged:Failed:Failed", "Failed:PlayerLeft:Failed" }));
        }

        [Test]
        public void CancelSetsIdleAndRaisesNothing()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Begin();
            List<string> events = Record(calibrator);

            calibrator.Cancel();

            Assert.AreEqual(CalibrationStep.Idle, calibrator.Step);
            Assert.IsEmpty(events);
        }

        [Test]
        public void HoldProgressIsSettableByTests()
        {
            var calibrator = new FakeCalibrator();

            calibrator.HoldProgress = 0.4f;

            Assert.AreEqual(0.4f, calibrator.HoldProgress);
        }

        [Test]
        public void BeginAfterCompleteRestartsTheFlow()
        {
            var calibrator = new FakeCalibrator();
            calibrator.Complete(Profile);

            calibrator.Begin();

            Assert.AreEqual(CalibrationStep.WaitingForFullBody, calibrator.Step);
            Assert.AreEqual(1, calibrator.BeginCount);
        }
    }
}
