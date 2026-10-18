# Tooling

## Purpose

Editor-only validators and project setup utilities. Today that is the package installer, which pins the project's package set, the rules that check `Packages/manifest.json` against it, and the project settings baseline, which applies the player, editor, time and rendering settings from code (the URP asset, the "Booth" quality level, lighting, the global volume profile, the Rendering prefab and `Main.unity`). The source-of-truth validators join it later. Nothing in this module ships in a build.

## Public API

No Contracts interfaces and no MonoBehaviours. Public static classes only:

- `PackageInstaller`: the `Required` and `Removed` package lists, and `InstallBatch()`, the headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.PackageInstaller.InstallBatch`).
- `PackageManifestRules`: pure checks on a manifest's `dependencies` (missing required packages, unapproved packages, inexact versions, Cinemachine major version).
- `ProjectSettingsBaseline`: `ApplyPlayerEditorAndTime()`, `ApplyRendering()` and `ApplyAllBatch()`, the idempotent, headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch`).
- `RenderingBaseline`: what `ApplyRendering()` does for the render pipeline (the URP asset and renderer, moving Unity's generated URP settings assets into `Assets/_Project/Settings/`, the single "Booth" quality level), plus the asset path constants.
- `MainSceneBaseline`: what `ApplyRendering()` does for content (the global volume profile, `Rendering.prefab`, `Main.unity` lighting and the build settings), plus the path and color constants.

## Dependencies

- `Bowling.Tooling.Editor` references `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime` (the URP asset, renderer data, `Volume`, `VolumeProfile` and `Tonemapping` types the baseline builds). It overrides references and lists no precompiled DLLs.
- `Bowling.Tooling.Tests.EditMode` references `Bowling.Tooling.Editor`, the same two URP assemblies and the Unity test runners, plus the precompiled `nunit.framework.dll` and `Newtonsoft.Json.dll` (to read `manifest.json`).

## Folder map

- `Editor/`: the editor-only assembly `Bowling.Tooling.Editor`.
- `Editor/Setup/`: project setup utilities (package installer, manifest rules, the project settings baseline and its rendering and scene parts).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Tooling.Tests.EditMode`).
