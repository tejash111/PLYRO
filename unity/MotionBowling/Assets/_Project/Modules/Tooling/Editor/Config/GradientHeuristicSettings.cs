using UnityEngine;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// The thresholds of G-6, the texture gradient heuristic (foundation spec, section 8.1). The committed asset
    /// <c>GradientHeuristic.asset</c> next to this script holds them; the rule uses the defaults below if the asset
    /// is missing.
    /// </summary>
    public sealed class GradientHeuristicSettings : ScriptableObject
    {
        /// <summary>Where the committed asset lives.</summary>
        public const string AssetPath = "Assets/_Project/Modules/Tooling/Editor/Config/GradientHeuristic.asset";

        public const int DefaultMaxStep = 3;
        public const float DefaultMaxFraction = 0.02f;

        [Tooltip("The largest difference (0-255) on a color channel between two neighbouring pixels that still counts as a smooth step. 0 is never a step.")]
        public int MaxStep = DefaultMaxStep;

        [Tooltip("The share (0-1) of opaque pixels on a smooth ramp above which a texture fails G-6.")]
        public float MaxFraction = DefaultMaxFraction;
    }
}
