# App

## Purpose

The single composition root. It bootstraps the game, constructs the plain C# services, wires every module to the others through their Contracts interfaces, and fixes the update order of the module drivers. No other module may reference a sibling module; only `Bowling.App` does, so only here do concrete types from different modules meet. Its editor assembly holds the build script, and its tests are the only place for integration tests that combine modules.

## Public API

The module is a skeleton for now: the types arrive with the App task of sub-project 1. It will have:

- `GameBootstrap`, a MonoBehaviour with `[DefaultExecutionOrder(-1000)]`, the only composition root. Its `Awake` constructs the services and hands references to module components through their public `Initialize(...)` methods.
- `UnityClock` (`IClock`, unscaled time) and `UnityConsoleGameLog` (`IGameLog`). Both live here until Platform replaces the logger.
- `ExecutionOrder`, the constants for the driver update order (PoseInput -300, ThrowDetection -200, Gameplay -100, Presentation 0, UI 100).

## Dependencies

- `Bowling.App` references every runtime module (`Bowling.Contracts`, `Bowling.PoseInput`, `Bowling.ThrowDetection`, `Bowling.BowlingPhysics`, `Bowling.Gameplay`, `Bowling.Presentation`, `Bowling.UI` and `Bowling.Platform`). The rules also allow `Unity.InputSystem`, added when it is needed. It overrides references and lists no precompiled DLLs.
- `Bowling.App.Editor` (editor only) references `Bowling.App`, for the build script.
- `Bowling.App.Tests.EditMode` and `Bowling.App.Tests.PlayMode` reference `Bowling.App`, every runtime module, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.
- Every other runtime assembly grants `InternalsVisibleTo` to the two App test assemblies, for integration tests.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.App`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/` and `Core/` arrive with the implementation (foundation spec section 4.2).
- `Editor/`: the editor-only assembly `Bowling.App.Editor` (the build script).
- `Tests/EditMode/`: EditMode integration tests (`Bowling.App.Tests.EditMode`).
- `Tests/PlayMode/`: PlayMode integration tests (`Bowling.App.Tests.PlayMode`).
