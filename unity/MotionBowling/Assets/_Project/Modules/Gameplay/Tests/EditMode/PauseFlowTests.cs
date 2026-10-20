using System.Linq;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// The pause rule and Paused (spec section 4, P-3 to P-5): absence pauses Calibrate, AwaitReady and AwaitThrow but
    /// never a roll in progress, the player returning resumes, a long absence abandons the game, and a turn-change
    /// Calibrate waits longer for the next player.
    /// </summary>
    public sealed class PauseFlowTests
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

        [Test]
        public void AbsentTwoSecondsInAwaitReadyPauses()
        {
            _rig.PlayToFirstBall();
            int disarmsBefore = _rig.Throws.DisarmCount;

            _rig.Input.SetPresent(false);
            _rig.Wait(1.75);
            Assert.AreEqual(GameState.AwaitReady, Current.State);
            _rig.Wait(0.25);

            Assert.AreEqual(GameState.Paused, Current.State);
            Assert.AreEqual(disarmsBefore + 1, _rig.Throws.DisarmCount);
        }

        [Test]
        public void ReturningResumesTheSameState()
        {
            PauseFromAwaitReady();
            int armsBefore = _rig.Throws.ArmCount;
            PinMask rackBefore = _rig.Sim.LastRack;

            Return();

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(armsBefore + 1, _rig.Throws.ArmCount, "AwaitReady re-arms");
            Assert.AreEqual(rackBefore, _rig.Sim.LastRack);
            Assert.That(_rig.States.Skip(_rig.States.Count - 2), Is.EqualTo(new[] { GameState.Paused, GameState.AwaitReady }));
        }

        [Test]
        public void PausedFromAwaitThrowResumesInAwaitReady()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            Assert.AreEqual(GameState.AwaitThrow, Current.State);

            _rig.Input.SetPresent(false);
            _rig.Wait(2.0);
            Assert.AreEqual(GameState.Paused, Current.State);
            Return();

            Assert.AreEqual(GameState.AwaitReady, Current.State);
        }

        [Test]
        public void CalibrateResumesWithAFreshCalibration()
        {
            _rig.StartGame();

            _rig.Input.SetPresent(false);
            _rig.Wait(2.0);
            Assert.AreEqual(GameState.Paused, Current.State);
            Assert.AreEqual(CalibrationStep.Idle, _rig.Calibrator.Step, "Paused cancels the calibration");
            Return();
            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(2, _rig.Calibrator.BeginCount);
            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            Assert.AreEqual(3, _rig.Calibrator.BeginCount);
            Assert.AreEqual(GameState.Calibrate, Current.State, "the Timeout count was reset");
        }

        [Test]
        public void ResumingCalibrateResetsTheTimeoutCount()
        {
            _rig.StartGame();
            _rig.Calibrator.Fail(CalibrationFailure.Timeout);
            Assert.AreEqual(2, _rig.Calibrator.BeginCount, "the first Timeout begins again");

            _rig.Input.SetPresent(false);
            _rig.Wait(2.0);
            Assert.AreEqual(GameState.Paused, Current.State);
            Return();
            Assert.AreEqual(3, _rig.Calibrator.BeginCount);
            _rig.Calibrator.Fail(CalibrationFailure.Timeout);

            // Without the reset, this would be the second Timeout, which falls back to the default profile.
            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(4, _rig.Calibrator.BeginCount);
        }

        [Test]
        public void AbandonAfterFifteenSecondsOfPause()
        {
            PauseFromAwaitReady();

            _rig.Wait(14.75);
            Assert.AreEqual(GameState.Paused, Current.State);
            _rig.Wait(0.25);

            Assert.AreEqual(GameState.Attract, Current.State);
            Assert.IsEmpty(_rig.Leaderboard.Top(3, LeaderboardScope.AllTime, 5), "an abandoned game has no entry");
        }

        [Test]
        public void FlickerDoesNotAddUp()
        {
            _rig.PlayToFirstBall();

            _rig.Input.SetPresent(false);
            _rig.Wait(1.75);
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
            _rig.Input.SetPresent(false);
            _rig.Wait(1.75);
            Assert.AreEqual(GameState.AwaitReady, Current.State, "the absence starts again when presence returns");
            _rig.Wait(0.25);

            Assert.AreEqual(GameState.Paused, Current.State);
        }

        [Test]
        public void LeavingDuringTheRollPausesAtTheNextState()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            _rig.Throws.EmitThrow(GameplayRig.Throw);

            _rig.Input.SetPresent(false);
            _rig.Wait(3.0);
            Assert.AreEqual(GameState.Rolling, Current.State, "Rolling never pauses");
            _rig.Sim.CompleteWith(PinMask.P10);
            Assert.AreEqual(GameState.Scoring, Current.State);
            Assert.AreEqual(1, _rig.Outcomes.Count, "the roll is still scored");
            _rig.Wait(1.5);
            Assert.AreEqual(GameState.AwaitReady, Current.State, "Scoring never pauses");
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Paused, Current.State);
            _rig.Wait(14.75);
            Assert.AreEqual(GameState.Paused, Current.State, "the abandon timer counts from the pause");
            _rig.Wait(0.25);

            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [Test]
        public void TurnChangeWaitsThirtySeconds()
        {
            ReachTurnChangeCalibrate();

            _rig.Wait(28.25);
            Assert.AreEqual(GameState.Calibrate, Current.State);
            _rig.Wait(0.25);

            Assert.AreEqual(GameState.Paused, Current.State);
        }

        [Test]
        public void FirstTurnCalibrateUsesTwoSeconds()
        {
            _rig.StartGame(2, 3);

            _rig.Input.SetPresent(false);
            _rig.Wait(1.75);
            Assert.AreEqual(GameState.Calibrate, Current.State);
            _rig.Wait(0.25);

            Assert.AreEqual(GameState.Paused, Current.State);
        }

        [Test]
        public void TheNextPlayerSteppingInBeginsCalibration()
        {
            ReachTurnChangeCalibrate();

            _rig.Input.SetPresent(true);
            _rig.Service.Tick();

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(2, _rig.Calibrator.BeginCount);
        }

        // The player steps back in, and the next Tick sees it.
        private void Return()
        {
            _rig.Input.SetPresent(true);
            _rig.Service.Tick();
        }

        // From Attract: the first ball's AwaitReady, then 2 s of absence, which leaves the game in Paused.
        private void PauseFromAwaitReady()
        {
            _rig.PlayToFirstBall();
            _rig.Input.SetPresent(false);
            _rig.Wait(2.0);
            Assert.AreEqual(GameState.Paused, Current.State);
        }

        // From Attract: player 1's open frame in a 2-player game, with the zone empty from the last ball on, which
        // leaves the game in player 2's Calibrate with no calibration begun.
        private void ReachTurnChangeCalibrate()
        {
            _rig.PlayToFirstBall(2, 3);
            _rig.Knock(3);
            _rig.EndScoring();
            _rig.Knock(4);
            _rig.Input.SetPresent(false);
            _rig.EndScoring();

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(1, Current.CurrentPlayer);
            Assert.AreEqual(1, _rig.Calibrator.BeginCount, "nobody is there to calibrate yet");
        }
    }
}
