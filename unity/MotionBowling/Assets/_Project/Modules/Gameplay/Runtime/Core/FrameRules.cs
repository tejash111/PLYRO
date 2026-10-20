using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Decides, after each ball, whether the frame is over and which pins the next ball faces (spec section 4,
    /// "Rack decision"). <see cref="ScoreCard"/> scores by pin counts and this decides the physics rack; the
    /// agreement tests keep the two in step. Freshness is an input and an output because a mask can't tell a
    /// fresh rack from one where ball 1 hit nothing (P-2).
    /// </summary>
    internal static class FrameRules
    {
        private const int LastBall = 3;

        /// <summary>
        /// The decision after a ball. <paramref name="ball"/> is the ball just rolled, 1..3 (3 only when
        /// <paramref name="isLastFrame"/>), else <see cref="ArgumentOutOfRangeException"/>.
        /// <paramref name="freshRack"/> says that ball met a fresh rack; <paramref name="standingAfter"/> is
        /// the pins it left.
        /// </summary>
        public static RackDecision AfterRoll(bool isLastFrame, int ball, bool freshRack, PinMask standingAfter)
        {
            if (ball < 1 || ball > (isLastFrame ? LastBall : LastBall - 1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ball), ball, isLastFrame ? "Ball must be 1..3." : "Ball must be 1..2 before the last frame.");
            }

            return isLastFrame
                ? AfterLastFrameRoll(ball, freshRack, standingAfter)
                : AfterOpenFrameRoll(ball, standingAfter);
        }

        // Frames before the last: a strike or the second ball ends the frame.
        private static RackDecision AfterOpenFrameRoll(int ball, PinMask standingAfter)
        {
            if (ball == 2 || standingAfter == PinMask.None)
            {
                return NewFrame();
            }

            return Carry(standingAfter);
        }

        // The last frame: clearing the rack resets it without ending the frame, and ball 3 ends it.
        private static RackDecision AfterLastFrameRoll(int ball, bool freshRack, PinMask standingAfter)
        {
            if (ball == LastBall)
            {
                return NewFrame();
            }

            if (standingAfter == PinMask.None)
            {
                return Reset();
            }

            // Ball 2 on a rack that wasn't reset (no strike on ball 1) and didn't clear it: an open frame.
            if (ball == 2 && !freshRack)
            {
                return NewFrame();
            }

            return Carry(standingAfter);
        }

        private static RackDecision NewFrame()
        {
            return new RackDecision(true, PinMask.All, true);
        }

        // Mid-frame reset of a cleared rack: a fresh rack, same frame.
        private static RackDecision Reset()
        {
            return new RackDecision(false, PinMask.All, true);
        }

        // The pins left stand for the next ball. All pins standing is a gutter, and still not a fresh rack.
        private static RackDecision Carry(PinMask standing)
        {
            return new RackDecision(false, standing, false);
        }
    }
}
