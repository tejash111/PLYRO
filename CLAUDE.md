# Motion Bowling (macOS)

A walk-up 3D bowling game for macOS. A camera watches the player's body, MediaPipe Pose tracks it, and a real bowling arm swing throws the ball. Built in Unity 6 (URP on Metal) with a native macOS tracking plugin.

The full design lives in `docs/superpowers/specs/`. Start with `2026-10-18-motion-bowling-system-design.md`.

## Sources of truth (non-negotiable)

These override everything else, including convenience, deadlines, and third-party defaults. Full definitions: system design spec, section 4.

- **SOT-1: No gradients.** No authored gradient anywhere: UI, USS, sprites, textures, materials, shaders, skybox, ambient light, particles, line/trail renderers, text, post-processing.
  - Every authored surface is one flat color at any instant.
  - Allowed: shading from real lights and shadows; fading a flat color over time; transform animations.
  - Banned list per asset type: system design, section 4.
  - Enforced by validators G-1 to G-6.
- **SOT-2: Modular, file- and component-based structure.**
  - Code lives in modules (`Assets/_Project/Modules/<Module>/`), each with its own assembly definition.
  - Modules talk only through `Bowling.Contracts`, and only `Bowling.App` wires them together.
  - One public type per file, and the file name matches the type name.
  - Components are small and single-purpose. UI components are folders of UXML + USS + C#.
  - Enforced by validators M-1 to M-8.

A change that breaks a source of truth isn't finished, even if it works.

## How we work: spec-driven with Superpowers

1. **Skills first.** Before any task, use the `finding-skills` project skill to pick the skills that apply. It also says what wins when a skill conflicts with these rules.
2. **No code without an approved spec and plan.**
   - Spec: `superpowers:brainstorming`, saved to `docs/superpowers/specs/`.
   - Plan: `superpowers:writing-plans`, saved to `docs/superpowers/plans/`.
3. **Build** with `superpowers:subagent-driven-development` (or `superpowers:executing-plans`).
   - Follow `superpowers:test-driven-development`: red, green, refactor.
   - Use `superpowers:using-git-worktrees` for isolation.
4. **Finish** with `superpowers:verification-before-completion` and `superpowers:requesting-code-review` before claiming done.
5. **Spec changes.** If a spec turns out wrong, update the spec first, then the plan, then the code.

## Module map (dependency rule)

Every module references `Bowling.Contracts` only. `Bowling.App` is the single composition root and references everything.

| Module | Assembly | Owns |
|---|---|---|
| Contracts | `Bowling.Contracts` | Shared data types, interfaces, lane geometry constants |
| PoseInput | `Bowling.PoseInput` | Native tracking bridge, filtering, player lock, calibration, hand cursor, recording/replay |
| ThrowDetection | `Bowling.ThrowDetection` | Swing state machine, ball parameter mapping, assist |
| BowlingPhysics | `Bowling.BowlingPhysics` | Lane, ball, pins, hook, settle, rack reset |
| Gameplay | `Bowling.Gameplay` | Game state machine, scoring, modes, players |
| Presentation | `Bowling.Presentation` | Camera director, replays, VFX, audio |
| UI | `Bowling.UI` | UI Toolkit components, screens, HUD |
| Platform | `Bowling.Platform` | Kiosk, power, storage, logging, settings |
| App | `Bowling.App` | Bootstrap, wiring, update order |
| Tooling | `Bowling.Tooling.Editor` | Source-of-truth validators (editor only) |

Native code lives in `native/TrackingCore` (camera + MediaPipe C++) and `native/MacPlatform` (kiosk/power).

## Skills

| Source | Skills | Role |
|---|---|---|
| Project (`.claude/skills/`) | `finding-skills` | Routes every task to the right skills; precedence; vetting new skills |
| Superpowers plugin | `superpowers:*` | Process: brainstorm → spec → plan → TDD → verify → review |
| Official Unity plugin (`unity@unity-agent-plugin`) | `unity:ui-uitk`, `unity:urp-postprocessing`, `unity:physics-3d-collision`, `unity:unity-cli`, `unity:unity-package-management`, … | Unity mechanics only; these never override the sources of truth |

Add a project skill only through `superpowers:writing-skills`, and only after the same lesson has come up more than once.

## Adding a module

A new module needs all of these, in one change:

1. A spec update: system design section 6 (any new contracts) and the foundation section 4 table.
2. `Modules/<Name>/` with a README and asmdefs per foundation section 4.
3. An entry in `tools/checks/module-rules.json`.
4. Wiring in `GameBootstrap`, plus an `ExecutionOrder` entry if the module has a driver.
5. A row in the module map above.

## Conventions

- **C#:**
  - Namespaces match the asmdef `rootNamespace`: `Bowling.<Module>`, `Bowling.<Module>.Editor`, `Bowling.<Module>.Tests`.
  - Use `PascalCase` for types and members, `_camelCase` for private fields.
  - One public type per file. Soft limit 300 lines per file, hard limit 400.
- **Units:** SI everywhere (metres, seconds, kilograms, radians unless the name says `Deg`). Lane frame: origin at the center of the foul line, +Z down the lane, +X to the bowler's right, +Y up.
- **Tuning values** live in ScriptableObject assets under `Assets/_Project/Config/`, never as magic numbers in code.
- **Privacy:** camera images are never written to disk or sent anywhere.
- **Install the pre-commit hook once per clone:** `bash tools/checks/install-hooks.sh`.
- **Check the dev machine:** `bash tools/dev/check-env.sh` (setup steps are in `docs/dev-setup.md`).
- **Run Unity tests:** `tools/ci/run_unity_tests.sh <EditMode|PlayMode> [assemblyNames]`. It runs headless and judges the NUnit XML, so it prints `PASS total=N` or `FAIL …`. Never call the Unity binary directly: `tools/ci/unity.sh` resolves the pinned editor.
- **Before claiming work is done:** `bash tools/ci/verify.sh` (all gates and tests; prints `VERIFY OK`).
