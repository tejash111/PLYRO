using System;
using UnityEngine;

namespace Bowling.Contracts
{
    // Physical facts (USBC dimensions, rounded) and game-wide clamps. All values are in metres or SI.
    // Pin numbering follows the standard, as seen by the bowler: 1 is the head pin; the second row is
    // 2 (left) and 3 (right); the third row is 4, 5, 6; the back row is 7, 8, 9, 10, left to right.
    public static class LaneSpec
    {
        public const float FoulLineToHeadPinM   = 18.288f;   // 60 ft
        public const float LaneWidthM           = 1.0541f;   // 41.5 in
        public const float GutterWidthM         = 0.235f;    // 9.25 in
        public const float ApproachLengthM      = 4.572f;    // 15 ft (visual only)
        public const float OilLengthM           = 12.192f;   // 40 ft oil pattern
        public const float PinSpacingM          = 0.3048f;   // 12 in, centre to centre
        public const float PinRowSpacingM       = 0.26396f;  // 12 in × sin 60°
        public const float PinHeightM           = 0.381f;    // 15 in
        public const float PinMaxDiameterM      = 0.121f;    // 4.766 in
        public const float PinMassKg            = 1.531f;    // 3 lb 6 oz
        public const float BallDiameterM        = 0.218f;
        public const float BallMassKg           = 6.35f;     // 14 lb
        public const float MinBallSpeedMps      = 4.5f;
        public const float MaxBallSpeedMps      = 9.5f;
        public const float MaxLaunchAngleDeg    = 3.0f;
        public const float MaxStartOffsetM      = 0.40f;     // keeps the ball on the lane at release

        private const int PinCount = 10;

        // 1..10, lane frame, base centre on the deck
        public static Vector3 PinSpot(int pinNumber)
        {
            if (pinNumber < 1 || pinNumber > PinCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pinNumber), pinNumber, "Pin number must be 1..10.");
            }

            // Row r (0..3) holds r + 1 pins, so the rows are 1 | 2-3 | 4-6 | 7-10.
            int row = 0;
            int firstPinInRow = 1;
            while (pinNumber >= firstPinInRow + row + 1)
            {
                firstPinInRow += row + 1;
                row++;
            }

            int indexInRow = pinNumber - firstPinInRow;
            float x = (indexInRow - row / 2f) * PinSpacingM;
            float z = FoulLineToHeadPinM + row * PinRowSpacingM;
            return new Vector3(x, 0f, z);
        }
    }
}
