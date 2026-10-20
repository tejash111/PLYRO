using System.Collections.Generic;
using System.Linq;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Turns in a game of 2 or more players (spec section 4, P-4): every new turn goes through Calibrate, so each
    /// player steps in and is armed with their own profile, while a single player calibrates only once.
    /// </summary>
    public sealed class MultiplayerFlowTests
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
        public void TwoPlayersCalibrateEveryTurn()
        {
            _rig.StartGame(2, 3);
            int[] turnPlayers = { 0, 1, 0, 1, 0, 1 };

            for (int turn = 0; turn < turnPlayers.Length; turn++)
            {
                Assert.AreEqual(GameState.Calibrate, Current.State, "turn " + (turn + 1));
                Assert.AreEqual(turnPlayers[turn], Current.CurrentPlayer, "turn " + (turn + 1));
                _rig.CompleteCalibration();
                PlayOpenFrame();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
            Assert.AreEqual(6, _rig.Calibrator.BeginCount);
            Assert.That(_rig.Outcomes.Select(o => o.PlayerIndex),
                Is.EqualTo(new[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1 }));
            IReadOnlyList<LeaderboardEntry> top = _rig.Leaderboard.Top(3, LeaderboardScope.Today, 5);
            Assert.AreEqual(2, top.Count);
            Assert.AreEqual("Player 1", top[0].DisplayName);
            Assert.AreEqual(21, top[0].Score);
            Assert.AreEqual(GameplayRig.UtcNow, top[0].PlayedAtUtc);
            Assert.AreEqual("Player 2", top[1].DisplayName);
            Assert.AreEqual(21, top[1].Score);
            Assert.AreEqual(GameplayRig.UtcNow, top[1].PlayedAtUtc);
        }

        [Test]
        public void EachTurnArmsThatTurnsProfile()
        {
            var profileA = new PlayerProfile(Handedness.Right, 0.6f, 0.4f, 0.5f);
            var profileB = new PlayerProfile(Handedness.Left, 0.7f, 0.42f, 0.45f);
            _rig.StartGame(2, 3);

            for (int turn = 0; turn < 6; turn++)
            {
                PlayerProfile profile = turn % 2 == 0 ? profileA : profileB;
                Assert.AreEqual(GameState.Calibrate, Current.State, "turn " + (turn + 1));
                _rig.CompleteCalibration(profile);
                AssertProfile(profile, _rig.Throws.LastContext.Player);
                _rig.Knock(3);
                _rig.EndScoring();
                AssertProfile(profile, _rig.Throws.LastContext.Player);
                _rig.Knock(4);
                _rig.EndScoring();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
        }

        [Test]
        public void OnePlayerCalibratesOnce()
        {
            _rig.PlayToFirstBall(1, 3);

            for (int frame = 1; frame <= 3; frame++)
            {
                PlayOpenFrame();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
            Assert.AreEqual(1, _rig.Calibrator.BeginCount);
            Assert.AreEqual(1, _rig.States.Count(s => s == GameState.Calibrate));
        }

        // In AwaitReady: 3 pins, then 4, each with its Scoring hold.
        private void PlayOpenFrame()
        {
            _rig.Knock(3);
            _rig.EndScoring();
            _rig.Knock(4);
            _rig.EndScoring();
        }

        private static void AssertProfile(PlayerProfile expected, PlayerProfile actual)
        {
            Assert.AreEqual(expected.BowlingHand, actual.BowlingHand);
            Assert.AreEqual(expected.ArmLengthM, actual.ArmLengthM);
            Assert.AreEqual(expected.ShoulderWidthM, actual.ShoulderWidthM);
            Assert.AreEqual(expected.NeutralImageX, actual.NeutralImageX);
        }
    }
}
