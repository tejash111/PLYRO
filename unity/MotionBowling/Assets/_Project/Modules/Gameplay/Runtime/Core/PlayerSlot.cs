using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>One seat in a game: a display name, a color, the calibrated profile and the score card.</summary>
    internal sealed class PlayerSlot
    {
        /// <summary>The seat at zero-based <paramref name="index"/>, with a card of <paramref name="frameCount"/> frames.</summary>
        public PlayerSlot(int index, int frameCount)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A player index is 0 or more.");
            }

            DisplayName = $"Player {index + 1}";
            ColorIndex = index;
            Card = new ScoreCard(frameCount);
        }

        /// <summary>"Player 1" for the first seat, and so on.</summary>
        public string DisplayName { get; }

        /// <summary>Index into the UI's player color set. The same as the seat index.</summary>
        public int ColorIndex { get; }

        /// <summary>The throw-tuning profile. Null until calibration (or the default profile) sets it.</summary>
        public PlayerProfile? Profile { get; set; }

        public ScoreCard Card { get; }
    }
}
