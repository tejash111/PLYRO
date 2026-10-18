# Developer setup (macOS)

What a Mac needs to build, test and run Motion Bowling. Check your machine at any time:

```bash
bash tools/dev/check-env.sh
```

It prints `OK <what>` or `MISSING <what> — <fix>` for each item and exits 1 if anything is missing. Each `MISSING` line names the section below that fixes it.

`<ver>` in the commands below is the pinned Unity editor version: `6000.3.26f1` at the time of writing. Once the Unity project exists, `tools/ci/unity-version.txt` is the source of truth. The checker reads it, and the `UNITY_VERSION` environment variable overrides it.

## Xcode

Full Xcode is required. The Command Line Tools alone aren't enough: Unity's Mac IL2CPP builds and the native plugins (`xcodebuild`) need it.

1. Install **Xcode** from the Mac App Store.
2. Point the command line at it:

   ```bash
   sudo xcode-select -s /Applications/Xcode.app/Contents/Developer
   ```

3. Accept the license:

   ```bash
   sudo xcodebuild -license accept
   ```

4. Check: `xcode-select -p` prints `/Applications/Xcode.app/Contents/Developer`, and `xcodebuild -version` exits 0.

## Git LFS

Large binary assets (models, textures, audio, native bundles, pose recordings) are stored with Git LFS. The patterns are in `.gitattributes`.

```bash
brew install git-lfs
git lfs install --local
```

Run `git lfs install --local` inside this repo. It's a per-clone setting, so repeat it in every new clone or worktree you create.

## Unity Hub and editor

The game is built with Unity 6.3 LTS, with the **Mac Build Support (IL2CPP)** module installed.

1. Install **Unity Hub** to `/Applications/Unity Hub.app`: download it from unity.com/download, or run `brew install --cask unity-hub`.
2. Open the Hub, sign in to your Unity account and activate a license. A Personal license is fine.
3. Install editor `<ver>` with the Mac IL2CPP module, either in the Hub UI (Installs, Install Editor, then tick **Mac Build Support (IL2CPP)**), or headless:

   ```bash
   "/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install --version <ver> --module mac-il2cpp
   ```

Leave Unity Hub's install location at its default, `/Applications/Unity/Hub/Editor`. On Apple Silicon, Hub installs the editor into `/Applications/Unity/Hub/Editor/<ver>-arm64/Unity.app`, and sometimes into `/Applications/Unity/Hub/Editor/<ver>/Unity.app`. Both `tools/dev/check-env.sh` and `tools/ci/unity.sh` try `<ver>-arm64` first, then `<ver>`. A folder counts only if it has `Unity.app/Contents/Info.plist` and an executable `Unity.app/Contents/MacOS/Unity`.

If you must install elsewhere, point the tools at it: `UNITY_HUB_EDITORS_DIR` (the folder that holds the `<ver>` folders) for both, or `UNITY_EDITOR` (the full path of the editor binary) for the CI scripts.

**Broken install.** If `check-env.sh` prints `MISSING Unity <ver> — incomplete install at <path>`, or `unity.sh` fails with `install at <path> is incomplete`, that folder is only half installed. Typically `Unity.app/Contents/Info.plist` is missing and `Contents/Frameworks` is empty. macOS kills such an editor the moment it starts: exit 137, `Killed: 9`, and an empty log. `codesign --verify --strict "<path>/Unity.app"` reports an invalid signature. To repair it, uninstall the editor in Unity Hub (Installs tab) and install it again, or delete the folder and install again. A complete `<ver>-arm64` next to a broken `<ver>` is used automatically, but remove the broken folder anyway so it doesn't confuse Hub.

## UnityYAMLMerge

`.gitattributes` sends `*.unity`, `*.prefab`, `*.asset` and `*.mat` through a merge driver called `unityyamlmerge`. Register it in this repo's git config (run inside the repo):

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

The driver path contains the editor version, so run these again after the pinned version changes. If your editor is in the `<ver>-arm64` folder, use that folder name in the path. `tools/ci/unity.sh path` shows which one the tools use.

## Pre-commit hook

The pre-commit hook runs the source-of-truth text rules (`tools/checks/check_sources_of_truth.sh --staged`) on staged files only. It does not run the editor rules (M-1, M-2, M-7 and G-4 to G-6), which need Unity: they run in step 3 of `bash tools/ci/verify.sh`. Install the hook once per clone:

```bash
bash tools/checks/install-hooks.sh
```

## Unity command-line helpers

`tools/ci/unity.sh` finds the pinned editor and runs it headless against `unity/MotionBowling`. `tools/ci/run_unity_tests.sh` runs one test platform on top of it. Both find the repo from their own location, so any working directory works. All of these environment variables are optional:

| Variable | Meaning |
|---|---|
| `UNITY_EDITOR` | Full path of the editor binary. Wins over the lookup below, and must be an executable file. |
| `UNITY_HUB_EDITORS_DIR` | Where Unity Hub installs editors. Default `/Applications/Unity/Hub/Editor`. The editor is `<dir>/<ver>-arm64/Unity.app/Contents/MacOS/Unity`, or else `<dir>/<ver>/Unity.app/Contents/MacOS/Unity`. See "Unity Hub and editor" for what makes a folder valid. |
| `UNITY_PROJECT_DIR` | Absolute path of the Unity project. Default: `unity/MotionBowling` in this repo. |
| `UNITY_LOG_NAME` | Name of the editor log, written to `<project>/Logs/ci/<name>.log` (gitignored). Default `run` for `unity.sh`, and the platform name (`EditMode` or `PlayMode`) for `run_unity_tests.sh`. |

`<ver>` is the version in `ProjectSettings/ProjectVersion.txt` once the project exists, else `tools/ci/unity-version.txt`. If the two differ, the scripts exit 2 with an error. Run `tools/ci/unity.sh path` to see which editor would run.

If an editor is killed at once (exit 137, `Killed: 9`, empty log), its install is damaged. See "Broken install" under "Unity Hub and editor".

## Verify and CI

`bash tools/ci/verify.sh` is the one command that checks everything (see the README, "Verify"). It runs from any working directory and needs no environment variables of its own; its Unity steps use the settings above. Its logs and test results are in `unity/MotionBowling/Logs/ci/`: `validate.log`, `EditMode.log`, `PlayMode.log`, `EditMode-results.xml` and `PlayMode-results.xml`.

Step 3 judges the editor log, and Unity logs compiler output only for the assemblies it recompiles. A warning that was already compiled into a warm `Library/` (for example by an editor you had open) would never reach the log. So step 3 first deletes `unity/MotionBowling/Library/ScriptAssemblies` and `unity/MotionBowling/Library/Bee` (the compiled scripts and the build graph that made them), which forces a full script recompile on every run. The rest of `Library/`, the asset import cache, is kept, so the recompile adds only seconds to a warm run. Acceptance criterion 1 (no `warning CS` in `Assets/_Project`) is therefore checked on every run, in a fresh checkout or not.

### CI runner

`.github/workflows/verify.yml` runs `bash tools/ci/verify.sh` on every push to `main` and on every pull request (a branch with an open pull request runs once, not twice), on a runner labelled `self-hosted`, `macOS` and `ARM64` (GitHub adds these labels to a self-hosted Apple Silicon Mac when you register it). The job times out after 90 minutes, and a newer run for the same ref (a new push to a branch or pull request) cancels the one still going. Afterwards it uploads `unity/MotionBowling/Logs/ci/` as the artifact `unity-ci-logs`, also when verify failed.

Set up the runner machine once, as the user that runs the GitHub Actions runner service:

1. Everything in this document: Xcode, Git LFS (the workflow checks out with `lfs: true`, so `git lfs` must be installed), Unity Hub with the pinned editor and the Mac IL2CPP module. Run `bash tools/dev/check-env.sh` there to confirm; it should print no `MISSING` line.
2. Activate a Unity license for that user, outside the workflow: sign in to Unity Hub as the runner's user and activate the license. The workflow never handles license or account credentials.
3. Register the machine as a self-hosted runner in the GitHub repository settings (Actions, Runners).
4. Don't open the project in the Unity editor on the runner. Two editors can't open one project, and verify.sh would then fail its Unity steps.

**Security.** The runner holds a Unity license and runs the repository's code (`verify.sh` and the Unity editor scripts of the branch under test) on your Mac. Use it only for a private repository, or, if the repository is public, require maintainer approval before workflows run for pull requests from forks (repository settings, Actions, General, "Fork pull request workflows"). The workflow itself asks for read-only repository access (`permissions: contents: read`). Its actions (`actions/checkout`, `actions/upload-artifact`) are pinned by tag, not by commit SHA; pinning them to a SHA is a follow-up, worth doing before the runner serves anything but trusted branches.

`actions/checkout` removes untracked and ignored files before it fetches, and `Library/` is ignored, so every CI run imports the project from scratch. That is slow. Setting `clean: false` on the checkout step would keep `Library/` and speed runs up; the warning check still sees a full compile, because step 3 deletes the compiled scripts itself (see above).

## Expected Unity console notes

Unity logs `Assembly for Assembly Definition File '...' will not be compiled, because it has no scripts associated with it.` for every assembly that has no code yet. At the moment these are the module test assemblies that have no scripts yet, 10 of them (BowlingPhysics, Presentation and UI each have EditMode and PlayMode, and Gameplay, Platform, PoseInput and ThrowDetection have EditMode). `Bowling.App.Editor` now has a script, so it no longer logs one. These notes are expected. They are Unity console notes, not compiler warnings, so they don't count against the zero `warning CS` gate. Each note goes away when its assembly gets its first script.

## Serialized PlayerSettings keys

Some settings have no public `PlayerSettings` API, so the baseline code reads and writes them by their serialized key. These are the exact keys in `unity/MotionBowling/ProjectSettings/ProjectSettings.asset` as written by Unity 6000.3.26f1 into a fresh project. They are top-level fields of the `PlayerSettings` document (`--- !u!129 &1`), so `FindProperty("<key>")` takes the bare name.

| Key | Fresh value | Type | Meaning |
|---|---|---|---|
| `cameraUsageDescription` | empty | string | Text of the camera permission prompt. |
| `macOSTargetOSVersion` | empty | string | Minimum macOS version of the built player. Empty means Unity's default. |
| `activeInputHandler` | `0` | int | Active Input Handling: `0` Input Manager (old), `1` Input System (new) only, `2` both. |

Found with:

```bash
grep -nE "cameraUsageDescription|macOSTargetOSVersion|activeInputHandler" unity/MotionBowling/ProjectSettings/ProjectSettings.asset
```

All three exist under these exact names, so no substitutes were needed. The line numbers in that output (328, 332 and 682) belong to this Unity version and shift between versions, so look the keys up by name.

## Serialized QualitySettings keys

Unity has no public API to remove quality levels or to set a level's render pipeline asset for a level other than the current one, so `RenderingBaseline` edits `ProjectSettings/QualitySettings.asset` (`--- !u!47 &1`) through `new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0])`. The keys, as Unity 6000.3.26f1 writes them:

| Key | Type | Meaning |
|---|---|---|
| `m_QualitySettings` | array of level structs | One struct per quality level. The baseline keeps one, named `Booth`. |
| `m_QualitySettings.Array.data[i].name` | string | The level's name (`QualitySettings.names`). |
| `m_QualitySettings.Array.data[i].vSyncCount` | int | `0` off, `1` every V blank, `2` every second V blank. |
| `m_QualitySettings.Array.data[i].customRenderPipeline` | object reference | The URP asset this level renders with (`QualitySettings.renderPipeline`). |
| `m_CurrentQuality` | int | Index of the level used in the editor. |
| `m_PerPlatformDefaultQuality` | map of platform name to int | Index of the default level for each platform. Each entry's value is the `second` field. |

`GraphicsSettings.defaultRenderPipeline` is a public API and needs no key. The URP global settings asset is registered in `ProjectSettings/GraphicsSettings.asset` by GUID (`m_RenderPipelineGlobalSettingsMap`), which is why the baseline moves it with `AssetDatabase.MoveAsset` instead of recreating it.
