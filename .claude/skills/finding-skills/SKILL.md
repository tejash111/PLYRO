---
name: finding-skills
description: Use at the start of any task in the Motion Bowling repository, before planning or writing code; when a skill named in CLAUDE.md, a spec or a plan is not in your available skills; when a skill's advice conflicts with CLAUDE.md or a spec; or when someone proposes installing a new skill, plugin or skill repository.
---

# Finding skills

## Overview

Pick skills from what is **actually installed**, use each one for what it's good at, and never let a skill override the project's sources of truth.

## Precedence (highest first)

1. `CLAUDE.md` sources of truth: SOT-1 (no gradients) and SOT-2 (modular structure).
2. Approved specs (`docs/superpowers/specs/`) and plans (`docs/superpowers/plans/`).
3. `superpowers:*` skills decide **process**.
4. `unity:*` skills (the official Unity plugin) decide **mechanics**: APIs, menus, settings, CLI usage.

When a skill's recommendation conflicts with 1 or 2, follow 1 or 2, use the skill only for mechanics, and tell the user about the conflict in one sentence. Example: `unity:urp-postprocessing` suggests Vignette and Bloom, but the spec allows Tonemapping Neutral only, so the profile gets Tonemapping only.

## Routing table

| Situation | Skills, in order |
|---|---|
| New feature, no approved spec | `superpowers:brainstorming` → after spec approval, `superpowers:writing-plans` |
| Approved spec, no plan | `superpowers:writing-plans` |
| Approved plan | `superpowers:using-git-worktrees` → `superpowers:subagent-driven-development` (or `superpowers:executing-plans`) → in each task, `superpowers:test-driven-development` plus the domain skills below → `superpowers:verification-before-completion` → `superpowers:requesting-code-review` → `superpowers:finishing-a-development-branch` |
| Bug, failing test, odd behavior | `superpowers:systematic-debugging` |
| UI Toolkit (UXML, USS, C# elements) | `unity:ui-uitk` |
| URP volumes, post-processing | `unity:urp-postprocessing` (mechanics only; SOT-1 decides the overrides) |
| URP renderer features | `unity:validate-urp-render-graph-renderer-feature` |
| Shader Graph custom nodes | `unity:shader-graph-create-custom-node`, plus an entry in `tools/checks/reviewed-shaders.txt` |
| Colliders, rigidbodies, layers | `unity:physics-3d-collision` |
| Adding or upgrading packages | `unity:unity-package-management`, then update the foundation spec's package table and `tools/checks/module-rules.json` |
| Driving the Editor or batch mode from the terminal | `unity:unity-cli` |
| Native plugins (TrackingCore, MacPlatform) | No skill. Follow the C API rules in the tracking-core spec, section 6. |
| A lesson that keeps recurring | `superpowers:writing-skills` (a new project skill, tested first) |

**Not for this project** (non-goals or decided elsewhere): `unity:new-unity-project` (the foundation spec defines the project), `unity:ui-ugui`, 2D and tilemap skills, IAP, LevelPlay, live ops, multiplayer, web, localization, and `unity:audio-*` (audio uses FMOD). The exception is IMGUI, which is allowed for `BOWLING_DEV` debug overlays.

## A skill named somewhere isn't installed

Say so in one line, use this routing table instead, and never plan around a skill you can't load. If the official Unity plugin is missing, ask the user to install it (below).

## Adding skills or plugins

**Trusted sources:**

- Anthropic's `claude-plugins-official` marketplace.
- Unity's `Unity-Technologies/unity-agent-plugin` marketplace (plugin `unity@unity-agent-plugin`).
- Both are enabled in `.claude/settings.json`.

**Anything else** (community repos, `npx skills add …`, `install.sh`):

1. Don't run its installer yourself.
2. Read its `SKILL.md` and scripts as text only, and list for the user: the source URL, what it adds, which commit to pin, and any conflicts with SOT-1, SOT-2 or the specs.
3. The user decides and installs it.
4. Afterwards, add it to the skills table in `CLAUDE.md`.
