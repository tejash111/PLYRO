using System;
using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>Validation, pending bonuses, marks (P-2) and the next-roll state of ScoreCard.</summary>
    public sealed class ScoreCardRulesTests
    {
        private static ScoreCard Play(int frames, params int[] rolls)
        {
            var card = new ScoreCard(frames);
            foreach (int pins in rolls)
            {
                card.AddRoll(pins);
            }

            return card;
        }

        // RollMark has no equality operator, so marks are compared field by field.
        private static void AssertMarks(IReadOnlyList<RollMark> actual, params (int Pins, MarkKind Kind)[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Pins, actual[i].Pins, $"mark {i} pins");
                Assert.AreEqual(expected[i].Kind, actual[i].Kind, $"mark {i} kind");
            }
        }

        private static void AssertNextRoll(ScoreCard card, int frame, int ball, int pinsStanding, bool fresh)
        {
            Assert.AreEqual(frame, card.CurrentFrame, "CurrentFrame");
            Assert.AreEqual(ball, card.CurrentBall, "CurrentBall");
            Assert.AreEqual(pinsStanding, card.PinsStanding, "PinsStanding");
            Assert.AreEqual(fresh, card.IsFreshRack, "IsFreshRack");
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(9)]
        [TestCase(11)]
        public void RejectsOtherFrameCounts(int frames)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreCard(frames));
        }

        [TestCase(3)]
        [TestCase(5)]
        [TestCase(10)]
        public void AcceptsThreeFiveAndTenFrames(int frames)
        {
            Assert.AreEqual(frames, new ScoreCard(frames).FrameCount);
        }

        [Test]
        public void SevenThenFourThrows()
        {
            var card = new ScoreCard(3);
            card.AddRoll(7);

            Assert.Throws<ArgumentOutOfRangeException>(() => card.AddRoll(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScoreCard(3).AddRoll(-1));
        }

        [Test]
        public void RollAfterCompleteThrows()
        {
            ScoreCard card = Play(3, 10, 10, 10, 10, 10);

            Assert.IsTrue(card.IsComplete);
            Assert.Throws<InvalidOperationException>(() => card.AddRoll(0));
            Assert.Throws<InvalidOperationException>(() => card.AddRoll(11), "complete is checked before the range");
        }

        [Test]
        public void PendingBonus()
        {
            var card = new ScoreCard(3);

            card.AddRoll(10);
            Assert.IsNull(card.FrameScore(1));
            Assert.IsNull(card.Cumulative(1));
            Assert.AreEqual(0, card.RunningTotal);

            card.AddRoll(3);
            Assert.IsNull(card.FrameScore(1));
            Assert.IsNull(card.Cumulative(1));
            Assert.AreEqual(0, card.RunningTotal);

            card.AddRoll(4);
            Assert.AreEqual(17, card.FrameScore(1));
            Assert.AreEqual(24, card.RunningTotal);
        }

        [Test]
        public void SparePendsUntilTheNextRoll()
        {
            var card = new ScoreCard(3);
            card.AddRoll(7);
            card.AddRoll(3);

            Assert.IsNull(card.FrameScore(1));
            Assert.AreEqual(0, card.RunningTotal);

            card.AddRoll(5);

            Assert.AreEqual(15, card.FrameScore(1));
        }

        [Test]
        public void CumulativeWaitsForEveryEarlierFrame()
        {
            ScoreCard card = Play(3, 10, 3);

            Assert.IsNull(card.FrameScore(2), "frame 2 is still open");
            Assert.IsNull(card.Cumulative(2));

            card.AddRoll(4);

            Assert.AreEqual(7, card.FrameScore(2));
            Assert.AreEqual(24, card.Cumulative(2));
        }

        [Test]
        public void MarksOfEachKind()
        {
            ScoreCard card = Play(3, 10, 7, 3, 0, 5);

            AssertMarks(card.Marks(1), (10, MarkKind.Strike));
            AssertMarks(card.Marks(2), (7, MarkKind.Count), (3, MarkKind.Spare));
            AssertMarks(card.Marks(3), (0, MarkKind.Miss), (5, MarkKind.Count));
        }

        [Test]
        public void AddRollReturnsTheRollsMark()
        {
            var card = new ScoreCard(3);

            Assert.AreEqual(MarkKind.Count, card.AddRoll(7));
            Assert.AreEqual(MarkKind.Spare, card.AddRoll(3));
            Assert.AreEqual(MarkKind.Strike, card.AddRoll(10));
            Assert.AreEqual(MarkKind.Miss, card.AddRoll(0));
        }

        [Test]
        public void GutterThenTenIsASpare()
        {
            ScoreCard card = Play(3, 0, 10, 5, 0, 3, 4);

            AssertMarks(card.Marks(1), (0, MarkKind.Miss), (10, MarkKind.Spare));
            Assert.AreEqual(15, card.FrameScore(1));
            Assert.AreEqual(27, card.RunningTotal);
        }

        [Test]
        public void FinalFrameStrikeGutterTen()
        {
            ScoreCard card = Play(3, 0, 0, 0, 0, 10, 0, 10);

            AssertMarks(card.Marks(3), (10, MarkKind.Strike), (0, MarkKind.Miss), (10, MarkKind.Spare));
            Assert.AreEqual(20, card.FrameScore(3));
        }

        [Test]
        public void FinalFrameGutterTenTen()
        {
            ScoreCard card = Play(3, 0, 0, 0, 0, 0, 10, 10);

            AssertMarks(card.Marks(3), (0, MarkKind.Miss), (10, MarkKind.Spare), (10, MarkKind.Strike));
            Assert.AreEqual(20, card.FrameScore(3));
        }

        [Test]
        public void NextRollState()
        {
            var card = new ScoreCard(3);
            AssertNextRoll(card, 1, 1, 10, true);

            card.AddRoll(7);
            AssertNextRoll(card, 1, 2, 3, false);

            card.AddRoll(3);
            AssertNextRoll(card, 2, 1, 10, true);

            card.AddRoll(10);
            AssertNextRoll(card, 3, 1, 10, true);

            card.AddRoll(10);
            AssertNextRoll(card, 3, 2, 10, true);

            card.AddRoll(4);
            AssertNextRoll(card, 3, 3, 6, false);
        }

        [Test]
        public void CompleteCardKeepsItsFrameAndBall()
        {
            ScoreCard card = Play(3, 10, 10, 10, 10, 10);

            AssertNextRoll(card, 3, 3, 0, false);
        }

        [Test]
        public void MarksIsANewArrayEachCall()
        {
            ScoreCard card = Play(3, 7, 3);

            Assert.AreNotSame(card.Marks(1), card.Marks(1));
            Assert.AreNotSame(card.Marks(3), card.Marks(3), "even an empty frame gets a new array");
        }

        [Test]
        public void FrameIndexOutOfRangeThrows()
        {
            var card = new ScoreCard(3);

            Assert.Throws<ArgumentOutOfRangeException>(() => card.FrameScore(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => card.Cumulative(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => card.Marks(4));
        }
    }
}
