# BowlingPhysics

## Purpose

Simulates one roll, from a `ThrowEvent` at the foul line until the pins have settled: the lane, the ball, the pins, the hook, the settle test and the rack reset. Results are believable in real units, deterministic on a given machine and tunable for game feel. It also reports milestones and impacts, so cameras and audio can react without depending on physics internals.

## Public API

The module is a skeleton for now: the types arrive with sub-project 5 (bowling physics spec). It will implement the Contracts interfaces `IRollSimulator` and `IRollBodies` (Contracts 6.4). Its public MonoBehaviour is `RollSimulator`, which delegates to the pure `RollStateMachine`.

## Dependencies

- `Bowling.BowlingPhysics` references `Bowling.Contracts` only. It overrides references and lists no precompiled DLLs.
- `Bowling.BowlingPhysics.Tests.EditMode` and `Bowling.BowlingPhysics.Tests.PlayMode` reference `Bowling.BowlingPhysics`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`. The PlayMode assembly covers the rigid-body behavior.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.BowlingPhysics`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/`, `Core/` and `Config/` arrive with the implementation (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the pure logic (`Bowling.BowlingPhysics.Tests.EditMode`).
- `Tests/PlayMode/`: PlayMode tests for the simulation (`Bowling.BowlingPhysics.Tests.PlayMode`).
