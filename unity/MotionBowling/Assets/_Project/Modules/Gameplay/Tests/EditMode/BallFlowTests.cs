using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// One ball, for a single player (spec sections 4 and 6, R22, P-9 to P-11): the throw rolls the simulator, the
    /// result is scored with the R22 event order, the Scoring hold depends on the mark, and a rejected swing re-arms
    /// without using the ball.
    /// </summary>
    public sealed class BallFlowTests
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
        public void AThrowRollsTheSimulator()
        {
            _rig.PlayToFirstBall();

            _rig.Throws.SetPhase(SwingPhase.Ready);
            _rig.Throws.EmitThrow(GameplayRig.Throw);

            Assert.AreEqual(GameState.Rolling, Current.State);
            Assert.AreEqual(1, _rig.Sim.RollCount);
            ThrowEvent expected = GameplayRig.Throw;
            ThrowEvent actual = _rig.Sim.LastThrow;
            Assert.AreEqual(expected.Timestamp, actual.Timestamp, "the camera-clock timestamp passes through untouched");
            Assert.AreEqual(expected.Hand, actual.Hand);
            Assert.AreEqual(expected.SpeedMps, actual.SpeedMps);
            Assert.AreEqual(expected.LaunchAngleDeg, actual.LaunchAngleDeg);
            Assert.AreEqual(expected.StartOffsetM, actual.StartOffsetM);
            Assert.AreEqual(expected.Hook, actual.Hook);
            Assert.AreEqual(expected.Confidence, actual.Confidence);
        }

        [Test]
        public void AScoredRollRaisesTheR22Events()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            _rig.Throws.EmitThrow(GameplayRig.Throw);
            Assert.AreEqual(GameState.Rolling, Current.State);
            _rig.Events.Clear();

            _rig.Sim.CompleteWith(PinMask.None);

            Assert.That(_rig.Events, Is.EqualTo(new[] { "State:Scoring", "Rolled:0:Strike", "Snapshot:Scoring" }));
            Assert.AreEqual(1, _rig.Outcomes.Count);
            Assert.AreEqual(PinMask.All, _rig.Outcomes[0].Result.StandingBefore);
            Assert.AreEqual(PinMask.None, _rig.Outcomes[0].Result.StandingAfter);
            Assert.AreEqual(1, Current.CurrentFrame, "Scoring names the roll just scored (P-11)");
            Assert.AreEqual(1, Current.CurrentBall, "Scoring names the roll just scored (P-11)");
            RollMark first = Current.Players[0].Frames[0].Rolls[0];
            Assert.AreEqual(10, first.Pins);
            Assert.AreEqual(MarkKind.Strike, first.Kind);
        }

        [Test]
        public void StrikeHoldsForTheReplay()
        {
            _rig.PlayToFirstBall();
            _rig.Knock(10);

            _rig.Wait(4.25);

            Assert.AreEqual(GameState.Scoring, Current.State);

            _rig.Wait(0.25);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(2, Current.CurrentFrame);
        }

        [Test]
        public void SpareHoldsForTheReplay()
        {
            _rig.PlayToFirstBall();
            _rig.Knock(7);
            _rig.EndScoring();

            _rig.Knock(3);

            Assert.AreEqual(MarkKind.Spare, _rig.Outcomes[1].Mark);

            _rig.Wait(4.25);

            Assert.AreEqual(GameState.Scoring, Current.State);

            _rig.Wait(0.25);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
        }

        [Test]
        public void AnOpenBallHoldsBriefly()
        {
            _rig.PlayToFirstBall();
            _rig.Knock(7);

            _rig.Wait(1.25);

            Assert.AreEqual(GameState.Scoring, Current.State);

            _rig.Wait(0.25);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(2, Current.CurrentBall);
            Assert.AreEqual(PinMask.P8 | PinMask.P9 | PinMask.P10, _rig.Sim.LastRack);
            Assert.AreEqual(2, _rig.Throws.ArmCount);
            Assert.That(_rig.Racks, Is.EqualTo(new[] { PinMask.All, PinMask.All, PinMask.P8 | PinMask.P9 | PinMask.P10 }),
                "Attract's rack, then each AwaitReady's");
        }

        [Test]
        public void ScoringHoldCountsFromTheRollsEnd()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            _rig.Throws.EmitThrow(GameplayRig.Throw);

            _rig.Wait(2.0);

            Assert.AreEqual(GameState.Rolling, Current.State, "Rolling has no timer");

            _rig.Sim.CompleteWith(PinMask.P8 | PinMask.P9 | PinMask.P10);

            Assert.AreEqual(GameState.Scoring, Current.State);

            _rig.Wait(1.25);

            Assert.AreEqual(GameState.Scoring, Current.State, "the hold starts when the roll ends, not when it began");

            _rig.Wait(0.25);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
        }

        [Test]
        public void ARejectedSwingReArmsWithoutUsingTheBall()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            Assert.AreEqual(GameState.AwaitThrow, Current.State);
            int racksBefore = _rig.Sim.SetRackCount;

            _rig.Throws.Reject(ThrowRejection.SwingTimeout);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(2, _rig.Throws.ArmCount);
            Assert.AreEqual(racksBefore + 1, _rig.Sim.SetRackCount);
            Assert.AreEqual(PinMask.All, _rig.Sim.LastRack);
            Assert.AreEqual(1, Current.CurrentBall, "the ball isn't used");
            Assert.AreEqual(0, _rig.Sim.RollCount);
            Assert.IsEmpty(_rig.Outcomes);
        }

        [Test]
        public void AThrowStraightFromAwaitReadyStillRolls()
        {
            _rig.PlayToFirstBall();

            _rig.Throws.EmitThrow(GameplayRig.Throw);

            Assert.AreEqual(GameState.Rolling, Current.State);
            Assert.AreEqual(1, _rig.Sim.RollCount);
        }

        [Test]
        public void ARejectionInAwaitReadyChangesNothing()
        {
            _rig.PlayToFirstBall();
            int eventsBefore = _rig.Events.Count;
            int logBefore = _rig.Log.Entries.Count;

            _rig.Throws.Reject(ThrowRejection.LowConfidence);

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.AreEqual(eventsBefore, _rig.Events.Count);
            Assert.AreEqual(1, _rig.Throws.ArmCount);
            Assert.AreEqual(logBefore, _rig.Log.Entries.Count, "the source is already waiting (P-10)");
        }

        [Test]
        public void APublishedSnapshotSurvivesLaterRolls()
        {
            _rig.PlayToFirstBall();
            _rig.Knock(10);
            GameSnapshot kept = _rig.Service.Current;

            _rig.EndScoring();
            _rig.Knock(3);
            _rig.EndScoring();
            _rig.Knock(4);

            Assert.AreNotSame(kept, _rig.Service.Current);
            Assert.AreEqual(17, Current.Players[0].Frames[0].Cumulative, "the strike's frame is final now");
            Assert.IsNull(kept.Players[0].Frames[0].Cumulative);
            Assert.AreEqual(0, kept.Players[0].Frames[1].Rolls.Count);
        }

        [Test]
        public void TheLastBallGoesToGameOver()
        {
            _rig.PlayToFirstBall();

            for (int frame = 1; frame <= 3; frame++)
            {
                _rig.Knock(3);
                _rig.EndScoring();
                _rig.Knock(4);
                _rig.EndScoring();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
        }
    }
}
