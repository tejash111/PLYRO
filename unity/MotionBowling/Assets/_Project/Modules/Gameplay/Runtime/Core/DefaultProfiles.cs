using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>The profile a player gets when calibration is skipped or fails (P-13).</summary>
    internal static class DefaultProfiles
    {
        /// <summary>The default profile, built from the <c>Default*</c> fields of <paramref name="tuning"/>.</summary>
        public static PlayerProfile For(GameplayTuning tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            return new PlayerProfile(
                tuning.DefaultBowlingHand,
                tuning.DefaultArmLengthM,
                tuning.DefaultShoulderWidthM,
                tuning.DefaultNeutralImageX);
        }
    }
}
