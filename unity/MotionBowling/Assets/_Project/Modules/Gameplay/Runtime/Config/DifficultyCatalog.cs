using System;
using Bowling.Contracts;
using UnityEngine;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The assist and bumper settings of each difficulty level (gameplay spec, decision D9). The committed asset at
    /// <see cref="AssetPath"/> holds the shipped values; the field defaults below are the same numbers.
    /// </summary>
    public sealed class DifficultyCatalog : ScriptableObject
    {
        /// <summary>Where the committed asset lives.</summary>
        public const string AssetPath = "Assets/_Project/Config/Gameplay/DifficultyCatalog.asset";

        [Header("Casual")]
        [Tooltip("Blend toward the ideal line (0 to 1) at Casual (D9).")]
        public float CasualAssist = 0.50f;

        [Tooltip("Gutter bumpers at Casual (D9).")]
        public bool CasualBumpers = true;

        [Header("Standard")]
        [Tooltip("Blend toward the ideal line (0 to 1) at Standard (D9).")]
        public float StandardAssist = 0.25f;

        [Tooltip("Gutter bumpers at Standard (D9).")]
        public bool StandardBumpers = false;

        [Header("Pro")]
        [Tooltip("Blend toward the ideal line (0 to 1) at Pro (D9).")]
        public float ProAssist = 0.00f;

        [Tooltip("Gutter bumpers at Pro (D9).")]
        public bool ProBumpers = false;

        /// <summary>The profile of <paramref name="level"/>. Throws for a value that is not a defined level.</summary>
        public DifficultyProfile For(Difficulty level)
        {
            switch (level)
            {
                case Difficulty.Casual:
                    return new DifficultyProfile(Difficulty.Casual, CasualAssist, CasualBumpers);
                case Difficulty.Standard:
                    return new DifficultyProfile(Difficulty.Standard, StandardAssist, StandardBumpers);
                case Difficulty.Pro:
                    return new DifficultyProfile(Difficulty.Pro, ProAssist, ProBumpers);
                default:
                    throw new ArgumentOutOfRangeException(nameof(level), level, "Not a defined difficulty level.");
            }
        }
    }
}
