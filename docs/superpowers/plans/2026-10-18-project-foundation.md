# Project Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the Unity 6.3 project, all module skeletons and `Bowling.Contracts`, with both sources of truth enforced automatically by shell checks, Unity editor validators, a pre-commit hook and `tools/ci/verify.sh`.

**Architecture:** Settings are code. Package set, player, editor, time, URP and scene settings are applied by idempotent editor batch methods in `Bowling.Tooling.Editor`, and pinned by EditMode tests. Source-of-truth rules run in two layers that share fixture-driven tests:

- **Text rules** (`tools/checks/check_sources_of_truth.sh`, bash/grep/awk) run fast in the pre-commit hook.
- **Asset rules** (`SourceOfTruthValidator`, Unity editor) need the asset database.

Shell wrappers under `tools/ci/` make every Unity run headless and judge results from NUnit XML, never from exit codes.

**Tech Stack:**

- Unity 6.3 LTS (`6000.3.26f1`, or the newest 6000.3 patch on execution day): URP, IL2CPP, Metal.
- C# (.NET Standard 2.1) with Unity Test Framework (NUnit).
- bash, grep and awk (macOS system versions).
- Git LFS.
- GitHub Actions on a self-hosted Apple Silicon runner.

**Spec:** `docs/superpowers/specs/2026-10-18-project-foundation-design.md`. Contracts come from `docs/superpowers/specs/2026-10-18-motion-bowling-system-design.md` section 6.

## Global Constraints

- **SOT-1:** at any single rendered frame, every authored surface or element is filled with exactly one flat color; no authored gradients anywhere (system design section 4).
- **SOT-2:** every runtime module references only `Bowling.Contracts`; `Bowling.App` is the single composition root; one public type per file, and the file name matches the type name; soft limit 300 lines, hard limit 400.
- **GC-1:** Unity 6 LTS: URP, Metal only, IL2CPP, target macOS Apple Silicon (arm64). The exact editor version is pinned in `ProjectVersion.txt`.
- **GC-2:** minimum macOS 14.0, Apple M1 or newer.
- **GC-3:** fully offline at runtime; no network calls.
- **GC-4:** camera images never leave process memory.
- **GC-5:** SI units; lane frame origin at the center of the foul line, +Z down the lane, +X to the bowler's right, +Y up.
- **GC-6:** physics fixed step 1/240 s; render target 60 fps (vsync).
- **C# namespaces** equal the assembly's `rootNamespace`. Subfolders don't add namespace segments.
- **No statics:** no static mutable state, no singletons, no `FindObjectOfType`/`GameObject.Find`/`Resources.Load` outside `Bowling.App`.
- **Paths:** the Unity project lives at `unity/MotionBowling/`. Shell scripts must work when the repo path contains spaces.
- **Commits:** every commit made by an AI agent ends with its `Co-Authored-By:` trailer.

## Review Focus

1. **Repo path with spaces** (for example `~/My Projects/bowling`). Every script quotes its paths. `test_checks.sh` runs the fixtures from a temp dir whose name contains a space (Task 6).
2. **Pre-commit with only deleted or renamed files, or nothing in scope staged.** The hook passes, and it never tries to read a deleted file (Task 6).
3. **A Unity run that produced no results** (the project is open in another Editor, Unity crashed, or a wrong filter selected zero tests). The run is reported as a failure, never a pass (Task 2).
4. **A banned token inside a `//` comment** (for example `// no Vignette here`). G-2 ignores full-line comments, but still flags the same token in code (Task 6).
5. **Unity editor missing, or a different version than pinned.** `unity.sh` exits 2 with a message naming the expected version and path, instead of hanging or using another editor (Task 2).

---

## File Structure

```
.gitignore, .gitattributes                           Task 1
docs/dev-setup.md                                    Task 1  (machine setup, UnityYAMLMerge)
tools/lib/assert.sh                                  Task 1  (shared bash test helpers)
tools/dev/check-env.sh, tools/dev/test_check_env.sh  Task 1
tools/ci/unity-version.txt                           Task 2
tools/ci/unity.sh, run_unity_tests.sh, check_test_results.sh, test_ci_scripts.sh   Task 2
tools/ci/fixtures/results/{passed,failed,zero}.xml   Task 2
tools/checks/check_sources_of_truth.sh               Tasks 6–7
tools/checks/test_checks.sh, fixtures/{pass,fail,warn}/<rule>/Assets/...           Tasks 6–7
tools/checks/install-hooks.sh, reviewed-shaders.txt  Task 6
tools/checks/module-rules.json                       Task 8
tools/checks/required-files.txt                      Task 16
tools/ci/verify.sh, .github/workflows/verify.yml     Task 16
test-data/pose-recordings/.gitkeep                   Task 14
unity/MotionBowling/                                 Task 2  (created by Unity)
  Packages/manifest.json                             Task 3
  ProjectSettings/*                                  Tasks 4–5 (written by the baseline methods)
  Assets/_Project/
    Settings/ URP-Booth.asset, URP-Booth_Renderer.asset, UniversalRenderPipelineGlobalSettings.asset,
              DefaultVolumeProfile.asset, GlobalVolumeProfile.asset            Task 5
    Prefabs/Rendering/Rendering.prefab               Task 5
    Scenes/Main.unity                                Tasks 5, 15
    Modules/
      Tooling/  README.md
                Editor/Bowling.Tooling.Editor.asmdef
                Editor/Setup/PackageInstaller.cs, ProjectSettingsBaseline.cs        Tasks 3–5
                Editor/Validation/Violation.cs, IValidationRule.cs, ValidationContext.cs,
                                  ModuleRules.cs, SourceOfTruthValidator.cs          Task 8
                Editor/Validation/Rules/ModuleStructureRule.cs (M-1), AsmdefReferenceRule.cs (M-2)   Task 8
                Editor/Validation/Rules/VolumeProfileRule.cs (G-4), SceneAndPrefabRule.cs (G-5),
                                        MainSceneRootRule.cs (M-7)                    Task 10
                Editor/Validation/Rules/TextureGradientRule.cs (G-6), GradientHeuristic.cs  Task 11
                Editor/Config/GradientHeuristicSettings.cs, GradientHeuristic.asset   Task 11
                Tests/EditMode/Bowling.Tooling.Tests.EditMode.asmdef + test files    Tasks 3–11
                Tests/EditMode/Fixtures~/                                            Task 8 (Unity ignores folders ending in ~)
      Contracts|PoseInput|ThrowDetection|BowlingPhysics|Gameplay|Presentation|UI|Platform|App|Testing/
                README.md, asmdefs per spec section 4, Runtime/AssemblyInfo.cs       Task 9
      Contracts/Runtime/{Pose,Input,Lane,Roll,Throw,Game,Platform}/*.cs              Tasks 12–13
      Testing/Runtime/*.cs                                                           Task 14
      App/Runtime/Core/{ExecutionOrder,UnityClock,UnityConsoleGameLog}.cs,
          Runtime/Components/GameBootstrap.cs, Editor/MainSceneSetup.cs              Task 15
```

**Shared conventions:**

- **Unity runs:** `tools/ci/unity.sh run <args>` runs headless against the project. `tools/ci/unity.sh exec <Class.Method>` runs an editor batch method, which must call `EditorApplication.Exit(0|1)`.
- **Tests:** `tools/ci/run_unity_tests.sh <EditMode|PlayMode> [assemblyNames]` runs tests and prints `PASS total=N` or `FAIL …`.
- **Logs** go to `unity/MotionBowling/Logs/ci/` (gitignored).

---

### Task 1: Developer environment check and git configuration

**Files:**
- Create: `.gitignore` (replaces the current one-line file), `.gitattributes`, `docs/dev-setup.md`, `tools/lib/assert.sh`, `tools/dev/check-env.sh`
- Test: `tools/dev/test_check_env.sh`

**Interfaces:**
- **Produces `tools/lib/assert.sh`:** a sourced bash library.
  - `assert_exit <code> <cmd...>` and `assert_output_contains <needle> <cmd...>`.
  - Both increment `PASSED`/`FAILED` and print `ok - <desc>` or `not ok - <desc>`.
  - `finish` exits 1 if `FAILED > 0`.
  - `<desc>` is the command line.
- **Produces `tools/dev/check-env.sh`:**
  - Prints one line per check, `OK <what>` or `MISSING <what> — <fix>`, and exits 1 if anything is missing.
  - Env override `UNITY_HUB_EDITORS_DIR` (default `/Applications/Unity/Hub/Editor`).
  - The version comes from `tools/ci/unity-version.txt`, or the `UNITY_VERSION` env var overrides it. Until Task 2 creates the file, the default is `6000.3.26f1`.

- [ ] **Step 1: Write the failing test `tools/dev/test_check_env.sh`.** It sources `tools/lib/assert.sh` and builds two temp editor dirs.
  - `empty/`: `assert_exit 1` and `assert_output_contains "MISSING Unity 6000.3.26f1"` for `UNITY_VERSION=6000.3.26f1 UNITY_HUB_EDITORS_DIR=<empty> tools/dev/check-env.sh`.
  - `fake/6000.3.26f1/Unity.app/Contents/MacOS/Unity` (an executable stub) plus `fake/6000.3.26f1/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/Variations/macos_arm64_player_development_il2cpp/` (an empty dir): output contains `OK Unity 6000.3.26f1` and `OK Mac IL2CPP module`.
- [ ] **Step 2:** Run `bash tools/dev/test_check_env.sh`. Expected: FAIL (`check-env.sh: No such file`).
- [ ] **Step 3: Implement `tools/dev/check-env.sh`.**
  - **Checks:**
    1. Xcode: `xcode-select -p` ends in `Xcode.app/Contents/Developer` and `xcodebuild -version` exits 0.
    2. `git lfs version`.
    3. Unity Hub at `/Applications/Unity Hub.app`.
    4. The Unity editor binary for the pinned version.
    5. A `Variations/*il2cpp*` directory, which shows the Mac IL2CPP module is installed.
  - **Fixes named in the output:** the matching section of `docs/dev-setup.md`.
- [ ] **Step 4: Write `docs/dev-setup.md`.** It has these sections, each with the exact commands:
  - **Xcode:** install from the App Store, then `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`, then `sudo xcodebuild -license accept`.
  - **Git LFS:** `brew install git-lfs`, then `git lfs install --local`, run in the repo.
  - **Unity Hub plus the editor with Mac Build Support (IL2CPP):** in the Hub UI, or `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install --version <ver> --module mac-il2cpp`. Sign in to Unity and activate a license (Personal is fine).
  - **UnityYAMLMerge:** the three `git config merge.unityyamlmerge.*` lines. The driver path is `/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge`, with arguments `merge -p %O %B %A %A`.
  - **Pre-commit hook:** `bash tools/checks/install-hooks.sh`.
- [ ] **Step 5: Write `.gitignore` and `.gitattributes`**, exactly as listed in spec section 10.
  - `.gitattributes` also sets `* text=auto` and `*.sh text eol=lf`.
  - The `.gitignore` paths are anchored under `unity/MotionBowling/`, for example `unity/MotionBowling/Library/`, plus `unity/MotionBowling/Assets/_TestTemp/`.
- [ ] **Step 6:** Run `bash tools/dev/test_check_env.sh`. Expected: every line is `ok`, and the exit code is 0.
- [ ] **Step 7: Human gate.**
  1. Run `bash tools/dev/check-env.sh` on this Mac. Expected today: `MISSING` for Xcode, Git LFS, Unity Hub and Unity.
  2. **Stop. Ask the user to install the missing items using `docs/dev-setup.md`.** They need an App Store install and a Unity account sign-in, which can't be automated.
  3. Re-run until every line is `OK`.
  4. If Unity Hub offers a newer `6000.3.x` patch than `6000.3.26f1`, install that one instead, and use its version in Task 2.
- [ ] **Step 8:** Run `git lfs install --local`. Then commit: `git add .gitignore .gitattributes docs/dev-setup.md tools/lib tools/dev && git commit -m "chore: dev environment check, git and LFS config"`.

### Task 2: Unity project, version pin, Unity CLI helpers

**Files:**
- Create: `tools/ci/unity-version.txt`, `tools/ci/unity.sh`, `tools/ci/run_unity_tests.sh`, `tools/ci/check_test_results.sh`, and `tools/ci/fixtures/results/passed.xml`, `failed.xml`, `zero.xml`
- Create (by Unity): `unity/MotionBowling/`
- Test: `tools/ci/test_ci_scripts.sh`

**Interfaces:**
- **Produces `tools/ci/unity.sh`:**
  - `unity.sh path` prints the editor binary, or exits 2 with `ERROR: Unity <ver> not found at <path>. Install it (docs/dev-setup.md) or set UNITY_EDITOR.`
    - Version: from `unity/MotionBowling/ProjectSettings/ProjectVersion.txt` (`m_EditorVersion:`) if present, else from `tools/ci/unity-version.txt`.
    - If both exist and differ, exit 2 with `ERROR: ProjectVersion.txt (<a>) != unity-version.txt (<b>)`.
    - `UNITY_EDITOR` overrides the resolved path. A value that isn't executable exits 2.
  - `unity.sh create` creates the project, and exits 1 if `unity/MotionBowling` already exists.
  - `unity.sh run <args...>` runs with `-batchmode -nographics -projectPath unity/MotionBowling -logFile unity/MotionBowling/Logs/ci/${UNITY_LOG_NAME:-run}.log` and passes the exit code through.
  - `unity.sh exec <Method>` is `run -executeMethod <Method>`.
- **Produces `run_unity_tests.sh <EditMode|PlayMode> [assemblyNames]`:** writes `unity/MotionBowling/Logs/ci/<Platform>-results.xml` (removed first), then runs `check_test_results.sh` on it.
- **Produces `check_test_results.sh <xml>`:**
  - Exits 0 and prints `PASS total=N` only if the file exists, the `<test-run>` element has `total` above 0, `failed="0"`, and `result` starts with `Passed`.
  - Otherwise it prints `FAIL <reason>`, followed by each `<test-case … result="Failed">` `fullname`, and exits 1.

- [ ] **Step 1: Write the failing test `tools/ci/test_ci_scripts.sh`.** This covers Review Focus 3 and 5. Using `assert.sh`:
  - `check_test_results.sh` exits 0 for `passed.xml`.
  - It exits 1 for `failed.xml`, and the output contains the failing test's fullname.
  - It exits 1 for `zero.xml`, which has `total="0"` (output contains `total=0`).
  - It exits 1 for a missing path (output contains `no results file`).
  - `UNITY_EDITOR=/nonexistent tools/ci/unity.sh path` exits 2, and the output contains `/nonexistent`.
  - With a temp `UNITY_HUB_EDITORS_DIR` and no editor, `unity.sh path` exits 2, and the output contains the version from `unity-version.txt`.

  Fixture XML is minimal NUnit3. For example, `passed.xml` is `<test-run id="2" testcasecount="1" result="Passed" total="1" passed="1" failed="0">` wrapping one passed `<test-case fullname="A.B.C" result="Passed"/>`.
- [ ] **Step 2:** Run `bash tools/ci/test_ci_scripts.sh`. Expected: FAIL (the scripts are missing).
- [ ] **Step 3: Implement the three scripts.** Write the pinned version into `tools/ci/unity-version.txt` (one line, for example `6000.3.26f1`). Parse XML with `grep -o`/`sed` on the `<test-run` element's attributes. No Python.
- [ ] **Step 4:** Run `bash tools/ci/test_ci_scripts.sh`. Expected: every line is `ok`.
- [ ] **Step 5: Create the project.**
  1. `tools/ci/unity.sh create`, which runs `<editor> -batchmode -nographics -createProject "$REPO/unity/MotionBowling" -logFile … -quit`.
  2. Expected: `unity/MotionBowling/ProjectSettings/ProjectVersion.txt` exists with `m_EditorVersion: <pinned>`, and `tools/ci/unity.sh path` exits 0.
- [ ] **Step 6: Record key names.** Run `grep -nE "cameraUsageDescription|macOSTargetOSVersion|activeInputHandler" unity/MotionBowling/ProjectSettings/ProjectSettings.asset`, and write the exact keys found into `docs/dev-setup.md` under "Serialized PlayerSettings keys". Task 4 uses them. If a key is absent, search the file for `TargetOSVersion`/`InputHandler` and record the closest match.
- [ ] **Step 7:** Commit: `git add tools/ci unity/MotionBowling docs/dev-setup.md && git commit -m "build: Unity 6.3 project and headless CLI helpers"`.

### Task 3: Package set

**Files:**
- Create: `Modules/Tooling/README.md` (4 template headings; Purpose: "Editor-only validators and project setup utilities")
- Create: `Modules/Tooling/Editor/Bowling.Tooling.Editor.asmdef` (Editor kind per spec section 4; `overrideReferences: true`, `precompiledReferences: []`, no references yet)
- Create: `Modules/Tooling/Editor/Setup/PackageInstaller.cs`
- Create: `Modules/Tooling/Tests/EditMode/Bowling.Tooling.Tests.EditMode.asmdef` (EditMode kind; references `Bowling.Tooling.Editor` and the TestRunners; precompiled `nunit.framework.dll` and `Newtonsoft.Json.dll`)
- Modify: `unity/MotionBowling/Packages/manifest.json` (through the installer only)
- Test: `Modules/Tooling/Tests/EditMode/PackageManifestTests.cs`

(All `Modules/...` paths are under `unity/MotionBowling/Assets/_Project/`.)

**Interfaces:**
- **Produces** `public static class PackageInstaller`:
  - `public static readonly string[] Required` = `com.unity.render-pipelines.universal`, `com.unity.inputsystem`, `com.unity.cinemachine`, `com.unity.test-framework`, `com.unity.test-framework.performance`, `com.unity.nuget.newtonsoft-json`.
  - `public static readonly string[] Removed` = `com.unity.visualscripting`, `com.unity.collab-proxy`, `com.unity.timeline`, `com.unity.ugui`, `com.unity.multiplayer.center`, `com.unity.ai.navigation`.
  - `public static void InstallBatch()`:
    - Calls `UnityEditor.PackageManager.Client.AddAndRemove(Required, Removed ∩ currently-installed)`.
    - Polls the request on `EditorApplication.update`.
    - On success it calls `EditorApplication.Exit(0)`. On failure it logs the error and calls `EditorApplication.Exit(1)`.

- [ ] **Step 1: Prerequisite.** If `com.unity.test-framework` isn't already in `manifest.json` (the default project normally includes it), do Step 3 first, then return here.
- [ ] **Step 2: Write the failing test `PackageManifestTests`.** It reads `Packages/manifest.json` with Newtonsoft and checks:
  - `RequiredPackagesPresent`: every `Required` name is a key in `dependencies`.
  - `NoUnapprovedPackages`: every key that doesn't start with `com.unity.modules.` is in `Required` ∪ {`com.unity.ide.rider`, `com.unity.ide.visualstudio`}.
  - `VersionsAreExact`: each `Required` version matches `^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$`.
  - `CinemachineIsMajor3`: the version starts with `3.`.
- [ ] **Step 3: Implement `PackageInstaller`** as specified in Interfaces.
- [ ] **Step 4:** Run `tools/ci/run_unity_tests.sh EditMode Bowling.Tooling.Tests.EditMode`. Expected: `FAIL`, with `RequiredPackagesPresent` and `NoUnapprovedPackages` failing.
- [ ] **Step 5:** Run `tools/ci/unity.sh exec Bowling.Tooling.Editor.PackageInstaller.InstallBatch`. Expected: exit 0, and `manifest.json` now lists exact versions.
- [ ] **Step 6:** Run `tools/ci/run_unity_tests.sh EditMode Bowling.Tooling.Tests.EditMode`. Expected: `PASS total=4`.
- [ ] **Step 7:** Record the resolved versions in this plan's "Execution notes" at the bottom. Then commit: `git add unity/MotionBowling/Packages unity/MotionBowling/Assets/_Project/Modules/Tooling docs/superpowers/plans && git commit -m "build: pinned package set"`.

### Task 4: Player, editor and time settings baseline

**Files:**
- Create: `Modules/Tooling/Editor/Setup/ProjectSettingsBaseline.cs`
- Test: `Modules/Tooling/Tests/EditMode/PlayerSettingsBaselineTests.cs`

**Interfaces:**
- **Produces** `public static class ProjectSettingsBaseline`:
  - `public const string CameraUsageDescription = "Motion Bowling uses the camera to see your bowling swing. Video stays on this Mac and is never saved.";`
  - `public static void ApplyPlayerEditorAndTime()`
  - `public static void ApplyAllBatch()`: calls every `Apply*` method, saves assets, then `EditorApplication.Exit(0)`, or `Exit(1)` on exception. Task 5 adds `ApplyRendering()` to it.

- [ ] **Step 1: Write the failing tests in `PlayerSettingsBaselineTests`.** One test per row; the values are verbatim from spec sections 3.1–3.3.
  - `PlayerSettings.companyName == "Shava Studios"`; `productName == "Motion Bowling"`.
  - `GetApplicationIdentifier(NamedBuildTarget.Standalone) == "ai.shavastudios.motionbowling"`.
  - `GetScriptingBackend(NamedBuildTarget.Standalone) == ScriptingImplementation.IL2CPP`.
  - `GetApiCompatibilityLevel(NamedBuildTarget.Standalone) == ApiCompatibilityLevel.NET_Standard`.
  - `GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneOSX) == false`, and `GetGraphicsAPIs(BuildTarget.StandaloneOSX)` equals `{ GraphicsDeviceType.Metal }`.
  - The serialized camera-usage key (from Task 2 Step 6) `== CameraUsageDescription`.
  - The serialized macOS minimum-version key `== "14.0"`.
  - The serialized `activeInputHandler == 1` (Input System only).
  - `runInBackground` and `visibleInBackground` are true; `fullScreenMode == FullScreenMode.FullScreenWindow`; `defaultScreenWidth == 1920`; `defaultScreenHeight == 1080`; `resizableWindow == false`.
  - `EditorSettings.serializationMode == SerializationMode.ForceText`; `VersionControlSettings.mode == "Visible Meta Files"`.
  - `EditorSettings.enterPlayModeOptionsEnabled` is true; `enterPlayModeOptions == EnterPlayModeOptions.DisableDomainReload`.
  - `Math.Abs(Time.fixedDeltaTime - 1f/240f) < 1e-7`; `Math.Abs(Time.maximumDeltaTime - 1f/30f) < 1e-6`.

  Serialized keys are read through `new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]).FindProperty(key)`.
- [ ] **Step 2:** Run `tools/ci/run_unity_tests.sh EditMode Bowling.Tooling.Tests.EditMode`. Expected: `FAIL`, with the baseline tests failing.
- [ ] **Step 3: Implement `ApplyPlayerEditorAndTime()`.**
  - Use the public `PlayerSettings`/`EditorSettings` APIs where they exist.
  - Use `SerializedObject` for the serialized keys and for `ProjectSettings/TimeManager.asset`. Iterate its properties once to find the fixed-timestep and maximum-timestep fields.
  - The method is idempotent.
- [ ] **Step 4:** Run `tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch`, then `tools/ci/run_unity_tests.sh EditMode Bowling.Tooling.Tests.EditMode`. Expected: `PASS`. If the input-handler change asks for a restart, the second batch launch picks it up.
- [ ] **Step 5:** Commit: `git add unity/MotionBowling/ProjectSettings unity/MotionBowling/Assets/_Project/Modules/Tooling && git commit -m "build: player, editor and time settings baseline"`.

### Task 5: URP, quality, lighting and Main scene baseline

**Files:**
- Modify: `Bowling.Tooling.Editor.asmdef` (add `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime`), `ProjectSettingsBaseline.cs`
- Create (by the baseline method): `Assets/_Project/Settings/*` (five assets, see File Structure), `Assets/_Project/Prefabs/Rendering/Rendering.prefab`, `Assets/_Project/Scenes/Main.unity`
- Test: `Modules/Tooling/Tests/EditMode/RenderingBaselineTests.cs` (the test asmdef gains the same two URP references)

**Interfaces:**
- **Produces** `public static void ApplyRendering()` on `ProjectSettingsBaseline`, called from `ApplyAllBatch`.
- **Produces these assets for Tasks 10 and 15:**
  - `Main.unity` (the only enabled scene in `EditorBuildSettings.scenes`).
  - The `Rendering.prefab` root contains:
    - `Main Camera`: tag `MainCamera`, `clearFlags = SolidColor`, background `#14161F`, URP additional data with post-processing on.
    - `Global Volume`: `isGlobal = true`, profile `GlobalVolumeProfile.asset`.

- [ ] **Step 1: Write the failing tests in `RenderingBaselineTests`:**
  - `GraphicsSettings.defaultRenderPipeline` is a `UniversalRenderPipelineAsset` at `Assets/_Project/Settings/URP-Booth.asset`.
  - `QualitySettings.names` equals `{ "Booth" }`; `QualitySettings.vSyncCount == 1`; `QualitySettings.renderPipeline` is the same asset.
  - The URP global settings asset and the default volume profile asset both have paths starting with `Assets/_Project/Settings/`. Use `GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>()` and the URP default-volume-profile graphics setting.
  - `GlobalVolumeProfile.asset` has exactly one component: an active `Tonemapping` whose `mode.value == TonemappingMode.Neutral`.
  - The prefab: the camera background `(Color32)` equals `(0x14, 0x16, 0x1F, 0xFF)` with `clearFlags == CameraClearFlags.SolidColor`; the `Volume` is global and its profile is that asset.
  - `Main.unity`, opened with `EditorSceneManager.OpenScene(path, OpenSceneMode.Single)`:
    - `RenderSettings.ambientMode == AmbientMode.Flat`.
    - `RenderSettings.ambientLight == #282C3E` (Palette.Background brightened 2×).
    - `RenderSettings.skybox == null`; `RenderSettings.fog == false`; `RenderSettings.defaultReflectionMode == DefaultReflectionMode.Custom` with `customReflectionTexture == null`.
    - Exactly one root object, which is an instance of `Rendering.prefab`. Task 15 adds `Bootstrap` and updates this assertion to the two roots in spec section 3.5.
  - `EditorBuildSettings.scenes` contains exactly one enabled entry, the `Main.unity` path.
- [ ] **Step 2:** Run the Tooling EditMode tests. Expected: `FAIL` on the new tests.
- [ ] **Step 3: Implement `ApplyRendering()`.**
  - Create or reuse the URP asset and renderer via `UniversalRenderPipelineAsset.Create(...)`.
  - Move or create the URP global settings and default volume profile under `_Project/Settings`.
  - Set the quality levels to just "Booth" through `SerializedObject` on `ProjectSettings/QualitySettings.asset`.
  - Build the profile, the prefab and the scene.
  - It is idempotent: a second run changes nothing (`git status` stays clean).
- [ ] **Step 4:** Run `tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch` twice, then the Tooling EditMode tests. Expected: `PASS`, and after the second run `git status --short unity/MotionBowling` shows no new changes.
- [ ] **Step 5:** Commit: `git add unity/MotionBowling && git commit -m "build: URP, quality and Main scene baseline (SOT-1 safe)"`.

### Task 6: Text checks, part 1 (runner, hook, G-1, G-2, G-3, G-3b)

**Files:**
- Create: `tools/checks/check_sources_of_truth.sh`, `tools/checks/install-hooks.sh`, `tools/checks/reviewed-shaders.txt` (a header comment only)
- Create: fixtures `tools/checks/fixtures/{pass,fail}/{G-1,G-2,G-3,G-3b}/Assets/_Project/...`, plus `fixtures/{pass,fail}/G-3b/reviewed-shaders.txt`
- Test: `tools/checks/test_checks.sh`

**Interfaces:**
- **Produces** `check_sources_of_truth.sh [--all | --staged | --root <AssetsDir>] [--rules <id,id>] [--reviewed <file>]`:
  - `--all` scans `unity/MotionBowling/Assets`.
  - `--staged` scans the staged files (`git diff --cached --name-only --diff-filter=ACMR`) under `unity/MotionBowling/Assets/`.
  - `--root` scans a fixture `Assets` folder. The reviewed list defaults to `tools/checks/reviewed-shaders.txt`, or to `<root>/../reviewed-shaders.txt` with `--root`.
  - **Output:** `<rule-id> <path>[:<line>] <message>`, with paths starting at `Assets/`. Warnings are prefixed `WARN `.
  - **Exit codes:** 0 clean (warnings allowed), 1 violations, 2 usage error.
  - **Scope and exclusions:** as in spec section 8.
- **Produces** `install-hooks.sh`: writes an executable `.git/hooks/pre-commit` that runs `tools/checks/check_sources_of_truth.sh --staged`.

- [ ] **Step 1: Write the fixtures and the failing `test_checks.sh`.**
  - **For each rule dir:**
    - `pass/<id>`: `--root <dir>/Assets --rules <id>` exits 0, and no output line starts with `<id> `.
    - `fail/<id>`: exits 1, and some line starts with `<id> `.
  - **Fixture content:**
    - G-1 fail: one `.uss` with `linear-gradient(`, and one with `text-shadow: 2px 2px 4px black;`.
    - G-1 pass: `text-shadow: 2px 2px 0px black;` and an ID selector `#hud-root {}`.
    - G-2 fail: `var g = new Gradient();`.
    - G-2 pass: `// no Vignette here` as a full-line comment (Review Focus 4).
    - G-3 fail: a `.shadergraph` containing `"m_Type": "UnityEditor.ShaderGraph.SampleGradient"`.
    - G-3b fail: an unlisted `.hlsl`.
    - G-3b pass: the same file listed in its `reviewed-shaders.txt`.
  - **Review Focus 1:** copy all fixtures into `$(mktemp -d)/with space/` and repeat the G-1 pass and fail assertions from there.
  - **Review Focus 2:**
    1. In a temp `git init` repo containing `unity/MotionBowling/Assets/_Project/a.uss` with `linear-gradient(`, staging it gives `--staged` exit 1.
    2. After `git commit` and then `git rm` of that file, `--staged` exits 0.
    3. Renaming a clean `.uss` with `git mv` exits 0.
    4. Staging only `README.md` exits 0.
- [ ] **Step 2:** Run `bash tools/checks/test_checks.sh`. Expected: FAIL (the script is missing).
- [ ] **Step 3: Implement the runner and G-1/G-2/G-3/G-3b** with grep/awk, using the patterns verbatim from spec section 8.1:
  - **G-1 blur:** the third length of the `text-shadow` value is non-zero.
  - **G-2:** skips lines that match `^\s*//`.
  - **Staged mode:** reads each file's staged content via `git show :<path>`, never the working tree.
- [ ] **Step 4:** Implement `install-hooks.sh` and run it.
- [ ] **Step 5:** Run `bash tools/checks/test_checks.sh`, then `tools/checks/check_sources_of_truth.sh --all`. Expected: every line is `ok`, and `--all` exits 0 on the real project.
- [ ] **Step 6:** Commit: `git add tools/checks && git commit -m "feat(checks): text rules G-1..G-3b, pre-commit hook"`. The hook now runs on this commit.

### Task 7: Text checks, part 2 (M-3, M-4, M-5, M-6, M-8)

**Files:**
- Modify: `tools/checks/check_sources_of_truth.sh`, `tools/checks/test_checks.sh`
- Create: fixtures `tools/checks/fixtures/{pass,fail}/{M-3,M-5,M-6,M-8}/Assets/_Project/...`. The M-4 files are generated by the test.

**Interfaces:**
- **Consumes** Task 6's runner contract. These rules are added to the default rule set.

- [ ] **Step 1: Add the fixtures and assertions.**
  - **M-3:**
    - Pass: `Foo.cs`, a namespace block with `public sealed class Foo` containing a nested `public enum Kind`; and `Bar.cs`, using a file-scoped `namespace X;` with `public readonly struct Bar`.
    - Fail: `Two.cs` declares `public class Two` and `public class Other`; `Mismatch.cs` declares `public class NotMismatch`.
  - **M-4** (generated in a temp `Assets`): a 401-line `.cs` exits 1 with an `M-4` line; a 301-line `.cs` exits 0 with a `WARN M-4` line.
  - **M-5:**
    - Fail: `Modules/Gameplay/Runtime/Core/X.cs` containing `FindObjectOfType`, and another containing `public static X Instance {`.
    - Pass: the same `FindObjectOfType` text under `Modules/App/Runtime/` and under `Modules/Gameplay/Tests/EditMode/`.
  - **M-6:** fail for `Assets/_Project/Scripts/Loose.cs`.
  - **M-8:**
    - Fail: `Modules/UI/Runtime/Components/Badge/` containing only `Badge.uxml` and `Badge.cs`; a component `.uss` with `color: #FF0000;`.
    - Pass: a complete folder, `#hex` only in `Modules/UI/Runtime/Styles/Tokens.uss`, and an ID selector `#badge-root` in a component `.uss`.
- [ ] **Step 2:** Run `bash tools/checks/test_checks.sh`. Expected: FAIL on the new assertions.
- [ ] **Step 3: Implement the rules.**
  - **M-3:** an awk brace-depth tracker. A public declaration (`class|struct|interface|enum|record|delegate`, any modifiers) counts as top-level when it sits at namespace depth: 1 for a block namespace, 0 for a file-scoped one.
  - **M-8 color literals:** only matched in declaration values, after a `:`.
  - **M-5 singleton regex:** `static\s+[A-Za-z_][A-Za-z0-9_<>,]*\s+Instance\b`.
- [ ] **Step 4:** Run `bash tools/checks/test_checks.sh`, then `tools/checks/check_sources_of_truth.sh --all`. Expected: all `ok`, and the real project is clean.
- [ ] **Step 5:** Commit: `git add tools/checks && git commit -m "feat(checks): text rules M-3..M-8"`.

### Task 8: Editor validator framework, M-1 and M-2

**Files:**
- Create: `tools/checks/module-rules.json`
- Create under `Modules/Tooling/Editor/Validation/`: `Violation.cs`, `IValidationRule.cs`, `ValidationContext.cs`, `ModuleRules.cs`, `SourceOfTruthValidator.cs`, `Rules/ModuleStructureRule.cs`, `Rules/AsmdefReferenceRule.cs`
- Modify: `Bowling.Tooling.Editor.asmdef` (`overrideReferences: true`, `precompiledReferences: ["Newtonsoft.Json.dll"]`)
- Test: `Modules/Tooling/Tests/EditMode/ModuleStructureRuleTests.cs`, `AsmdefReferenceRuleTests.cs`, fixtures in `Modules/Tooling/Tests/EditMode/Fixtures~/{M-1,M-2}/{pass,fail}/Modules/...` (asmdef JSON as plain files; the `~` suffix keeps Unity from compiling them)

**Interfaces:**
- **Produces:**
  ```csharp
  public readonly struct Violation { public readonly string RuleId, Path, Message; public readonly int Line; public readonly bool IsWarning;
      public Violation(string ruleId, string path, int line, string message, bool isWarning); public override string ToString(); } // "<id> <path>[:<line>] <message>", line omitted when 0
  public interface IValidationRule { string Id { get; } IEnumerable<Violation> Run(ValidationContext context); }
  public sealed class ValidationContext {
      public string RepoRoot; public string ModulesRoot = "Assets/_Project/Modules"; public string MainScenePath = "Assets/_Project/Scenes/Main.unity";
      public ModuleRules Rules; public static ValidationContext ForProject(); }   // RepoRoot = Application.dataPath/../../..; Rules loaded from tools/checks/module-rules.json
  public sealed class ModuleRules { public static ModuleRules Load(string jsonPath); /* typed view of the JSON below */ }
  public static class SourceOfTruthValidator {
      public static IReadOnlyList<IValidationRule> CreateRules();            // Task 8: M-1, M-2; Tasks 10–11 append theirs
      public static IReadOnlyList<Violation> RunAll(ValidationContext context);
      [MenuItem("Bowling/Validate Sources of Truth")] public static void RunFromMenu();
      public static void RunBatch(); }   // prints each violation via Debug.Log, then EditorApplication.Exit(any non-warning ? 1 : 0)
  ```
- **Produces `tools/checks/module-rules.json`** with this exact shape and the values from spec section 4:
  ```json
  { "modules": { "<Module>": { "runtime": true, "editor": false, "editMode": true, "playMode": false, "testOnly": false } },
    "assemblies": { "<AssemblyName>": { "references": ["..."], "precompiled": ["..."], "allowUnsafeCode": false } },
    "testAssemblies": { "references": ["$module", "$moduleAllowed", "Bowling.Contracts", "Bowling.Testing", "UnityEngine.TestRunner", "UnityEditor.TestRunner", "Unity.PerformanceTesting"],
                        "precompiled": ["nunit.framework.dll", "Newtonsoft.Json.dll"] } }
  ```
  - `$module` means the module's runtime assembly, or its editor assembly when it has no runtime.
  - `$moduleAllowed` expands to everything `$module` may reference.
  - `testOnly: true` (Testing only) requires `defineConstraints: ["UNITY_INCLUDE_TESTS"]` on its runtime asmdef.
  - `$allRuntime` (allowed in `assemblies.*.references`) expands to every `Bowling.<Module>` runtime assembly.
  - `Bowling.App.Tests.*` uses `testAssemblies` plus `$allRuntime`.

- [ ] **Step 1: Write the failing tests.** Each test builds a `ValidationContext` with `ModulesRoot` pointing at a fixture folder and `Rules` loaded from a fixture `module-rules.json`.
  - **M-1:**
    - Pass: a complete module.
    - Fail on each of: a missing `README.md`; a README whose headings differ from the 4-heading template; a missing expected asmdef; a wrong `rootNamespace`; `autoReferenced: true`; `overrideReferences: false`; an EditMode asmdef without `includePlatforms: ["Editor"]`; the Testing runtime asmdef without `UNITY_INCLUDE_TESTS`; a module folder absent from `modules`.
    - Each fail case asserts exactly one `M-1` violation naming the file.
  - **M-2:**
    - Fail: `Bowling.Gameplay` references `Bowling.BowlingPhysics`; `Bowling.UI` lists `Newtonsoft.Json.dll`; `allowUnsafeCode: true` outside PoseInput.
    - Pass: a test asmdef referencing `$module` and the TestRunners.
    - **GUID resolution:** a `"GUID:<32 hex>"` reference resolves through `AssetDatabase.GUIDToAssetPath`, falling back to a violation `unresolvable GUID reference` if it can't. Test it with a fixture GUID that doesn't exist.
- [ ] **Step 2:** Run the Tooling EditMode tests. Expected: `FAIL` (the types are missing, so compile errors show in the log; `check_test_results.sh` reports `no results file`).
- [ ] **Step 3: Implement** the framework, the two rules and `module-rules.json` for the real project, covering all 11 modules and every assembly in spec section 4.
- [ ] **Step 4:** Run the Tooling EditMode tests. Expected: `PASS`. Then run `tools/ci/unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch`. Expected: exit 1, with M-1 violations for the 10 modules that don't exist yet. That is the red for Task 9.
- [ ] **Step 5:** Commit: `git add tools/checks/module-rules.json unity/MotionBowling/Assets/_Project/Modules/Tooling && git commit -m "feat(validation): editor validator framework, M-1, M-2"`.

### Task 9: Module skeleton

**Files:**
- Create, for each of Contracts, PoseInput, ThrowDetection, BowlingPhysics, Gameplay, Presentation, UI, Platform, App and Testing:
  - `README.md`, from the 4-heading template, with Purpose and Public API taken from CLAUDE.md's module map and the module's spec.
  - Every asmdef its row in spec section 4 requires (App adds `Editor/Bowling.App.Editor.asmdef`; Testing adds `Tests/EditMode/`), with the field values from the asmdef-kinds table.
- Create: `Runtime/AssemblyInfo.cs` in each runtime assembly. It contains only `[assembly: InternalsVisibleTo(...)]` for that module's test assemblies and `Bowling.App.Tests.EditMode`/`PlayMode`. This also keeps no runtime assembly empty.
- Modify: `Modules/Tooling/README.md` (Folder map now final)

**Interfaces:**
- **Produces:**
  - Assembly names exactly as in spec section 4, which every later task references.
  - `Bowling.PoseInput` has `allowUnsafeCode: true`, and it alone does.
  - `Bowling.App` references every runtime module.

- [ ] **Step 1: The test already exists.** It is the real-project `RunBatch` from Task 8 Step 4, currently failing.
- [ ] **Step 2:** Create the folders, READMEs, asmdefs and `AssemblyInfo.cs` files.
- [ ] **Step 3: Verify.**
  1. `tools/ci/unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch`. Expected: exit 0.
  2. `grep "warning CS" unity/MotionBowling/Logs/ci/run.log | grep -c "Assets/_Project"`. Expected: `0`.
  3. `tools/checks/check_sources_of_truth.sh --all`. Expected: exit 0.
- [ ] **Step 4:** Commit: `git add unity/MotionBowling/Assets/_Project/Modules && git commit -m "feat: module skeleton for all 11 modules"`.

### Task 10: Editor rules G-4, G-5, M-7

**Files:**
- Create: `Modules/Tooling/Editor/Validation/Rules/VolumeProfileRule.cs`, `SceneAndPrefabRule.cs`, `MainSceneRootRule.cs`
- Modify: `SourceOfTruthValidator.CreateRules()`
- Test: `Modules/Tooling/Tests/EditMode/VolumeProfileRuleTests.cs`, `SceneAndPrefabRuleTests.cs`, `MainSceneRootRuleTests.cs`

**Interfaces:**
- **Consumes:** `IValidationRule`, `ValidationContext`, `Violation` (Task 8).
- **Produces:**
  - `VolumeProfileRule` (Id `G-4`) scans `t:VolumeProfile` under `Assets/`, excluding `Assets/Plugins/FMOD/`.
  - `SceneAndPrefabRule` (Id `G-5`) scans enabled build scenes plus `t:Prefab` under `Assets/_Project`.
  - `MainSceneRootRule` (Id `M-7`) checks `context.MainScenePath`.
  - **Test hook:** `ValidationContext` gains `public string[] ScenePaths` (null means use build settings) and `public string AssetSearchRoot = "Assets"`.

- [ ] **Step 1: Write the failing tests.** Each creates assets under `Assets/_TestTemp/<TestName>/` in `SetUp`, calls `AssetDatabase.DeleteAsset("Assets/_TestTemp")` in `TearDown`, and points the context at them.
  - **G-4:**
    - Fail: a profile with an active `Vignette`, `intensity` overridden to 0.25.
    - Pass: an active `Vignette` at intensity 0.
    - Fail: an active `Bloom` at 0.1.
    - Pass: a profile with only `Tonemapping`.
  - **G-5:**
    - Fail: a temp scene with `ambientMode = Trilight`; a temp scene with `fog = true`; a temp scene with a skybox material set; a prefab whose camera uses `clearFlags = Skybox`; a prefab `LineRenderer` with 2 color keys.
    - Pass: a 1-key `LineRenderer`, and the real `Main.unity`.
  - **M-7:**
    - Fail: a temp scene with a loose root `Cube`.
    - Pass: a temp scene with roots `Bootstrap` and a prefab instance.
  - Each fail case expects exactly one violation with the rule id and the asset path.
- [ ] **Step 2:** Run the Tooling EditMode tests. Expected: `FAIL`.
- [ ] **Step 3: Implement the three rules.**
  - **Scenes:** open them additively with `EditorSceneManager.OpenScene(path, OpenSceneMode.Additive)`, set each active before reading `RenderSettings`, and close any scene the rule opened.
  - **Prefabs:** use `PrefabUtility.LoadPrefabContents`/`UnloadPrefabContents`.
- [ ] **Step 4:** Run the Tooling EditMode tests, then `RunBatch`. Expected: `PASS`, and exit 0.
- [ ] **Step 5:** Commit: `git add unity/MotionBowling/Assets/_Project/Modules/Tooling && git commit -m "feat(validation): G-4 volume, G-5 scene/prefab, M-7 scene roots"`.

### Task 11: Editor rule G-6, texture gradient heuristic

**Files:**
- Create: `Modules/Tooling/Editor/Validation/Rules/GradientHeuristic.cs`, `TextureGradientRule.cs`
- Create: `Modules/Tooling/Editor/Config/GradientHeuristicSettings.cs`, `Modules/Tooling/Editor/Config/GradientHeuristic.asset`
- Test: `Modules/Tooling/Tests/EditMode/GradientHeuristicTests.cs`, `TextureGradientRuleTests.cs`

**Interfaces:**
- **Produces:**
  ```csharp
  public static class GradientHeuristic { public static float SmoothStepFraction(Color32[] pixels, int width, int height, int maxStep); }
  // fraction of fully opaque pixels (a == 255) having at least one 4-neighbour with 0 < |Δ| ≤ maxStep on any of r, g, b
  public sealed class GradientHeuristicSettings : ScriptableObject { public int MaxStep = 3; public float MaxFraction = 0.02f; }
  ```
  `TextureGradientRule` (Id `G-6`):
  - **Scans** `Assets/_Project/Art/**` and `Assets/_Project/Modules/UI/**`.
  - **Skips** `TextureImporterType.NormalMap`, `!sRGBTexture` and the lightmap importer types.
  - **Non-PNG files** produce the violation `convert to PNG`.
  - **PNGs** are decoded with `ImageConversion.LoadImage` from `File.ReadAllBytes`.
  - **Message format:** `G-6 <path> <x.x>% smooth-ramp pixels (max <y.y>%)`.
  - **Settings** are loaded from `GradientHeuristic.asset`, or the defaults are used if the asset is missing.
  - `ValidationContext` gains `public string[] TextureSearchRoots` (null means the two defaults above).

- [ ] **Step 1: Write the failing tests.**
  - `GradientHeuristicTests` (pure, with arrays built in code):
    - A 64×64 single color gives 0.
    - A 256×4 horizontal ramp of r = x gives more than 0.9.
    - 64×64 two-color vertical stripes, 8 px wide, give 0.
    - Two flat regions with a one-pixel blend column of Δ = 60 give 0.
    - Pixels with a < 255 are ignored.
  - `TextureGradientRuleTests`: the test writes PNGs via `EncodeToPNG` into `Assets/_TestTemp/Art/`, then imports them.
    - The ramp gives one `G-6`.
    - Flat stripes give none.
    - The ramp imported as a NormalMap gives none.
    - A `.tga` gives `convert to PNG`.
- [ ] **Step 2:** Run the Tooling EditMode tests. Expected: `FAIL`.
- [ ] **Step 3: Implement** the heuristic, the settings type, the asset and the rule. Register the rule in `CreateRules()`.
- [ ] **Step 4:** Run the Tooling EditMode tests, then `RunBatch`. Expected: `PASS`, and exit 0.
- [ ] **Step 5:** Commit: `git add unity/MotionBowling/Assets/_Project/Modules/Tooling && git commit -m "feat(validation): G-6 texture gradient heuristic"`.

### Task 12: Contracts — Pose, Input, Lane, Roll

**Files** (all under `Modules/Contracts/Runtime/`, one type per file; the names, members and comments come **exactly** from system design sections 6.1, 6.2, 6.4 and 6.7):
- `Pose/`: `PoseJoint`, `Landmark`, `PersonPose`, `PoseSample`, `PlayerPose`
- `Input/`: `TrackingStatus`, `ITrackingSource`, `Handedness`, `PlayerProfile`, `IPlayerInput`, `CalibrationStep`, `CalibrationFailure`, `IPlayerCalibrator`, `IHandCursor`, `ICameraPreview`, `CameraInfo`, `TrackingStats`, `ITrackingDiagnostics`, `IRecordingIndicator`
- `Lane/`: `LaneSpec`
- `Roll/`: `PinMask`, `PinMaskExtensions`, `RollEndReason`, `RollMilestone`, `RollResult`, `ImpactKind`, `ImpactEvent`, `IRollSimulator`, `IRollBodies`
- Test (under `Modules/Contracts/Tests/EditMode/`): `PinMaskExtensionsTests.cs`, `LaneSpecTests.cs`, `RollResultTests.cs`, `ReadonlyStructConstructorTests.cs`

**Interfaces:**
- **Produces:** every type above, with the signatures in the system design. Tasks 13–15 and all later sub-projects compile against them.
- `PinMaskExtensions.Count(this PinMask)` returns `int`.
- `LaneSpec.PinSpot(int pinNumber)` returns `Vector3` with y = 0.

- [ ] **Step 1: Write the failing tests.**
  - `PinMaskExtensionsTests`: `PinMask.All.Count() == 10`; each of `P1`…`P10` counts 1; `PinMask.None.Count() == 0`.
  - `LaneSpecTests`: ten `[TestCase(pin, x, z)]` rows from the table in spec section 5, each within `1e-4f`, plus `y == 0`; `PinSpot(0)` and `PinSpot(11)` throw `ArgumentOutOfRangeException`.
  - `RollResultTests`: `All→None` gives 10; `All→P7|P10` gives 8; `P7|P10→P10` gives 1.
  - `ReadonlyStructConstructorTests.EveryReadonlyStructHasFullConstructor`: for every public value type in `typeof(LaneSpec).Assembly` that has `IsReadOnlyAttribute` and isn't an enum:
    - There is exactly one public instance constructor.
    - Its parameter count equals the instance field count.
    - Parameter *i* has the type of field *i* (in `GetFields()` order) and its name equals the field name with a lowercase first letter.
- [ ] **Step 2:** Run `tools/ci/run_unity_tests.sh EditMode Bowling.Contracts.Tests.EditMode`. Expected: `FAIL` (no results, because of compile errors).
- [ ] **Step 3: Implement the types.**
  - `PinSpot`: compute row `r` and in-row index `i` from the pin number (1 | 2–3 | 4–6 | 7–10), then x = `(i − r/2f) × PinSpacingM` and z = `FoulLineToHeadPinM + r × PinRowSpacingM`.
  - `Count`: a popcount over the low 10 bits.
- [ ] **Step 4:** Run the Contracts tests, then `tools/checks/check_sources_of_truth.sh --all`. Expected: `PASS`, and exit 0 (M-3 confirms one type per file).
- [ ] **Step 5:** Commit: `git add unity/MotionBowling/Assets/_Project/Modules/Contracts && git commit -m "feat(contracts): pose, input, lane and roll types"`.

### Task 13: Contracts — Throw, Game, Platform

**Files** (under `Modules/Contracts/Runtime/`, exact from system design sections 6.3, 6.5 and 6.6):
- `Throw/`: `Difficulty`, `DifficultyProfile`, `ThrowContext`, `SwingPhase`, `ThrowRejection`, `ThrowEvent`, `IThrowSource`
- `Game/`: `GameState`, `MarkKind`, `RollMark`, `FrameView`, `PlayerSnapshot`, `GameSnapshot`, `RollOutcome`, `IGameEvents`, `IGameCommands`, `UiCue`, `IUiCues`
- `Platform/`: `IClock`, `IGameLog`, `LeaderboardEntry`, `LeaderboardScope`, `ILeaderboardStore`, `OperatorSettings`, `ISettingsStore`, `IOperatorActions`
- Test: `Modules/Contracts/Tests/EditMode/OperatorSettingsDefaultsTests.cs`

**Interfaces:**
- **Produces:** every type above, with the signatures in the system design.
- The `OperatorSettings` defaults are exact: `SchemaVersion = 1`, `CameraUniqueId = null`, `DefaultDifficulty = Casual`, `MasterVolume = 0.8f`, `PoseRecordingEnabled = false`.

- [ ] **Step 1: Write the failing test** `OperatorSettingsDefaultsTests.DefaultsMatchSpec`, asserting the five defaults above. `ReadonlyStructConstructorTests` from Task 12 automatically covers the new structs (`DifficultyProfile`, `ThrowContext`, `ThrowEvent`, `RollMark`, `RollOutcome`).
- [ ] **Step 2:** Run `tools/ci/run_unity_tests.sh EditMode Bowling.Contracts.Tests.EditMode`. Expected: `FAIL`.
- [ ] **Step 3:** Implement the types.
- [ ] **Step 4:** Run the Contracts tests, `check_sources_of_truth.sh --all` and `RunBatch`. Expected: `PASS`, exit 0 and exit 0.
- [ ] **Step 5:** Commit: `git add unity/MotionBowling/Assets/_Project/Modules/Contracts && git commit -m "feat(contracts): throw, game and platform types"`.

### Task 14: Testing fakes

**Files:**
- Create under `Modules/Testing/Runtime/`, one file each: `FakeClock`, `FakeTrackingSource`, `FakePlayerInput`, `FakeCalibrator`, `FakeThrowSource`, `FakeRollSimulator`, `RecordingGameLog`, `InMemoryLeaderboardStore`, `InMemorySettingsStore`, `FakeGameEvents`, `FakeGameCommands`, `FakeHandCursor`, `FakeCameraPreview`, `FakeTrackingDiagnostics`, `FakeRecordingIndicator`, `FakeOperatorActions`, `FakeUiCues`, `TestDataPaths`
- Create: `test-data/pose-recordings/.gitkeep`
- Test: `Modules/Testing/Tests/EditMode/FakeSemanticsTests.cs` (plus one more file if it passes 300 lines, split by area)

**Interfaces:**
- **Produces** the fakes with the controls from spec section 6. The semantics below are pinned by the tests and mirror the contracts:
  - **`FakeClock`:** `Now` starts at 0. `Advance(double s)` adds `s`; a negative `s` throws `ArgumentOutOfRangeException`.
  - **`FakeTrackingSource` and `FakePlayerInput`:** `ReadSamplesSince`/`ReadPosesSince` append only items with `Timestamp > t`, oldest first, and return the count.
    - `FakeTrackingSource.Start()`/`Stop()` set `Status` to `Running`/`Stopped` and count calls in `StartCount`/`StopCount`.
    - `FakePlayerInput.SetPresent`/`SetStatus` raise their events only when the value changes.
  - **`FakeCalibrator`:**
    - `Begin()` sets `WaitingForFullBody` and increments `BeginCount`.
    - `Complete(p)` sets `Done`, then raises `StepChanged(Done)` and then `Completed(p)`.
    - `Fail(r)` sets `Failed`, then raises `StepChanged(Failed)` and then `Failed(r)`.
    - `Cancel()` sets `Idle`.
  - **`FakeThrowSource`:**
    - `Arm(ctx)` sets `WaitingForReady`, records `LastContext` and increments `ArmCount`.
    - `Disarm()` sets `Disarmed` and increments `DisarmCount`.
    - `SetPhase(p)` raises `PhaseChanged`.
    - `EmitThrow(e)` throws `InvalidOperationException` when disarmed; otherwise it raises `Thrown(e)`, then sets `Disarmed`.
    - `Reject(r)` raises `Rejected(r)`, then sets `WaitingForReady`.
  - **`FakeRollSimulator`:**
    - `Standing` starts at `All`.
    - `SetRack`/`SetBumpers` record their arguments; `SetRack` throws while rolling.
    - `Roll(e)` throws if already rolling; otherwise it records `LastThrow`, sets `IsRolling`, and raises `Milestone(Released)`.
    - `CompleteWith(after, gutter = false, reason = Settled)` throws unless rolling; otherwise it raises `Milestone(Settled)`, then `Completed(new RollResult(Standing, after, gutter, reason))`, then sets `Standing = after` and `IsRolling = false`.
  - **`InMemoryLeaderboardStore(Func<DateTime> utcNow, TimeZoneInfo zone)`:** `Top` orders by `Score` descending, then `PlayedAtUtc` ascending. `Today` compares local dates in `zone`.
  - **`InMemorySettingsStore`:** `Load()` returns a copy of what was saved, or `new OperatorSettings()`. `Save` stores a copy.
  - **`FakeGameEvents`:** `SetState(s)` publishes a new `GameSnapshot` carrying the previous snapshot's fields with `State = s`, raising `StateChanged(s)` and then `SnapshotChanged`.
  - **`FakeOperatorActions`:** `ApplySettings(s)` stores `s` and raises `SettingsApplied(s)`.
  - **`TestDataPaths.PoseRecordings`:** the absolute `<repo>/test-data/pose-recordings`, resolved as `Application.dataPath/../../../test-data/pose-recordings`.

- [ ] **Step 1: Write the failing tests** in `FakeSemanticsTests`, one test per bullet above, asserting the exact event order with a recorded `List<string>` of event names.
  - `TestDataPaths.PoseRecordings` ends with `test-data/pose-recordings`.
  - `File.Exists(Path.Combine(<repo>, "CLAUDE.md"))` holds.
- [ ] **Step 2:** Run `tools/ci/run_unity_tests.sh EditMode Bowling.Testing.Tests.EditMode`. Expected: `FAIL`.
- [ ] **Step 3:** Implement the fakes. Create `test-data/pose-recordings/.gitkeep`.
- [ ] **Step 4:** Run the Testing tests, `check_sources_of_truth.sh --all` and `RunBatch`. Expected: `PASS`, exit 0 and exit 0.
- [ ] **Step 5:** Commit: `git add test-data unity/MotionBowling/Assets/_Project/Modules/Testing && git commit -m "feat(testing): contract fakes with pinned semantics"`.

### Task 15: App skeleton and Bootstrap

**Files:**
- Create: `Modules/App/Runtime/Core/ExecutionOrder.cs`, `UnityClock.cs`, `UnityConsoleGameLog.cs`; `Modules/App/Runtime/Components/GameBootstrap.cs`; `Modules/App/Editor/MainSceneSetup.cs`
- Modify: `Main.unity` (through `MainSceneSetup`); `RenderingBaselineTests` (root assertion becomes the two roots)
- Test: `Modules/App/Tests/EditMode/UnityConsoleGameLogTests.cs`; `Modules/App/Tests/PlayMode/BootstrapSceneTests.cs`, `ExecutionOrderTests.cs`

**Interfaces:**
- **Produces:**
  ```csharp
  public static class ExecutionOrder { public const int Bootstrap = -1000, PoseInput = -300, ThrowDetection = -200, Gameplay = -100, Presentation = 0, UI = 100; }
  public sealed class UnityClock : IClock { public double Now => /* Time.unscaledTimeAsDouble */; }
  public sealed class UnityConsoleGameLog : IGameLog { } // Info→Debug.Log("[<category>] <message>"), Warn→Debug.LogWarning(same), Error→Debug.LogError(same) then Debug.LogException(ex) when ex != null
  [DefaultExecutionOrder(-1000)] public sealed class GameBootstrap : MonoBehaviour { public IClock Clock { get; } public IGameLog Log { get; } } // Awake: construct both, Log.Info("App", "Boot")
  public static class MainSceneSetup { public static void EnsureBootstrapBatch(); } // adds root "Bootstrap" with GameBootstrap to Main.unity if absent, saves, Exit(0)
  ```

- [ ] **Step 1: Write the failing tests.**
  - `UnityConsoleGameLogTests`: `LogAssert.Expect(LogType.Log, "[Cat] msg")` for `Info`; `LogType.Warning` for `Warn`; for `Error` with an exception, both the `LogType.Error` message and `LogType.Exception`.
  - `BootstrapSceneTests` (`[UnityTest]`): `SceneManager.LoadScene("Main")`, wait one frame, then:
    - Exactly one `GameBootstrap` (`Object.FindObjectsByType` is allowed in tests).
    - `LogAssert.Expect(LogType.Log, "[App] Boot")`.
    - `bootstrap.Clock.Now >= 0`.
  - `ExecutionOrderTests`:
    - `GameBootstrap`'s `DefaultExecutionOrder` equals `ExecutionOrder.Bootstrap`.
    - For every type named `*Driver` in a loaded `Bowling.<Module>` assembly, the attribute's order equals the `ExecutionOrder` constant named `<Module>`. There are none yet, and the loop must still run.
  - Update `RenderingBaselineTests`: the `Main.unity` roots are exactly `Bootstrap` plus the `Rendering` prefab instance.
- [ ] **Step 2:** Run `tools/ci/run_unity_tests.sh EditMode` and `tools/ci/run_unity_tests.sh PlayMode`. Expected: `FAIL`.
- [ ] **Step 3:** Implement the types, then run `tools/ci/unity.sh exec Bowling.App.Editor.MainSceneSetup.EnsureBootstrapBatch`.
- [ ] **Step 4:** Run both test platforms, `check_sources_of_truth.sh --all` and `RunBatch`. Expected: `PASS`, `PASS`, exit 0 and exit 0.
- [ ] **Step 5:** Commit: `git add unity/MotionBowling && git commit -m "feat(app): composition root skeleton and Bootstrap"`.

### Task 16: verify.sh, required files, CI and acceptance

**Files:**
- Create: `tools/ci/verify.sh`, `tools/checks/required-files.txt` (empty, plus a header comment), `.github/workflows/verify.yml`
- Modify: `README.md` (add a "Verify" section: `bash tools/ci/verify.sh`), `docs/dev-setup.md` (CI runner notes)

**Interfaces:**
- **Consumes:** every script from Tasks 1–8.
- **Produces `tools/ci/verify.sh`:** it stops at the first failure, prints `== step N: <name> ==` before each step, and finally prints `VERIFY OK`. The steps:
  1. `check_sources_of_truth.sh --all`.
  2. The shell suites: `tools/checks/test_checks.sh`, `tools/ci/test_ci_scripts.sh`, `tools/dev/test_check_env.sh`.
  3. `unity.sh exec …SourceOfTruthValidator.RunBatch`, with `UNITY_LOG_NAME=validate`, plus a check that `Logs/ci/validate.log` has no `warning CS` line mentioning `Assets/_Project`.
  4. `run_unity_tests.sh EditMode`.
  5. `run_unity_tests.sh PlayMode`.
  6. Every non-comment line of `required-files.txt` exists relative to the repo root.
- **Produces `verify.yml`:**
  - `on: [push, pull_request]`; `runs-on: [self-hosted, macOS, ARM64]`.
  - Steps: `actions/checkout` with `lfs: true`, then `bash tools/ci/verify.sh`, then upload `unity/MotionBowling/Logs/ci/` as an artifact, even on failure.

- [ ] **Step 1: Write the failing check.** Run `bash tools/ci/verify.sh`. Expected: `No such file`.
- [ ] **Step 2:** Implement `verify.sh`, `required-files.txt` and `verify.yml`. Update the README and `dev-setup.md`.
- [ ] **Step 3:** Run `bash tools/ci/verify.sh` on a clean checkout: `git worktree add "$(mktemp -d)/mb-verify" HEAD`, then run it from that worktree. The first run there includes a full Unity import, which is slow. Expected: steps 1–6 pass, then `VERIFY OK`, exit 0 (acceptance 5). Record how long it takes in the execution notes, then `git worktree remove` it.
- [ ] **Step 4: Manual acceptance 6.**
  1. Create `unity/MotionBowling/Assets/_Project/Modules/UI/Runtime/Styles/Bad.uss` containing `.x { background-image: linear-gradient(red, blue); }`.
  2. `git add` it, then `git commit -m test`. Expected: the commit is rejected, and the output contains `G-1 Assets/_Project/Modules/UI/Runtime/Styles/Bad.uss`.
  3. Delete the file and unstage it.
- [ ] **Step 5: Manual acceptance 7.**
  1. Temporarily add `"Bowling.BowlingPhysics"` to `Bowling.Gameplay.asmdef` `references`.
  2. Run `tools/ci/unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch`. Expected: exit 1, with an `M-2` line naming `Bowling.Gameplay.asmdef`.
  3. Revert the change.
- [ ] **Step 6:** Confirm acceptance 1–4 from the step 3 output, and record the results in Execution notes.
- [ ] **Step 7:** Commit: `git add tools .github README.md docs && git commit -m "ci: verify.sh single entry point and GitHub Actions workflow"`.
  - **Not verifiable locally:** the workflow needs a registered self-hosted runner with an activated Unity license. Say so in the hand-off.

---

## Execution notes

(Filled in by the executor: the Unity version used, the resolved package versions (Task 3), the serialized PlayerSettings keys (Task 2), the `verify.sh` duration, and any deviation from this plan with its reason.)

### Task 3: Package set

Unity 6000.3.26f1. Resolved by `PackageInstaller.InstallBatch` (`AddAndRemove` with bare package names, so Unity picked the version it verifies for this editor). Exact versions now in `Packages/manifest.json`:

| Package | Version |
|---|---|
| `com.unity.render-pipelines.universal` | 17.3.0 |
| `com.unity.inputsystem` | 1.20.1 |
| `com.unity.cinemachine` | 3.1.7 |
| `com.unity.test-framework` | 1.6.0 |
| `com.unity.test-framework.performance` | 3.6.0 |
| `com.unity.nuget.newtonsoft-json` | 3.2.2 |

- Removed: `com.unity.multiplayer.center` (the only `Removed` package the fresh manifest had). `com.unity.ugui` stays in `packages-lock.json` as a transitive dependency, not in `manifest.json`. The 34 `com.unity.modules.*` entries are unchanged.
- Deviation, order: the fresh manifest had no `com.unity.test-framework`, so the installer ran before the tests (the brief's Step 1 branch).
- Deviation, `PackageManifestRules` takes the manifest's parsed `dependencies` (a dictionary), not JSON text. The spec gives `Bowling.Tooling.Editor` no precompiled references, and `Newtonsoft.Json.dll` doesn't exist before the install, so the tests (which have Newtonsoft) parse the JSON.
- Side effect, committed in `325a162` (ruling R10): installing URP made Unity create `Assets/UniversalRenderPipelineGlobalSettings.asset` and `Assets/DefaultVolumeProfile.asset`, edit `ProjectSettings/GraphicsSettings.asset` and `InputManager.asset`, and add `ProjectSettings/ShaderGraphSettings.asset`. Task 5 relocates them (spec 3.4: `Assets/_Project/Settings/`).
- Wording, "installed": in `PackageInstaller` (its `Required` and `Removed` lists and `Verify()`), a package counts as installed when it is a direct dependency in `Packages/manifest.json`. A package that is only a transitive dependency (such as `com.unity.ugui`) does not count.

### Execution rulings that supersede plan text

Where a task's text below disagrees with one of these rulings, the ruling wins.

- **R18:** `ThrowEvent` is created in Task 12, not Task 13.
- **R19:** the fakes live in `Modules/Testing/Runtime/Core/`, one public type per file; their tests are split by area under `Modules/Testing/Tests/EditMode/`.
- **R21:** a source updates its own observable state before raising events, and raises change events on every actual change. `CompleteWith`, `EmitThrow`, `Reject` and the calibrator's `Begin`/`Cancel` follow this, which supersedes the event orders in the Task 14 bullets.
- **R21b:** `Disarm` raises `PhaseChanged` when the phase changes.
- **R21c:** `SetBumpers` throws while rolling, `Reject` throws when disarmed, and `RaiseMilestone` accepts only `EnteredGutter`, `ReachedPins` and `EnteredPit`, and only while rolling.
- **R22:** gameplay event order: every transition assigns `Current` first, then raises `StateChanged`, then `SnapshotChanged`; for a scored roll the order is `StateChanged(Scoring)`, `RollScored`, `SnapshotChanged` (gameplay spec sections 4 and 6).
- **R23:** Task 16 Step 3 runs on a `git archive HEAD` copy, not on `git worktree add`: `D="$(mktemp -d)/mb verify"; mkdir -p "$D"; git archive HEAD | tar -x -C "$D"`, then `bash tools/ci/verify.sh` from inside `$D` (the path has a space on purpose). The archive has no `.git`, so `verify.sh` must not depend on the repo's git. `verify.yml` sets `timeout-minutes: 90`, `runs-on: [self-hosted, macOS, ARM64]`, checks out with `lfs: true`, and uploads `unity/MotionBowling/Logs/ci/` with `if: always()`.
- **R24:** the step 3 `warning CS` gate must not pass vacuously on a warm `Library/`. Before the Unity call, `step_validate` deletes `unity/MotionBowling/Library/ScriptAssemblies` and `unity/MotionBowling/Library/Bee` (quoted, only those), which forces a full script recompile on every run. Also: `verify.yml` gets `permissions: contents: read`; `docs/dev-setup.md` warns that the self-hosted runner holds a Unity license and runs repository code, so it is for private repositories (or fork pull requests need maintainer approval); the README status line is brought up to date.

### Task 16: verify.sh, required files, CI and acceptance

Commit `ci: verify.sh single entry point and GitHub Actions workflow`. `tools/ci/verify.sh` runs the six steps of the spec (section 9) in order, prints `== step N: <name> ==` first, stops at the first failure with that step's exit code (and `VERIFY FAILED at step N: ...` on stderr), and ends with `VERIFY OK`. Unity 6000.3.26f1.

**Duration of `verify.sh`**

| Run | Library | Time |
|---|---|---|
| This worktree (twice, once before and once after the manual checks) | warm | 65 s and 66 s |
| Clean checkout: `git archive HEAD` into `<tmp>/mb verify` (R23) | none (cold import and full compile) | 94 s |

The brief expected a slow first import (it bounded the run at about 40 minutes); on this Mac (Apple Silicon, 16 GB) the whole run from a fresh copy, import included, took 94 s. A slower machine, or one that has to download the packages, will take longer; the workflow's job limit is 90 minutes. The clean copy was deleted afterwards.

**Acceptance criteria (spec section 11)**

1. Compiles with zero errors and zero `warning CS` in `_Project`: in the clean-checkout run, `validate.log` (the first editor run in that fresh project, so the log of the full compile; 907 KB against 58 KB for a warm run) has 0 `warning CS` and 0 `error CS` lines, and `Library/ScriptAssemblies/` held every `Bowling.*.dll`, compiled in that run. The log has ten `Assembly for Assembly Definition File ... will not be compiled, because it has no scripts` notes, which are expected (`docs/dev-setup.md`) and are not compiler warnings. To show the gate catches real Unity output, a throw-away `int unused;` in `Bowling.Contracts` (in the disposable copy only) produced `Assets/_Project/Modules/Contracts/Runtime/ZzWarnProbe.cs(7,17): warning CS0168: ...` in `validate.log`, and `check_no_project_warnings` exited 1 on it.
2. All 11 module folders exist (App, BowlingPhysics, Contracts, Gameplay, Platform, PoseInput, Presentation, Testing, ThrowDetection, Tooling, UI), each with `README.md` and its asmdefs. M-1 and M-2 pass: `Source-of-truth validation: 0 violation(s), 0 warning(s).` from `RunBatch` in the clean checkout.
3. Contracts tests pass: `Bowling.Contracts.Tests.EditMode`, 29 of 29. (The whole EditMode run is 377 of 377: App 4, Contracts 29, Testing 132, Tooling 212. PlayMode is 4 of 4.)
4. Validator tests pass: `Bowling.Tooling.Tests.EditMode`, 212 of 212 (the editor rules and the validator framework), and `tools/checks/test_checks.sh`, 553 assertions (the text rules, with a passing and a failing case per rule).
5. Clean checkout, `bash tools/ci/verify.sh` from inside `<tmp>/mb verify`: steps 1 to 6, then `VERIFY OK`, exit 0.
6. Manual: with `Assets/_Project/Modules/UI/Runtime/Styles/Bad.uss` (`.x { background-image: linear-gradient(red, blue); }`) staged, `git commit -m test` was rejected, exit 1, with `G-1 Assets/_Project/Modules/UI/Runtime/Styles/Bad.uss:1 banned gradient function: 'linear-gradient('`. The file was unstaged and deleted, and the empty `Styles/` folder (which this step created) removed.
7. Manual: with `"Bowling.BowlingPhysics"` added to `Bowling.Gameplay.asmdef` `references`, `tools/ci/unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch` exited 1 with `M-2 Assets/_Project/Modules/Gameplay/Runtime/Bowling.Gameplay.asmdef:6 references 'Bowling.BowlingPhysics', which module-rules.json does not allow Bowling.Gameplay to reference`. The asmdef was restored byte for byte (same blob hash), and `git status` was clean afterwards.

**Not verified locally:** `.github/workflows/verify.yml` has not run. It needs a registered self-hosted Apple Silicon runner with an activated Unity license, and there is none here. It was only checked to be valid YAML with the intended `runs-on`, `timeout-minutes`, `lfs: true`, `bash tools/ci/verify.sh` and `if: always()` upload. `docs/dev-setup.md` ("Verify and CI") lists what the runner needs.

**Deviations and notes**

- Step 2 of `verify.sh` also runs a fourth suite, `tools/ci/test_verify.sh`, besides the three of the plan. It tests `verify.sh` against stub steps in a copied tree (step order, stopping at the first failure, exit codes, the warning check, required files), needs no Unity, and has no hook or switch in `verify.sh`. A mutation check (removing `UNITY_LOG_NAME=validate`, the stale-log delete, or the `Assets/_Project` filter) made it fail.
- The clean-checkout run needs the new files in `HEAD`, so the commit was made before it and amended afterwards with these notes only.
- Superseded by R24 (fix round): the first version relied on a cold `Library/` for the step 3 warning check. Unity logs compiler output only for the assemblies it recompiles, so on a warm `Library/` the check passed vacuously (shown below). Step 3 now deletes `Library/ScriptAssemblies` and `Library/Bee` first; see "R24 fix round" below.
- `verify.sh` takes no arguments (exit 2 if given any).

**R24 fix round (commit `fix(ci): verify forces a recompile so the warning gate always sees compiler output (R24)`)**

- Problem: on a warm `Library/` the step 3 gate passed vacuously. Shown with a probe: `int unused;` in a new `Bowling.Contracts` file, compiled once by an editor run (`warm1.log`: 2 `warning CS0168` lines), then a second run on the same `Library/` (`warm2.log`): 0 `warning CS` lines, although the probe was still in the code.
- Fix: `step_validate` deletes `unity/MotionBowling/Library/ScriptAssemblies` and `unity/MotionBowling/Library/Bee` before the Unity call. This deletion is enough: Unity then recompiles every script assembly and logs the compiler output, with no other part of `Library/` touched. Deleting `Library/Bee` does trigger the recompile that logs the compiler output, so no search for a different deletion was needed (a smaller one was not tried).
- Probe, warm `Library/`, the probe still in place: `bash tools/ci/verify.sh` run twice. Both runs ended `VERIFY FAILED at step 3 ... (exit 1)` with `Assets/_Project/Modules/Contracts/Runtime/ZzWarnProbe.cs(7,17): warning CS0168: The variable 'unused' is declared but never used`, in 48 s and 41 s (they stop at step 3). With the probe removed: `VERIFY OK`, exit 0.
- Timing with the fix, warm `Library/`, whole run: 68 s and 80 s (65 s and 66 s before the fix), so the forced recompile costs about 3 to 15 s. `validate.log` of a warm run is now about 250 KB (it was 58 KB), because it holds the recompile's output.
- `tools/ci/test_verify.sh` (now 59 assertions) tests that step 3 deletes exactly those two folders before Unity starts (the stub `unity.sh` lists `Library/`; three other folders must remain), that nothing is deleted when an earlier step fails, and that a project with no `Library/` still passes. Mutation-checked: no deletion, deleting only `ScriptAssemblies`, and deleting an extra folder each fail it.
- `.github/workflows/verify.yml` has `permissions: contents: read`. `docs/dev-setup.md` documents the forced recompile and the self-hosted runner's security note, and the missing blank line before "Expected Unity console notes" is fixed. The README status line now says the foundation is implemented on `worktree-foundation`.
