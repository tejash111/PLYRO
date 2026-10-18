# UI

## Purpose

Everything the player and operator read or point at: the attract screen, mode selection with the hand cursor, calibration prompts, the in-game HUD and scorecards, the pause overlay, results and the keyboard-only operator panel. It is built with UI Toolkit from components (UXML, USS and C# together), readable from 3 m away and usable with an arm instead of a mouse.

## Public API

The module is a skeleton for now: the types arrive with sub-project 7 (UI spec). It will implement the Contracts interface `IUiCues`, through `UiCueHub`. Its only MonoBehaviour will be `UiRoot` (`[DefaultExecutionOrder(100)]`), which owns the UI documents and exposes the `Initialize(...)` entry point for App. It consumes `IGameEvents`, `IGameCommands`, `IHandCursor`, `IPlayerCalibrator`, `IThrowSource`, `IPlayerInput`, `ICameraPreview`, `ILeaderboardStore`, `IRecordingIndicator`, `IOperatorActions` and `ITrackingDiagnostics` through Contracts.

## Dependencies

- `Bowling.UI` references `Bowling.Contracts` only; UI Toolkit is part of UnityEngine. It overrides references and lists no precompiled DLLs.
- `Bowling.UI.Tests.EditMode` and `Bowling.UI.Tests.PlayMode` reference `Bowling.UI`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.UI`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies). The implementation adds `Core/` and `Config/`, and in place of `Components/` it adds UI-component folders plus `Styles/`, `Screens/` and `Root/` (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the pure logic (`Bowling.UI.Tests.EditMode`).
- `Tests/PlayMode/`: PlayMode tests for the screens and components (`Bowling.UI.Tests.PlayMode`).
