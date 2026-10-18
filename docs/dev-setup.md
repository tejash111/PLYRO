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

The pre-commit hook runs the source-of-truth validators on staged files. Install it once per clone:

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

## Expected Unity console notes

Unity logs `Assembly for Assembly Definition File '...' will not be compiled, because it has no scripts associated with it.` for every module assembly that has no code yet (at the moment, the test assemblies and `Bowling.App.Editor`). This is expected. These are Unity console notes, not compiler warnings, so they don't count against the zero `warning CS` gate. Each note goes away when its assembly gets its first script.

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
