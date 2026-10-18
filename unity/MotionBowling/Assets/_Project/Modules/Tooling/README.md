# Tooling

## Purpose

Editor-only validators and project setup utilities. Today that is the package installer, which pins the project's package set, the rules that check `Packages/manifest.json` against it, and the project settings baseline, which applies the player, editor and time settings from code. The source-of-truth validators join it later. Nothing in this module ships in a build.

## Public API

No Contracts interfaces and no MonoBehaviours. Public static classes only:

- `PackageInstaller`: the `Required` and `Removed` package lists, and `InstallBatch()`, the headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.PackageInstaller.InstallBatch`).
- `PackageManifestRules`: pure checks on a manifest's `dependencies` (missing required packages, unapproved packages, inexact versions, Cinemachine major version).
- `ProjectSettingsBaseline`: `ApplyPlayerEditorAndTime()` and `ApplyAllBatch()`, the idempotent, headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch`).

## Dependencies

- `Bowling.Tooling.Editor` references no other assembly yet. It overrides references and lists no precompiled DLLs.
- `Bowling.Tooling.Tests.EditMode` references `Bowling.Tooling.Editor` and the Unity test runners, plus the precompiled `nunit.framework.dll` and `Newtonsoft.Json.dll` (to read `manifest.json`).

## Folder map

- `Editor/`: the editor-only assembly `Bowling.Tooling.Editor`.
- `Editor/Setup/`: project setup utilities (package installer, manifest rules and the project settings baseline).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Tooling.Tests.EditMode`).
