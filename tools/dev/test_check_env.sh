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

# make_editor <hub dir> <install dir name> <Mac player variation>: a complete fake install, with
# Info.plist, an executable editor binary and one Mac player variation (…_il2cpp or …_mono).
make_editor() {
  local contents="$1/$2/$APP"
  mkdir -p "$contents/MacOS" "$contents/PlaybackEngines/MacStandaloneSupport/Variations/$3"
  printf '<plist version="1.0"></plist>\n' > "$contents/Info.plist"
  printf '#!/bin/sh\nexit 0\n' > "$contents/MacOS/Unity"
  chmod +x "$contents/MacOS/Unity"
}

IL2CPP=macos_arm64_player_development_il2cpp
MONO=macos_arm64_player_development_mono

# empty: no editor installed at all.
mkdir -p "$EDITORS/empty"

# fake: the pinned editor with the Mac IL2CPP module.
make_editor "$EDITORS/fake" "$VERSION" "$IL2CPP"

# partial: the pinned editor without the Mac IL2CPP module (Mono variation only).
make_editor "$EDITORS/partial" "$VERSION" "$MONO"

# arm64only: the Apple Silicon install folder (<ver>-arm64) with the IL2CPP module.
make_editor "$EDITORS/arm64only" "$VERSION-arm64" "$IL2CPP"

# both: <ver>-arm64 (Mono only) and <ver> (with IL2CPP) are both valid. -arm64 wins, so the
# IL2CPP check looks inside it and finds no IL2CPP.
make_editor "$EDITORS/both" "$VERSION-arm64" "$MONO"
make_editor "$EDITORS/both" "$VERSION" "$IL2CPP"

# incomplete: <ver> has the binary and an IL2CPP folder, but no Info.plist (a damaged install).
make_editor "$EDITORS/incomplete" "$VERSION" "$IL2CPP"
rm "$EDITORS/incomplete/$VERSION/$APP/Info.plist"

# brokenplusarm: damaged <ver> next to a complete <ver>-arm64.
make_editor "$EDITORS/brokenplusarm" "$VERSION" "$IL2CPP"
rm "$EDITORS/brokenplusarm/$VERSION/$APP/Info.plist"
make_editor "$EDITORS/brokenplusarm" "$VERSION-arm64" "$IL2CPP"

# No editor installed: exit 1 and say so.
assert_exit 1 env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/empty" "$CHECK_ENV"
assert_output_contains "MISSING Unity $VERSION" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/empty" "$CHECK_ENV"
assert_output_contains "MISSING Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/empty" "$CHECK_ENV"

# Editor and IL2CPP module installed; the OK line names the resolved editor.
assert_output_contains "OK Unity $VERSION ($EDITORS/fake/$VERSION/$APP/MacOS/Unity)" \
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

# Only <ver>-arm64 is installed: found there, and the IL2CPP check looks inside it.
assert_output_contains "OK Unity $VERSION ($EDITORS/arm64only/$VERSION-arm64/$APP/MacOS/Unity)" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/arm64only" "$CHECK_ENV"
assert_output_contains "OK Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/arm64only" "$CHECK_ENV"

# Both installed and valid: <ver>-arm64 wins, and its (missing) IL2CPP is what counts.
assert_output_contains "OK Unity $VERSION ($EDITORS/both/$VERSION-arm64/$APP/MacOS/Unity)" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/both" "$CHECK_ENV"
assert_output_contains "MISSING Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/both" "$CHECK_ENV"

# Only a damaged <ver> (no Info.plist): not OK, the broken folder is named, and the IL2CPP module
# inside it doesn't count.
assert_exit 1 env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/incomplete" "$CHECK_ENV"
assert_output_contains "MISSING Unity $VERSION — incomplete install at $EDITORS/incomplete/$VERSION; repair or remove it in Unity Hub" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/incomplete" "$CHECK_ENV"
assert_output_contains "MISSING Mac IL2CPP module" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/incomplete" "$CHECK_ENV"

# Damaged <ver> next to a complete <ver>-arm64: the complete one is used and nothing is reported broken.
assert_output_contains "OK Unity $VERSION ($EDITORS/brokenplusarm/$VERSION-arm64/$APP/MacOS/Unity)" \
  env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/brokenplusarm" "$CHECK_ENV"
assert_exit 0 env UNITY_VERSION="$VERSION" UNITY_HUB_EDITORS_DIR="$EDITORS/brokenplusarm" \
  sh -c '! "$1" | grep -q incomplete' sh "$CHECK_ENV"

finish
