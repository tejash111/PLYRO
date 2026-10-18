# Motion Bowling for macOS: System Design

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Owner:** Tejash
- **Drafted by:** Claude
- **Type:** Umbrella spec. It defines the whole system, the global constraints and the shared contracts, and splits the work into sub-projects that each have their own spec, plan and implementation cycle.

---

## 1. Intent

**What:** a 3D ten-pin bowling game for macOS that you play with your body. A camera watches the player, MediaPipe Pose tracks the skeleton, and a natural bowling arm swing throws the ball. The swing's speed, direction and wrist roll decide how the ball travels. No controller, mouse or touch is needed.

**Who:** anyone who walks up to it: kids, adults, first-timers and experienced bowlers. The main venue is a public walk-up booth (an event, a mall or an office lobby). The same build also works at home.

**Why:** a walk-up experience people understand without instructions, that feels like real bowling, and that runs reliably all day on one Mac.

**Success looks like:**

| # | Criterion | Target |
|---|---|---|
| SC-1 | A first-time player finishes a 3-frame game with no help from staff | 80% or more of field-test sessions |
| SC-2 | Intended throws detected on the validation recordings | 95% or more |
| SC-3 | False throws during non-throw movement (waving, walking, talking) | At most 1 per 10 minutes of recorded movement |
| SC-4 | Render frame rate on the reference booth hardware at 1080p | 60 fps or more at p99 |
| SC-5 | Motion to photon (arm movement → ball-in-hand moves on screen) | 100 ms or less at p95 |
| SC-6 | Unattended uptime in a booth day (10 hours) | No crash or freeze; automatic relaunch within 10 s if the process dies |
| SC-7 | Source-of-truth violations shipped | 0 (enforced by automated checks, section 4) |

## 2. What was said vs. what is assumed

**Said by Tejash:**

- macOS only.
- The camera tracks the body, using MediaPipe.
- Anyone can come and bowl by throwing the ball with their body.
- The game is a 3D environment.
- Industry standard, not Electron: it must not lag. Unity was chosen.
- Spec-driven development with the Superpowers plugin.
- Sources of truth: (1) no gradients, (2) a modular, file- and component-based structure.
- Good skill files for Unity, UI and the rest.

**Assumed by Claude (please confirm or correct):**

| ID | Assumption |
|---|---|
| A-1 | The primary venue is a public walk-up booth. Home use is supported but isn't the design driver. |
| A-2 | One active bowler at a time. Up to 4 players take turns on one machine. |
| A-3 | Apple Silicon only (M1 or newer), macOS 14 Sonoma or newer. |
| A-4 | The art direction is clean stylized 3D with solid-color materials. This follows from SOT-1. |
| A-5 | English UI only for v1. |
| A-6 | Fully offline. No accounts, no personal data, no telemetry. |
| A-7 | Working title "Motion Bowling", bundle id `ai.shavastudios.motionbowling`. |
| A-8 | Reference booth hardware is a Mac mini (M2 or newer) with a 55-inch 1080p/4K TV and a wide-angle camera (section 8). The fanless MacBook Air is the minimum dev machine but isn't recommended for all-day booths, because it throttles under sustained load. |

## 3. Goals, non-goals, anti-goals

**Goals (v1):**

- G-1: Walk-up play: step into the zone, pick a mode with your hand, bowl.
- G-2: Real ten-pin rules and scoring, with 1–4 players and 3, 5 or 10 frames.
- G-3: Swing-driven throws with speed, aim, start position and hook, plus a difficulty-scaled assist so beginners have fun.
- G-4: Believable physics for ball and pins, measured in real-world units.
- G-5: Broadcast-style cameras, strike and spare replays, and satisfying audio.
- G-6: Kiosk-grade reliability: auto-start, auto-recover, an operator panel, and a local leaderboard.

**Non-goals (v1, deliberately excluded):**

- Online play, accounts, cloud leaderboards
- A rigged player avatar that mirrors your body (the camera preview with a skeleton overlay covers feedback)
- Windows, iOS, Intel Macs, VR, the Mac App Store
- Ball and skin customization, a store, monetization
- Telemetry and remote crash reporting
- Localization
- Foul-line detection
- Bowling variants (candlepin, duckpin, nine-pin)

**Anti-goals (actively avoided):**

- Any gradient (SOT-1)
- God objects, manager singletons, scripts that do everything (SOT-2)
- Storing or transmitting camera images
- Needing staff to start each game
- Needing a mouse or keyboard to play (keyboard is for the operator only)

## 4. Sources of truth and global constraints

Every implementation plan copies this section into its **Global Constraints** header. The values are exact.

### SOT-1: No gradients

**Rule:** at any single rendered frame, every authored surface or element is filled with exactly one flat color. No color may be defined as an interpolation between two or more colors across space.

**Allowed:**

- **Lighting and shadows.** Shading produced by real lights, shadows, ambient occlusion and reflection probes is allowed. Light is not an authored gradient.
- **Time-based changes.** Animating a flat color over time is allowed (a hover color transition, a fade in or out), and so are transform animations (scale pops, slides). At any instant the element is still one flat color.
- **Hard-edged multi-color textures.** Patterns made of flat color regions with no interpolation are allowed, such as lane board stripes, arrows, or a palette atlas.
- **Antialiasing.** The edge smoothing that MSAA, SMAA or font rendering applies is allowed. It is a rasterization artifact, not an authored gradient.

**Banned, by asset type** (the validator checks each one):

| Area | Banned |
|---|---|
| UI (USS/UXML) | Any `linear-gradient`, `radial-gradient` or `conic-gradient`; gradient images or sprites; soft shadows or glows |
| Text | Vertex color gradients, color-gradient presets |
| Textures and sprites | Any smooth color ramp, soft radial falloff, or blurred alpha edge used as a color or alpha source |
| Materials and shaders | Shader Graph `Gradient` and `Sample Gradient` nodes; procedural ramps; ramp or toon gradient textures |
| Environment | Gradient (trilight) ambient lighting; procedural or gradient skyboxes; fog |
| Particles | Soft round particle sprites. Particles use hard-edged flat shapes. Color over lifetime is allowed, because it's time-based. |
| Line and trail renderers | More than one color key along the length |
| Post-processing | Vignette, lens flares and film grain (grain makes surfaces non-flat). Bloom is off by default (open question Q-3). |

**Enforcement:** the source-of-truth validator in the foundation spec runs in CI and in a pre-commit check. Reviewers reject violations.

### SOT-2: Modular, file- and component-based structure

**Modules:**

- Each module lives in `Assets/_Project/Modules/<Module>/`.
- It has `Runtime/`, `Editor/` (optional) and `Tests/` folders, each with its own assembly definition (`.asmdef`).
- Each module has a `README.md` stating its purpose, public API and dependencies.

**Dependency rule:**

- Every runtime module references only `Bowling.Contracts`.
- `Bowling.App` is the single composition root and the only assembly that references other modules.
- The rule is enforced through asmdef references, which the compiler checks, and through the structure validator.

**Files:**

- One public type per file, and the file name matches the type name.
- Soft limit 300 lines; hard limit 400 lines (the validator fails the build above that).

**Components:**

- MonoBehaviours are small and single-purpose. They are thin adapters around plain C# classes that hold the logic.
- Plain C# classes are testable in EditMode without a scene.
- Use composition over inheritance.

**No global state:**

- No static singletons, no service locators, no `FindObjectOfType` at runtime.
- Dependencies come in through constructors (plain classes) or serialized references set by `Bowling.App`.

**Configuration:**

- Tuning values live in ScriptableObject assets under `Assets/_Project/Config/`.
- No magic numbers in logic. Constants that are physical facts (lane dimensions) live in `LaneSpec` in Contracts.

**Prefabs and scenes:**

- Every 3D component is a prefab (`Ball`, `Pin`, `PinDeck`, `Lane`, `Alley`), using nested prefabs and variants.
- Scenes only contain prefab instances plus one `Bootstrap` object. There is no logic that exists only in a scene.

**UI components:**

- Every UI component is a folder `Components/<Name>/` containing `<Name>.uxml`, `<Name>.uss` and `<Name>.cs`.
- Screens are built from components.
- Colors and spacing come only from design tokens in `Tokens.uss`.

**Native code:** split by responsibility into separate files (capture, inference, buffer, C API, preview). See the TrackingCore spec.

### Global constraints

| ID | Constraint |
|---|---|
| GC-1 | Unity 6 LTS: URP, Metal only, IL2CPP, target macOS Apple Silicon (arm64). The exact editor version is pinned in `ProjectVersion.txt`. |
| GC-2 | Minimum macOS 14.0, Apple M1 or newer. |
| GC-3 | Fully offline at runtime. No network calls. |
| GC-4 | Camera images never leave process memory. Only landmark data may be recorded, and only when the operator turns recording on. |
| GC-5 | SI units. Lane frame: origin at the center of the foul line, +Z down the lane, +X to the bowler's right, +Y up. |
| GC-6 | Physics fixed step is 1/240 s. Render target is 60 fps (vsync). Tracking target is 30 fps or more (60 if the camera allows). |
| GC-7 | Players need no mouse, keyboard or touch. The keyboard is for the operator only. |
| GC-8 | The UI is readable at 3 m: prompt text 64 px or larger, body text 36 px or larger, measured at 1080p reference resolution. |

## 5. Architecture overview

```
┌─────────────────────────── TrackingCore.bundle (native, own threads) ───────────────────────────┐
│  Capture thread (AVFoundation)  →  Inference thread (MediaPipe C++ PoseLandmarker)  →  Ring buffer │
│  + Metal preview texture (CVMetalTextureCache)                                C API (tc_*)         │
└───────────────────────────────────────────────────────────────────────────────────────────────────┘
                                   │ P/Invoke, once per Unity frame: "all samples since t"
┌──────────────────────────────── Unity (IL2CPP, Metal) — main thread ──────────────────────────────┐
│ PoseInput ──IPlayerInput──► ThrowDetection ──IThrowSource──► Gameplay ──IRollSimulator──► BowlingPhysics │
│    │  IHandCursor                                               │ IGameEvents        │ IRollBodies/Impacts │
│    ▼                                                            ▼                    ▼                     │
│   UI  ◄──────────────────────────── IGameEvents ───────────── Presentation (cameras, replay, audio)       │
│ Platform: storage, settings, logging, kiosk (ILeaderboardStore, ISettingsStore, IGameLog)                  │
│ App: composition root — constructs everything, wires interfaces, owns update order                        │
└───────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### What happens during one throw

1. **Capture and inference.** TrackingCore captures frames and runs `PoseLandmarker` on each one. It writes a `PoseSample` (up to 3 people, 33 image + 33 world landmarks each, camera timestamp) into the ring buffer.
2. **Pose input.** Each Unity frame, PoseInput pulls every new sample. It converts axes to player space, locks onto the player in the play zone, and applies a One Euro filter. The result is a `PlayerPose` for each sample, plus the hand cursor.
3. **Throw detection.** When Gameplay arms it with a `ThrowContext`, ThrowDetection runs the swing state machine over the poses. On release it emits a `ThrowEvent` (speed, angle, start offset, hook, confidence) with the assist already applied.
4. **Gameplay** calls `IRollSimulator.Roll(throwEvent)`.
5. **Physics.** BowlingPhysics simulates at 240 Hz. It emits `RollMilestone` and `ImpactEvent` events, then a `RollResult` once the pins have settled.
6. **Scoring.** Gameplay scores the roll, updates the scorecard, publishes `GameSnapshot` and `RollOutcome`, and decides the next state: next ball, next player, or game over.
7. **Feedback.** UI and Presentation react to the game events: HUD, prompts, camera cuts, replay, audio.

## 6. Shared contracts (`Bowling.Contracts`)

Contracts is the only assembly every module depends on. Its content is fixed by this spec. Changing it requires updating this spec first.

- It depends only on `UnityEngine.CoreModule` (for `Vector2`, `Vector3`, `Transform`).
- It contains only data types, interfaces, enums and `LaneSpec`. There is no behavior in it beyond trivial helpers.
- It follows SOT-2: one type per file under `Modules/Contracts/Runtime/<Area>/`.
- **Constructors:** every `readonly struct` has one public constructor taking all of its fields in declaration order, even where the listing below omits it for brevity. Classes with public fields need no constructor.
- **Events:** implementations update their observable state before raising an event, and raise change events (`PhaseChanged`, `StepChanged`, `StateChanged`) on every actual change, so handlers see the new state and may call back into the source.

### 6.1 Pose (`Contracts/Runtime/Pose/`)

```csharp
public enum PoseJoint : byte          // MediaPipe Pose indices (person's own left/right)
{
    Nose = 0, LeftShoulder = 11, RightShoulder = 12, LeftElbow = 13, RightElbow = 14,
    LeftWrist = 15, RightWrist = 16, LeftPinky = 17, RightPinky = 18, LeftIndex = 19,
    RightIndex = 20, LeftThumb = 21, RightThumb = 22, LeftHip = 23, RightHip = 24,
    LeftKnee = 25, RightKnee = 26, LeftAnkle = 27, RightAnkle = 28,
    LeftHeel = 29, RightHeel = 30, LeftFootIndex = 31, RightFootIndex = 32
}
// Arrays always hold all 33 MediaPipe landmarks; indices 1–10 (face detail) have no enum names.

public readonly struct Landmark
{
    public readonly Vector3 Position;
    public readonly float Visibility;   // 0..1
    public readonly float Presence;     // 0..1
    public Landmark(Vector3 position, float visibility, float presence);
}

public sealed class PersonPose        // one detected person in one camera frame, unfiltered
{
    public const int LandmarkCount = 33;
    public readonly Landmark[] Image = new Landmark[LandmarkCount]; // x,y in [0,1], origin top-left of the UNMIRRORED camera image; z = MediaPipe relative depth
    public readonly Landmark[] World = new Landmark[LandmarkCount]; // RAW MediaPipe world coordinates as reported: metres, hip-centred, MediaPipe axes
}

public sealed class PoseSample        // one camera frame
{
    public const int MaxPeople = 3;
    public double Timestamp;           // seconds, camera presentation time (monotonic)
    public long FrameIndex;
    public int PersonCount;            // 0..MaxPeople
    public readonly PersonPose[] People = { new PersonPose(), new PersonPose(), new PersonPose() };
}

public sealed class PlayerPose        // the locked player, filtered; one per PoseSample while a player is locked
{
    public double Timestamp;
    public readonly Landmark[] Image = new Landmark[PersonPose.LandmarkCount];
    public readonly Landmark[] World = new Landmark[PersonPose.LandmarkCount]; // PLAYER SPACE (below), metres
    public float Confidence;           // mean visibility of shoulders, elbows, wrists, hips
}
```

Tracking sources and recordings always carry raw MediaPipe axes. Only `PlayerPose.World` is in player space.

**Player space:** the frame used for all world landmarks after PoseInput.

- The origin is the midpoint of the hips.
- +Y is up.
- +X is the camera image's right, which is the player's left when they face the camera.
- +Z is toward the camera, so a forward throw moves the wrist in +Z.
- The units are metres.

Converting from MediaPipe's world axes happens in exactly one function, `PoseAxes.ToPlayerSpace`, in PoseInput. The spike (section 11) confirms MediaPipe's sign conventions.

### 6.2 Tracking source and player input (`Contracts/Runtime/Input/`)

```csharp
public enum TrackingStatus : byte { Starting, Running, NoCamera, PermissionDenied, ModelMissing, Failed, Stopped }

public interface ITrackingSource                   // raw samples; implemented by Native, Recorded, Synthetic sources
{
    TrackingStatus Status { get; }
    void Start();
    void Stop();
    int ReadSamplesSince(double timestamp, List<PoseSample> into); // appends all samples with Timestamp > timestamp, oldest first; returns count
}

public enum Handedness : byte { Right, Left }

public readonly struct PlayerProfile
{
    public readonly Handedness BowlingHand;
    public readonly float ArmLengthM;       // shoulder→wrist, bowling side
    public readonly float ShoulderWidthM;
    public readonly float NeutralImageX;    // hip-midpoint image x at calibration (0..1, unmirrored)
}

public interface IPlayerInput                      // implemented by PoseInput
{
    TrackingStatus Status { get; }
    bool IsPlayerPresent { get; }                  // a player is locked and inside the play zone
    PlayerPose Latest { get; }                     // null when no player is locked
    int ReadPosesSince(double timestamp, List<PlayerPose> into);
    event Action<bool> PresenceChanged;
    event Action<TrackingStatus> StatusChanged;
}

public enum CalibrationStep : byte { Idle, WaitingForFullBody, WaitingForRaisedHand, Holding, Done, Failed }
public enum CalibrationFailure : byte { Timeout, PlayerLeft, TrackingLost }

public interface IPlayerCalibrator                 // implemented by PoseInput
{
    CalibrationStep Step { get; }
    float HoldProgress { get; }                    // 0..1 while Holding
    void Begin();
    void Cancel();
    event Action<CalibrationStep> StepChanged;
    event Action<PlayerProfile> Completed;
    event Action<CalibrationFailure> Failed;
}

public interface IHandCursor                       // implemented by PoseInput
{
    bool IsActive { get; }                         // a hand is raised into the cursor box
    Vector2 Position { get; }                      // 0..1 screen space, origin top-left, ALREADY MIRRORED for display
}

public interface ICameraPreview                    // implemented by PoseInput (from TrackingCore's Metal texture)
{
    Texture Texture { get; }                       // null when unavailable; UNMIRRORED — the UI mirrors it for display
}

public readonly struct CameraInfo
{
    public readonly string UniqueId;
    public readonly string Name;
    public readonly bool IsContinuityCamera;
    public readonly int MaxFps;
}

public readonly struct TrackingStats
{
    public readonly float CaptureFps;
    public readonly float InferenceFps;
    public readonly float InferenceMsP50;
    public readonly float InferenceMsP95;
    public readonly long FramesDroppedStale;
    public readonly long SamplesOverwritten;
}

public interface ITrackingDiagnostics              // implemented by PoseInput; used by the operator panel
{
    TrackingStats Stats { get; }
    IReadOnlyList<CameraInfo> Cameras { get; }
}

public interface IRecordingIndicator               // implemented by PoseInput's recorder; the UI shows a "Recording" badge
{
    bool IsRecording { get; }
}
```

### 6.3 Throw (`Contracts/Runtime/Throw/`)

```csharp
public enum Difficulty : byte { Casual, Standard, Pro }

public readonly struct DifficultyProfile
{
    public readonly Difficulty Level;
    public readonly float Assist;           // 0..1, blend toward the ideal line
    public readonly bool Bumpers;           // gutter bumpers on/off
}

public readonly struct ThrowContext
{
    public readonly PlayerProfile Player;
    public readonly DifficultyProfile Difficulty;
}

public enum SwingPhase : byte { Disarmed, WaitingForReady, Ready, Backswing, ForwardSwing, Released }
public enum ThrowRejection : byte { LowConfidence, SwingTimeout, SwingTooSmall, PlayerLeft }

public readonly struct ThrowEvent
{
    public readonly double Timestamp;       // release, camera clock (s)
    public readonly Handedness Hand;
    public readonly float SpeedMps;         // clamped to [LaneSpec.MinBallSpeedMps, LaneSpec.MaxBallSpeedMps]
    public readonly float LaunchAngleDeg;   // + toward +X; clamped to ±LaneSpec.MaxLaunchAngleDeg
    public readonly float StartOffsetM;     // lateral release position, + = +X; clamped to ±LaneSpec.MaxStartOffsetM
    public readonly float Hook;             // −1..1; + curves toward +X, − toward −X
    public readonly float Confidence;       // 0..1
}

public interface IThrowSource                      // implemented by ThrowDetection (and test fakes)
{
    SwingPhase Phase { get; }
    void Arm(in ThrowContext context);
    void Disarm();
    event Action<SwingPhase> PhaseChanged;
    event Action<ThrowEvent> Thrown;               // fires once; the source is already Disarmed when it fires
    event Action<ThrowRejection> Rejected;         // swing aborted; the source is already back in WaitingForReady when it fires
}
```

### 6.4 Roll and physics (`Contracts/Runtime/Roll/`)

```csharp
[Flags]
public enum PinMask : ushort
{
    None = 0, P1 = 1, P2 = 1 << 1, P3 = 1 << 2, P4 = 1 << 3, P5 = 1 << 4,
    P6 = 1 << 5, P7 = 1 << 6, P8 = 1 << 7, P9 = 1 << 8, P10 = 1 << 9, All = 0x3FF
}
public static class PinMaskExtensions { public static int Count(this PinMask mask); }

public enum RollEndReason : byte { Settled, BallStopped, Timeout }
public enum RollMilestone : byte { Released, EnteredGutter, ReachedPins, EnteredPit, Settled }

public readonly struct RollResult
{
    public readonly PinMask StandingBefore;
    public readonly PinMask StandingAfter;
    public readonly bool Gutter;
    public readonly RollEndReason EndReason;
    public int PinsKnocked => (StandingBefore & ~StandingAfter).Count();
}

public enum ImpactKind : byte { BallPin, PinPin, PinDeck, PinPit, BallLane, BallGutter, BallBumper }

public readonly struct ImpactEvent
{
    public readonly ImpactKind Kind;
    public readonly float ImpulseNs;        // collision impulse magnitude, N·s
    public readonly Vector3 Position;       // lane frame
}

public interface IRollSimulator                    // implemented by BowlingPhysics
{
    PinMask Standing { get; }
    bool IsRolling { get; }
    void SetBumpers(bool enabled);
    void SetRack(PinMask standing);                // instant: standing pins on their spots, others removed, ball parked
    void Roll(in ThrowEvent throwEvent);           // throws InvalidOperationException if IsRolling
    event Action<RollMilestone> Milestone;
    event Action<ImpactEvent> Impact;
    event Action<RollResult> Completed;
}

public interface IRollBodies                       // read-only access for cameras and replay
{
    Transform Ball { get; }
    IReadOnlyList<Transform> Pins { get; }         // index 0 = pin 1 … index 9 = pin 10
}
```

### 6.5 Game (`Contracts/Runtime/Game/`)

```csharp
public enum GameState : byte
{
    Attract, Join, ModeSelect, Calibrate, AwaitReady, AwaitThrow, Rolling, Scoring, Paused, GameOver, Results
}

public enum MarkKind : byte { Count, Miss, Strike, Spare }

public readonly struct RollMark { public readonly int Pins; public readonly MarkKind Kind; }

public sealed class FrameView
{
    public int FrameNumber;                          // 1-based
    public IReadOnlyList<RollMark> Rolls;            // 0..3 entries
    public int? Cumulative;                          // null until the frame's score is final
}

public sealed class PlayerSnapshot
{
    public string DisplayName;                       // "Player 1".."Player 4"
    public int ColorIndex;                           // 0..3 → palette player colors
    public IReadOnlyList<FrameView> Frames;
    public int RunningTotal;                         // score of all frames whose score is final
}

public sealed class GameSnapshot
{
    public GameState State;
    public IReadOnlyList<PlayerSnapshot> Players;
    public int CurrentPlayer;                        // index into Players
    public int CurrentFrame;                         // 1-based
    public int CurrentBall;                          // 1..3
    public int FrameCount;                           // 3, 5 or 10
    public DifficultyProfile Difficulty;
}

public readonly struct RollOutcome
{
    public readonly int PlayerIndex;
    public readonly RollResult Result;
    public readonly MarkKind Mark;
}

public interface IGameEvents                        // implemented by Gameplay
{
    GameSnapshot Current { get; }
    event Action<GameState> StateChanged;
    event Action<GameSnapshot> SnapshotChanged;
    event Action<RollOutcome> RollScored;
}

public interface IGameCommands                      // implemented by Gameplay; called by UI (dwell buttons) and operator panel
{
    void SelectMode(int playerCount, int frameCount, Difficulty difficulty);
    void Pause();
    void Resume();
    void Abandon();                                  // back to Attract
}

public enum UiCue : byte { DwellTick, Confirm, Back, Toast }

public interface IUiCues                             // implemented by UI; consumed by Presentation audio
{
    event Action<UiCue> Cue;
}
```

### 6.6 Platform services (`Contracts/Runtime/Platform/`)

```csharp
public interface IClock { double Now { get; } }    // seconds, monotonic; Unity time in production, fake in tests

public interface IGameLog
{
    void Info(string category, string message);
    void Warn(string category, string message);
    void Error(string category, string message, Exception exception = null);
}

public sealed class LeaderboardEntry { public string DisplayName; public int Score; public int FrameCount; public DateTime PlayedAtUtc; }
public enum LeaderboardScope : byte { Today, AllTime }

public interface ILeaderboardStore
{
    IReadOnlyList<LeaderboardEntry> Top(int frameCount, LeaderboardScope scope, int max);
    void Submit(LeaderboardEntry entry);
    void Reset();
}

public sealed class OperatorSettings
{
    public int SchemaVersion = 1;
    public string CameraUniqueId;                    // null = first available camera
    public Difficulty DefaultDifficulty = Difficulty.Casual;
    public float MasterVolume = 0.8f;                // 0..1
    public bool PoseRecordingEnabled;                // default false
}

public interface ISettingsStore { OperatorSettings Load(); void Save(OperatorSettings settings); }

public interface IOperatorActions                    // implemented by Platform; called by the UI operator panel
{
    OperatorSettings Settings { get; }
    void ApplySettings(OperatorSettings settings);   // saves, then App re-applies camera/volume/recording
    void ResetLeaderboard();
    void Quit();                                     // clean exit (launchd does NOT relaunch on clean exit)
    event Action<OperatorSettings> SettingsApplied;
}
```

### 6.7 Lane geometry (`Contracts/Runtime/Lane/LaneSpec.cs`)

These are physical facts (USBC dimensions, rounded) and game-wide clamps. All values are in metres or SI.

```csharp
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
    public static Vector3 PinSpot(int pinNumber);        // 1..10, lane frame, base centre on the deck
}
```

Pin numbering follows the standard, as seen by the bowler: 1 is the head pin; the second row is 2 (left) and 3 (right); the third row is 4, 5, 6; the back row is 7, 8, 9, 10, left to right. `PinSpot(n)` is calculated as:

- Row `r` (0–3) sits at z = `FoulLineToHeadPinM + r × PinRowSpacingM`.
- Pin `i` within the row (0..r, left to right) sits at x = `(i − r/2) × PinSpacingM`.

## 7. Timing, threading, update order

**TrackingCore threads.** Capture and inference each run on their own native thread. Unity never waits on them. The ring buffer holds the last 2 seconds of samples, 120 at 60 fps.

**Unity main thread, per frame**, in this order (set by `Bowling.App` using `DefaultExecutionOrder`):

1. `PoseInputDriver`: pulls samples, filters them, updates the cursor, runs calibration.
2. `ThrowDetectionDriver`: consumes every new `PlayerPose` since the last frame.
3. `GameplayDriver`: advances the state machine and timers (`IClock`).
4. Presentation and UI read the state and render.

**Physics** runs in `FixedUpdate` at 1/240 s. The physics settings are in the BowlingPhysics spec.

**Two clocks:**

- Throw math uses the **camera clock** (`PoseSample.Timestamp`).
- Game timers use **`IClock`**, which is Unity's unscaled time.

The two are never mixed in one calculation.

## 8. Performance and latency budget

**Reference booth hardware (assumption A-8):**

- Mac mini (M2 or newer).
- A 55-inch TV at 1080p60. 4K TVs render with URP render scale 0.5.
- A camera with at least 90° diagonal field of view at 1080p60: a wide-angle USB webcam, or an iPhone through Continuity Camera with the ultra-wide lens.
- The FOV requirement exists because a 1.9 m person with a raised arm (about 2.4 m tall) has to fit in frame at 2.5 m. That needs about 51° of vertical FOV, which in 16:9 means about 90° diagonal.

| Stage | Budget |
|---|---|
| Camera exposure and delivery (60 fps) | about 16 ms |
| MediaPipe `full` model, CPU/XNNPACK | 25 ms or less (measured in spike S0) |
| PoseInput, ThrowDetection, Gameplay | 1 ms or less per frame in total |
| Physics step, 4 substeps per 60 fps frame | 1 ms or less per frame in total |
| Render (URP, 1080p) | 12 ms or less |
| Display | about 8–16 ms |
| **Motion to photon** | **100 ms or less at p95 (SC-5)** |

## 9. Error handling

| Situation | Detected by | Behavior |
|---|---|---|
| Camera permission denied | TrackingCore → `TrackingStatus.PermissionDenied` | Full-screen operator message explaining how to enable the camera in System Settings. The game never starts without tracking. |
| No camera, or the selected camera is unplugged | `NoCamera` | Fall back to the first available camera. If there is none, show an operator message and retry every 2 s. |
| Model file missing | `ModelMissing` | Operator message. This is a build defect, caught by a CI check (foundation spec). |
| Tracking stalls (no sample for 1 s while `Running`) | PoseInput watchdog | Restart the tracking source once. If it still stalls, show `Failed` with the operator message and retry every 5 s. |
| Player leaves mid-game | `PresenceChanged(false)` | Pause after 3 s absent, with a "Step back in" prompt and a 15 s countdown, then abandon to Attract. |
| Swing can't be scored (low confidence, timeout) | `ThrowRejection` | Friendly prompt ("Try again: swing your arm all the way back"). The ball isn't consumed. |
| Physics anomaly (ball or pin leaves the world bounds, or NaN) | BowlingPhysics | End the roll with `Timeout`. Pins outside bounds count as knocked down. Log an error. |
| Settings or leaderboard file corrupt | Platform stores | Rename to `*.corrupt-<timestamp>`, recreate defaults, log a warning. |
| Unhandled exception or process crash | launchd `KeepAlive` (platform spec) | Relaunch within 10 s (SC-6). |

## 10. Testing strategy

- **TDD everywhere** (`superpowers:test-driven-development`), using the Unity Test Framework.
- **EditMode tests (fast, no scene):** scoring, the game state machine with a fake clock, One Euro filter, player lock, calibration, throw detection against recorded poses, parameter mapping, `LaneSpec.PinSpot`, stores.
- **PlayMode tests:**
  - Physics determinism: the same throw gives the same pins, 10 out of 10 times on the same machine.
  - Known throws give expected outcomes within a tolerance.
  - No tunneling in 1,000 seeded random throws.
  - A full-game smoke test with `RecordedTrackingSource`, run headless.
- **Native tests:** XCTest for the ring buffer, the C API lifecycle, and the axis-conversion fixtures.
- **Recorded pose fixtures:**
  - Throw recordings in `test-data/pose-recordings/` (Git LFS), from spike S0 and later sessions.
  - These are the regression suite for SC-2 and SC-3.
- **Source-of-truth checks:** `tools/checks/` scripts plus `Bowling.Tooling.Editor` validators run in CI. Any violation fails the build.
- **Field test:** before release, run 2 booth days with first-time players to measure SC-1 and SC-6.

## 11. Decomposition and build order

Each row is its own sub-project with its own spec → plan → implementation cycle. Plans are written one sub-project at a time, in this order.

| # | Sub-project | Spec | Depends on |
|---|---|---|---|
| S0 | **Tracking feasibility spike** (no spec; see below) | — | — |
| 1 | Project foundation | `2026-10-18-project-foundation-design.md` | — |
| 2 | TrackingCore native plugin | `2026-10-18-tracking-core-design.md` | 1 |
| 3 | Pose input | `2026-10-18-pose-input-design.md` | 1 |
| 4 | Throw detection | `2026-10-18-throw-detection-design.md` | 3, S0 recordings |
| 5 | Bowling physics | `2026-10-18-bowling-physics-design.md` | 1 |
| 6 | Gameplay and scoring | `2026-10-18-gameplay-design.md` | 1 |
| 7 | UI | `2026-10-18-ui-design.md` | 1 |
| 8 | Presentation | `2026-10-18-presentation-design.md` | 5 |
| 9 | Platform and release | `2026-10-18-platform-release-design.md` | 1. Finished last, once everything is integrated. |

After foundation, sub-projects 2, 3, 5, 6 and 7 can be built in parallel, because they only share Contracts.

**Spike S0: tracking feasibility.** This is throwaway code. Per Superpowers, a spike produces an answer, not a spec.

- **Question:** on the reference hardware and camera, does MediaPipe Pose (`full`, then `heavy`) hold 30 fps or more with usable wrist tracking during a real bowling swing?
- **What it also establishes:**
  - MediaPipe's world-axis signs.
  - The wrist visibility pattern during the backswing.
- **Method:** a Python script using the official `mediapipe` package, in `tools/spikes/pose-recorder/` (throwaway). It writes recordings in the `PoseRecording` JSON format defined in the pose-input spec.
- **Outputs that are kept:**
  - `docs/research/2026-10-tracking-spike-report.md`: measured fps and latency, axis signs, recommended model.
  - 30 or more labeled throw recordings plus 10 minutes of non-throw movement, from at least 8 people of varied height and handedness, in `test-data/pose-recordings/`.

## 12. Repository layout

```
bowling/
├─ CLAUDE.md, README.md
├─ .claude/                     settings.json (plugins), skills/ (project skills)
├─ docs/
│  ├─ superpowers/specs/        design specs (this folder)
│  ├─ superpowers/plans/        implementation plans (one per sub-project)
│  └─ research/                 spike reports, measurements
├─ unity/MotionBowling/         Unity project root (Assets/, Packages/, ProjectSettings/)
│  └─ Assets/
│     ├─ _Project/
│     │  ├─ Modules/<Module>/{Runtime,Editor,Tests}/   one asmdef each + README.md
│     │  ├─ Config/             ScriptableObject tuning assets
│     │  ├─ Art/  Audio/  Prefabs/  Scenes/  Settings/ (URP assets, volume profiles)
│     ├─ Plugins/macOS/         TrackingCore.bundle, MacPlatform.bundle (built by tools/build, Git LFS)
│     └─ StreamingAssets/Models/pose_landmarker_full.task
├─ native/
│  ├─ TrackingCore/             Xcode project + Bazel workspace (MediaPipe C++)
│  └─ MacPlatform/              Xcode project (kiosk, power assertions)
├─ art-source/                  Blender sources (Git LFS)
├─ test-data/pose-recordings/   Git LFS
└─ tools/
   ├─ checks/                   source-of-truth scripts (pre-commit + CI)
   ├─ build/                    native build, Unity build, sign, notarize, DMG
   ├─ kiosk/                    launchd agent install/uninstall
   └─ spikes/                   throwaway spike code (never shipped)
```

## 13. Decisions log

| # | Decision | Made by |
|---|---|---|
| D-1 | Unity 6 instead of Electron/Three.js, for lower latency and studio-standard tooling | Tejash |
| D-2 | MediaPipe Pose for body tracking | Tejash |
| D-3 | Camera and MediaPipe C++ live in a native plugin on their own threads. Unity only reads landmarks. | Claude (approved with the Unity approach) |
| D-4 | URP, PhysX, Cinemachine, UI Toolkit, FMOD | Claude (approved with the Unity approach) |
| D-5 | Star dependency graph through `Bowling.Contracts`, with a single composition root in `Bowling.App`, and no DI framework | Claude's call |
| D-6 | The S0 spike uses Python MediaPipe for recordings, replacing the earlier "homuler Unity plugin" de-risk step. It's faster and decoupled, and the recordings become test fixtures. | Claude's call |
| D-7 | Calibration is one gesture: "raise your bowling hand and hold 1 s". It captures handedness, arm length, shoulder width and neutral position together. | Claude's call |
| D-8 | Hook is a normalized −1..1 value in `ThrowEvent`. Physics owns the conversion to forces. | Claude's call |
| D-9 | Bumpers on and assist 0.5 by default (Casual), for walk-up audiences | Claude's call |
| D-10 | Leaderboard uses "Player N" names only. No initials or photos (no personal data). | Claude's call |
| D-11 | Native plugin binaries are committed via Git LFS, so Unity-only developers don't need Bazel | Claude's call |

## 14. Open questions for review

- **Q-1 (A-1):** Is the booth the primary venue? It drives kiosk mode, the attract loop and the default difficulty.
- **Q-2 (A-4):** Is clean stylized flat-color 3D the right look? Realistic textures would need gradient-free texture authoring, which is much harder.
- **Q-3:** Should bloom be banned? It's a light effect, not an authored gradient, but it produces soft halos. Default: off.
- **Q-4 (A-7):** The product name and bundle id.
- **Q-5:** Should the leaderboard allow 3-letter initials picked by gesture? This adds a screen; the default is no.
