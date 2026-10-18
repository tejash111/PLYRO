# Sub-project 6: Gameplay and Scoring

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 1 (foundation). Built entirely against `Bowling.Testing` fakes.
- **Module:** `Bowling.Gameplay`

## 1. Purpose

Gameplay runs the session, from attract mode through to the results screen:

- who's playing;
- which frame and ball it is;
- arming throws;
- rolling;
- scoring under standard ten-pin rules;
- pausing when the player leaves;
- writing to the leaderboard.

It is the only module that knows the game's rules.

**Implements:** `IGameEvents` and `IGameCommands` (Contracts 6.5).

**Consumes:**

- `IPlayerInput`
- `IPlayerCalibrator`
- `IThrowSource`
- `IRollSimulator`
- `ILeaderboardStore`
- `ISettingsStore` (for the default difficulty)
- `IClock`
- `IGameLog`

## 2. Components

```
Modules/Gameplay/Runtime/
├─ Components/
│  └─ GameplayDriver.cs          [DefaultExecutionOrder(-100)] calls GameplayService.Tick() each Update
├─ Core/
│  ├─ GameplayService.cs         facade: implements IGameEvents + IGameCommands; owns the pieces below
│  ├─ GameStateMachine.cs        states, transitions, timers (section 4)
│  ├─ GameSession.cs             players, frame count, difficulty, turn order, current indices
│  ├─ PlayerSlot.cs              display name, color index, profile (nullable), ScoreCard
│  ├─ ScoreCard.cs               ten-pin scoring for N frames (section 5)
│  ├─ FrameRules.cs              pure: "is frame complete", "next rack" decisions
│  ├─ SnapshotBuilder.cs         builds immutable GameSnapshot / PlayerSnapshot / FrameView
│  └─ DefaultProfiles.cs         fallback PlayerProfile when calibration fails twice
└─ Config/
   ├─ GameplayTuning.cs          ScriptableObject: every timer in section 4
   └─ DifficultyCatalog.cs       ScriptableObject: Casual/Standard/Pro → DifficultyProfile
```

**Difficulty catalog defaults** (system design D-9):

| Level | Assist | Bumpers |
|---|---|---|
| Casual | 0.50 | on |
| Standard | 0.25 | off |
| Pro | 0.00 | off |

## 3. Session model

- **Players:** 1–4. `DisplayName` is "Player 1" to "Player 4"; `ColorIndex` is 0–3.
- **Frame count:** 3, 5 or 10.
- **Turn order** is frame-major: P1 frame 1, P2 frame 1, …, P1 frame 2, …
- **Calibration happens:**
  - at the start of the first turn in a single-player game;
  - at the start of **every** turn when there are 2 or more players, because a different person steps in each turn.
- **Throw context** = `PlayerSlot.Profile` + the selected `DifficultyProfile`.

## 4. State machine

Every duration is a `GameplayTuning` field, with the default shown. Time comes from `IClock`.

| State | On enter | Leaves when | Next |
|---|---|---|---|
| Attract | `throwSource.Disarm()`; `sim.SetBumpers(false)`; `sim.SetRack(All)` (only if `!sim.IsRolling`) | `input.IsPlayerPresent` becomes true | Join |
| Join | — | `joinGreeting` (1.0 s) elapses; or presence false | ModeSelect; or Attract |
| ModeSelect | Selection preset to 1 player, 3 frames, default difficulty from settings | `SelectMode(...)` is called; or `modeSelectTimeout` (20 s) passes with the player present, which auto-selects the preset; or presence false for `modeSelectAbsent` (5 s) | Calibrate; Calibrate; Attract |
| Calibrate | `calibrator.Begin()` | `Completed(profile)` stores the profile; on the first `Failed(Timeout)`, `Begin()` runs again; on a second `Failed(Timeout)`, `DefaultProfiles.For(...)` is used | AwaitReady; Calibrate; AwaitReady |
| AwaitReady | `sim.SetBumpers(difficulty.Bumpers)`; `sim.SetRack(rackForThisBall)`; `throwSource.Arm(context)` | `throwSource.Phase` becomes Ready, Backswing or ForwardSwing | AwaitThrow |
| AwaitThrow | — | `Thrown(evt)` calls `sim.Roll(evt)`; `Rejected` | Rolling; AwaitReady (re-armed; the ball isn't consumed) |
| Rolling | — | `sim.Completed(result)` | Scoring |
| Scoring | `ScoreCard.AddRoll(result.PinsKnocked)`; raise `RollScored`; publish the snapshot | `scoringHold` (1.5 s) elapses, or `scoringHoldReplay` (4.5 s) when the mark is a Strike or Spare (presentation replay 3.5 s or less, plus 1.0 s) | the next ball's AwaitReady; the next player's Calibrate or AwaitReady; or GameOver |
| Paused | `throwSource.Disarm()`; remember the resume state | Presence true again: resume, re-entering that state (Calibrate restarts; AwaitReady re-arms); or `abandonAfterPause` (15 s) elapses; or `Resume()` | (resume state); Attract (no leaderboard entry); (resume state) |
| GameOver | Submit one `LeaderboardEntry` per player | `gameOverHold` (2.0 s) elapses | Results |
| Results | — | `resultsHold` (10 s) elapses; or presence false for 3 s; or `SelectMode(...)` ("Play again") | Attract; Attract; Calibrate (new game, same players count) |

**Pause rule.** In Calibrate, AwaitReady and AwaitThrow, presence false for `pauseAfterAbsent` (2.0 s) goes to Paused. PoseInput already debounces 1.0 s, so this is about 3 s of real absence. Rolling and Scoring never pause; the pause applies when the next state is entered.

**Turn change with 2 or more players.**

- When the next turn belongs to a different player, Calibrate shows "Player N, step in".
- The zone being empty is expected while players swap, so the pause rule waits for `turnChangeWait` (30 s) instead of 2 s before Paused.

**Operator commands:**

- `Pause()` goes to Paused from any state except Attract, Results and Paused.
- `Resume()` leaves Paused.
- `Abandon()` goes to Attract from any state. It is ignored while Rolling, and runs when the roll completes instead.

**Rack decision (`FrameRules`):**

- **Frames before the last:** after ball 1, if it was a strike the frame ends and the next rack is `All`; otherwise ball 2 gets `StandingAfter`. After ball 2, the frame ends and the next rack is `All`.
- **The last frame** (frame N, where N is the frame count):
  - Ball 1 strike: ball 2 gets `All`. If ball 2 is also a strike, ball 3 gets `All`; otherwise ball 3 gets ball 2's `StandingAfter`.
  - Ball 1 non-strike: ball 2 gets `StandingAfter`. If it's a spare, ball 3 gets `All`; otherwise the frame ends.

## 5. Scoring (`ScoreCard`)

`ScoreCard(int frameCount)` accepts 3, 5 or 10 and throws `ArgumentOutOfRangeException` otherwise.

**Rules:**

- **Strike:** 10 plus the next two rolls.
- **Spare:** 10 plus the next roll.
- **Open frame:** the sum of its pins.
- **The final frame uses tenth-frame rules,** with up to 3 balls, whatever the frame count. The maximum score is 30 × N: 90, 150 or 300.

**API:**

- `AddRoll(int pins)`.
  - Throws `ArgumentOutOfRangeException` if `pins` is below 0 or above the pins standing for this ball.
  - Throws `InvalidOperationException` if the card is complete.
- `IsComplete`.
- `CurrentFrame` (1-based).
- `CurrentBall` (1–3).
- `PinsStanding` (for validation).
- `FrameScore(int frame)` returns `int?`: null until the frame's bonus rolls are known.
- `Cumulative(int frame)` returns `int?`.
- `RunningTotal`.
- `Marks(int frame)` returns `IReadOnlyList<RollMark>`.

**Marks:**

| Mark | When |
|---|---|
| `Strike` | 10 on a full rack |
| `Spare` | Clears the remaining pins on the frame's 2nd ball, or on the 3rd ball of the final frame after a non-strike ball 2 on a fresh rack |
| `Miss` | 0 pins |
| `Count` | Anything else |

**`RollOutcome.Mark`** is the mark of the roll just added.

**Test vectors (EditMode, all required):**

| Game | Rolls | Expected |
|---|---|---|
| Perfect, 10 frames | 12 × 10 | 300; cumulatives 30, 60, …, 300 |
| Gutter game | 20 × 0 | 0; all marks `Miss` |
| All 9–0 | (9, 0) × 10 | 90 |
| All spares 5/5 | (5, 5) × 10 + 5 | 150 |
| Strike/spare alternating | Frames 1–9 alternate X, 5/5 (X first); frame 10 = 5, 5, 10 | 200; every frame scores 20 |
| Classic sample | 10, 7/3, 9/0, 10, 0/8, 8/2, 0/6, 10, 10, 10/8/1 | Cumulatives 20, 39, 48, 66, 74, 84, 90, 120, 148, 167 |
| 3-frame perfect | 6 × 10 | 90 |
| 5-frame perfect | 8 × 10 | 150 |
| 3-frame open | (3, 4) × 3 | 21 |
| Validation | 7 then 4 in one frame | `ArgumentOutOfRangeException` |
| Validation | Roll after complete | `InvalidOperationException` |
| Pending bonus | X, then 3 | `FrameScore(1) == null` until the next roll |

## 6. Snapshots and events

- `SnapshotBuilder` creates a **new** `GameSnapshot` on every change. Published snapshots are never mutated, so the UI can keep references.
- **Event order for a scored roll:** `RollScored`, then `SnapshotChanged`, then (later) `StateChanged`.
- `StateChanged` fires on every transition, including Paused and resume. `SnapshotChanged` also fires on state changes, because the snapshot includes `State`.
- **Exceptions:** an exception thrown by a subscriber is caught, logged via `IGameLog.Error("Gameplay", …)`, and never breaks the state machine.

## 7. Leaderboard

At GameOver, each player submits `LeaderboardEntry { DisplayName, Score = RunningTotal, FrameCount, PlayedAtUtc = now }`. An abandoned game submits nothing. Reads for the Results screen go through `ILeaderboardStore.Top(frameCount, Today, 5)`.

## 8. Tests (EditMode with `Bowling.Testing` fakes)

- **ScoreCard:** all the vectors in section 5.
- **FrameRules:** every rack decision in section 4, including all final-frame branches.
- **GameStateMachine**, driven by `FakeClock`, `FakePlayerInput`, `FakeCalibrator`, `FakeThrowSource` and `FakeRollSimulator`:
  - **Happy path:** Attract → … → Results → Attract, for 1 player and 3 frames. Assert the exact state sequence and the `Arm`/`Roll`/`SetRack` calls.
  - **Multiplayer:** 2 players and 3 frames. Calibration runs every turn, and `turnChangeWait` applies.
  - **Rejected throw:** returns to AwaitReady and doesn't consume a ball.
  - **Calibration failing twice:** the default profile is used and the game continues.
  - **Pause:** after 2.0 s absent, then resume to the same state; or abandon after 15 s with no leaderboard write.
  - **Timeouts:** ModeSelect auto-selects after 20 s; absence in Results goes to Attract after 3 s.
  - **Operator commands:** `Pause`, `Resume`, and `Abandon` while Rolling (deferred).
  - **Strike or spare** uses `scoringHoldReplay`; an open frame uses `scoringHold`.
  - **Subscriber exceptions** are logged and don't break the flow.
- **SnapshotBuilder:** a published snapshot is unchanged after later rolls (reference equality plus a value check).

## 9. Acceptance criteria

1. All tests in section 8 pass.
2. `GameplayService.Tick()` allocates nothing in steady states (AwaitThrow, Rolling). Snapshots allocate only on change.
3. A full 10-frame, 4-player game simulated with fakes completes in under 1 s of test time, using `FakeClock`.

## 10. Decisions made here (Claude's calls, please check)

- **Short games use tenth-frame rules in their final frame.**
- **Recalibrate every turn in multiplayer,** with no identity check. Whoever steps in plays the current player's turn.
- **ModeSelect auto-starts a 1-player, 3-frame game after 20 s,** so walk-up players who don't use the menu still get to play.
- **No fouls, no splits shown.** Split marking is a possible later UI enhancement.
