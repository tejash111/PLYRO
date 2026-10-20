using System.Linq;
using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// The operator's Pause, Resume and Abandon (spec section 4, P-5, P-6): where each applies, the deferral of Pause
    /// and Abandon while a roll is in progress, and the P-6 log where a command doesn't apply.
    /// </summary>
    public sealed class OperatorCommandTests
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
        public void PauseAndResumeFromAwaitThrow()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            Assert.AreEqual(GameState.AwaitThrow, Current.State);
            int disarmsBefore = _rig.Throws.DisarmCount;

            _rig.Service.Pause();
            Assert.AreEqual(GameState.Paused, Current.State);
            Assert.Greater(_rig.Throws.DisarmCount, disarmsBefore);
            _rig.Wait(30);
            Assert.AreEqual(GameState.Paused, Current.State, "the player is present: no abandon, no auto-resume");
            int armsBefore = _rig.Throws.ArmCount;
            _rig.Service.Resume();

            Assert.AreEqual(GameState.AwaitReady, Current.State);
            Assert.Greater(_rig.Throws.ArmCount, armsBefore);
        }

        [TestCase(GameState.Attract)]
        [TestCase(GameState.Results)]
        [TestCase(GameState.Paused)]
        public void PauseIsIgnoredWhereItDoesNotApply(GameState state)
        {
            Reach(state);
            _rig.Events.Clear();

            _rig.Service.Pause();

            Assert.IsEmpty(_rig.Events);
            Assert.AreEqual(state, Current.State);
            AssertIgnored("Pause ignored in " + state);
        }

        [Test]
        public void PauseDuringRollingWaitsForTheNextState()
        {
            ReachRolling();

            _rig.Service.Pause();
            Assert.AreEqual(GameState.Rolling, Current.State);
            _rig.Sim.CompleteWith(PinMask.None);
            Assert.AreEqual(GameState.Scoring, Current.State);
            Assert.AreEqual(1, _rig.Outcomes.Count);
            _rig.EndScoring();
            Assert.AreEqual(GameState.AwaitReady, Current.State);
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Paused, Current.State);
            _rig.Service.Resume();

            Assert.AreEqual(GameState.AwaitReady, Current.State);
        }

        [Test]
        public void PauseDuringScoringWaitsToo()
        {
            _rig.PlayToFirstBall();
            _rig.Knock(3);
            Assert.AreEqual(GameState.Scoring, Current.State);

            _rig.Service.Pause();
            Assert.AreEqual(GameState.Scoring, Current.State);
            _rig.EndScoring();
            Assert.AreEqual(GameState.AwaitReady, Current.State);
            _rig.Service.Tick();

            Assert.AreEqual(GameState.Paused, Current.State);
        }

        [Test]
        public void ADirectPauseSatisfiesADeferredOne()
        {
            ReachRolling();
            _rig.Service.Pause();
            _rig.Sim.CompleteWith(PinMask.None);
            _rig.EndScoring();
            Assert.AreEqual(GameState.AwaitReady, Current.State);

            _rig.Service.Pause();
            Assert.AreEqual(GameState.Paused, Current.State);
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Paused, Current.State);
            Assert.AreEqual(GameState.Paused, _rig.States[_rig.States.Count - 1]);
            Assert.AreEqual(GameState.AwaitReady, _rig.States[_rig.States.Count - 2], "the deferred pause adds no second Paused");
            _rig.Service.Resume();

            Assert.AreEqual(GameState.AwaitReady, Current.State);
        }

        [Test]
        public void PauseInGameOverSubmitsOnce()
        {
            PlayToGameOver();

            _rig.Service.Pause();
            Assert.AreEqual(GameState.Paused, Current.State);
            _rig.Service.Resume();
            Assert.AreEqual(GameState.GameOver, Current.State);
            Assert.AreEqual(1, _rig.Leaderboard.Top(3, LeaderboardScope.AllTime, 5).Count, "one submission per game");
            _rig.Wait(2.0);

            Assert.AreEqual(GameState.Results, Current.State);
        }

        [Test]
        public void ResumeOutsidePausedIsIgnored()
        {
            _rig.PlayToFirstBall();
            _rig.Events.Clear();

            _rig.Service.Resume();

            Assert.IsEmpty(_rig.Events);
            Assert.AreEqual(GameState.AwaitReady, Current.State);
            AssertIgnored("Resume ignored in AwaitReady");
        }

        [Test]
        public void AbandonDuringRollingWaitsForTheRoll()
        {
            ReachRolling();

            _rig.Service.Abandon();
            Assert.AreEqual(GameState.Rolling, Current.State);
            _rig.Sim.CompleteWith(PinMask.None);

            Assert.AreEqual(GameState.Attract, Current.State);
            Assert.That(_rig.States.Skip(_rig.States.Count - 2), Is.EqualTo(new[] { GameState.Rolling, GameState.Attract }));
            Assert.IsEmpty(_rig.Outcomes, "the roll is not scored");
            Assert.AreEqual(PinMask.All, _rig.Sim.LastRack, "the roll has ended, so Attract resets the rack");
            Assert.IsEmpty(_rig.Leaderboard.Top(3, LeaderboardScope.AllTime, 5));
        }

        [Test]
        public void AbandonFromCalibrateCancelsCalibration()
        {
            _rig.StartGame();
            Assert.AreNotEqual(CalibrationStep.Idle, _rig.Calibrator.Step);

            _rig.Service.Abandon();

            Assert.AreEqual(GameState.Attract, Current.State);
            Assert.AreEqual(CalibrationStep.Idle, _rig.Calibrator.Step);
            Assert.AreEqual(0, Current.Players.Count, "Attract drops the session");
        }

        [TestCase(GameState.Paused)]
        [TestCase(GameState.Results)]
        public void AbandonFromPausedAndResultsGoesToAttract(GameState state)
        {
            Reach(state);

            _rig.Service.Abandon();

            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [Test]
        public void AbandonInAttractIsIgnored()
        {
            _rig.Events.Clear();

            _rig.Service.Abandon();

            Assert.IsEmpty(_rig.Events);
            Assert.AreEqual(GameState.Attract, Current.State);
            AssertIgnored("Abandon ignored in Attract");
        }

        // From Attract: Attract itself, Paused (an operator pause at the first ball) or Results.
        private void Reach(GameState state)
        {
            switch (state)
            {
                case GameState.Paused:
                    _rig.PlayToFirstBall();
                    _rig.Service.Pause();
                    break;
                case GameState.Results:
                    PlayToGameOver();
                    _rig.Wait(_rig.Tuning.GameOverHold);
                    break;
            }

            Assert.AreEqual(state, Current.State);
        }

        // From Attract: the first ball is thrown, which leaves the game in Rolling.
        private void ReachRolling()
        {
            _rig.PlayToFirstBall();
            _rig.Throws.SetPhase(SwingPhase.Ready);
            _rig.Throws.EmitThrow(GameplayRig.Throw);
            Assert.AreEqual(GameState.Rolling, Current.State);
        }

        // From Attract: a 1-player, 3-frame game of open frames (3, then 4), which leaves the game in GameOver.
        private void PlayToGameOver()
        {
            _rig.PlayToFirstBall();
            for (int ball = 1; ball <= 6; ball++)
            {
                _rig.Knock(ball % 2 == 1 ? 3 : 4);
                _rig.EndScoring();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
        }

        private void AssertIgnored(string message)
        {
            Assert.That(_rig.Log.Entries, Has.Exactly(1).Matches<GameLogEntry>(e =>
                e.Level == GameLogLevel.Info && e.Category == "Gameplay" && e.Message == message));
        }
    }
}
