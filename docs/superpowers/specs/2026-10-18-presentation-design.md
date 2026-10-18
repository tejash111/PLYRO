# Sub-project 8: Presentation (Art, Cameras, Replays, Audio)

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 5 (physics bodies and events); uses Gameplay and PoseInput events through Contracts
- **Module:** `Bowling.Presentation`

## 1. Purpose

Presentation makes the game look and sound like a bowling alley:

- the 3D environment and lighting;
- the ball in the player's hand before release;
- broadcast-style camera cuts;
- strike and spare replays;
- celebration effects;
- all audio.

It reacts to events. It never changes game state.

**Consumes:**

- `IGameEvents`
- `IRollSimulator` (`Milestone`, `Impact`)
- `IRollBodies`
- `IPlayerInput` (wrist pose for the ball in hand)
- `IThrowSource` (phase)
- `IUiCues`
- `IOperatorActions.SettingsApplied` (volume)

## 2. Art direction (SOT-1 applied to 3D)

The look is clean, stylized 3D. Shading comes only from light: solid-color materials, crisp shapes, a lit neon atmosphere without glow effects.

**Materials:**

- URP Lit or Simple Lit with a flat **Base Color**.
- No albedo textures, with one exception: hard-edged pattern textures that pass validator G-6. Examples are the lane board stripes, the targeting arrows and dots, and the pin neck stripes.
- Roughness and smoothness are scalar values per material.
- Normal maps are allowed (they aren't color).

**Palette:** `Config/Presentation/Palette.asset` is a ScriptableObject of named flat colors. Materials take their base colors from it. The player colors match the UI tokens.

| Name | Hex |
|---|---|
| Background | #14161F |
| Wall | #1F2233 |
| MaskingUnit | #262A40 |
| LaneBoardA | #D9A066 |
| LaneBoardB | #C98F55 |
| LaneArrow | #7A4E2D |
| Gutter | #3A3F4B |
| PinBody | #F5F3EE |
| PinStripe | #D7263D |
| NeonPink | #FF4FA3 |
| NeonCyan | #3CE7FF |
| Player1 | #2E86DE |
| Player2 | #E4572E |
| Player3 | #17BEBB |
| Player4 | #B97CF3 |

**Emissive "neon":**

- Neon signs and strips use flat emissive colors. Their brightness reads clearly without bloom.
- Bloom stays off (open question Q-3 in the system design). If Q-3 allows bloom, it gets enabled in the volume profile and the G-4 rule is relaxed. Nothing else changes.

**Assets:**

- Blender source files live in `art-source/` (Git LFS) and are exported as FBX to `Assets/_Project/Art/Models/`.
- Meshes are low-to-mid poly with clean silhouettes.
- The pin render mesh matches the `LaneSpec` dimensions. The physics hull is separate (physics spec).

## 3. Environment (`Alley.prefab`)

- **Main lane dressing:** visuals aligned to the physics `Lane.prefab`, which is nested inside.
  - Approach with dots.
  - Lane board stripes and arrows at 4.6 m.
  - Gutters, capping and division boards.
- **Pin area:** the masking unit above the pins, carrying an in-world scoreboard.
  - The scoreboard is a UI Toolkit panel rendered to a `RenderTexture` and shown on a flat unlit quad.
  - It shows the current player's name and total.
  - It uses UI's components through a dedicated `UIDocument` owned by `UiRoot`. App passes Presentation the `RenderTexture` reference.
- **Neighbouring lanes:** two decorative static lanes, left and right, with standing pins. They have no physics; their pins are GPU-instanced.
- **Room:** side walls, a back wall with flat neon shapes, and ceiling light panels (flat emissive) above the lane.
- **Scene rule (M-7):** everything is a prefab. `Main.unity` holds `Bootstrap`, `Alley`, `Cameras` and `UI`.

## 4. Lighting and rendering

**Ambient:**

- Environment lighting is a **flat Color** (SOT-1), Palette.Background brightened 2×.
- No skybox. Cameras clear to a solid Palette.Background.
- No fog.

**Lights:**

- **Baked:** indirect light and area lights for the room, using the Progressive GPU lightmapper.
- **Realtime:** one spot "lane key light" above the pin deck and one above the approach, both casting soft realtime shadows for the ball and pins.

**Probes:**

- A light probe grid along the lane: every 1 m, at 3 heights (0.1, 0.5, 1.2 m).
- 3 box-projected baked reflection probes along the lane. They give the glossy lane surface (smoothness 0.85) its reflections of the room's flat colors.

**URP asset (`URP-Booth.asset`):**

| Setting | Value |
|---|---|
| Rendering path | Forward+ |
| MSAA | 4× |
| HDR | On |
| Render scale | 1.0 at 1080p; 0.5 on 4K displays, set at startup from the screen size |
| Shadow distance | 30 m, 2 cascades |
| Soft shadows | Medium |
| SSAO renderer feature | Intensity 0.4 (a lighting effect, allowed) |
| Post-processing | Tonemapping Neutral only |

## 5. Cameras (`Cameras.prefab`, Cinemachine 3)

The main camera has a `CinemachineBrain`. `CameraDirector` (plain class) decides which camera is live, and `CameraDirectorBinding` (MonoBehaviour) applies that by setting camera priorities.

| Camera | Placement (lane frame) | Live when |
|---|---|---|
| `AttractCam` | `CinemachineSplineDolly` on a slow loop over the lane | GameState Attract, Join |
| `BowlerCam` | Position (0, 1.6, −2.2), looking at (0, 0.2, 10) | ModeSelect, Calibrate, AwaitReady, AwaitThrow, and after `Settled` + 0.5 s in Scoring |
| `BallFollowCam` | Follows `IRollBodies.Ball` with offset (0, 0.8, −2.5), position damping 0.3 | From `Milestone.Released` until the ball reaches z > 13 m |
| `PinCam` | (−0.9, 0.9, 16.8), looking at (0, 0.2, 18.8) | From ball z > 13 m until `Settled` + 0.5 s |
| `ReplayCam` | (1.2, 0.3, 17.6), looking at (0, 0.15, 18.7) | During a replay |

**Blends:**

- BowlerCam → BallFollowCam: 0.4 s ease-in-out.
- BallFollowCam → PinCam: 0.6 s ease-in-out.
- Everything else is a cut.

## 6. Ball in hand (`HandBallPresenter`)

- **When it's shown:** during AwaitReady and AwaitThrow, a visual-only ball in the current player's palette color appears in the BowlerCam view, following the bowling-side wrist.
- **Mapping:**
  - Start from the wrist's player-space (x, y), relative to the shoulder and normalized by arm length.
  - Map it onto a plane 1.2 m in front of BowlerCam, mirrored like the UI cursor. Range: ±0.5 m horizontally, −0.6 to +0.3 m vertically.
  - Smooth it with presentation's own light One Euro filter (`minCutoff` 2.0, `beta` 0.5). This is a separate instance from PoseInput's filter, used for visuals only.
- **Occlusion:** while the wrist is occluded (visibility < 0.35), the ball holds its last position and fades to 50% opacity over 0.15 s. That's a time-based flat fade, which is allowed.
- **Release:** on `Milestone.Released`, the hand ball hides, and the physics ball, activated by `Roll`, carries on.

## 7. Replays (`RollRecorder`, `ReplayPlayer`, `ReplayProxies`)

- **Recording:**
  - `RollRecorder` samples the poses of `IRollBodies.Ball` and the 10 pins once per rendered frame, from `Released` to `Settled`.
  - The buffer is a ring of 12 s at 60 fps, preallocated.
- **Playback:**
  - **Trigger:** on `RollScored` with a Strike or Spare mark, as long as the roll lasted 0.5 s or more.
  - **Content:** the 2.5 s before `Settled`, played at 0.4× speed (capped at 3.5 s in total) through ReplayCam.
  - **Proxies:** the replay drives `ReplayProxies`, which is a visual-only copy of the ball and 10 pins. While it plays, the physics bodies' renderers are hidden. Physics is never touched.
  - **Ending:** the replay can't be skipped in v1. It ends before gameplay's `scoringHoldReplay` (4.5 s) expires.
- **Strike and spare banners** are UI's job ("Strike!", "Spare!"), triggered by `RollScored` there.

## 8. Effects (`CelebrationVfx`)

- **Strike:** a flat confetti burst behind the pin deck. A `ParticleSystem` emits mesh particles (quads and triangles), unlit, in flat palette colors. Color over lifetime may fade alpha (time-based, allowed). No soft sprites, no trails.
- **Spare:** a smaller burst.
- **Gutter:** no effect, only a sound.
- **No ball trail.** A trail would need gradients and adds nothing.

## 9. Audio (FMOD Studio)

**Files:**

- The FMOD Studio project lives in `audio-source/MotionBowling/` (Git LFS).
- Banks are built into `Assets/StreamingAssets/FMOD/`.
- FMOD for Unity sits in `Assets/Plugins/FMOD/` (vendor folder, excluded from SOT-2 structure checks).

**Isolation:** `AudioDirector` (plain class) maps game events to `IAudioPort` calls. `FmodAudioPort` implements `IAudioPort`. Tests use `FakeAudioPort`. FMOD types never leave `FmodAudioPort`.

| Event | Trigger | Parameters |
|---|---|---|
| `event:/Ball/Roll` | Start on `Released`; stop on `EnteredPit` or `Settled` | `speed` 0–10 m/s, `surface` (oil 0 / dry 1 / gutter 2); 3D, attached to the ball |
| `event:/Pins/HitBall` | `ImpactEvent` `BallPin` | `impulse` 0–20 N·s |
| `event:/Pins/HitPin` | `PinPin` | `impulse` |
| `event:/Pins/HitDeck` | `PinDeck`, `PinPit` | `impulse` |
| `event:/Ball/Gutter` | `EnteredGutter` | — |
| `event:/Ball/Bumper` | `BallBumper` | `impulse` |
| `event:/Ball/PitThud` | `EnteredPit` | — |
| `event:/Rack/Clack` | `SetRack` was called (observed via `GameState` AwaitReady entry) | — |
| `event:/Stingers/Strike`, `/Spare`, `/Gutter`, `/GameOver` | `RollScored` mark / `GameOver` | — |
| `event:/UI/DwellTick`, `/UI/Confirm`, `/UI/Back`, `/UI/Toast` | `IUiCues.Cue` | — |
| `event:/Music/Attract`, `event:/Music/Game` | Attract vs. other states | `intensity` 0–1 (rises in the last frame) |
| `event:/Ambience/Alley` | Always on | — |

**Mixer:**

- Buses: Master, Music, SFX, UI, Ambience.
- Master volume follows `OperatorSettings.MasterVolume`.
- `Pins/*` is limited to 8 voices, stealing the quietest.
- Impacts below 0.05 N·s are already filtered by physics.

## 10. Tests

- **EditMode:**
  - `CameraDirector`: every row of section 5, including the z thresholds and the +0.5 s holds.
  - `AudioDirector` with `FakeAudioPort`: each event in section 9 maps to the right calls and parameters; voice limiting.
  - `RollRecorder` and `ReplayPlayer`: ring wrap; the replay window selection; duration ≤ 3.5 s.
  - `HandBallPresenter` mapping: mirroring and the range clamps.
- **PlayMode:**
  - `Alley` + `Lane` + `Cameras` load with no missing references.
  - A full roll driven by `DebugThrowSource` produces the camera sequence Bowler → BallFollow → Pin → Bowler. This combines modules, so it lives in `Bowling.App.Tests.PlayMode`.
  - The source-of-truth validators pass on all presentation assets.
- **On device (manual, recorded in the plan):** with a strike replay playing at 1080p on the reference hardware, rendering holds 60 fps or more at p99, measured with the Unity Profiler and the Performance Testing package.

## 11. Acceptance criteria

1. All tests pass, and the G-4, G-5 and G-6 validators pass on every presentation asset.
2. SC-4: 60 fps or more at p99 on the reference hardware in every state.
3. A strike replay starts within 0.3 s of `RollScored` and ends before the scoring hold expires.
4. No FMOD type is referenced outside `FmodAudioPort` (checked by a grep test).

## 12. Decisions made here (Claude's calls, please check)

- **The stylized flat-color art direction** (system design Q-2).
- **Replay through visual proxies** rather than re-simulating, so it's deterministic and doesn't touch physics.
- **No ball trail.**
- **No sweep animation in v1** (see physics spec section 12): a clack sound instead.
- **FMOD** as the audio middleware. Check the FMOD license tier for your budget.
