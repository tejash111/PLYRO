using System.Collections.Generic;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>
    /// Replays whole games through <see cref="ScoreCard"/> and <see cref="FrameRules"/> side by side, so the
    /// rack the physics will see and the pins the card scores against can never diverge.
    /// </summary>
    public sealed class FrameRulesAgreementTests
    {
        private static IEnumerable<TestCaseData> Games()
        {
            yield return Game("PerfectGame", 10, Repeat(12, 10));
            yield return Game("GutterGame", 10, Repeat(20, 0));
            yield return Game("AllNineZero", 10, Repeat(10, 9, 0));
            yield return Game("AllSpares", 10, Concat(Repeat(10, 5, 5), 5));
            yield return Game("StrikeSpareAlternating", 10, StrikeSpareAlternating());
            yield return Game("ClassicSample", 10, 10, 7, 3, 9, 0, 10, 0, 8, 8, 2, 0, 6, 10, 10, 10, 8, 1);
            yield return Game("ThreeFramePerfect", 3, Repeat(5, 10));
            yield return Game("FiveFramePerfect", 5, Repeat(7, 10));
            yield return Game("ThreeFrameOpen", 3, Repeat(3, 3, 4));
            yield return Game("GutterThenTenIsASpare", 3, 0, 10, 5, 0, 3, 4);
            yield return Game("FinalFrameStrikeGutterTen", 3, 0, 0, 0, 0, 10, 0, 10);
            yield return Game("FinalFrameGutterTenTen", 3, 0, 0, 0, 0, 0, 10, 10);
        }

        [TestCaseSource(nameof(Games))]
        public void AgreesWithScoreCard(int frames, int[] rolls)
        {
            var card = new ScoreCard(frames);
            PinMask rack = PinMask.All;
            bool fresh = true;

            for (int i = 0; i < rolls.Length; i++)
            {
                bool isLastFrame = card.CurrentFrame == card.FrameCount;
                int frameBefore = card.CurrentFrame;
                int ball = card.CurrentBall;
                PinMask standingAfter = Knock(rack, rolls[i]);

                card.AddRoll(rolls[i]);
                RackDecision decision = FrameRules.AfterRoll(isLastFrame, ball, fresh, standingAfter);

                string at = $"roll {i + 1} ({rolls[i]})";
                Assert.AreEqual(card.CurrentFrame != frameBefore || card.IsComplete, decision.FrameComplete, $"{at}: FrameComplete");
                if (!card.IsComplete)
                {
                    Assert.AreEqual(card.PinsStanding, decision.NextRack.Count(), $"{at}: pins on the next rack");
                    Assert.AreEqual(card.IsFreshRack, decision.NextRackFresh, $"{at}: next rack fresh");
                }

                rack = decision.NextRack;
                fresh = decision.NextRackFresh;
            }

            Assert.IsTrue(card.IsComplete, "the game ends with the card complete");
        }

        // Knocks the lowest-numbered standing pins.
        private static PinMask Knock(PinMask rack, int pins)
        {
            Assert.LessOrEqual(pins, rack.Count(), "a roll can't knock more pins than stand");
            PinMask result = rack;
            for (int bit = 0; bit < 10 && pins > 0; bit++)
            {
                var pin = (PinMask)(1 << bit);
                if ((result & pin) != 0)
                {
                    result &= ~pin;
                    pins--;
                }
            }

            return result;
        }

        private static TestCaseData Game(string name, int frames, params int[] rolls)
        {
            return new TestCaseData(frames, rolls).SetName($"AgreesWithScoreCard_{name}");
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

        private static int[] Concat(int[] head, params int[] tail)
        {
            var rolls = new List<int>(head);
            rolls.AddRange(tail);
            return rolls.ToArray();
        }

        private static int[] StrikeSpareAlternating()
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
            return rolls.ToArray();
        }
    }
}
