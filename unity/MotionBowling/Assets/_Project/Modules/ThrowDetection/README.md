# ThrowDetection

## Purpose

Recognizes a real bowling arm swing in the locked player's pose stream and turns it into one `ThrowEvent`: speed, launch angle, start offset, hook and confidence, with the difficulty assist applied. It must catch real throws reliably, ignore everything else, and be fully deterministic so pose recordings work as regression tests.

## Public API

The module is a skeleton for now: the types arrive with sub-project 4 (throw detection spec). It will implement the Contracts interface `IThrowSource` (Contracts 6.3). Its public MonoBehaviours will be `ThrowDetectionDriver` (`[DefaultExecutionOrder(-200)]`) and, in development builds only, `DebugThrowSource` and `ThrowDebugOverlay`.

## Dependencies

- `Bowling.ThrowDetection` references `Bowling.Contracts` only. It overrides references and lists no precompiled DLLs.
- `Bowling.ThrowDetection.Tests.EditMode` references `Bowling.ThrowDetection`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.ThrowDetection`. For now it holds only `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies); `Components/`, `Core/` and `Config/` arrive with the implementation (foundation spec section 4.2).
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.ThrowDetection.Tests.EditMode`).
