#!/usr/bin/env bash
# Tests tools/dev/check-env.sh against throw-away Unity Hub editor trees.
# Run: bash tools/dev/test_check_env.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$ROOT/tools/lib/assert.sh"

CHECK_ENV="$ROOT/tools/dev/check-env.sh"
VERSION="6000.3.26f1"

# The editor root contains a space on purpose: check-env.sh must quote its paths.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
EDITORS="$WORK/hub editors"
APP="Unity.app/Contents"

# empty: no editor installed at all.
mkdir -p "$EDITORS/empty"

# fake: the pinned editor with the Mac IL2CPP module.
mkdir -p "$EDITORS/fake/$VERSION/$APP/MacOS"
printf '#!/bin/sh\nexit 0\n' > "$EDITORS/fake/$VERSION/$APP/MacOS/Unity"
chmod +x "$EDITORS/fake/$VERSION/$APP/MacOS/Unity"
mkdir -p "$EDITORS/fake/$VERSION/$APP/PlaybackEngines/MacStandaloneSupport/Variations/macos_arm64_player_development_il2cpp"

# partial: the pinned editor without the Mac IL2CPP module (Mono variation only).
mkdir -p "$EDITORS/partial/$VERSION/$APP/MacOS"
printf '#!/bin/sh\nexit 0\n' > "$EDITORS/partial/$VERSION/$APP/MacOS/Unity"
chmod +x "$EDITORS/partial/$VERSION/$APP/MacOS/Unity"
mkdir -p "$EDITORS/partial/$VERSION/$APP/PlaybackEngines/MacStandaloneSupport/Variations/macos_arm64_player_development_mono"

# No editor installed: exit 1 and say so.
assert_exit 1 env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/empty" "$CHECK_ENV"
assert_output_contains "MISSING Unity $VERSION" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/empty" "$CHECK_ENV"
assert_output_contains "MISSING Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/empty" "$CHECK_ENV"

# Editor and IL2CPP module installed.
assert_output_contains "OK Unity $VERSION" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/fake" "$CHECK_ENV"
assert_output_contains "OK Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/fake" "$CHECK_ENV"

# Editor installed, IL2CPP module not.
assert_output_contains "OK Unity $VERSION" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/partial" "$CHECK_ENV"
assert_output_contains "MISSING Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/partial" "$CHECK_ENV"

# UNITY_VERSION selects which editor is looked for.
assert_output_contains "MISSING Unity 6000.9.9f1" \
  env UNITY_VERSION=6000.9.9f1 UNITY_HUB_EDITORS_DIR="$EDITORS/fake" "$CHECK_ENV"

finish
