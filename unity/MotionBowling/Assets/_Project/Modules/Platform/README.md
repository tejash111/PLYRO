# Platform

## Purpose

Makes the game a dependable macOS product at runtime: local storage, logging, operator controls, kiosk mode, keeping the display awake and the bridge to the native `MacPlatform` bundle. The signed, notarized `.dmg` build itself lives in `Bowling.App.Editor` and `tools/build`.

## Public API

The module is a skeleton for now: the types arrive with sub-project 9 (platform and release spec). It will implement the Contracts interfaces `ISettingsStore`, `ILeaderboardStore`, `IGameLog` and `IOperatorActions` (Contracts 6.6). Its public MonoBehaviours will be `PlatformLifecycle`, `KioskController` and `OperatorShortcut`.

## Dependencies

- `Bowling.Platform` references `Bowling.Contracts`. The rules also allow `Unity.InputSystem` (the operator shortcut) and the precompiled `Newtonsoft.Json.dll` (settings and leaderboard files); each is added when its first feature needs it. It overrides references and lists no precompiled DLLs yet.
- `Bowling.Platform.Tests.EditMode` references `Bowling.Platform`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.Platform`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/`, `Core/` and `Config/` arrive with the implementation (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Platform.Tests.EditMode`).
