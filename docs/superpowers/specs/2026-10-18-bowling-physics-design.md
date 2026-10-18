# Sub-project 5: Bowling Physics

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 1 (foundation). Developed against `FakeThrowSource` and `DebugThrowSource` throws; no camera needed.
- **Module:** `Bowling.BowlingPhysics`

## 1. Purpose

This module simulates one roll: from a `ThrowEvent` at the foul line until the pins have settled. Results must be:

- believable, in real units;
- deterministic on a given machine;
- tunable for game feel.

It also reports milestones and impacts, so cameras and audio can react without depending on physics internals.

**Implements:** `IRollSimulator` and `IRollBodies` (Contracts 6.4).

## 2. Components

```
Modules/BowlingPhysics/Runtime/
├─ Components/
│  ├─ RollSimulator.cs          MonoBehaviour: implements IRollSimulator + IRollBodies; delegates to RollStateMachine
│  ├─ BallBody.cs               owns ball Rigidbody config + launch + park
│  ├─ PinBody.cs                owns one pin Rigidbody; spot placement; standing query
│  ├─ PinRack.cs                10 PinBody refs (index = pin number − 1); SetRack
│  ├─ HookForce.cs              FixedUpdate: applies HookModel lateral force to the ball
│  ├─ BumperRails.cs            enables/disables bumper colliders
│  ├─ ImpactReporter.cs         OnCollisionEnter on ball/pins → ImpactEvent (threshold, kind classification)
│  └─ LaneTriggers.cs           trigger volumes: Gutter L/R, PinDeckEntry, Pit
├─ Core/
│  ├─ RollStateMachine.cs       Idle → Rolling → Settling → Done (section 6)
│  ├─ HookModel.cs              pure: lateral acceleration from (z, hook)
│  ├─ PinStandingRule.cs        pure: standing test from pose
│  ├─ SettleRule.cs             pure: "everything still for N seconds"
│  └─ WorldBounds.cs            pure: NaN / out-of-bounds detection
└─ Config/
   └─ PhysicsTuning.cs          ScriptableObject; asset Config/BowlingPhysics/PhysicsTuning.asset
```

**Prefabs** (in `Assets/_Project/Prefabs/Physics/`):

- **`Lane.prefab`** contains:
  - the oil and dry surface colliders;
  - the gutters, bumpers (disabled by default), pin deck, kickbacks, pit and back cushion;
  - the trigger volumes;
  - a `PinRack` holding 10 `Pin` instances;
  - `RollSimulator`.
- **`Ball.prefab`** and **`Pin.prefab`** are the physics bodies.

The visual dressing lives in presentation's `Alley.prefab`. Physics prefabs only carry simple, flat-colored placeholder renderers (SOT-1). Presentation can swap the meshes without touching colliders.

## 3. World and geometry (lane frame, GC-5)

| Element | Geometry (from `LaneSpec`) |
|---|---|
| Lane surface | Box, top at y = 0, x ∈ ±`LaneWidthM`/2, z ∈ [−`ApproachLengthM`, `FoulLineToHeadPinM` − 0.15]. Split at z = `OilLengthM` into an **Oil** collider and a **Dry** collider. |
| Gutters | Half-pipe approximated by 3 boxes per side, top edge at y = 0, width `GutterWidthM`, floor at y = −0.045, running the full lane length into the pit |
| Bumpers | Box rails at x = ±(`LaneWidthM`/2 + 0.03), 0.10 m tall, the full lane length; disabled by default |
| Pin deck | Box, top at y = 0, z ∈ [`FoulLineToHeadPinM` − 0.15, `FoulLineToHeadPinM` + 0.87], x ∈ ±(`LaneWidthM`/2) |
| Kickbacks | Vertical walls at x = ±(`LaneWidthM`/2 + `GutterWidthM`), from the deck start to the pit end, 0.6 m tall |
| Pit | Floor at y = −0.25 from z = deck end to deck end + 0.8. Back cushion: a vertical box at the pit end, bounciness 0.05. |

**Layers:** `Ball`, `Pin`, `LaneSurface`, `Gutter`, `Bumper`, `Deck`, `Pit`, `Trigger`.

- All physical layers collide with each other.
- `Trigger` only meets `Ball` and `Pin`.
- Visual layers have no colliders.

## 4. Bodies and materials (initial values, tuned against section 9)

**Ball (`BallBody`):**

- `SphereCollider` with radius `BallDiameterM`/2 = 0.109.
- Mass `BallMassKg` (6.35). Drag 0. Angular drag 0.02.
- `maxAngularVelocity` = 100 rad/s. Unity's default of 7 would cap rolling at 0.76 m/s.
- Collision detection: `ContinuousDynamic`. Interpolation: `Interpolate`.

**Pin (`PinBody`):**

- A convex `MeshCollider` built from a simplified pin hull (64 vertices or fewer) at the `LaneSpec` dimensions.
- Mass `PinMassKg` (1.531). `centerOfMass` set to (0, 0.145, 0) above the base.
- Collision detection: `ContinuousSpeculative`. Interpolation: `Interpolate`.
- `solverIterations` 12, `solverVelocityIterations` 4.

| PhysicMaterial | Dynamic μ | Static μ | Bounciness | Friction combine | Bounce combine |
|---|---|---|---|---|---|
| Ball | 1.0 | 1.0 | 0.0 | Multiply | Average |
| LaneOil | 0.04 | 0.05 | 0.0 | Average | Average |
| LaneDry | 0.20 | 0.22 | 0.0 | Average | Average |
| Pin | 0.40 | 0.45 | 0.55 | Average | Average |
| Deck | 0.25 | 0.30 | 0.20 | Average | Average |
| Gutter, Kickback | 0.30 | 0.30 | 0.10 | Average | Average |
| Bumper | 0.10 | 0.10 | 0.30 | Average | Average |
| PitCushion | 0.80 | 0.80 | 0.05 | Average | Minimum |

Because the ball uses `Multiply` for friction, the ball-to-lane friction equals the lane's μ. In PhysX, Multiply takes priority over Average.

**Project physics settings** (`ProjectSettings/DynamicsManager.asset`, owned by this sub-project):

| Setting | Value |
|---|---|
| Gravity | −9.81 |
| Default solver iterations | 12 |
| Default solver velocity iterations | 4 |
| Bounce threshold | 1.0 |
| Sleep threshold | 0.005 |
| Default contact offset | 0.005 |
| Enhanced determinism | **on** |
| Auto sync transforms | off |
| Simulation mode | FixedUpdate |
| Friction type | Patch |
| Contact generation | PCM |

`Time.fixedDeltaTime` = 1/240 is already set by the foundation sub-project.

## 5. Launch and hook

**`Roll(throwEvent)`:**

1. Throw `InvalidOperationException` if `IsRolling`.
2. Activate the ball. Place it at (`StartOffsetM`, 0.109 + 0.001, −0.30), with no rotation.
3. Set the linear velocity to `SpeedMps` × (sin θ, 0, cos θ), where θ = `LaunchAngleDeg`.
4. Set the angular velocity to 0. The ball skids on oil and starts rolling in the dry zone through friction, as real balls do.
5. Raise `Milestone(Released)`. The state machine moves to `Rolling`.

**Hook (`HookModel`, applied by `HookForce` in FixedUpdate):**

```
ramp(z) = 0                                   for z < LaneSpec.OilLengthM
        = (z − OilLengthM) / hookRampM (1.5)  for OilLengthM ≤ z < OilLengthM + hookRampM
        = 1                                   afterwards
aX      = Hook × hookAccelMax (1.4 m/s²) × ramp(z)
F       = (BallMassKg × aX, 0, 0)  applied with ForceMode.Force at the ball centre
```

The force applies only while all of these hold:

- the ball is on the lane surface (not in a gutter, which `LaneTriggers` tracks);
- z < `FoulLineToHeadPinM` + 0.30;
- the roll state is `Rolling`.

A sanity check of these values: at 7 m/s with Hook = −1, the ball moves about 0.45 m in x by the head pin, with an entry angle of about 8°.

## 6. Roll state machine

```
Idle ──Roll()──► Rolling
Rolling:
   ball enters Gutter trigger      → Gutter = true, Milestone(EnteredGutter) (once)
   ball z ≥ FoulLineToHeadPinM − 0.15 → Milestone(ReachedPins) (once) → Settling
   ball enters Pit trigger         → Milestone(EnteredPit) → Settling (if not already)
   ball speed < 0.05 m/s for 1.0 s → EndReason = BallStopped → Settling
Settling:
   all active pins |v| < settleLinear (0.05 m/s) AND |ω| < settleAngular (0.5 rad/s) for settleHold (0.5 s)
      → EndReason = Settled (unless already BallStopped) → Done
   settleTimeout (4.0 s) after entering Settling → EndReason = Timeout → Done
Any state: rollTimeout (10 s) after Roll() → EndReason = Timeout → Done
Any state: WorldBounds violation by any body → log error, EndReason = Timeout → Done
Done: evaluate standing pins → raise Milestone(Settled), then Completed(RollResult) → Idle
```

**Ball in the pit:** 0.5 s after `EnteredPit`, the ball becomes kinematic and is deactivated, so it can't disturb the deck.

**Standing rule (`PinStandingRule`).** A pin that was standing before the roll is still standing after it if all of these hold:

- The angle between its up axis and world up is 12° or less.
- Its base is above y = −0.02, so it hasn't fallen into the pit or a gutter.
- Its base position is within the deck: |x| ≤ `LaneWidthM`/2 + 0.02, and z within the deck bounds.

A pin that was moved but is still upright counts as standing, as in real bowling.

**World bounds (`WorldBounds`):** a violation is a NaN, or |x| > 3, y < −2, y > 3, z < −6 or z > 25. Any pin outside the bounds counts as knocked down.

## 7. Rack (`SetRack`)

`SetRack(mask)` is instant and is only valid while `Idle`:

1. Every pin in `mask` is activated and teleported to `LaneSpec.PinSpot(n)` with no rotation and zero velocity, then put to sleep.
2. Every other pin is deactivated (removed by the "sweep").
3. The ball is deactivated (parked). Presentation shows its own visual ball in the player's hand.
4. `Standing` becomes `mask`.

A real pinsetter re-spots standing pins where they were lifted from. Here they go back to their original spots, which is a simplification.

**Bumpers:** `SetBumpers(enabled)` toggles the rail colliders and renderers. It is only valid while `Idle`.

## 8. Impacts

`ImpactReporter` raises `ImpactEvent` for collisions with an impulse of 0.05 N·s or more:

- **Kind** is classified from the layer pair: Ball–Pin is `BallPin`, Pin–Pin is `PinPin`, Pin–Deck is `PinDeck`, Pin–Pit is `PinPit`, Ball–LaneSurface is `BallLane` (only when the ball lands from a lofted release), Ball–Gutter is `BallGutter`, Ball–Bumper is `BallBumper`.
- **Rate limit:** at most 32 events per fixed step. Excess events are dropped, and the drops are counted in a debug counter.
- **Position** is the contact point in the lane frame.

## 9. Tuning targets (PlayMode acceptance tests)

Each test uses a fresh `SetRack(All)`. Seeded variations perturb the throw by ±0.01 m in offset, ±0.2° in angle and ±0.2 m/s in speed, using `System.Random(seed)`.

| ID | Throw | Pass when |
|---|---|---|
| T-1 | 8.0 m/s, 0°, offset 0, hook 0 (dead centre) | Reaches the pins in 2.0–2.6 s; knocks 5 or more pins |
| T-2 | Right-handed pocket: arrives at the head pin at x ∈ [+0.05, +0.08] with entry angle −3° to −6° (set up with offset +0.25, angle −0.2°, hook −0.6, 7.5 m/s, adjusted during tuning) | 50% or more strikes over 50 seeded variations |
| T-3 | offset +0.40, angle +3°, bumpers off | `Gutter == true`, `PinsKnocked == 0` |
| T-4 | T-3 with bumpers on | `Gutter == false`; the ball reaches the pins |
| T-5 | 1,000 random throws, uniformly distributed within the `LaneSpec` clamps and hook ±1 | No NaN; no body out of bounds; 0 cases where the ball centre passed within 0.05 m of a standing pin's axis without a `BallPin` impact (tunneling) |
| T-6 | `MinBallSpeedMps`, 0°, offset 0 | Reaches the pins (no `BallStopped`) |
| T-7 | `SetRack(P7 \| P10)` | Exactly pins 7 and 10 active, at their spots; `Standing == P7 \| P10` |
| T-8 | 7.5 m/s, 0°, offset 0: hook −1 vs 0 vs +1 | x at the head pin with hook −1 is at least 0.25 m less than with hook 0; with +1, at least 0.25 m more |
| T-9 | Determinism: T-2's base throw, 10 runs | Identical `StandingAfter` 10 out of 10; ball position at the head pin within 1e-4 m |
| T-10 | Performance: T-5 run with the Performance Testing package | Mean physics time of 1 ms or less per 60 fps frame on the reference hardware (run on device, not in CI) |

If the initial material values miss T-1 or T-2, tune `PhysicsTuning` and the materials in this order:

1. Pin bounciness.
2. Pin friction.
3. Pin center of mass.
4. Deck friction.

Record the final values in the plan notes.

## 10. Other tests (EditMode)

- `HookModel`: the ramp boundaries, the sign, and zero in the oil zone.
- `PinStandingRule`: the 11° and 13° tilt cases, the off-deck case, the pit case.
- `SettleRule`: the hold timing with `FakeClock`-style explicit time input.
- `WorldBounds`: NaN detection and each boundary.
- `RollStateMachine`: every transition and timeout, driven by synthetic inputs and without Unity physics.

## 11. Acceptance criteria

1. T-1 to T-9 pass in CI (PlayMode). T-10 passes on the reference hardware.
2. All EditMode tests pass.
3. The physics prefabs contain only flat-colored placeholder materials, and the SOT-1 validator passes.
4. `RollSimulator` is the only public entry point. The other components are wired by the `Lane` prefab and aren't referenced outside the module.

## 12. Decisions made here (Claude's calls, please check)

- **Hook as an explicit lateral force** in the dry zone, rather than relying on emergent spin-and-friction physics. It is predictable, tunable and testable, at the cost of some realism.
- **No sweep animation in v1.** The rack changes instantly while the bowler camera is about 18 m away (the presentation spec adds a clack sound). A sweep bar is deferred to v1.1.
- **Standing pins are re-spotted** to their original spots.
- **Two lane colliders** (oil and dry) with different friction, rather than a custom friction field.
