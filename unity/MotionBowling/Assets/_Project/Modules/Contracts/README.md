# Contracts

## Purpose

The shared vocabulary of the game: data types, enums, interfaces and the lane geometry constants. Every other module references `Bowling.Contracts`, and modules talk to each other only through it. It holds no behavior beyond trivial helpers (`PinMaskExtensions.Count` and `LaneSpec.PinSpot`) and depends only on `UnityEngine.CoreModule` types such as `Vector2`, `Vector3` and `Transform`.

## Public API

Defined exactly by system design section 6, one type per file under `Runtime/<Area>/`. The module is a skeleton for now: the types arrive with the Contracts task of sub-project 1. The areas are:

- `Pose/` (6.1): pose and landmark data types.
- `Input/` (6.2): `ITrackingSource`, `IPlayerInput`, `IPlayerCalibrator`, `IHandCursor`, `ICameraPreview`, `ITrackingDiagnostics`, `IRecordingIndicator` and their data types.
- `Throw/` (6.3): `IThrowSource`, `ThrowEvent` and the throw data types.
- `Roll/` (6.4): `IRollSimulator`, `IRollBodies`, `PinMask`, `RollResult` and the roll events.
- `Game/` (6.5): `IGameEvents`, `IGameCommands`, `IUiCues` and the game state and snapshot types.
- `Platform/` (6.6): `IClock`, `IGameLog`, `ILeaderboardStore`, `ISettingsStore`, `IOperatorActions` and their data types.
- `Lane/` (6.7): `LaneSpec`, the physical lane constants and `PinSpot`.

No MonoBehaviours.

## Dependencies

- `Bowling.Contracts` references no other assembly, only the Unity engine modules. It overrides references and lists no precompiled DLLs.
- `Bowling.Contracts.Tests.EditMode` references `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.Contracts`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); the `<Area>/` folders arrive with the types.
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Contracts.Tests.EditMode`).
