# Tooling

## Purpose

Editor-only validators and project setup utilities. The setup half is the package installer, which pins the project's package set, the rules that check `Packages/manifest.json` against it, and the project settings baseline, which applies the player, editor, time and rendering settings from code (the URP asset, the "Booth" quality level, lighting, the global volume profile, the Rendering prefab and `Main.unity`). The validation half is the editor layer of the source-of-truth checks: a rule framework, and the two SOT-2 module-structure rules M-1 and M-2. More rules join it later. Nothing in this module ships in a build.

## Public API

No Contracts interfaces and no MonoBehaviours. Public types only:

- `PackageInstaller`: the `Required` and `Removed` package lists, and `InstallBatch()`, the headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.PackageInstaller.InstallBatch`).
- `PackageManifestRules`: pure checks on a manifest's `dependencies` (missing required packages, unapproved packages, inexact versions, Cinemachine major version).
- `ProjectSettingsBaseline`: `ApplyPlayerEditorAndTime()`, `ApplyRendering()` and `ApplyAllBatch()`, the idempotent, headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.ProjectSettingsBaseline.ApplyAllBatch`).
- `RenderingBaseline`: what `ApplyRendering()` does for the render pipeline (the URP asset and renderer, moving Unity's generated URP settings assets into `Assets/_Project/Settings/`, the single "Booth" quality level), plus the asset path constants.
- `MainSceneBaseline`: what `ApplyRendering()` does for content (the global volume profile, `Rendering.prefab`, `Main.unity` lighting and the build settings), plus the path and color constants.
- `Violation`: one finding, printed as `<rule-id> <path>[:<line>] <message>` (the text checks' format). Warnings don't fail a batch run.
- `IValidationRule`: `Id` and `Run(ValidationContext)`, the contract every editor rule implements.
- `ValidationContext`: the repo root, project root, modules root, main scene path and the `ModuleRules`. `ForProject()` builds the context for the open project.
- `ModuleRules`: a typed, strictly checked view of `tools/checks/module-rules.json` (modules and their asmdefs, allowed references per assembly, the test assembly rules, and the `$module`, `$moduleAllowed` and `$allRuntime` tokens). An `assemblies` key that ends in `*` is a prefix (`Bowling.App.Tests.*`).
- `SourceOfTruthValidator`: `CreateRules()`, `RunAll(context)`, the menu item **Bowling > Validate Sources of Truth**, and `RunBatch()`, the headless entry point (`tools/ci/unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch`, exit code 1 on any non-warning violation).
- `ModuleStructureRule` (M-1): every module folder is listed in the rules, and every listed module has its README (the four template headings) and exactly its asmdefs, each with the name, root namespace, platforms and required fields of its kind.
- `AsmdefReferenceRule` (M-2): every asmdef references only what the rules allow its assembly, including `GUID:` references (resolved through the AssetDatabase), precompiled DLLs and `allowUnsafeCode`.

## Dependencies

- `Bowling.Tooling.Editor` references `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime` (the URP asset, renderer data, `Volume`, `VolumeProfile` and `Tonemapping` types the baseline builds), plus the precompiled `Newtonsoft.Json.dll` (to read `module-rules.json` and asmdef files). It overrides references and lists no other precompiled DLLs.
- `Bowling.Tooling.Tests.EditMode` references `Bowling.Tooling.Editor`, the same two URP assemblies and the Unity test runners, plus the precompiled `nunit.framework.dll` and `Newtonsoft.Json.dll` (to read `manifest.json`).

## Folder map

- `Editor/`: the editor-only assembly `Bowling.Tooling.Editor`.
- `Editor/Setup/`: project setup utilities (package installer, manifest rules, the project settings baseline and its rendering and scene parts).
- `Editor/Validation/`: the rule framework (`Violation`, `IValidationRule`, `ValidationContext`, `ModuleRules`, `SourceOfTruthValidator`) and its internal asmdef readers.
- `Editor/Validation/Rules/`: the rules, one per file (`ModuleStructureRule`, `AsmdefReferenceRule`).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Tooling.Tests.EditMode`).
- `Tests/EditMode/Fixtures~/`: plain-file fixtures for the M-1 and M-2 tests (`M-1` and `M-2`, each with `pass` and `fail/<case>` folders). The `~` keeps Unity from importing them.
