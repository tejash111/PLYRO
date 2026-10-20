using System;
using Bowling.Contracts;

namespace Bowling.Gameplay
{
    /// <summary>
    /// The default player profile (P-13): used when calibration fails twice (P-4), and as the fallback for a player
    /// with no profile.
    /// </summary>
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
