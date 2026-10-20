# Sub-project 6: Gameplay and Scoring

- **Status:** Approved by Tejash on 2026-10-18; plan-time refinements approved on 2026-10-20 with the plan `docs/superpowers/plans/2026-10-19-gameplay.md`
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
│  ├─ RackDecision.cs            FrameRules result: frame complete, next rack, next rack fresh (P-2)
│  ├─ PresenceTracker.cs         when presence last became false; AbsentFor(now) (P-3)
│  ├─ PauseTracker.cs            resume state, deferred operator pause, absence seen while paused (P-5, P-6)
│  ├─ GamePublisher.cs           raises the game events in the section 6 order, one subscriber at a time (P-8)
│  ├─ LeaderboardWriter.cs       submits one LeaderboardEntry per player at GameOver (P-12)
│  ├─ CalibrationRun.cs          the Calibrate attempt: timeout count, running flag, Begin/Cancel calls (P-4)
│  ├─ StateEntryActions.cs       on-enter effects on throw source and simulator (the Attract guard; AwaitReady's rack, bumpers, Arm)
│  ├─ LobbyFlow.cs               the Attract, Join and ModeSelect rules: presence, the greeting and the ModeSelect timers (P-3)
│  ├─ GameEndFlow.cs             the GameOver and Results rules: the leaderboard submission, the holds, Results absence, play again (P-6, P-12)
│  ├─ TurnFlow.cs                the Calibrate, AwaitReady, AwaitThrow and Rolling inputs: calibration results, swing phases, throws, rejections, roll results (P-4, P-10)
│  ├─ PauseFlow.cs               Paused, the pause rule and the operator commands: resume, abandon, the turn-change wait, the deferred Pause and Abandon (P-3, P-5, P-6)
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
| Attract | `throwSource.Disarm()`; then, only if `!sim.IsRolling` (both calls throw while a roll is in progress): `sim.SetBumpers(false)` and `sim.SetRack(All)` | `input.IsPlayerPresent` becomes true | Join |
| Join | — | `joinGreeting` (1.0 s) elapses; or presence false | ModeSelect; or Attract |
| ModeSelect | Selection preset to 1 player, 3 frames, default difficulty from settings | `SelectMode(...)` is called; or `modeSelectTimeout` (20 s) passes with the player present, which auto-selects the preset; or presence false for `modeSelectAbsent` (5 s) | Calibrate; Calibrate; Attract |
| Calibrate | `calibrator.Begin()` | `Completed(profile)` stores the profile; on the first `Failed(Timeout)`, `Begin()` runs again; on a second `Failed(Timeout)`, `DefaultProfiles.For(...)` is used | AwaitReady; Calibrate; AwaitReady |
| AwaitReady | `sim.SetBumpers(difficulty.Bumpers)`; `sim.SetRack(rackForThisBall)`; `throwSource.Arm(context)` | `throwSource.Phase` becomes Ready, Backswing or ForwardSwing | AwaitThrow |
| AwaitThrow | — | `Thrown(evt)` calls `sim.Roll(evt)`; `Rejected` | Rolling; AwaitReady (re-armed; the ball isn't consumed) |
| Rolling | — | `sim.Completed(result)` | Scoring |
| Scoring | `ScoreCard.AddRoll(result.PinsKnocked)`; build and assign the new `Current` (State = Scoring); then raise `StateChanged(Scoring)`, `RollScored` and `SnapshotChanged`, in that order | `scoringHold` (1.5 s) elapses, or `scoringHoldReplay` (4.5 s) when the mark is a Strike or Spare (presentation replay 3.5 s or less, plus 1.0 s) | the next ball's AwaitReady; the next player's Calibrate or AwaitReady; or GameOver |
| Paused | `throwSource.Disarm()`; remember the resume state | Presence true again: resume, re-entering that state (Calibrate restarts; AwaitReady re-arms); or `abandonAfterPause` (15 s) elapses; or `Resume()` | (resume state); Attract (no leaderboard entry); (resume state) |
| GameOver | Submit one `LeaderboardEntry` per player | `gameOverHold` (2.0 s) elapses | Results |
| Results | — | `resultsHold` (10 s) elapses; or presence false for `resultsAbsent` (3 s); or `SelectMode(...)` ("Play again") | Attract; Attract; Calibrate (new game, same players count) |

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
| `Strike` | 10 on a fresh rack (ball 1, or a final-frame reset) |
| `Spare` | Clears every standing pin on a rack that is not fresh |
| `Miss` | 0 pins |
| `Count` | Anything else |

A rack is *fresh* at ball 1, and after a final-frame reset (a strike or spare). Every other rack is a leftover.

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
| 3-frame perfect | 5 × 10 | 90 |
| 5-frame perfect | 7 × 10 | 150 |
| 3-frame open | (3, 4) × 3 | 21 |
| Gutter then ten | 3 frames: 0, 10, 5, 0, 3, 4 | frame 1 marks Miss, Spare; total 27 |
| Final X, 0, 10 | 3 frames: 0, 0, 0, 0, 10, 0, 10 | final marks Strike, Miss, Spare; total 20 |
| Validation | 7 then 4 in one frame | `ArgumentOutOfRangeException` |
| Validation | Roll after complete | `InvalidOperationException` |
| Pending bonus | X, then 3 | `FrameScore(1) == null` until the next roll |

## 6. Snapshots and events

- `SnapshotBuilder` creates a **new** `GameSnapshot` on every change. Published snapshots are never mutated, so the UI can keep references.
- **Rule:** every transition assigns `Current` first, then raises `StateChanged`, then `SnapshotChanged`. Handlers therefore always see the new `Current`.
- **Event order for a scored roll:** `Current` is already the new snapshot (State = Scoring) before any event, then `StateChanged(Scoring)`, then `RollScored`, then `SnapshotChanged`.
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

## 11. Plan-time refinements (2026-10-19, approved with the plan)

Writing the plan (`docs/superpowers/plans/2026-10-19-gameplay.md`) surfaced one error and several open points in sections 1 to 10. Each item is a decision the plan relies on. Where an item differs from an earlier section, this section wins.

- **P-1 (error in §5):** an N-frame perfect game is N + 2 strikes.
  - 3-frame perfect is **5 × 10** (90). 5-frame perfect is **7 × 10** (150).
  - The earlier drafts of these two vectors had one strike too many. The extra roll would end in `InvalidOperationException`.
- **P-2 (marks):** a rack is *fresh* at ball 1, and after a final-frame reset (a strike or spare).
  - **Strike:** 10 pins on a fresh rack.
  - **Spare:** clears every standing pin on a rack that isn't fresh. So `0, 10` is a Spare, and the final frame `X, 0, 10` reads Strike, Miss, Spare.
  - **Miss:** 0 pins. **Count:** anything else.
  - `FrameRules` takes freshness as an input, because a mask can't tell a fresh rack from one where ball 1 hit nothing.
- **P-3 (absence and timers):**
  - "Presence false for X" means continuous absence of at least X. It is measured from the moment presence last became false (`PresenceChanged`, with a poll each Tick as a backstop), and it carries across state changes. Presence returning resets it.
  - Every timer fires on the first Tick at which the elapsed time is at least its duration.
  - At most one timer-driven transition happens per Tick.
- **P-4 (Calibrate):**
  - `Begin()` runs on entry only if the player is present.
  - Any Tick in Calibrate with the player present and no calibration running calls `Begin()`. That covers entering while absent, and the end of a `Failed(PlayerLeft)` or `Failed(TrackingLost)`.
  - Only `Failed(Timeout)` counts toward the two-failure fallback. The count resets on every Calibrate entry, including a resume.
  - A *turn-change* Calibrate uses `turnChangeWait` as its pause threshold. That is a new turn in a game of 2 or more players, not the first turn.
- **P-5 (Paused):**
  - On enter, Paused also calls `calibrator.Cancel()`.
  - The resume state is the state it paused from, except that AwaitThrow resumes as AwaitReady, which re-arms.
  - It resumes when the player is present after having been absent at some point during this pause, or on `Resume()`.
  - It abandons to Attract after `abandonAfterPause` of absence, counted from the later of the pause entry and the start of the absence. So an operator pause with the player present lasts until `Resume()`.
- **P-6 (operator commands):**
  - `Pause()` in Rolling or Scoring is deferred to the first Tick after the next state is entered. A pause that begins (from the presence rule or a direct `Pause()`) satisfies a pending deferred `Pause()`, so it is not applied again.
  - Resuming into GameOver doesn't submit again: there is one submission per game.
  - Attract on enter also calls `calibrator.Cancel()`.
  - A command where it doesn't apply is ignored and logged `Info("Gameplay", "<Command> ignored in <State>")`: `SelectMode` outside ModeSelect and Results, `Pause` in Attract, Results or Paused, `Resume` outside Paused, and `Abandon` in Attract.
  - `SelectMode` validates first, in any state: players 1–4, frames 3, 5 or 10, and a defined `Difficulty`. Anything else throws `ArgumentOutOfRangeException`.
  - "Play again" is `SelectMode` in Results with whatever arguments the UI passes.
- **P-7 (run to completion):**
  - `Tick`, the four commands and every dependency event handler run to completion.
  - A call that arrives while another is running is queued, and runs after it in call order. Examples: a subscriber calling `Abandon()` from `StateChanged`, or a dependency raising an event from inside a Gameplay call.
  - So every transition raises its full event sequence before the next one begins.
- **P-8 (subscriber isolation):** each subscriber of each event is invoked on its own. One that throws is logged as `Error("Gameplay", "<EventName> subscriber failed", exception)`, and the others still run.
- **P-9 (scoring against the rack):**
  - The pin count is `(rack & ~StandingAfter).Count()`, and the next rack comes from `rack & StandingAfter`.
  - A `StandingBefore` that differs from the rack is logged as a warning.
  - `RollOutcome.Result` is the simulator's result, unchanged.
- **P-10 (unexpected inputs):**
  - `Thrown` is also accepted in AwaitReady (the source passed Ready within one frame).
  - `Rejected` in AwaitReady is ignored, because the source is already waiting.
  - These are ignored and logged `Warn("Gameplay", "<Event> ignored in <State>")`: `Thrown` or `Rejected` anywhere else, `RollCompleted` outside Rolling, and `CalibrationCompleted` or `CalibrationFailed` outside Calibrate.
- **P-11 (snapshots):**
  - During Scoring, `CurrentPlayer`, `CurrentFrame` and `CurrentBall` name the roll just scored. They move on when Scoring ends.
  - *Lobby* snapshots have no players, `CurrentPlayer` 0, `CurrentFrame` 1 and `CurrentBall` 1, with `FrameCount = PresetFrameCount` and `Difficulty` set to the preset difficulty. They are Attract, Join, ModeSelect, and Paused when paused from Join or ModeSelect.
  - The preset difficulty is read from settings at construction and on each ModeSelect entry.
  - Attract drops the session.
  - The constructor enters Attract: it runs Attract's on-enter actions and assigns `Current`, but raises no events.
- **P-12 (leaderboard):**
  - All entries of one game share one `PlayedAtUtc`, from an injected `Func<DateTime> utcNow`.
  - A `Submit` that throws is logged as an Error. The other entries are still submitted, and the game still reaches Results.
- **P-13 (tuning):**
  - New `GameplayTuning` fields: `ResultsAbsent` 3.0 s (the Results row's `resultsAbsent`), `PresetPlayerCount` 1 and `PresetFrameCount` 3.
  - The default profile `DefaultProfiles.For(tuning)` uses: Right hand, `ArmLengthM` 0.60, `ShoulderWidthM` 0.38, `NeutralImageX` 0.5.
  - The assets live at `Assets/_Project/Config/Gameplay/GameplayTuning.asset` and `DifficultyCatalog.asset`.
- **P-14 (components, §2):** eleven more `Core/` types: `RackDecision`, `PresenceTracker`, `PauseTracker`, `GamePublisher`, `LeaderboardWriter`, `CalibrationRun`, `StateEntryActions`, `LobbyFlow`, `GameEndFlow`, `TurnFlow` and `PauseFlow`.
