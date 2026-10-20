using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Calibrate and the way out of it (spec section 4, P-4, P-7): the Begin rules, the two-Timeout fallback to the
    /// default profile, and arming the throw source on entering AwaitReady until the swing starts.
    /// </summary>
    public sealed class CalibrationFlowTests
    {
        private GameplayRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GameplayRig();
        }

        [TearDown]
        public void TearDown()
        {
            _rig?.Dispose();
        }

        private GameSnapshot Current => _rig.Service.Current;

        private static void AssertProfile(PlayerProfile expected, PlayerProfile actual)
        {
            Assert.AreEqual(expected.BowlingHand, actual.BowlingHand);
            Assert.AreEqual(expected.ArmLengthM, actual.ArmLengthM);
            Assert.AreEqual(expected.ShoulderWidthM, actual.ShoulderWidthM);
            Assert.AreEqual(expected.NeutralImageX, actual.NeutralImageX);
        }

        private static void AssertDefaultProfile(PlayerProfile actual)
        {
            AssertProfile(new PlayerProfile(Handedness.Right, 0.60f, 0.38f, 0.5f), actual);
        }

        [Test]
        public void CompletedCalibrationArmsTheThrowSource()
        {
            _rig.PlayToFirstBall(1, 3, Difficulty.Standard);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(1, _rig.Throws.ArmCount);
            AssertProfile(GameplayRig.Profile, _rig.Throws.LastContext.Player);
            Assert.AreEqual(Difficulty.Standard, _rig.Throws.LastContext.Difficulty.Level);
            Assert.AreEqual(0.25f, _rig.Throws.LastContext.Difficulty.Assist);
            Assert.IsFalse(_rig.Throws.LastContext.Difficulty.Bumpers);
            Assert.IsFalse(_rig.Sim.LastBumpers);
            Assert.AreEqual(PinMask.All, _rig.Sim.LastRack);
        }

        [Test]
        public void CompletingCalibrationRaisesTheAwaitReadySequence()
        {
            _rig.StartGame();
            _rig.Events.Clear();

            _rig.CompleteCalibration();

            // The PhaseChanged that Arm raises is queued and ignored, so it adds nothing.
            Assert.That(_rig.Events, Is.EqualTo(new[] { "State:AwaitReady", "Snapshot:AwaitReady" }));
            Assert.IsEmpty(_rig.Log.Entries);
        }

        [Test]
        public void CasualTurnsBumpersOn()
        {
            _rig.PlayToFirstBall();

            Assert.IsTrue(_rig.Sim.LastBumpers);
        }

        [TestCase(SwingPhase.Ready)]
        [TestCase(SwingPhase.Backswing)]
        [TestCase(SwingPhase.ForwardSwing)]
        public void ReadyPhasesMoveToAwaitThrow(SwingPhase phase)
        {
            _rig.PlayToFirstBall();

            _rig.Throws.SetPhase(phase);

            Assert.AreEqual(GameState.AwaitThrow, Current.State);
            Assert.AreEqual("Snapshot:AwaitThrow", _rig.Events[_rig.Events.Count - 1]);
        }

        [TestCase(SwingPhase.WaitingForReady)]
        [TestCase(SwingPhase.Disarmed)]
        [TestCase(SwingPhase.Released)]
        public void OtherPhasesStayInAwaitReadyWithoutALog(SwingPhase phase)
        {
            _rig.PlayToFirstBall();
            int eventsBefore = _rig.Events.Count;

            _rig.Throws.SetPhase(phase);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(eventsBefore, _rig.Events.Count);
            Assert.IsEmpty(_rig.Log.Entries);
        }

        [Test]
        public void PhaseChangesOutsideAwaitReadyAreIgnoredWithoutALog()
        {
            _rig.StartGame();
            int eventsBefore = _rig.Events.Count;

            _rig.Throws.SetPhase(SwingPhase.Ready);

            Assert.AreEqual(GameState.Calibrate, Current.State, "Calibrate ignores the swing phase");
            Assert.AreEqual(eventsBefore, _rig.Events.Count);

            _rig.CompleteCalibration();
            _rig.Throws.SetPhase(SwingPhase.Backswing);
            eventsBefore = _rig.Events.Count;

            _rig.Throws.SetPhase(SwingPhase.ForwardSwing);

            Assert.AreEqual(GameState.AwaitThrow, Current.State, "AwaitThrow ignores further phases");
            Assert.AreEqual(eventsBefore, _rig.Events.Count);
            Assert.IsEmpty(_rig.Log.Entries);
        }

        [Test]
        public void FirstTimeoutRetriesSecondUsesTheDefaultProfile()
        {
            _rig.StartGame();

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(2, _rig.Calibrator.BeginCount);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(1, _rig.Throws.ArmCount);
            AssertDefaultProfile(_rig.Throws.LastContext.Player);
        }

        [Test]
        public void PlayerLeftIsNotAFailure()
        {
            _rig.StartGame();
            _rig.Input.SetPresent(false);
            _rig.Calibrator.Fail(CalibrationFailure.PlayerLeft);
            _rig.Service.Tick();

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(1, _rig.Calibrator.BeginCount, "nobody is there to calibrate");

            _rig.Input.SetPresent(true);
            _rig.Service.Tick();

            Assert.AreEqual(2, _rig.Calibrator.BeginCount);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(3, _rig.Calibrator.BeginCount);
            Assert.AreEqual(GameState.Calibrate, Current.State);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            AssertDefaultProfile(_rig.Throws.LastContext.Player);
        }

        [Test]
        public void TrackingLostIsNotAFailure()
        {
            _rig.StartGame();

            _rig.Calibrator.Fail(CalibrationFailure.TrackingLost);
            _rig.Service.Tick();

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(2, _rig.Calibrator.BeginCount);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(GameState.Calibrate, Current.State, "one Timeout is not enough");
            Assert.AreEqual(3, _rig.Calibrator.BeginCount);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            AssertDefaultProfile(_rig.Throws.LastContext.Player);
        }

        [Test]
        public void CalibrateWithNobodyPresentWaitsToBegin()
        {
            _rig.Join();
            _rig.Input.SetPresent(false);

            _rig.Service.SelectMode(1, 3, Difficulty.Casual);

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(0, _rig.Calibrator.BeginCount);

            _rig.Input.SetPresent(true);
            _rig.Service.Tick();

            Assert.AreEqual(1, _rig.Calibrator.BeginCount);
        }

        [Test]
        public void TickDoesNotBeginWhileACalibrationIsRunning()
        {
            _rig.StartGame();

            _rig.Service.Tick();
            _rig.Wait(0.25);

            Assert.AreEqual(1, _rig.Calibrator.BeginCount);
        }

        [Test]
        public void TimeoutWhileAbsentBeginsOnceThePlayerReturns()
        {
            _rig.StartGame();
            _rig.Input.SetPresent(false);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(1, _rig.Calibrator.BeginCount, "no Begin for an absent player");

            _rig.Input.SetPresent(true);
            _rig.Service.Tick();

            Assert.AreEqual(2, _rig.Calibrator.BeginCount);
            Assert.AreEqual(GameState.Calibrate, Current.State);

            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(GameState.AwaitReady, Current.State, "the first Timeout still counts");
        }
    }
}
