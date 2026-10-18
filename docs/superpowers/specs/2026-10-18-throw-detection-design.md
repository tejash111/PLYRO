# Sub-project 4: Throw Detection

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 3 (pose input), and the spike S0 recordings for tuning and regression
- **Module:** `Bowling.ThrowDetection`

## 1. Purpose

Throw detection recognizes a real bowling arm swing from the locked player's pose stream. It turns that swing into one `ThrowEvent`: speed, launch angle, start offset, hook and confidence, with the difficulty assist applied.

It must:

- reliably catch real throws (SC-2: 95% or more);
- ignore everything else (SC-3: at most 1 false throw per 10 min);
- be fully deterministic, so recordings work as regression tests.

**Implements:** `IThrowSource` (Contracts 6.3).

## 2. Components

```
Modules/ThrowDetection/Runtime/
├─ Components/
│  ├─ ThrowDetectionDriver.cs     [DefaultExecutionOrder(-200)] pulls new PlayerPoses each Update → ThrowDetector
│  ├─ DebugThrowSource.cs         #if BOWLING_DEV — keyboard IThrowSource for dev without a camera
│  └─ ThrowDebugOverlay.cs        #if BOWLING_DEV — IMGUI text: phase, speeds, last features (flat, no gradients)
├─ Core/
│  ├─ ThrowDetector.cs            implements IThrowSource; owns SwingStateMachine; feeds features
│  ├─ SwingStateMachine.cs        the FSM in section 4 (pure, no Unity calls besides math types)
│  ├─ SwingSignals.cs             per-pose derived signals (section 3)
│  ├─ SwingSignalsBuilder.cs      computes SwingSignals from PlayerPose history
│  ├─ ReleaseWindow.cs            ring of the last 0.30 s of SwingSignals
│  ├─ ThrowMapper.cs              release-window features → raw ThrowEvent values (section 5)
│  ├─ HandRollEstimator.cs        forearm-relative hand roll rate (section 5.4)
│  └─ AssistBlender.cs            difficulty assist (section 6)
└─ Config/
   └─ ThrowTuning.cs              ScriptableObject; asset Config/ThrowDetection/ThrowTuning.asset
```

Every threshold below is a `ThrowTuning` field, with the default shown. All positions are in player space (system design 6.1). The bowling side `b` comes from `ThrowContext.Player.BowlingHand`.

## 3. Signals (per `PlayerPose`)

| Signal | Definition |
|---|---|
| `W`, `E`, `S` | Wrist, elbow and shoulder positions on side `b` (metres) |
| `L` | `ThrowContext.Player.ArmLengthM` |
| `v` | Wrist velocity: central difference over neighbouring poses using their timestamps; backward difference for the newest pose (m/s) |
| `speed` | `|v|` |
| `vf` | Forward speed `v.z` (+ = toward the camera) |
| `vis` | Wrist visibility on side `b` |
| `behind` | `W.z < S.z − backswingDepth·L` (`backswingDepth` = 0.15) |
| `low` | `W.y < S.y − lowDrop·L` (`lowDrop` = 0.60) |
| `inFront` | `W.z > 0` (in front of the hip midpoint plane) |

## 4. Swing state machine

```
Disarmed ──Arm(ctx)──► WaitingForReady

WaitingForReady ──► Ready
   when player present AND pose Confidence ≥ minConfidence (0.50)
        AND speed < readyMaxSpeed (0.35 m/s) continuously for readyHoldTime (0.40 s)
        AND W.y ≤ S.y + 0.20·L (not raised overhead) AND NOT behind

Ready ──► Backswing
   when (behind AND low)
     OR (vis < occludedVisibility (0.35) for ≥ 2 consecutive poses
         AND over the previous 0.30 s mean v.y < −0.30 m/s or mean v.z < −0.30 m/s)   // arm swung back behind the body

Backswing ──► ForwardSwing
   when vf > forwardMinSpeed (1.20 m/s) AND vis ≥ occludedVisibility for ≥ 2 consecutive poses

ForwardSwing ──► Released   (first condition to hold; release time tR = that pose's timestamp)
   (a) bottom-and-rise: v.y crosses from < 0 to ≥ 0 while inFront
   (b) forward peak:    vf has reached ≥ forwardMinSpeed and now < peakDropRatio (0.70) × max vf since ForwardSwing,
                        for 2 consecutive poses

Released: compute features over the release window → ThrowEvent → Disarmed (PhaseChanged) → raise Thrown

Guards (checked every pose):
   Backswing or ForwardSwing lasting > swingTimeout (2.0 s) since Backswing entry → Rejected(SwingTimeout) → WaitingForReady
   At Released: wrist path length since Backswing entry < minSwingPath (0.80·L)     → Rejected(SwingTooSmall) → WaitingForReady
   At Released: release-window mean Confidence < minReleaseConfidence (0.45)        → Rejected(LowConfidence) → WaitingForReady
   Player presence lost while in Ready/Backswing/ForwardSwing                       → Rejected(PlayerLeft) → WaitingForReady
   Disarm() at any time → Disarmed (no event)
```

- `PhaseChanged` fires on every phase change.
- **Ordering:** while armed, `Thrown` fires at most once. Gameplay re-arms for the next ball.
- **Approach steps:** walking toward the camera during the throw is supported. Player space is hip-centred, so the steps don't trigger the backswing.
- **Hand-raise exclusion:** the `Ready` condition requires the wrist to be at or below shoulder height. This keeps a raised hand (cursor use) from looking like the start of a swing.

## 5. Mapping the release window to ball parameters (`ThrowMapper`)

The release window covers [tR − `releaseWindow` (0.20 s), tR].

### 5.1 Speed

```
peak      = max(speed) over the window                       (m/s)
norm      = peak / L                                          (1/s, scale-free: kids and adults comparable)
t         = smoothstep(clamp01((norm − speedNormLow) / (speedNormHigh − speedNormLow)))   // Low 3.0, High 12.0
SpeedMps  = lerp(LaneSpec.MinBallSpeedMps, LaneSpec.MaxBallSpeedMps, t)                  // 4.5 → 9.5 m/s
```

### 5.2 Launch angle

The lane runs into the screen and the player faces the screen. Lane +X is the bowler's right, which is player-space −X. Lane +Z is player-space +Z.

```
vx̄, vz̄         = mean v.x, mean v.z over the window
LaunchAngleDeg = clamp(angleGain (0.5) × atan2(−vx̄, max(vz̄, 0.1)) in degrees, ±LaneSpec.MaxLaunchAngleDeg)
```

### 5.3 Start offset

```
dx           = hipMidImageX(tR) − Player.NeutralImageX          // image x is unmirrored: player's right = smaller x
bodyOffset   = −dx × offsetGain (2.0 m per image unit)            // stepping to your right moves you to lane +X
releaseSide  = (Hand == Right ? +1 : −1) × releaseSideOffset (0.12 m)
StartOffsetM = clamp(bodyOffset + releaseSide, ±LaneSpec.MaxStartOffsetM)
```

### 5.4 Hook

**Roll rate (`HandRollEstimator`):**

1. Project the hand vector (index fingertip − pinky fingertip, side `b`) onto the plane perpendicular to the forearm (elbow → wrist).
2. Measure its signed angular velocity around the forearm axis.
3. Take the mean over the window, in rad/s.

**Lateral curl:** `curl` = (mean `v.x` over the second half of the window − mean `v.x` over the first half), converted to lane X (negated).

```
hookRaw = rollWeight (0.7) × (rollRate / fullRollRate (15 rad/s)) × handSign + curlWeight (0.3) × (curl / fullCurl (1.5 m/s))
Hook    = hookEnabled (true) ? clamp(hookGain (0.6) × hookRaw, −1, 1) : 0
```

`handSign` maps the roll direction so that a natural hook curves toward the bowler's non-dominant side: lane −X for right-handers, +X for left-handers.

**Validating the sign:** the S0 recordings labeled `intent: hook-left` or `hook-right` must produce a `Hook` with the matching sign in at least 80% of cases. If they don't, the sign convention in `HandRollEstimator` gets fixed. This is a test, not a judgement call.

### 5.5 Confidence

```
Confidence = mean(PlayerPose.Confidence over window) × mean(vis over window)
```

## 6. Assist (`AssistBlender`)

This uses `a = ThrowContext.Difficulty.Assist` (Casual 0.5, Standard 0.25, Pro 0). The assist nudges the throw; it never fully corrects it.

```
xTarget        = (Hand == Right ? +1 : −1) × pocketOffset (0.065 m)        // 1-3 pocket (righty), 1-2 (lefty)
angleIdeal     = atan((xTarget − StartOffsetM) / LaneSpec.FoulLineToHeadPinM) in degrees
LaunchAngleDeg = lerp(LaunchAngleDeg, angleIdeal, a)
Hook           = Hook × (1 − hookDamping (0.5) × a)
```

Speed and start offset are never assisted. Bumpers are a physics concern (`DifficultyProfile.Bumpers`). The final values are re-clamped to the `LaneSpec` limits.

## 7. Debug throw source (`DebugThrowSource`, dev builds only)

- It implements `IThrowSource` with the same `Arm`/`Disarm` semantics. App wires it instead of `ThrowDetector` when the `-tracking=debug` launch argument is given in a `BOWLING_DEV` build.
- **Keys (Input System):**
  - ←/→ adjust start offset by ±0.05 m.
  - ↑/↓ adjust the angle by ±0.25°.
  - W/S adjust speed by ±0.25 m/s.
  - A/D adjust hook by ±0.1.
  - Space throws.
- The current values show in `ThrowDebugOverlay`.

## 8. Tests

**EditMode, with synthetic swings.** `SyntheticSwing` is a test helper in this module's tests. It generates `PlayerPose` sequences from parameters: arm length, swing duration, peak speed, lateral component, roll rate, an occlusion window, and approach steps.

- **FSM:**
  - A clean swing passes through Ready → Backswing → ForwardSwing → Released in order, and `Thrown` fires once.
  - An occluded backswing (wrist visibility 0.1 for 0.3 s) is still detected.
  - A timeout (a slow 2.5 s swing) gives `Rejected(SwingTimeout)`.
  - A short flick gives `Rejected(SwingTooSmall)`.
  - A hand raised overhead never reaches `Ready`.
  - `Disarm` mid-swing raises no events.
  - Presence loss gives `Rejected(PlayerLeft)`.
- **Mapping:**
  - Speed is monotonic in peak wrist speed.
  - Equal `norm` gives equal `SpeedMps` for arm lengths 0.45 and 0.75.
  - The angle sign: a swing toward the player's right gives a positive `LaunchAngleDeg`.
  - The start-offset sign: the hip shifted toward the player's right gives a positive `StartOffsetM`.
  - Clamps are respected.
- **Assist:**
  - `a = 0` leaves the values unchanged.
  - `a = 1` gives an angle equal to `angleIdeal` and a hook scaled by 0.5.
  - Re-clamping is applied.
- **Determinism:** the same pose sequence gives a bit-identical `ThrowEvent`, 100 times out of 100.

**Recording regression (EditMode, in `Bowling.App.Tests.EditMode`).** This test combines two modules, so it lives in App's integration tests (foundation section 4). `ThrowRecordingRegressionTests` runs every recording in `test-data/pose-recordings/` through `PoseInput`'s pipeline and then `ThrowDetector`. It uses the recorded calibration: the first raised-hand hold, or a profile from the subject metadata if there was none.

- **SC-2:** a throw label counts as detected if exactly one `Thrown` occurs within [t0, t1 + 0.3 s]. The suite fails if fewer than 95% are detected.
- **SC-3:** every `Thrown` outside throw labels is a false positive. The suite fails if there's more than 1 per 10 minutes of non-throw labeled time.
- **Hook sign:** the 80% criterion in 5.4.
- **Output:** a per-file table printed to the test log (detected / missed / false). This is the tuning dashboard.

## 9. Tuning workflow

1. Change a `ThrowTuning` field.
2. Run the regression suite.
3. Keep the change only if SC-2, SC-3 and the hook-sign result improve or hold.
4. Record each kept change in the plan's notes, with before and after numbers.

The tuning asset is the only place thresholds live (SOT-2).

## 10. Acceptance criteria

1. All EditMode tests pass.
2. The regression suite meets SC-2 and SC-3 on the S0 corpus. At least 30 throws and 10 minutes of non-throw movement, from 8 people of varied height and handedness.
3. On device, the release event fires within 50 ms of the true release, by visual check on 10 throws using 240 fps phone video.
4. `ThrowDetector` processing takes 0.1 ms or less per frame at p95.

## 11. Decisions made here (Claude's calls, please check)

- **Two release triggers** (bottom-and-rise, or forward-speed peak), because the front camera sees the swing mostly in depth.
- **Occlusion as a backswing signal:** the wrist disappearing behind the body is expected, not an error.
- **Speed normalized by arm length,** so children and adults get comparable ball speeds.
- **Hook from hand roll plus lateral curl,** with a modest gain (0.6). It can be switched off (`hookEnabled`) if field tests show it feels random.
- **The assist nudges only the angle and the hook,** never speed or position. Players still feel their own throw.
