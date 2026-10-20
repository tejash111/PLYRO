using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>Turn order, ball and rack tracking, and misuse of <see cref="GameSession"/>.</summary>
    public sealed class GameSessionTests
    {
        private static readonly DifficultyProfile StandardProfile = new DifficultyProfile(Difficulty.Standard, 0.5f, false);

        private static GameSession NewSession(int players, int frames)
        {
            return new GameSession(players, frames, StandardProfile);
        }

        // A non-gutter result that ended with the pins settled.
        private static RollResult Leave(PinMask before, PinMask after)
        {
            return new RollResult(before, after, false, RollEndReason.Settled);
        }

        // The rack with its lowest-numbered pins removed.
        private static PinMask Knock(PinMask rack, int pins)
        {
            PinMask after = rack;
            for (int bit = 1; pins > 0 && bit <= (int)PinMask.All; bit <<= 1)
            {
                var pin = (PinMask)bit;
                if ((rack & pin) != PinMask.None)
                {
                    after &= ~pin;
                    pins--;
                }
            }

            return after;
        }

        private static MarkKind Roll(GameSession session, int pins)
        {
            return session.ScoreRoll(Leave(session.Rack, Knock(session.Rack, pins)));
        }

        private static (int Player, int Frame, int Ball) Position(GameSession session)
        {
            return (session.CurrentPlayer, session.CurrentFrame, session.CurrentBall);
        }

        // Plays two players through 3 frames, every turn 3 then 4, calling onBallStart before each roll.
        private static List<bool> PlayThreeFrameDuel(GameSession session, Action<GameSession> onBallStart)
        {
            var advances = new List<bool>();
            while (!session.IsOver)
            {
                onBallStart(session);
                Roll(session, session.CurrentBall == 1 ? 3 : 4);
                if (!session.IsOver)
                {
                    advances.Add(session.Advance());
                }
            }

            return advances;
        }

        [TestCase(0, 3)]
        [TestCase(5, 3)]
        [TestCase(-1, 10)]
        [TestCase(1, 4)]
        [TestCase(2, 0)]
        [TestCase(2, 11)]
        public void ValidateModeRejects(int players, int frames)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameSession.ValidateMode(players, frames));
            Assert.Throws<ArgumentOutOfRangeException>(() => NewSession(players, frames));
        }

        [TestCase(1, 3)]
        [TestCase(2, 5)]
        [TestCase(4, 10)]
        public void ValidateModeAccepts(int players, int frames)
        {
            Assert.DoesNotThrow(() => GameSession.ValidateMode(players, frames));
            Assert.AreEqual(players, NewSession(players, frames).Players.Count);
        }

        [Test]
        public void StartsAtTheFirstBall()
        {
            GameSession session = NewSession(3, 5);

            Assert.AreEqual((0, 1, 1), Position(session));
            Assert.AreEqual(PinMask.All, session.Rack);
            Assert.AreEqual(5, session.FrameCount);
            Assert.AreEqual(Difficulty.Standard, session.Difficulty.Level);
            Assert.AreSame(session.Players[0], session.Current);
            Assert.IsFalse(session.IsOver);
            Assert.IsFalse(session.LeaderboardSubmitted);
        }

        [Test]
        public void PlayersAreNamedAndColored()
        {
            GameSession session = NewSession(4, 5);

            Assert.AreEqual(4, session.Players.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual($"Player {i + 1}", session.Players[i].DisplayName);
                Assert.AreEqual(i, session.Players[i].ColorIndex);
                Assert.IsFalse(session.Players[i].Profile.HasValue, $"player {i} profile");
                Assert.AreEqual(5, session.Players[i].Card.FrameCount, $"player {i} card");
            }
        }

        [Test]
        public void TurnOrderIsFrameMajor()
        {
            GameSession session = NewSession(2, 3);
            var positions = new List<(int, int, int)>();

            List<bool> advances = PlayThreeFrameDuel(session, s => positions.Add(Position(s)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (0, 1, 1), (0, 1, 2), (1, 1, 1), (1, 1, 2),
                    (0, 2, 1), (0, 2, 2), (1, 2, 1), (1, 2, 2),
                    (0, 3, 1), (0, 3, 2), (1, 3, 1), (1, 3, 2),
                },
                positions);
            CollectionAssert.AreEqual(
                new[] { false, true, false, true, false, true, false, true, false, true, false },
                advances);
            Assert.IsTrue(session.IsOver);
        }

        [Test]
        public void StrikeEndsTheTurn()
        {
            GameSession session = NewSession(2, 3);

            Assert.AreEqual(MarkKind.Strike, Roll(session, 10));
            Assert.IsTrue(session.Advance());

            Assert.AreEqual((1, 1, 1), Position(session));
            Assert.AreEqual(PinMask.All, session.Rack);
        }

        [Test]
        public void RackFollowsFrameRules()
        {
            GameSession session = NewSession(1, 3);

            Roll(session, 7);
            Assert.IsFalse(session.Advance());

            Assert.AreEqual(PinMask.P8 | PinMask.P9 | PinMask.P10, session.Rack);
            Assert.AreEqual(2, session.CurrentBall);
        }

        [Test]
        public void ScoresAgainstItsOwnRack()
        {
            GameSession session = NewSession(1, 3);

            // StandingBefore disagrees with the rack on purpose: the session trusts its own rack (P-9).
            MarkKind mark = session.ScoreRoll(Leave(PinMask.P1, PinMask.P1 | PinMask.P2));

            Assert.AreEqual(MarkKind.Count, mark);
            Assert.AreEqual(8, session.Current.Card.Marks(1)[0].Pins);
            session.Advance();
            Assert.AreEqual(PinMask.P1 | PinMask.P2, session.Rack);
        }

        [Test]
        public void KnockedPinsStayDown()
        {
            GameSession session = NewSession(1, 3);
            Roll(session, 7);
            session.Advance();

            // The simulator reports every pin standing, but 7 are already down: nothing new falls.
            MarkKind mark = session.ScoreRoll(Leave(PinMask.All, PinMask.All));

            Assert.AreEqual(MarkKind.Miss, mark);
            session.Advance();
            Assert.AreEqual((0, 2, 1), Position(session));
        }

        [Test]
        public void FinalFrameGivesThreeBalls()
        {
            GameSession session = NewSession(1, 3);
            var positions = new List<(int, int, int)>();

            for (int roll = 0; roll < 5; roll++)
            {
                positions.Add(Position(session));
                Assert.AreEqual(PinMask.All, session.Rack, $"rack before roll {roll + 1}");
                Roll(session, 10);
                if (!session.IsOver)
                {
                    session.Advance();
                }
            }

            Assert.AreEqual((0, 3, 3), positions[4]);
            Assert.IsTrue(session.IsOver);
        }

        [Test]
        public void CardAndSessionAgree()
        {
            GameSession session = NewSession(2, 3);

            PlayThreeFrameDuel(session, s =>
            {
                Assert.AreEqual(s.Current.Card.CurrentFrame, s.CurrentFrame, "frame");
                Assert.AreEqual(s.Current.Card.CurrentBall, s.CurrentBall, "ball");
                Assert.AreEqual(s.Current.Card.PinsStanding, s.Rack.Count(), "pins on the rack");
            });
        }

        [Test]
        public void MisuseThrows()
        {
            GameSession fresh = NewSession(1, 3);
            Assert.Throws<InvalidOperationException>(() => fresh.Advance(), "Advance before any roll");

            GameSession scored = NewSession(1, 3);
            Roll(scored, 3);
            Assert.Throws<InvalidOperationException>(() => Roll(scored, 4), "ScoreRoll twice without Advance");

            GameSession over = NewSession(1, 3);
            for (int roll = 0; roll < 5; roll++)
            {
                Roll(over, 10);
                if (!over.IsOver)
                {
                    over.Advance();
                }
            }

            Assert.IsTrue(over.IsOver);
            Assert.Throws<InvalidOperationException>(() => Roll(over, 0), "ScoreRoll after IsOver");
            Assert.Throws<InvalidOperationException>(() => over.Advance(), "Advance after IsOver");
        }
    }
}
