# Presentation

## Purpose

Makes the game look and sound like a bowling alley: the 3D environment and lighting, the ball in the player's hand before release, broadcast-style camera cuts, strike and spare replays, celebration effects and all audio. It reacts to events and never changes game state.

## Public API

The module is a skeleton for now: the types arrive with sub-project 8 (presentation spec). It implements no Contracts interface. It consumes `IGameEvents`, `IRollSimulator`, `IRollBodies`, `IPlayerInput`, `IThrowSource`, `IUiCues` and `IOperatorActions.SettingsApplied` through Contracts, and App wires them in. Its public MonoBehaviours (the camera director, replay, VFX and audio components) are defined by the presentation spec.

## Dependencies

- `Bowling.Presentation` references `Bowling.Contracts`. The rules also allow `Unity.Cinemachine`, `FMODUnity`, `Unity.RenderPipelines.Universal.Runtime` and `Unity.RenderPipelines.Core.Runtime`; each is added when its first feature needs it. It overrides references and lists no precompiled DLLs.
- `Bowling.Presentation.Tests.EditMode` and `Bowling.Presentation.Tests.PlayMode` reference `Bowling.Presentation`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.Presentation`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/`, `Core/` and `Config/` arrive with the implementation (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the pure logic (`Bowling.Presentation.Tests.EditMode`).
- `Tests/PlayMode/`: PlayMode tests for cameras, replays and audio (`Bowling.Presentation.Tests.PlayMode`).
