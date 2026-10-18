# Testing

## Purpose

Test-only fakes for every Contracts interface, so each module can be built and tested without the others. Every fake is a plain C# class that is deterministic and controllable from tests. The assembly is constrained to `UNITY_INCLUDE_TESTS`, so it never ships in a build.

## Public API

No MonoBehaviours. Every type is a public class in the namespace `Bowling.Testing`, one per file. The fakes implement the Contracts interfaces below, and the order in which each one sets state and raises events is pinned by `Tests/EditMode/`.

- `FakeClock` (`IClock`): `Now` starts at 0; `Advance(seconds)` moves it forward and throws `ArgumentOutOfRangeException` for a negative or NaN amount.
- Input:
  - `FakeTrackingSource` (`ITrackingSource`): `Push(PoseSample)`, `SetStatus`; `Start`/`Stop` set `Running`/`Stopped` and are counted in `StartCount`/`StopCount`. Starts `Stopped`.
  - `FakePlayerInput` (`IPlayerInput`): `SetPresent`, `SetStatus` (events only when the value changes), `PushPose(PlayerPose)`. Starts `Stopped`, absent, with no pose.
  - `FakeCalibrator` (`IPlayerCalibrator`): `Complete(profile)`, `Fail(reason)`, `HoldProgress` setter; `BeginCount`.
  - `FakeHandCursor` (`IHandCursor`): `Set(active, position)`.
  - `FakeCameraPreview` (`ICameraPreview`): `Set(Texture)`.
  - `FakeTrackingDiagnostics` (`ITrackingDiagnostics`): `Set(TrackingStats)`, `SetCameras(...)`.
  - `FakeRecordingIndicator` (`IRecordingIndicator`): `Set(bool)`.
- Throw and roll:
  - `FakeThrowSource` (`IThrowSource`): `SetPhase`, `EmitThrow(ThrowEvent)`, `Reject(reason)`; records `LastContext`, `ArmCount` and `DisarmCount`. Only `SetPhase` raises `PhaseChanged`.
  - `FakeRollSimulator` (`IRollSimulator`): `CompleteWith(standingAfter, gutter, reason)`, `RaiseMilestone`, `RaiseImpact`; records `LastRack`, `LastBumpers` and `LastThrow` with call counts. `SetRack` also sets `Standing`.
- Game:
  - `FakeGameEvents` (`IGameEvents`): `SetState`, `Publish(GameSnapshot)`, `RaiseRollScored`. `Current` starts as an Attract snapshot with no players.
  - `FakeGameCommands` (`IGameCommands`): `Calls` (text, in order) and `SelectModeCalls` (typed).
  - `FakeUiCues` (`IUiCues`): `Raise(UiCue)`.
- Platform:
  - `RecordingGameLog` (`IGameLog`): `Entries` as `GameLogEntry` (`GameLogLevel`, category, message, exception).
  - `InMemoryLeaderboardStore` (`ILeaderboardStore`): built with a `Func<DateTime>` for UTC now and a `TimeZoneInfo`; "Today" compares local dates in that zone.
  - `InMemorySettingsStore` (`ISettingsStore`): saves and loads copies.
  - `FakeOperatorActions` (`IOperatorActions`): `Calls`; `ApplySettings` raises `SettingsApplied`.
- `TestDataPaths.PoseRecordings`: the absolute `<repo>/test-data/pose-recordings` folder, computed from `Application.dataPath`.

`GameLogLevel` and `GameLogEntry` are the only types here that implement no interface: they describe what `RecordingGameLog` records.

## Dependencies

- `Bowling.Testing` references `Bowling.Contracts` only, and sets `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. It overrides references and lists no precompiled DLLs.
- Every module's test assemblies reference `Bowling.Testing`, except Tooling's (`Bowling.Tooling.Tests.EditMode` tests editor code and uses no fakes). No runtime assembly references it, and `Bowling.App` does not.
- `Bowling.Testing.Tests.EditMode` references `Bowling.Testing`, `Bowling.Contracts` and the Unity test runners, plus the precompiled `nunit.framework.dll`. It tests the fakes' semantics.

## Folder map

- `Runtime/`: the test-only assembly `Bowling.Testing`, with `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies).
- `Runtime/Core/`: the fakes, one public type per file.
- `Tests/EditMode/`: EditMode tests for the fakes (`Bowling.Testing.Tests.EditMode`), one file per area.
