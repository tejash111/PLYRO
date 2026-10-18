# Motion Bowling

A walk-up 3D bowling game for macOS. Stand in front of the screen, swing your arm like a real bowler, and the ball rolls down the lane. Body tracking uses MediaPipe Pose through a native macOS plugin. The game is built in Unity 6.

Status: **design approved; planning.** The specs in `docs/superpowers/specs/` are approved, and implementation plans go in `docs/superpowers/plans/`. There is no code yet.

## Where to start

1. Read [the system design](docs/superpowers/specs/2026-10-18-motion-bowling-system-design.md).
2. Read [CLAUDE.md](CLAUDE.md) for the sources of truth and the workflow.

## Verify

```bash
bash tools/ci/verify.sh
```

This is the single check for local runs and for CI. It stops at the first failure and ends with `VERIFY OK` (exit 0). The steps:

1. Source-of-truth text checks (`tools/checks/check_sources_of_truth.sh --all`).
2. The shell test suites: `tools/checks/test_checks.sh`, `tools/ci/test_ci_scripts.sh`, `tools/dev/test_check_env.sh` and `tools/ci/test_verify.sh`.
3. The editor validators in Unity batch mode, and no `warning CS` from `Assets/_Project` in the editor log.
4. Unity EditMode tests.
5. Unity PlayMode tests.
6. Every path in `tools/checks/required-files.txt` exists.

Steps 3 to 5 start the Unity editor: close any Unity window that has `unity/MotionBowling` open first. A run in a fresh checkout imports the whole project and is slow. Logs and results go to `unity/MotionBowling/Logs/ci/`. The setup is in [docs/dev-setup.md](docs/dev-setup.md); the CI workflow is `.github/workflows/verify.yml`.

## Specs

| # | Spec | Build order |
|---|---|---|
| 0 | [System design](docs/superpowers/specs/2026-10-18-motion-bowling-system-design.md) | Umbrella |
| S0 | Tracking feasibility spike (throwaway; see system design section 11) | First, in parallel with 1 |
| 1 | [Project foundation](docs/superpowers/specs/2026-10-18-project-foundation-design.md) | First |
| 2 | [TrackingCore native plugin](docs/superpowers/specs/2026-10-18-tracking-core-design.md) | After 1 |
| 3 | [Pose input](docs/superpowers/specs/2026-10-18-pose-input-design.md) | After 1 |
| 4 | [Throw detection](docs/superpowers/specs/2026-10-18-throw-detection-design.md) | After 3 + S0 recordings |
| 5 | [Bowling physics](docs/superpowers/specs/2026-10-18-bowling-physics-design.md) | After 1 |
| 6 | [Gameplay and scoring](docs/superpowers/specs/2026-10-18-gameplay-design.md) | After 1 |
| 7 | [UI](docs/superpowers/specs/2026-10-18-ui-design.md) | After 1 |
| 8 | [Presentation](docs/superpowers/specs/2026-10-18-presentation-design.md) | After 5 |
| 9 | [Platform and release](docs/superpowers/specs/2026-10-18-platform-release-design.md) | After 1, finished last |

## Developer setup (Claude Code)

The project enables these plugins in `.claude/settings.json`, so you'll be asked to install them when you trust the folder:

- **superpowers** (Anthropic's official marketplace) runs the spec-driven workflow: brainstorm, spec, plan, TDD, review.
- **unity** (`unity@unity-agent-plugin`, Unity's official plugin) provides Unity-authored skills for UI Toolkit, URP, physics, packages and the Unity CLI. Skills that drive the Editor also need the [Unity CLI](https://docs.unity.com/en-us/unity-cli/use-unity-cli).
- **csharp-lsp** gives C# code intelligence. It needs `brew install csharp-ls` or `dotnet tool install --global csharp-ls`.

The project skill `.claude/skills/finding-skills` routes each task to the right skills. It also settles conflicts: when a skill's advice clashes with the sources of truth in `CLAUDE.md`, the sources of truth win.
