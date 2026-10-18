# Contracts

## Purpose

The shared vocabulary of the game: data types, enums, interfaces and the lane geometry constants. Every other module references `Bowling.Contracts`, and modules talk to each other only through it. It holds no behavior beyond trivial helpers (`PinMaskExtensions.Count` and `LaneSpec.PinSpot`) and depends only on `UnityEngine.CoreModule` types such as `Vector2`, `Vector3` and `Transform`.

## Public API

Defined exactly by system design section 6, one type per file under `Runtime/<Area>/`, all in the namespace `Bowling.Contracts`. Every `readonly struct` has one public constructor taking all its fields in declaration order. The areas are:

- `Pose/` (6.1): pose and landmark data types.
- `Input/` (6.2): `ITrackingSource`, `IPlayerInput`, `IPlayerCalibrator`, `IHandCursor`, `ICameraPreview`, `ITrackingDiagnostics`, `IRecordingIndicator` and their data types.
- `Throw/` (6.3): `IThrowSource`, `ThrowEvent`, `ThrowContext`, `Difficulty`, `DifficultyProfile`, `SwingPhase` and `ThrowRejection`.
- `Roll/` (6.4): `IRollSimulator`, `IRollBodies`, `PinMask`, `RollResult` and the roll events.
- `Game/` (6.5): `IGameEvents`, `IGameCommands`, `IUiCues` and the data types `GameState`, `GameSnapshot`, `PlayerSnapshot`, `FrameView`, `RollMark`, `MarkKind`, `RollOutcome` and `UiCue`.
- `Platform/` (6.6): `IClock`, `IGameLog`, `ILeaderboardStore`, `ISettingsStore`, `IOperatorActions` and the data types `LeaderboardEntry`, `LeaderboardScope` and `OperatorSettings`.
- `Lane/` (6.7): `LaneSpec`, the physical lane constants and `PinSpot`.

The snapshot, frame and settings types (`GameSnapshot`, `PlayerSnapshot`, `FrameView`, `LeaderboardEntry`, `OperatorSettings`) are classes with public fields and no constructor. `OperatorSettings` carries the spec defaults: `SchemaVersion = 1`, `CameraUniqueId = null`, `DefaultDifficulty = Casual`, `MasterVolume = 0.8f`, `PoseRecordingEnabled = false`.

No MonoBehaviours.

## Dependencies

- `Bowling.Contracts` references no other assembly, only the Unity engine modules. It overrides references and lists no precompiled DLLs.
- `Bowling.Contracts.Tests.EditMode` references `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.Contracts`.
  - `AssemblyInfo.cs`: `InternalsVisibleTo` for the test assemblies.
  - `<Area>/`: the types, one per file (`Pose/`, `Input/`, `Throw/`, `Roll/`, `Game/`, `Platform/`, `Lane/`).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Contracts.Tests.EditMode`): `PinMaskExtensions`, `LaneSpec.PinSpot`, `RollResult.PinsKnocked`, the `OperatorSettings` defaults, and a reflection check that every `readonly struct` has its full constructor.
