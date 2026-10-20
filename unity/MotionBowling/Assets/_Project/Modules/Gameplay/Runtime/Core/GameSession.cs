using System;
using System.Collections.Generic;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The state of one game: who is playing, whose turn it is (frame-major, so every player bowls frame 1
    /// before anyone bowls frame 2), which ball, and the rack that ball faces. The state machine calls
    /// <see cref="ScoreRoll"/> with each physics result and <see cref="Advance"/> when the scoring hold ends.
    /// </summary>
    internal sealed class GameSession
    {
        // The modes the game offers (spec section 3), not tuning.
        private const int MinPlayers = 1;
        private const int MaxPlayers = 4;

        private readonly PlayerSlot[] _slots;
        private readonly IReadOnlyList<PlayerSlot> _players;

        private int _currentPlayer;
        private int _currentFrame = 1;
        private int _currentBall = 1;
        private PinMask _rack = PinMask.All;
        private bool _rackFresh = true;
        private RackDecision? _pending;             // the decision for the scored roll, until Advance

        /// <summary>A game for <paramref name="playerCount"/> players of <paramref name="frameCount"/> frames.</summary>
        public GameSession(int playerCount, int frameCount, DifficultyProfile difficulty)
        {
            ValidateMode(playerCount, frameCount);

            FrameCount = frameCount;
            Difficulty = difficulty;
            _slots = new PlayerSlot[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                _slots[i] = new PlayerSlot(i, frameCount);
            }

            _players = Array.AsReadOnly(_slots);
        }

        public IReadOnlyList<PlayerSlot> Players => _players;

        public int FrameCount { get; }

        public DifficultyProfile Difficulty { get; }

        /// <summary>Index into <see cref="Players"/> of the player whose turn it is. Starts at 0.</summary>
        public int CurrentPlayer => _currentPlayer;

        /// <summary>1-based frame of the current turn. Starts at 1.</summary>
        public int CurrentFrame => _currentFrame;

        /// <summary>Ball of the current turn: 1..2, or 1..3 in the last frame. Starts at 1.</summary>
        public int CurrentBall => _currentBall;

        /// <summary>The pins standing for the current ball. Starts as <see cref="PinMask.All"/>.</summary>
        public PinMask Rack => _rack;

        public PlayerSlot Current => _slots[_currentPlayer];

        /// <summary>Every card is complete.</summary>
        public bool IsOver
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (!_slots[i].Card.IsComplete)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>The final scores have been offered to the leaderboard (once per game).</summary>
        public bool LeaderboardSubmitted { get; set; }

        /// <summary>
        /// Throws <see cref="ArgumentOutOfRangeException"/> unless there are 1 to 4 players and 3, 5 or 10 frames.
        /// </summary>
        public static void ValidateMode(int playerCount, int frameCount)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount), playerCount, $"A game has {MinPlayers} to {MaxPlayers} players.");
            }

            if (frameCount != 3 && frameCount != 5 && frameCount != 10)
            {
                throw new ArgumentOutOfRangeException(nameof(frameCount), frameCount, "A game has 3, 5 or 10 frames.");
            }
        }

        /// <summary>
        /// Scores a roll for the current player and keeps the rack decision for <see cref="Advance"/>. Pins are
        /// counted against this session's own <see cref="Rack"/> and not against <c>StandingBefore</c> (P-9), so
        /// a pin that is already down can't fall again. Throws <see cref="InvalidOperationException"/> when the
        /// game is over or a scored roll still waits for <see cref="Advance"/>.
        /// </summary>
        public MarkKind ScoreRoll(in RollResult result)
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The game is over.");
            }

            if (_pending.HasValue)
            {
                throw new InvalidOperationException("A scored roll is waiting for Advance.");
            }

            int pins = (_rack & ~result.StandingAfter).Count();
            PinMask standingAfter = _rack & result.StandingAfter;

            // The pure decision first, so a throw below leaves the session untouched.
            RackDecision decision = FrameRules.AfterRoll(_currentFrame == FrameCount, _currentBall, _rackFresh, standingAfter);
            MarkKind mark = Current.Card.AddRoll(pins);
            _pending = decision;
            return mark;
        }

        /// <summary>
        /// Moves on after a scored roll: to the next ball of this frame (returns false) or to the next turn,
        /// frame-major (returns true). Throws <see cref="InvalidOperationException"/> without a scored roll
        /// to move on from, or when the game is over.
        /// </summary>
        public bool Advance()
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The game is over.");
            }

            if (!_pending.HasValue)
            {
                throw new InvalidOperationException("No scored roll to advance from.");
            }

            RackDecision decision = _pending.Value;
            _pending = null;
            _rack = decision.NextRack;
            _rackFresh = decision.NextRackFresh;

            if (!decision.FrameComplete)
            {
                _currentBall++;
                return false;
            }

            _currentBall = 1;
            _currentPlayer++;
            if (_currentPlayer == _slots.Length)
            {
                _currentPlayer = 0;
                _currentFrame++;
            }

            return true;
        }
    }
}
