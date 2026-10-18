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

Leave Unity Hub's install location at its default, `/Applications/Unity/Hub/Editor`. The editor ends up at `/Applications/Unity/Hub/Editor/<ver>/Unity.app`.

If you must install elsewhere, point the tools at it: `UNITY_HUB_EDITORS_DIR` for `tools/dev/check-env.sh`, and `UNITY_EDITOR` (the full path of the editor binary) for the CI scripts.

## UnityYAMLMerge

`.gitattributes` sends `*.unity`, `*.prefab`, `*.asset` and `*.mat` through a merge driver called `unityyamlmerge`. Register it in this repo's git config (run inside the repo):

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge' merge -p %O %B %A %A"
git config merge.unityyamlmerge.recursive binary
```

The driver path contains the editor version, so run these again after the pinned version changes.

## Pre-commit hook

The pre-commit hook runs the source-of-truth validators on staged files. Install it once per clone:

```bash
bash tools/checks/install-hooks.sh
```
