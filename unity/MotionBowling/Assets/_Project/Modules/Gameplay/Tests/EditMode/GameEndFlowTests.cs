using System;
using System.Collections.Generic;
using Bowling.Contracts;
using Bowling.Testing;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// The end of a game (spec section 4, P-6, P-12): GameOver submits one leaderboard entry per player and holds,
    /// Results holds or gives up on an absent player, "play again" starts a new game, and Attract resets the lane.
    /// </summary>
    public sealed class GameEndFlowTests
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
        public void HappyPathOnePlayerThreeFrames()
        {
            _rig.PlayToFirstBall();
            foreach (int pins in new[] { 10, 7, 3, 10, 4, 2 })
            {
                _rig.Knock(pins);
                _rig.EndScoring();
            }

            _rig.Wait(2.0);
            _rig.Wait(10.0);

            var expected = new List<GameState>
            {
                GameState.Join, GameState.ModeSelect, GameState.Calibrate,
                GameState.AwaitReady, GameState.AwaitThrow, GameState.Rolling, GameState.Scoring,
            };
            for (int ball = 2; ball <= 6; ball++)
            {
                expected.AddRange(new[] { GameState.AwaitReady, GameState.AwaitThrow, GameState.Rolling, GameState.Scoring });
            }

            expected.AddRange(new[] { GameState.GameOver, GameState.Results, GameState.Attract });
            Assert.That(_rig.States, Is.EqualTo(expected));
            PinMask lastThree = PinMask.P8 | PinMask.P9 | PinMask.P10;
            PinMask lastSix = PinMask.P5 | PinMask.P6 | PinMask.P7 | lastThree;
            Assert.That(_rig.Racks, Is.EqualTo(new[]
            {
                PinMask.All, PinMask.All, PinMask.All, lastThree, PinMask.All, PinMask.All, lastSix, PinMask.All,
            }), "Attract's rack, each AwaitReady's, then Attract's again");
            Assert.AreEqual(6, _rig.Throws.ArmCount);
            Assert.AreEqual(6, _rig.Sim.RollCount);
            IReadOnlyList<LeaderboardEntry> top = _rig.Leaderboard.Top(3, LeaderboardScope.Today, 5);
            Assert.AreEqual(1, top.Count);
            AssertEntry("Player 1", 56, 3, top[0]);
            Assert.AreEqual(0, Current.Players.Count, "Attract drops the session");
            Assert.That(_rig.Events, Has.None.EndsWith(" stale"));
        }

        [Test]
        public void GameOverHoldsThenResults()
        {
            PlayToGameOver();

            _rig.Wait(1.75);
            Assert.AreEqual(GameState.GameOver, Current.State);
            _rig.Wait(0.25);
            Assert.AreEqual(GameState.Results, Current.State);
            _rig.Wait(9.75);
            Assert.AreEqual(GameState.Results, Current.State);
            _rig.Wait(0.25);
            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [Test]
        public void ResultsWithNobodyForThreeSecondsReturnsToAttract()
        {
            PlayToResults();

            _rig.Input.SetPresent(false);
            _rig.Wait(2.75);
            Assert.AreEqual(GameState.Results, Current.State);
            _rig.Wait(0.25);
            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [Test]
        public void ResultsAbsenceCountsFromResultsEntry()
        {
            _rig.PlayToFirstBall();
            PlayOpenFrame();
            PlayOpenFrame();
            _rig.Knock(3);
            _rig.EndScoring();
            _rig.Knock(4);
            _rig.Input.SetPresent(false);       // the player leaves during the last Scoring hold
            _rig.EndScoring();
            _rig.Wait(_rig.Tuning.GameOverHold);
            Assert.AreEqual(GameState.Results, Current.State);

            // Gone for 3.5 s by now, but Results counts from its own entry (P-3).
            _rig.Service.Tick();
            Assert.AreEqual(GameState.Results, Current.State);
            _rig.Wait(2.75);
            Assert.AreEqual(GameState.Results, Current.State);
            _rig.Wait(0.25);
            Assert.AreEqual(GameState.Attract, Current.State);
        }

        [Test]
        public void PlayAgainStartsANewGame()
        {
            PlayToResults();

            _rig.Service.SelectMode(1, 3, Difficulty.Pro);

            Assert.AreEqual(GameState.Calibrate, Current.State);
            Assert.AreEqual(0, Current.Players[0].RunningTotal);
            Assert.AreEqual(Difficulty.Pro, Current.Difficulty.Level);
            Assert.AreEqual(2, _rig.Calibrator.BeginCount);
            Assert.That(_rig.Log.Entries, Has.None.Matches<GameLogEntry>(e => e.Message == "SelectMode ignored in Results"));
        }

        [Test]
        public void AttractAfterTheGameResetsTheLane()
        {
            PlayToGameOver(Difficulty.Casual);
            int disarmsBefore = _rig.Throws.DisarmCount;
            Assert.IsTrue(_rig.Sim.LastBumpers, "Casual bowls with bumpers");
            Assert.AreNotEqual(PinMask.All, _rig.Sim.LastRack, "the last ball faced a partial rack");

            _rig.Wait(2.0);
            _rig.Wait(10.0);

            Assert.AreEqual(GameState.Attract, Current.State);
            Assert.Greater(_rig.Throws.DisarmCount, disarmsBefore);
            Assert.IsFalse(_rig.Sim.LastBumpers);
            Assert.AreEqual(PinMask.All, _rig.Sim.LastRack);
        }

        [Test]
        public void AFailingSubmitIsLoggedAndTheOthersStillGo()
        {
            var store = new ThrowingLeaderboardStore("Player 1");
            _rig.Dispose();
            _rig = new GameplayRig(store);
            _rig.StartGame(2, 3);
            for (int turn = 1; turn <= 6; turn++)
            {
                _rig.CompleteCalibration();
                PlayOpenFrame();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
            Assert.That(_rig.Log.Entries, Has.Exactly(1).Matches<GameLogEntry>(e =>
                e.Level == GameLogLevel.Error && e.Category == "Gameplay"
                && e.Message == "Leaderboard submit failed for Player 1" && ReferenceEquals(e.Exception, store.Thrown)));
            IReadOnlyList<LeaderboardEntry> top = _rig.Leaderboard.Top(3, LeaderboardScope.Today, 5);
            Assert.AreEqual(1, top.Count);
            AssertEntry("Player 2", 21, 3, top[0]);

            _rig.Wait(2.0);

            Assert.AreEqual(GameState.Results, Current.State, "the game still reaches Results (P-12)");
        }

        [TestCase("store")]
        [TestCase("log")]
        [TestCase("utcNow")]
        public void LeaderboardWriterRejectsNullArguments(string parameter)
        {
            Func<DateTime> utcNow = () => GameplayRig.UtcNow;

            var thrown = Assert.Throws<ArgumentNullException>(() => new LeaderboardWriter(
                parameter == "store" ? null : _rig.Leaderboard,
                parameter == "log" ? null : _rig.Log,
                parameter == "utcNow" ? null : utcNow));

            Assert.AreEqual(parameter, thrown.ParamName);
        }

        // From Attract: a 1-player, 3-frame game of open frames (3, then 4), which leaves the game in GameOver.
        private void PlayToGameOver(Difficulty difficulty = Difficulty.Casual)
        {
            _rig.PlayToFirstBall(1, 3, difficulty);
            for (int frame = 1; frame <= 3; frame++)
            {
                PlayOpenFrame();
            }

            Assert.AreEqual(GameState.GameOver, Current.State);
        }

        // From Attract: PlayToGameOver, then the GameOver hold, which leaves the game in Results.
        private void PlayToResults()
        {
            PlayToGameOver();
            _rig.Wait(_rig.Tuning.GameOverHold);
            Assert.AreEqual(GameState.Results, Current.State);
        }

        // In AwaitReady: 3 pins, then 4, each with its Scoring hold.
        private void PlayOpenFrame()
        {
            _rig.Knock(3);
            _rig.EndScoring();
            _rig.Knock(4);
            _rig.EndScoring();
        }

        private static void AssertEntry(string name, int score, int frameCount, LeaderboardEntry actual)
        {
            Assert.AreEqual(name, actual.DisplayName);
            Assert.AreEqual(score, actual.Score);
            Assert.AreEqual(frameCount, actual.FrameCount);
            Assert.AreEqual(GameplayRig.UtcNow, actual.PlayedAtUtc);
        }
    }
}
