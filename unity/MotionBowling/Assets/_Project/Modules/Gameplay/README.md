# Gameplay

## Purpose

Runs the session from attract mode to the results screen: who is playing, which frame and ball it is, arming throws, rolling, scoring under standard ten-pin rules, pausing when the player leaves, and writing to the leaderboard. It is the only module that knows the game's rules, and it is built entirely against `Bowling.Testing` fakes.

## Public API

- `GameplayService`, the facade. It implements the Contracts interfaces `IGameEvents` (`Current`, `StateChanged`, `SnapshotChanged`, `RollScored`) and `IGameCommands` (`SelectMode`, `Pause`, `Resume`, `Abandon`), and adds `Tick()`. Every entry point runs to completion, and a call made while another runs is queued (P-7). A subscriber that throws is logged and does not stop the others (P-8).
- `GameplayDriver`, a MonoBehaviour with `[DefaultExecutionOrder(-100)]`. `Initialize(GameplayService)` gives it the service once; its `Update` calls `Tick()` and does nothing before `Initialize`.
- `GameplayTuning`, a ScriptableObject with every state timer, the mode-selection preset and the default player profile. `GameplayTuning.AssetPath` names the committed asset.
- `DifficultyCatalog`, a ScriptableObject with the assist and bumper setting of each difficulty. `For(Difficulty)` returns the `DifficultyProfile`. `DifficultyCatalog.AssetPath` names the committed asset.

Every other type in the module is `internal`.

## Dependencies

- `Bowling.Gameplay` references `Bowling.Contracts` only. It consumes the other modules' interfaces (`IPlayerInput`, `IPlayerCalibrator`, `IThrowSource`, `IRollSimulator`, `ILeaderboardStore`, `ISettingsStore`, `IClock`, `IGameLog`) through Contracts, and App wires the real implementations in. It overrides references and lists no precompiled DLLs.
- `GameBootstrap` does not construct `GameplayService` yet (D-B). The wiring arrives with the integrating plan, once PoseInput, ThrowDetection, BowlingPhysics and Platform provide their implementations. Until then every test builds the service on the `Bowling.Testing` fakes.
- `Bowling.Gameplay.Tests.EditMode` references `Bowling.Gameplay`, `Bowling.Contracts`, `Bowling.Testing` and the Unity test runners, plus the precompiled `nunit.framework.dll`.
- `Bowling.App.Editor` references this module so that `ConfigAssetSetup` can create the two config assets.

## Folder map

- `Runtime/`: the runtime assembly `Bowling.Gameplay`, plus `AssemblyInfo.cs` (`InternalsVisibleTo` for the test assemblies).
  - `Components/`: `GameplayDriver`, the only MonoBehaviour.
  - `Config/`: the ScriptableObject types `GameplayTuning` and `DifficultyCatalog`.
  - `Core/`: the plain C# game logic.
    - The facade: `GameplayService`, which serializes entry points and forwards the events.
    - The host: `GameStateMachine`, the states, transitions and timers. It runs the Scoring hold and the turn changes itself and hands the rest to its four flows:
      - `LobbyFlow`: Attract, Join and ModeSelect.
      - `TurnFlow`: Calibrate, AwaitReady, AwaitThrow and Rolling.
      - `PauseFlow`: the pause rule, Resume, abandon, and the deferred operator commands.
      - `GameEndFlow`: GameOver and Results.
    - The collaborators:
      - `CalibrationRun`: the bookkeeping of one calibration attempt.
      - `StateEntryActions`: the on-enter effects on the throw source and the simulator.
      - `PresenceTracker`: whether the player is present, and since when they have been absent.
      - `PauseTracker`: the state to resume and a pending operator pause.
      - `GamePublisher`: owns `Current` and the three events, and keeps their order.
      - `LeaderboardWriter`: offers a finished game's scores to the leaderboard.
    - The pure parts:
      - `ScoreCard`: ten-pin scoring for 3, 5 or 10 frames.
      - `FrameRules` and `RackDecision`: whether a frame is over and the rack the next ball faces.
      - `GameSession` and `PlayerSlot`: who plays, whose turn it is, and each player's card.
      - `SnapshotBuilder`: turns a session into the immutable `GameSnapshot`.
      - `DefaultProfiles`: the profile used when calibration is skipped or fails.
- `Tests/EditMode/`: EditMode tests for the module (`Bowling.Gameplay.Tests.EditMode`).
  - `GameplayRig` builds a service on the fakes for the flow tests.
  - `GameplayDriverTests` and `ConfigAssetTests` cover the driver and the committed assets.
- The committed assets live outside the module, in `Assets/_Project/Config/Gameplay/`: `GameplayTuning.asset` and `DifficultyCatalog.asset`. `ConfigAssetSetup` in `Bowling.App.Editor` creates them.
