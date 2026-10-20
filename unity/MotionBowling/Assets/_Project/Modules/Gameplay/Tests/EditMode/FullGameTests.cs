using System.Diagnostics;
using System.Linq;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Acceptance criterion 3: a full 10-frame, 4-player game on the fakes runs to the end in under a second of wall
    /// time, calibrates every turn, and puts every player's final score on the leaderboard.
    /// </summary>
    public sealed class FullGameTests
    {
        private const int Players = 4;
        private const int Frames = 10;
        private const int TurnLimit = 1000;     // a guard: a stuck game fails the test instead of hanging it

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

        [Test]
        public void FourPlayersTenFramesUnderASecond()
        {
            int[][] scripts =
            {
                Repeat(10, 12),                         // Player 1: perfect, 12 x 10
                Pairs(9, 0, 10),                        // Player 2: (9, 0) x 10
                Pairs(5, 5, 10).Append(5).ToArray(),    // Player 3: (5, 5) x 10, then 5
                Repeat(0, 20),                          // Player 4: 20 x 0
            };
            var nextRoll = new int[Players];

            var stopwatch = Stopwatch.StartNew();
            _rig.StartGame(Players, Frames);
            for (int turn = 0; _rig.Service.Current.State != GameState.GameOver; turn++)
            {
                Assert.Less(turn, TurnLimit, "the game never ended; stuck in " + _rig.Service.Current.State);
                GameState state = _rig.Service.Current.State;
                switch (state)
                {
                    case GameState.Calibrate:
                        _rig.CompleteCalibration();
                        break;
                    case GameState.AwaitReady:
                        int bowler = _rig.Service.Current.CurrentPlayer;
                        _rig.Knock(scripts[bowler][nextRoll[bowler]++]);
                        break;
                    case GameState.Scoring:
                        _rig.EndScoring();
                        break;
                    default:
                        Assert.Fail("unexpected state " + state);
                        break;
                }
            }

            _rig.Wait(_rig.Tuning.GameOverHold);
            stopwatch.Stop();

            Assert.AreEqual(GameState.Results, _rig.Service.Current.State);
            Assert.Less(stopwatch.ElapsedMilliseconds, 1000, "wall time of the whole game");
            Assert.AreEqual(Players * Frames, _rig.Calibrator.BeginCount, "one calibration per turn");
            for (int player = 0; player < Players; player++)
            {
                Assert.AreEqual(scripts[player].Length, nextRoll[player], "every roll of Player " + (player + 1) + " was bowled");
            }

            var scores = _rig.Leaderboard.Top(Frames, LeaderboardScope.AllTime, 5)
                .ToDictionary(entry => entry.DisplayName, entry => entry.Score);
            Assert.AreEqual(Players, scores.Count);
            Assert.AreEqual(300, scores["Player 1"]);
            Assert.AreEqual(90, scores["Player 2"]);
            Assert.AreEqual(150, scores["Player 3"]);
            Assert.AreEqual(0, scores["Player 4"]);
            Assert.That(_rig.Events, Has.None.EndsWith(" stale"));
        }

        // `count` rolls of `pins`.
        private static int[] Repeat(int pins, int count) => Enumerable.Repeat(pins, count).ToArray();

        // `first`, then `second`, `count` times over.
        private static int[] Pairs(int first, int second, int count) =>
            Enumerable.Repeat(new[] { first, second }, count).SelectMany(pair => pair).ToArray();
    }
}
