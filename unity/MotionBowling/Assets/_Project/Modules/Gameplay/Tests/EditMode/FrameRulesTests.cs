using System;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Gameplay.Tests
{
    /// <summary>The rack decision table of spec section 4, with the freshness of P-2.</summary>
    public sealed class FrameRulesTests
    {
        // Frames before the last.
        [TestCase(false, 1, true, PinMask.None, true, PinMask.All, true, TestName = "Decides_Open_Strike")]
        [TestCase(false, 1, true, PinMask.P7 | PinMask.P10, false, PinMask.P7 | PinMask.P10, false, TestName = "Decides_Open_Leave")]
        [TestCase(false, 1, true, PinMask.All, false, PinMask.All, false, TestName = "Decides_Open_Gutter")]
        [TestCase(false, 2, false, PinMask.P10, true, PinMask.All, true, TestName = "Decides_Open_OpenFrame")]
        [TestCase(false, 2, false, PinMask.None, true, PinMask.All, true, TestName = "Decides_Open_Spare")]
        // The last frame.
        [TestCase(true, 1, true, PinMask.None, false, PinMask.All, true, TestName = "Decides_Last_Strike")]
        [TestCase(true, 1, true, PinMask.P7 | PinMask.P10, false, PinMask.P7 | PinMask.P10, false, TestName = "Decides_Last_Leave")]
        [TestCase(true, 1, true, PinMask.All, false, PinMask.All, false, TestName = "Decides_Last_Gutter")]
        [TestCase(true, 2, true, PinMask.None, false, PinMask.All, true, TestName = "Decides_Last_StrikeStrike")]
        [TestCase(true, 2, true, PinMask.P4, false, PinMask.P4, false, TestName = "Decides_Last_StrikeThenLeave")]
        [TestCase(true, 2, true, PinMask.All, false, PinMask.All, false, TestName = "Decides_Last_StrikeThenGutter")]
        [TestCase(true, 2, false, PinMask.None, false, PinMask.All, true, TestName = "Decides_Last_Spare")]
        [TestCase(true, 2, false, PinMask.P10, true, PinMask.All, true, TestName = "Decides_Last_OpenFrame")]
        [TestCase(true, 3, true, PinMask.None, true, PinMask.All, true, TestName = "Decides_Last_ThirdBallFresh")]
        [TestCase(true, 3, false, PinMask.P10, true, PinMask.All, true, TestName = "Decides_Last_ThirdBallLeave")]
        public void Decides(
            bool isLastFrame,
            int ball,
            bool freshRack,
            PinMask standingAfter,
            bool expectedComplete,
            PinMask expectedNextRack,
            bool expectedNextFresh)
        {
            RackDecision decision = FrameRules.AfterRoll(isLastFrame, ball, freshRack, standingAfter);

            Assert.AreEqual(expectedComplete, decision.FrameComplete, "FrameComplete");
            Assert.AreEqual(expectedNextRack, decision.NextRack, "NextRack");
            Assert.AreEqual(expectedNextFresh, decision.NextRackFresh, "NextRackFresh");
        }

        [TestCase(false, 3, true, PinMask.None)]
        [TestCase(false, 3, false, PinMask.P10)]
        [TestCase(false, 0, true, PinMask.All)]
        [TestCase(false, 4, false, PinMask.None)]
        [TestCase(true, 0, true, PinMask.All)]
        [TestCase(true, 4, true, PinMask.None)]
        [TestCase(true, -1, false, PinMask.P10)]
        public void RejectsImpossibleBalls(bool isLastFrame, int ball, bool freshRack, PinMask standingAfter)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => FrameRules.AfterRoll(isLastFrame, ball, freshRack, standingAfter));
        }
    }
}
