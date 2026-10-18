# PoseInput

## Purpose

Turns raw tracking samples into clean, player-centric input for the rest of the game. It picks the one player who is playing, smooths their pose, converts it to player space, calibrates them, drives the hand cursor, supervises the tracker's health, and records and replays pose sessions for tuning and tests. It bridges to the native `TrackingCore` plugin but is built and tested first against recorded and fake sources.

## Public API

The module is a skeleton for now: the types arrive with sub-project 3 (pose input spec). It will implement these Contracts interfaces:

- `IPlayerInput`
- `IPlayerCalibrator`
- `IHandCursor`
- `ICameraPreview`
- `ITrackingDiagnostics`
- `IRecordingIndicator`

It will also provide three `ITrackingSource` implementations (native, recorded and null). Its public MonoBehaviours will be `PoseInputDriver` (`[DefaultExecutionOrder(-300)]`) and `PoseRecorderHotkeys`.

## Dependencies

- `Bowling.PoseInput` references `Bowling.Contracts` only. It overrides references and lists no precompiled DLLs yet.
- The precompiled `Newtonsoft.Json.dll` is allowed (pose recordings are JSON) and is added when the recorder needs it.
- `allowUnsafeCode` is on, for this assembly alone, for the zero-allocation native interop (pose input spec section 7).
- `Bowling.PoseInput.Tests.EditMode` references `Bowling.PoseInput`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.PoseInput`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/`, `Core/` and `Config/` arrive with the implementation (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.PoseInput.Tests.EditMode`).
