using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>The required scoring vectors of spec section 5, with the roll counts of P-1.</summary>
    public sealed class ScoreCardVectorTests
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

        private static int[] Repeat(int count, params int[] pattern)
        {
            var rolls = new List<int>();
            for (int i = 0; i < count; i++)
            {
                rolls.AddRange(pattern);
            }

            return rolls.ToArray();
        }

        [Test]
        public void PerfectGame()
        {
            ScoreCard card = Play(10, Repeat(12, 10));

            Assert.AreEqual(300, card.RunningTotal);
            for (int frame = 1; frame <= 10; frame++)
            {
                Assert.AreEqual(30 * frame, card.Cumulative(frame), $"frame {frame}");
            }

            Assert.IsTrue(card.IsComplete);
        }

        [Test]
        public void GutterGame()
        {
            ScoreCard card = Play(10, Repeat(20, 0));

            Assert.AreEqual(0, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
            for (int frame = 1; frame <= 10; frame++)
            {
                IReadOnlyList<RollMark> marks = card.Marks(frame);
                Assert.AreEqual(2, marks.Count, $"frame {frame}");
                foreach (RollMark mark in marks)
                {
                    Assert.AreEqual(MarkKind.Miss, mark.Kind, $"frame {frame}");
                }
            }
        }

        [Test]
        public void AllNineZero()
        {
            ScoreCard card = Play(10, Repeat(10, 9, 0));

            Assert.AreEqual(90, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
        }

        [Test]
        public void AllSpares()
        {
            int[] rolls = new List<int>(Repeat(10, 5, 5)) { 5 }.ToArray();

            ScoreCard card = Play(10, rolls);

            Assert.AreEqual(150, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
        }

        [Test]
        public void StrikeSpareAlternating()
        {
            var rolls = new List<int>();
            for (int frame = 1; frame <= 9; frame++)
            {
                if (frame % 2 == 1)
                {
                    rolls.Add(10);
                }
                else
                {
                    rolls.AddRange(new[] { 5, 5 });
                }
            }

            rolls.AddRange(new[] { 5, 5, 10 });

            ScoreCard card = Play(10, rolls.ToArray());

            Assert.AreEqual(200, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
            for (int frame = 1; frame <= 10; frame++)
            {
                Assert.AreEqual(20, card.FrameScore(frame), $"frame {frame}");
            }
        }

        [Test]
        public void ClassicSample()
        {
            ScoreCard card = Play(10, 10, 7, 3, 9, 0, 10, 0, 8, 8, 2, 0, 6, 10, 10, 10, 8, 1);

            int[] expected = { 20, 39, 48, 66, 74, 84, 90, 120, 148, 167 };
            for (int frame = 1; frame <= 10; frame++)
            {
                Assert.AreEqual(expected[frame - 1], card.Cumulative(frame), $"frame {frame}");
            }

            Assert.AreEqual(167, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
        }

        [Test]
        public void ThreeFramePerfect()
        {
            ScoreCard card = Play(3, Repeat(5, 10));

            Assert.AreEqual(90, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
        }

        [Test]
        public void FiveFramePerfect()
        {
            ScoreCard card = Play(5, Repeat(7, 10));

            Assert.AreEqual(150, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
        }

        [Test]
        public void ThreeFrameOpen()
        {
            ScoreCard card = Play(3, Repeat(3, 3, 4));

            Assert.AreEqual(21, card.RunningTotal);
            Assert.IsTrue(card.IsComplete);
        }
    }
}
