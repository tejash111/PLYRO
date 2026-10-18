# Testing

## Purpose

Test-only fakes for every Contracts interface, so each module can be built and tested without the others. Every fake is a plain C# class that is deterministic and controllable from tests. The assembly is constrained to `UNITY_INCLUDE_TESTS`, so it never ships in a build.

## Public API

The module is a skeleton for now: the fakes arrive with the Testing task of sub-project 1 (foundation spec section 6). No MonoBehaviours. It will hold:

- `FakeClock` (`IClock`).
- `FakeTrackingSource`, `FakePlayerInput`, `FakeCalibrator`, `FakeHandCursor`, `FakeCameraPreview`, `FakeTrackingDiagnostics` and `FakeRecordingIndicator` (the input interfaces).
- `FakeThrowSource` (`IThrowSource`) and `FakeRollSimulator` (`IRollSimulator`).
- `FakeGameEvents` and `FakeGameCommands` (`IGameEvents`, `IGameCommands`), and `FakeUiCues` (`IUiCues`).
- `RecordingGameLog` (`IGameLog`), `InMemoryLeaderboardStore`, `InMemorySettingsStore` and `FakeOperatorActions`.
- `TestDataPaths`, which resolves `test-data/pose-recordings/` from the Unity project path.

## Dependencies

- `Bowling.Testing` references `Bowling.Contracts` only, and sets `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. It overrides references and lists no precompiled DLLs.
- Every test assembly in the project references `Bowling.Testing`. No runtime assembly does, and `Bowling.App` does not reference it.
- `Bowling.Testing.Tests.EditMode` references `Bowling.Testing`, `Bowling.Contracts` and the Unity test runners, plus the precompiled `nunit.framework.dll`. It tests the fakes' semantics.

## Folder map

- `Runtime/`: the test-only assembly `Bowling.Testing`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); the fakes arrive with the implementation.
- `Tests/EditMode/`: EditMode tests for the fakes (`Bowling.Testing.Tests.EditMode`).
