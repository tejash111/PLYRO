using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Ten-pin scoring for 3, 5 or 10 frames (spec section 5). The final frame always uses tenth-frame
    /// rules (up to 3 balls). Marks follow P-2: a rack is fresh at ball 1 and after a final-frame reset.
    /// </summary>
    internal sealed class ScoreCard
    {
        // The rules of bowling, not tuning.
        private const int PinCount = 10;
        private const int NoRoll = -1;

        private readonly int _frameCount;
        private readonly int[] _rolls;
        private readonly RollMark[] _marks;
        private readonly int[] _frameFirstRoll; // index into _rolls of each frame's first roll, or NoRoll
        private int _rollCount;

        private int _frame;                     // 0-based frame of the next roll
        private int _ball = 1;
        private int _pinsStanding = PinCount;
        private bool _freshRack = true;
        private bool _finalBonusEarned;         // the final frame's ball 1 cleared the rack
        private bool _complete;

        /// <summary>A card of <paramref name="frameCount"/> frames: 3, 5 or 10.</summary>
        public ScoreCard(int frameCount)
        {
            if (frameCount != 3 && frameCount != 5 && frameCount != 10)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), frameCount, "A card has 3, 5 or 10 frames.");
            }

            _frameCount = frameCount;
            int maxRolls = 2 * (frameCount - 1) + 3;
            _rolls = new int[maxRolls];
            _marks = new RollMark[maxRolls];
            _frameFirstRoll = new int[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                _frameFirstRoll[i] = NoRoll;
            }
        }

        public int FrameCount => _frameCount;

        public bool IsComplete => _complete;

        /// <summary>1-based frame of the next roll. Unchanged once the card is complete.</summary>
        public int CurrentFrame => _frame + 1;

        /// <summary>Ball (1..3) of the next roll. Unchanged once the card is complete.</summary>
        public int CurrentBall => _ball;

        /// <summary>Pins standing for the next roll. 0 once the card is complete.</summary>
        public int PinsStanding => _complete ? 0 : _pinsStanding;

        /// <summary>The next roll meets a fresh rack (P-2). False once the card is complete.</summary>
        public bool IsFreshRack => !_complete && _freshRack;

        /// <summary>Cumulative of the last final frame, or 0 when no frame is final.</summary>
        public int RunningTotal
        {
            get
            {
                int total = 0;
                for (int frame = 1; frame <= _frameCount; frame++)
                {
                    int? score = FrameScore(frame);
                    if (score == null)
                    {
                        break;
                    }

                    total += score.Value;
                }

                return total;
            }
        }

        /// <summary>
        /// Adds a roll and returns its mark. Throws <see cref="InvalidOperationException"/> when the card is
        /// complete (checked first), and <see cref="ArgumentOutOfRangeException"/> when <paramref name="pins"/>
        /// is below 0 or above the pins standing.
        /// </summary>
        public MarkKind AddRoll(int pins)
        {
            if (_complete)
            {
                throw new InvalidOperationException("The score card is complete.");
            }

            if (pins < 0 || pins > _pinsStanding)
            {
                throw new ArgumentOutOfRangeException(nameof(pins), pins, $"Pins must be 0..{_pinsStanding}.");
            }

            MarkKind kind = MarkOf(pins);
            if (_ball == 1)
            {
                _frameFirstRoll[_frame] = _rollCount;
            }

            _rolls[_rollCount] = pins;
            _marks[_rollCount] = new RollMark(pins, kind);
            _rollCount++;

            if (_frame == _frameCount - 1)
            {
                AdvanceFinalFrame(pins);
            }
            else
            {
                AdvanceOpenFrame(pins);
            }

            return kind;
        }

        /// <summary>The frame's score (1-based), or null until its bonus rolls are known.</summary>
        public int? FrameScore(int frame)
        {
            int index = FrameIndex(frame);
            int start = _frameFirstRoll[index];
            if (start == NoRoll)
            {
                return null;
            }

            if (index == _frameCount - 1)
            {
                return _complete ? SumRolls(start, _rollCount) : (int?)null;
            }

            if (_rolls[start] == PinCount)
            {
                return _rollCount >= start + 3 ? PinCount + _rolls[start + 1] + _rolls[start + 2] : (int?)null;
            }

            if (_rollCount < start + 2)
            {
                return null;
            }

            int open = _rolls[start] + _rolls[start + 1];
            if (open < PinCount)
            {
                return open;
            }

            return _rollCount >= start + 3 ? PinCount + _rolls[start + 2] : (int?)null;
        }

        /// <summary>Running total through the frame, or null unless it and every earlier frame are final.</summary>
        public int? Cumulative(int frame)
        {
            FrameIndex(frame);
            int total = 0;
            for (int f = 1; f <= frame; f++)
            {
                int? score = FrameScore(f);
                if (score == null)
                {
                    return null;
                }

                total += score.Value;
            }

            return total;
        }

        /// <summary>The frame's marks so far (0..3), in a new array on every call so snapshots can keep it.</summary>
        public IReadOnlyList<RollMark> Marks(int frame)
        {
            int index = FrameIndex(frame);
            int start = _frameFirstRoll[index];
            if (start == NoRoll)
            {
                return new RollMark[0];
            }

            int end = index + 1 < _frameCount && _frameFirstRoll[index + 1] != NoRoll
                ? _frameFirstRoll[index + 1]
                : _rollCount;
            var marks = new RollMark[end - start];
            Array.Copy(_marks, start, marks, 0, marks.Length);
            return marks;
        }

        private int FrameIndex(int frame)
        {
            if (frame < 1 || frame > _frameCount)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame, $"Frame must be 1..{_frameCount}.");
            }

            return frame - 1;
        }

        private MarkKind MarkOf(int pins)
        {
            if (pins == 0)
            {
                return MarkKind.Miss;
            }

            if (pins == _pinsStanding)
            {
                return _freshRack ? MarkKind.Strike : MarkKind.Spare;
            }

            return MarkKind.Count;
        }

        private int SumRolls(int start, int end)
        {
            int sum = 0;
            for (int i = start; i < end; i++)
            {
                sum += _rolls[i];
            }

            return sum;
        }

        // Frames before the last: a strike or the second ball ends the frame.
        private void AdvanceOpenFrame(int pins)
        {
            if (_ball == 1 && pins < PinCount)
            {
                _ball = 2;
                _pinsStanding -= pins;
                _freshRack = false;
                return;
            }

            _frame++;
            _ball = 1;
            _pinsStanding = PinCount;
            _freshRack = true;
        }

        // The final frame: clearing the rack on ball 1 or 2 resets it and earns the third ball.
        private void AdvanceFinalFrame(int pins)
        {
            if (_ball == 3)
            {
                _complete = true;
                return;
            }

            if (pins == _pinsStanding)
            {
                _finalBonusEarned = true;
                _ball++;
                _pinsStanding = PinCount;
                _freshRack = true;
                return;
            }

            _pinsStanding -= pins;
            _freshRack = false;
            if (_ball == 1 || _finalBonusEarned)
            {
                _ball++;
            }
            else
            {
                _complete = true;
            }
        }
    }
}
