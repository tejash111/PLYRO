# Sub-project 1: Project Foundation

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** nothing
- **Unblocks:** every other sub-project

## 1. Purpose

Create the Unity project and module skeleton so that every later sub-project can start on day one, with both sources of truth enforced automatically. Run "rules first, features second". After this sub-project it is impossible to merge a gradient or a cross-module reference by accident.

## 2. Scope

**In scope:**

- The Unity project with pinned packages and SOT-safe project settings.
- Module folders, asmdefs and READMEs.
- `Bowling.Contracts`, implemented exactly as system design section 6.
- The `Bowling.Testing` fakes.
- The `Bowling.App` bootstrap skeleton.
- The source-of-truth validators (editor and shell).
- The pre-commit hook and `tools/ci/verify.sh`.
- Git LFS and Unity YAML merge configuration.

**Out of scope:** any feature behavior. Those modules are empty except for their README and asmdef.

## 3. Unity project

**Location:** `unity/MotionBowling/`.

**Editor version:** Unity 6.3 LTS. Use the latest `6000.3` patch on the day the plan runs (on 2026-10-18 that is `6000.3.26f1`). It is pinned in `tools/ci/unity-version.txt` and `ProjectSettings/ProjectVersion.txt`, and the two must match.

**Packages** (exact versions are pinned in `Packages/manifest.json` and recorded in the plan):

| Package | Why |
|---|---|
| `com.unity.render-pipelines.universal` | Rendering (GC-1) |
| `com.unity.inputsystem` | Operator keyboard shortcuts |
| `com.unity.cinemachine` (3.x) | Camera director (presentation) |
| `com.unity.test-framework` | EditMode and PlayMode tests |
| `com.unity.test-framework.performance` | Frame-time tests |
| `com.unity.nuget.newtonsoft-json` | JSON for recordings, settings and leaderboard |

FMOD for Unity is added by the presentation sub-project, not here.

Default packages that aren't in this table are removed (for example Visual Scripting, Collab, Timeline, uGUI and Multiplayer Center). The IDE integration packages (`com.unity.ide.rider`, `com.unity.ide.visualstudio`) and the built-in `com.unity.modules.*` packages stay.

### 3.1 Player settings

| Setting | Value |
|---|---|
| Company name | Shava Studios |
| Product name | Motion Bowling (assumption A-7) |
| Bundle identifier | `ai.shavastudios.motionbowling` |
| Scripting backend | IL2CPP |
| API compatibility | .NET Standard 2.1 |
| Architecture | Apple Silicon. This is a per-user build setting that isn't stored in `ProjectSettings`, so the platform build script enforces it at build time. |
| Graphics API | Metal only |
| Camera usage description | "Motion Bowling uses the camera to see your bowling swing. Video stays on this Mac and is never saved." |
| Run in background | On |
| Visible in background | On |
| Fullscreen mode | Fullscreen window |
| Default resolution | 1920×1080 |
| Resizable window | Off |
| Minimum macOS | 14.0 |

### 3.2 Editor settings

- Asset serialization: Force Text.
- Visible meta files.
- Enter Play Mode options: domain reload **off**, scene reload on. This is safe because SOT-2 bans static state. It's also a useful tripwire: code that leaks static state misbehaves immediately.

### 3.3 Time settings

- `Fixed Timestep` = 0.0041666667 (1/240 s, GC-6).
- `Maximum Allowed Timestep` = 0.0333333 (1/30 s).

### 3.4 Graphics, quality and lighting baseline (SOT-1-safe)

- **Quality:** a single quality level named `Booth`, using `Settings/URP-Booth.asset`. Vsync is every V blank.
- **Global volume profile:** contains only Tonemapping in Neutral mode. No Vignette, no Bloom, no Lens Flare, no Film Grain.
- **`Main.unity` lighting:**
  - Environment Lighting Source = **Color**.
  - Skybox material = none.
  - Fog off.
  - Environment reflections source = Custom with no cubemap. Reflection probes come later in presentation.
- **Camera:** the default camera prefab clears to a solid color, `Palette.Background` from the presentation spec. Until presentation exists, use `#14161F`.
- **Where the settings assets live:** the URP asset, its renderer, the URP global settings, Unity 6's default volume profile and `GlobalVolumeProfile.asset` all live in `Assets/_Project/Settings/`.

### 3.5 Scenes

There is one scene, `Assets/_Project/Scenes/Main.unity`, and it is the only scene in the build settings. It has two root objects:

- `Bootstrap`, which carries `GameBootstrap` from App.
- An instance of `Assets/_Project/Prefabs/Rendering/Rendering.prefab`, which holds the Main Camera and the Global Volume.

Later sub-projects add prefab instances, never loose objects (SOT-2).

## 4. Module skeleton

Create each of these, with the assembly names below:

```
Assets/_Project/Modules/<Module>/
├─ README.md
├─ Runtime/Bowling.<Module>.asmdef
├─ Editor/Bowling.<Module>.Editor.asmdef          (only where listed)
└─ Tests/
   ├─ EditMode/Bowling.<Module>.Tests.EditMode.asmdef
   └─ PlayMode/Bowling.<Module>.Tests.PlayMode.asmdef  (only where listed)
```

| Module | Runtime | Editor | EditMode tests | PlayMode tests |
|---|---|---|---|---|
| Contracts | ✓ | | ✓ | |
| PoseInput | ✓ | | ✓ | |
| ThrowDetection | ✓ | | ✓ | |
| BowlingPhysics | ✓ | | ✓ | ✓ |
| Gameplay | ✓ | | ✓ | |
| Presentation | ✓ | | ✓ | ✓ |
| UI | ✓ | | ✓ | ✓ |
| Platform | ✓ | | ✓ | |
| App | ✓ | ✓ (build script, platform spec) | ✓ (integration) | ✓ (integration) |
| Tooling | | ✓ | ✓ | |
| Testing | ✓ (test-only) | | ✓ (fake semantics) | |

**Asmdef rules.** Every asmdef sets `autoReferenced: false`. The rest depends on its kind (M-1 checks this table):

| Kind | `name` | `rootNamespace` | `includePlatforms` | Other required fields |
|---|---|---|---|---|
| Runtime | `Bowling.<Module>` | `Bowling.<Module>` | `[]` | `references`: `Bowling.Contracts` plus the allowed packages below |
| Editor | `Bowling.<Module>.Editor` | `Bowling.<Module>.Editor` | `["Editor"]` | — |
| EditMode tests | `Bowling.<Module>.Tests.EditMode` | `Bowling.<Module>.Tests` | `["Editor"]` | `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` |
| PlayMode tests | `Bowling.<Module>.Tests.PlayMode` | `Bowling.<Module>.Tests` | `[]` | Same as EditMode tests |
| Testing (fakes) | `Bowling.Testing` | `Bowling.Testing` | `[]` | `defineConstraints: ["UNITY_INCLUDE_TESTS"]` (never ships); references `Bowling.Contracts` only |

- **Precompiled DLLs:** every asmdef sets `overrideReferences: true` and lists its precompiled DLLs explicitly. Otherwise auto-referenced DLLs such as `Newtonsoft.Json.dll` would reach every module.
- **Test asmdef references:** their module, `Bowling.Contracts`, `Bowling.Testing`, `UnityEngine.TestRunner` and `UnityEditor.TestRunner`. They may also reference `Unity.PerformanceTesting`, the precompiled `Newtonsoft.Json.dll` (for JSON fixtures), and anything their own module may reference.
- **`Bowling.App`** references every runtime module.
- **Unsafe code:** only `Bowling.PoseInput` sets `allowUnsafeCode: true`, for the zero-allocation interop in pose-input spec section 7. Any other module that needs it requires a spec change first.

**Allowed external references** (stored as data in `tools/checks/module-rules.json`, which validator M-2 reads):

| Assembly | May additionally reference |
|---|---|
| `Bowling.Contracts` | nothing |
| `Bowling.PoseInput` | `Newtonsoft.Json.dll` (precompiled) |
| `Bowling.ThrowDetection` | nothing |
| `Bowling.BowlingPhysics` | nothing |
| `Bowling.Gameplay` | nothing |
| `Bowling.Presentation` | `Unity.Cinemachine`, `FMODUnity`, `Unity.RenderPipelines.Universal.Runtime`, `Unity.RenderPipelines.Core.Runtime` |
| `Bowling.UI` | nothing (UI Toolkit is part of UnityEngine) |
| `Bowling.Platform` | `Newtonsoft.Json.dll` (precompiled), `Unity.InputSystem` |
| `Bowling.App` | every `Bowling.*` runtime assembly, `Unity.InputSystem` |
| `Bowling.App.Editor` | every `Bowling.*` runtime assembly (build script only) |
| `Bowling.App.Tests.*` | every `Bowling.*` runtime assembly, plus `Bowling.Testing`. This is the **only** place for integration tests that combine modules. |
| `Bowling.Tooling.Editor` | `Unity.RenderPipelines.Universal.Runtime`, `Unity.RenderPipelines.Core.Runtime`, `Newtonsoft.Json.dll` (precompiled) |

### 4.1 Module README template

Every module README has exactly these headings:

```markdown
# <Module>
## Purpose            (2–4 sentences)
## Public API         (the interfaces from Contracts it implements, and its public MonoBehaviours)
## Dependencies       (assemblies referenced and why)
## Folder map         (one line per subfolder)
```

### 4.2 Runtime folder convention inside a module

```
Runtime/
├─ Components/   MonoBehaviours (thin adapters), one per file
├─ Core/         plain C# logic (no MonoBehaviour), one type per file; may have area subfolders (e.g. Core/Recording/)
└─ Config/       ScriptableObject type definitions (the assets themselves live in Assets/_Project/Config/<Module>/)
```

The Tooling module has no `Runtime/`. Its `Editor/` holds `Setup/` (package installer and project-settings baseline), `Validation/` (validators and rules) and `Config/`.

The UI module replaces `Components/` with UI-component folders, and adds `Styles/`, `Screens/<Name>Screen/` (and `Screens/<Name>Overlay/` for overlays) and `Root/` (which holds its single MonoBehaviour, `UiRoot`). See the UI spec. Those are the only extra top-level folders allowed.

## 5. `Bowling.Contracts`

Implement system design section 6 **exactly**: names, members, comments, and one type per file under `Runtime/<Area>/`.

The only behavior is:

- `PinMaskExtensions.Count`, a popcount.
- `LaneSpec.PinSpot`, using the formula in system design section 6.7.
- Constructors for the readonly structs.

**Tests (EditMode):**

- `PinMask.All.Count() == 10`, and every single-pin value counts as 1.
- `LaneSpec.PinSpot` matches this table to within 1e-4 m:

| Pin | x | z |
|---|---|---|
| 1 | 0 | 18.288 |
| 2 | −0.1524 | 18.55196 |
| 3 | +0.1524 | 18.55196 |
| 4 | −0.3048 | 18.81592 |
| 5 | 0 | 18.81592 |
| 6 | +0.3048 | 18.81592 |
| 7 | −0.4572 | 19.07988 |
| 8 | −0.1524 | 19.07988 |
| 9 | +0.1524 | 19.07988 |
| 10 | +0.4572 | 19.07988 |

- `PinSpot(0)` and `PinSpot(11)` throw `ArgumentOutOfRangeException`.
- `RollResult.PinsKnocked` is correct for: full rack to empty (10); full rack to `P7|P10` (8); `P7|P10` to `P10` (1).

## 6. `Bowling.Testing` (test-only fakes)

Every one of these is a plain C# class implementing a Contracts interface. Each is deterministic and controllable from tests.

| Fake | Implements | Controls |
|---|---|---|
| `FakeClock` | `IClock` | `Advance(seconds)` |
| `FakeTrackingSource` | `ITrackingSource` | `Push(PoseSample)`, `SetStatus(...)` |
| `FakePlayerInput` | `IPlayerInput` | `SetPresent(bool)`, `PushPose(PlayerPose)` |
| `FakeCalibrator` | `IPlayerCalibrator` | `Complete(PlayerProfile)`, `Fail(reason)` |
| `FakeThrowSource` | `IThrowSource` | `EmitThrow(ThrowEvent)`, `Reject(reason)`; records `Arm`/`Disarm` calls |
| `FakeRollSimulator` | `IRollSimulator` | `CompleteWith(PinMask standingAfter, bool gutter = false)` |
| `RecordingGameLog` | `IGameLog` | Exposes the logged entries |
| `InMemoryLeaderboardStore` | `ILeaderboardStore` | — |
| `InMemorySettingsStore` | `ISettingsStore` | — |
| `FakeGameEvents` | `IGameEvents` | `SetState(GameState)` raises `StateChanged` then `SnapshotChanged`; `Publish(GameSnapshot)`; `RaiseRollScored(RollOutcome)` |
| `FakeGameCommands` | `IGameCommands` | Records every call and its arguments |
| `FakeHandCursor` | `IHandCursor` | `Set(bool active, Vector2 position)` |
| `FakeCameraPreview` | `ICameraPreview` | `Set(Texture)` |
| `FakeTrackingDiagnostics` | `ITrackingDiagnostics` | `Set(TrackingStats)`, `SetCameras(...)` |
| `FakeRecordingIndicator` | `IRecordingIndicator` | `Set(bool)` |
| `FakeOperatorActions` | `IOperatorActions` | Records calls; `ApplySettings` raises `SettingsApplied` |
| `FakeUiCues` | `IUiCues` | `Raise(UiCue)` |
| `TestDataPaths` | — | Absolute path of `test-data/pose-recordings/` resolved from the Unity project path |

## 7. `Bowling.App` skeleton

- `GameBootstrap` (MonoBehaviour, `[DefaultExecutionOrder(-1000)]`) is the only composition root.
- In `Awake` it constructs plain C# services and hands references to module components through their public `Initialize(...)` methods.
- In this sub-project it constructs only `UnityClock : IClock` (unscaled time) and a `UnityConsoleGameLog : IGameLog`. Both live in App until Platform replaces the logger.
- Each later sub-project's plan adds its own wiring to `GameBootstrap` as a final task.
- **Execution order constants:** these live in `App/Runtime/Core/ExecutionOrder.cs`, and modules use the literal numbers in their `[DefaultExecutionOrder]` attributes:

| Driver | Order |
|---|---|
| PoseInput | −300 |
| ThrowDetection | −200 |
| Gameplay | −100 |
| Presentation | 0 |
| UI | 100 |

The App PlayMode test checks the attributes against these constants with reflection.

## 8. Source-of-truth validators

Two layers share one rule set:

- **Fast text checks** (`tools/checks/check_sources_of_truth.sh`, bash + grep) run in the pre-commit hook. They must finish in under 2 s.
- **Unity editor validators** (`Bowling.Tooling.Editor`) need asset APIs. They run from the menu **Bowling ▸ Validate Sources of Truth** and in batch mode via `-executeMethod Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch`, which exits with code 1 on any violation.

**Output format** (one line per violation): `<rule-id> <path>[:<line>] <message>`

**Scan scope:**

- `Assets/_Project/**`, plus `.uss`/`.uxml` anywhere under `Assets/`.
- Excluded: `Assets/_Project/Modules/Tooling/**` (it contains the banned patterns as data), `tools/checks/**`, and vendor folders (`Assets/Plugins/FMOD/**`, `Packages/**`).

### 8.1 SOT-1 rules (no gradients)

| ID | Layer | Detects |
|---|---|---|
| G-1 | text | In `.uss`/`.uxml`: `linear-gradient(`, `radial-gradient(`, `conic-gradient(`, and `text-shadow` with a non-zero blur radius |
| G-2 | text | In `.cs`, ignoring lines whose first non-space characters are `//`: `new Gradient(`, `GradientColorKey`, `GradientAlphaKey`, `colorGradient`, `enableVertexGradient`, `VertexGradient`, `TMP_ColorGradient`, `AmbientMode.Trilight`, `AmbientMode.Skybox`, `ambientSkyColor`, `ambientEquatorColor`, `ambientGroundColor`, `RenderSettings.fog = true`, `Vignette`, `ScreenSpaceLensFlare` |
| G-3 | text | In `.shadergraph`/`.shadersubgraph`: node types `UnityEditor.ShaderGraph.GradientNode`, `UnityEditor.ShaderGraph.SampleGradient`, `UnityEditor.ShaderGraph.BlackbodyNode` |
| G-3b | text | Any `.shader`/`.hlsl`/`.shadergraph` under `_Project` that isn't listed in `tools/checks/reviewed-shaders.txt` (format: `<path> # reviewed by <name> on <date>: <why gradient-free>`) |
| G-4 | editor | Every `VolumeProfile` asset under `Assets/` (vendor folders excluded): an active `Vignette`, `ScreenSpaceLensFlare`, `FilmGrain` or `Bloom` (Q-3 default) whose intensity is above 0. Unity 6's default profile carries every override at zero intensity, so zero-intensity overrides pass. |
| G-5 | editor | Every scene in the build plus every prefab under `_Project`: `RenderSettings.ambientMode` not `Flat`; fog on; skybox not null; any `Camera.clearFlags == Skybox`; any `LineRenderer`/`TrailRenderer` whose `colorGradient` has more than one color key or more than one alpha key |
| G-6 | editor | Every color `Texture2D`/`Sprite` under `_Project/Art/**` and `_Project/Modules/UI/**`. Skipped: normal maps, non-sRGB data textures, lightmaps. Fails when more than 2% of opaque pixels differ from a 4-neighbour by a small non-zero step (0 < Δ ≤ 3/255 on any channel), which is the signature of a smooth ramp. Thresholds live in `Tooling/Editor/Config/GradientHeuristic.asset`. The check reads the source PNG bytes, so any other color-texture format in the scanned folders fails G-6 with "convert to PNG". |

### 8.2 SOT-2 rules (modular structure)

| ID | Layer | Detects |
|---|---|---|
| M-1 | editor | A `Modules/<X>/` folder missing its README or expected asmdefs; asmdef names or rootNamespace that don't match section 4 |
| M-2 | editor | An asmdef reference outside `tools/checks/module-rules.json` |
| M-3 | text | More than one public top-level type in a `.cs` file; file name ≠ public type name |
| M-4 | text | A `.cs` file over 400 lines (fail) or over 300 lines (warning only) |
| M-5 | text | In runtime `.cs` outside `Modules/App/`: `FindObjectOfType`, `FindFirstObjectByType`, `FindAnyObjectByType`, `GameObject.Find`, `Resources.Load`, and `static <Type> Instance` singleton properties |
| M-6 | text | A `.cs` file under `Assets/_Project/` but outside `Modules/` |
| M-7 | editor | A root object in `Main.unity` that's neither a prefab instance root nor named `Bootstrap` |
| M-8 | text | A UI component folder `UI/Runtime/Components/<Name>/` missing `<Name>.uxml`, `<Name>.uss` or `<Name>.cs`; a color literal (`#hex`, `rgb(`, `rgba(`) in any `.uss` other than `UI/Runtime/Styles/Tokens.uss` |

### 8.3 Validator tests

- Each rule has one passing and one failing case, in `Tooling/Tests/EditMode`.
- Editor-layer cases create temporary assets under `Assets/_TestTemp/` and delete them in teardown.
- Text-layer cases run the shell script against fixture folders in `tools/checks/fixtures/{pass,fail}/<rule-id>/`, invoked from `tools/checks/test_checks.sh`.

## 9. Hooks and CI

**`tools/checks/install-hooks.sh`** installs `.git/hooks/pre-commit`, which runs `check_sources_of_truth.sh` on staged files only.

**`tools/ci/verify.sh`** is the single entry point for local runs and CI. It runs these steps in order and stops at the first failure:

1. `check_sources_of_truth.sh --all`
2. `test_checks.sh`
3. Unity batch mode `SourceOfTruthValidator.RunBatch`
4. Unity EditMode tests
5. Unity PlayMode tests
6. `required-files` check: every path in `tools/checks/required-files.txt` exists (the file starts empty; later sub-projects append entries)

**How Unity steps are judged:**

- Unity documents no process exit codes for test runs. So test steps pass only when the NUnit XML results file exists, its `total` is above 0 and its `failed` is 0.
- Step 3 also fails on any `warning CS` from `Assets/_Project` in the editor log, which covers acceptance criterion 1.

**Helper scripts:**

- `tools/ci/unity.sh` resolves the editor and runs it.
- `tools/ci/run_unity_tests.sh` runs one test platform.
- `tools/ci/check_test_results.sh` judges a results file.
- `tools/dev/check-env.sh` checks the developer machine, with install steps in `docs/dev-setup.md`.

The Unity editor path comes from the `UNITY_EDITOR` environment variable. If that's unset, it's derived from `ProjectVersion.txt` plus the Unity Hub default install path.

**CI provider (Claude's call; change if you use something else):** a GitHub Actions workflow `.github/workflows/verify.yml` on a self-hosted Apple Silicon runner. It calls `tools/ci/verify.sh`. The Unity license is activated on the runner once, out of band.

## 10. Git configuration

- **`.gitignore`:**
  - The standard Unity set: `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/`, `MemoryCaptures/`, `*.csproj`, `*.sln`, `.vs/`, `.idea/`.
  - Native build output: `native/**/build/`, `native/**/bazel-*`.
  - Also `.DS_Store` and `Assets/_TestTemp/`.
- **`.gitattributes` with Git LFS for:** `*.fbx *.blend *.png *.psd *.tga *.exr *.wav *.ogg *.bank *.task *.dylib`, `Assets/Plugins/macOS/*.bundle/**` and `test-data/**`.
- **Unity YAML merge:** `*.unity *.prefab *.asset *.mat` use `merge=unityyamlmerge`. `docs/dev-setup.md` explains how to register UnityYAMLMerge in your git config.

## 11. Acceptance criteria

1. The project opens with the pinned editor and compiles with zero errors and zero warnings in `_Project`.
2. All 11 module folders exist with READMEs and the asmdefs from section 4. M-1 and M-2 pass.
3. Contracts matches system design section 6, and its tests pass.
4. Every validator rule has passing and failing tests, and they pass.
5. On a clean checkout, `tools/ci/verify.sh` exits 0.
6. **Manual check:** staging a `.uss` containing `linear-gradient(` is rejected by the pre-commit hook with a `G-1` line.
7. **Manual check:** adding a `Bowling.Gameplay → Bowling.BowlingPhysics` reference fails M-2.

## 12. Plan-time refinements (2026-10-18, pending your approval with the plan)

Writing the implementation plan surfaced the changes now reflected above:

- The Unity 6.3 LTS version pin.
- Removing unused default packages.
- Architecture as a build-time setting.
- Where the settings assets live.
- The `Rendering` prefab as a second scene root.
- Tests for the Testing fakes.
- Explicit precompiled references (Newtonsoft.Json is a precompiled DLL, not an asmdef).
- The Tooling folder layout.
- G-2 ignores comment lines.
- G-4 judges intensity and scans all of `Assets/`.
- G-6 reads PNG only.
- Unity results judged from XML.
- The helper scripts.

## 13. Decisions made here (Claude's calls, please check)

- No DI framework. Wiring is done by hand in `GameBootstrap` (system design D-5).
- Domain reload is off in the editor.
- The texture gradient heuristic (G-6) uses a 2% threshold. It may need tuning once real art exists.
- The CI provider is GitHub Actions on a self-hosted runner.
