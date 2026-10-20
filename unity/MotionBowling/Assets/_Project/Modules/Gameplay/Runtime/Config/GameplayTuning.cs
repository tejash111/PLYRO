using Bowling.Contracts;
using UnityEngine;

namespace Bowling.Gameplay
{
    /// <summary>
    /// Every duration, preset and default-profile value of the game state machine (gameplay spec, section 4 and P-13).
    /// The committed asset at <see cref="AssetPath"/> holds the shipped values; the field defaults below are the same
    /// numbers, so a freshly created instance matches the spec.
    /// </summary>
    public sealed class GameplayTuning : ScriptableObject
    {
        /// <summary>Where the committed asset lives.</summary>
        public const string AssetPath = "Assets/_Project/Config/Gameplay/GameplayTuning.asset";

        [Header("State timers (seconds)")]
        [Tooltip("Seconds. How long Join shows its greeting before ModeSelect (spec section 4, Join row).")]
        public float JoinGreeting = 1.0f;

        [Tooltip("Seconds. ModeSelect with the player present for this long auto-selects the preset (ModeSelect row).")]
        public float ModeSelectTimeout = 20f;

        [Tooltip("Seconds. ModeSelect with the player absent for this long returns to Attract (ModeSelect row).")]
        public float ModeSelectAbsent = 5f;

        [Tooltip("Seconds. Absence in Calibrate, AwaitReady or AwaitThrow for this long goes to Paused (pause rule).")]
        public float PauseAfterAbsent = 2f;

        [Tooltip("Seconds. The pause threshold in a turn-change Calibrate, used instead of PauseAfterAbsent (turn change rule, P-4).")]
        public float TurnChangeWait = 30f;

        [Tooltip("Seconds. How long Scoring holds after an open frame (Scoring row).")]
        public float ScoringHold = 1.5f;

        [Tooltip("Seconds. How long Scoring holds after a Strike or Spare, to leave room for the replay (Scoring row).")]
        public float ScoringHoldReplay = 4.5f;

        [Tooltip("Seconds. Absence of this long while Paused abandons the game to Attract (Paused row, P-5).")]
        public float AbandonAfterPause = 15f;

        [Tooltip("Seconds. How long GameOver holds before Results (GameOver row).")]
        public float GameOverHold = 2f;

        [Tooltip("Seconds. How long Results holds before returning to Attract (Results row).")]
        public float ResultsHold = 10f;

        [Tooltip("Seconds. Absence of this long in Results returns to Attract (Results row).")]
        public float ResultsAbsent = 3f;

        [Header("Mode selection preset")]
        [Tooltip("Players (1 to 4). The preset a ModeSelect timeout auto-selects (ModeSelect row, P-13).")]
        public int PresetPlayerCount = 1;

        [Tooltip("Frames (3, 5 or 10). The preset a ModeSelect timeout auto-selects (ModeSelect row, P-13).")]
        public int PresetFrameCount = 3;

        [Header("Default player profile: used when calibration fails twice (P-4), and as the fallback for a player with no profile")]
        [Tooltip("Which hand bowls (P-13).")]
        public Handedness DefaultBowlingHand = Handedness.Right;

        [Tooltip("Metres. Shoulder to wrist, bowling side (P-13).")]
        public float DefaultArmLengthM = 0.60f;

        [Tooltip("Metres. Shoulder to shoulder (P-13).")]
        public float DefaultShoulderWidthM = 0.38f;

        [Tooltip("Image fraction (0 to 1, unmirrored). Hip midpoint x at the neutral stance (P-13).")]
        public float DefaultNeutralImageX = 0.5f;
    }
}
