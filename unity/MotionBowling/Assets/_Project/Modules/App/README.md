# App

## Purpose

The single composition root. It bootstraps the game, constructs the plain C# services, wires every module to the others through their Contracts interfaces, and fixes the update order of the module drivers. No other module may reference a sibling module; only `Bowling.App` does, so only here do concrete types from different modules meet. Its editor assembly holds the build script, and its tests are the only place for integration tests that combine modules.

## Public API

- `GameBootstrap`, a MonoBehaviour with `[DefaultExecutionOrder(-1000)]`, the only composition root. `Awake` constructs `UnityClock` and `UnityConsoleGameLog`, exposes them as `Clock` and `Log`, and logs `[App] Boot`. Later sub-projects add the wiring that hands references to module components through their public `Initialize(...)` methods.
- `UnityClock` (`IClock`, `Time.unscaledTimeAsDouble`) and `UnityConsoleGameLog` (`IGameLog`, writes `[category] message` to the Unity console; `Error` also logs the exception). Both live here until Platform replaces the logger.
- `ExecutionOrder`, constants only: `Bootstrap` -1000, `PoseInput` -300, `ThrowDetection` -200, `Gameplay` -100, `Presentation` 0, `UI` 100. Modules write the literal number in their drivers' `[DefaultExecutionOrder]`; `ExecutionOrderTests` checks every `*Driver` type against the constant named after its module.
- `MainSceneSetup` (editor): `EnsureBootstrap()` and the batch entry point `EnsureBootstrapBatch()` add the `Bootstrap` root with `GameBootstrap` to `Main.unity` when it is missing, and change nothing when it is there. Run it with `tools/ci/unity.sh exec Bowling.App.Editor.MainSceneSetup.EnsureBootstrapBatch`.
- `ConfigAssetSetup` (editor): `EnsureGameplayAssets()` and the batch entry point `EnsureGameplayAssetsBatch()` create the `Assets/_Project/Config/Gameplay` folders and any missing `GameplayTuning.asset` and `DifficultyCatalog.asset` from the defaults of their types, and change nothing when they exist. Run it with `tools/ci/unity.sh exec Bowling.App.Editor.ConfigAssetSetup.EnsureGameplayAssetsBatch`.
- Pending Gameplay wiring: `GameBootstrap` does not construct `GameplayService` yet. When PoseInput, ThrowDetection, BowlingPhysics and Platform provide their implementations, the integrating plan constructs the service in `Awake` with them, `Clock`, `Log` and the two Gameplay assets (serialized references on `GameBootstrap`), adds `GameplayDriver` to the Bootstrap object, calls its `Initialize`, and hands the service to UI and Presentation as `IGameEvents` and `IGameCommands`.

## Dependencies

- `Bowling.App` references every runtime module (`Bowling.Contracts`, `Bowling.PoseInput`, `Bowling.ThrowDetection`, `Bowling.BowlingPhysics`, `Bowling.Gameplay`, `Bowling.Presentation`, `Bowling.UI` and `Bowling.Platform`). The rules also allow `Unity.InputSystem`, added when it is needed. It overrides references and lists no precompiled DLLs.
- `Bowling.App.Editor` (editor only) references `Bowling.App` and `Bowling.Gameplay`, for the build script and `ConfigAssetSetup`.
- `Bowling.App.Tests.EditMode` and `Bowling.App.Tests.PlayMode` reference `Bowling.App`, every runtime module, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.
- Every other runtime assembly grants `InternalsVisibleTo` to the two App test assemblies, for integration tests.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.App`, plus `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies).
  - `Components/`: `GameBootstrap`.
  - `Core/`: `ExecutionOrder`, `UnityClock`, `UnityConsoleGameLog`.
- `Editor/`: the editor-only assembly `Bowling.App.Editor`. It holds `MainSceneSetup` and `ConfigAssetSetup`; the build script comes later.
- `Tests/EditMode/`: EditMode tests (`Bowling.App.Tests.EditMode`), currently `UnityConsoleGameLogTests`.
- `Tests/PlayMode/`: PlayMode tests (`Bowling.App.Tests.PlayMode`): `BootstrapSceneTests` loads `Main` and checks the single `GameBootstrap`; `ExecutionOrderTests` checks the order constants and every driver's attribute.
