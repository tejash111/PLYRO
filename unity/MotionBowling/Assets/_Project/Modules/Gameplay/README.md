# Gameplay

## Purpose

Runs the session from attract mode to the results screen: who is playing, which frame and ball it is, arming throws, rolling, scoring under standard ten-pin rules, pausing when the player leaves, and writing to the leaderboard. It is the only module that knows the game's rules, and it is built entirely against `Bowling.Testing` fakes.

## Public API

The module is a skeleton for now: the types arrive with sub-project 6 (gameplay spec). It will implement the Contracts interfaces `IGameEvents` and `IGameCommands` (Contracts 6.5). Its public MonoBehaviour will be `GameplayDriver` (`[DefaultExecutionOrder(-100)]`), which ticks the plain C# `GameplayService`.

## Dependencies

- `Bowling.Gameplay` references `Bowling.Contracts` only. It consumes the other modules' interfaces (`IPlayerInput`, `IThrowSource`, `IRollSimulator`, `ILeaderboardStore` and more) through Contracts, and App wires the real implementations in. It overrides references and lists no precompiled DLLs.
- `Bowling.Gameplay.Tests.EditMode` references `Bowling.Gameplay`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.Gameplay`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/`, `Core/` and `Config/` arrive with the implementation (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Gameplay.Tests.EditMode`).
