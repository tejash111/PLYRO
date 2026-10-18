#!/usr/bin/env bash
# Checks that this Mac can build and test Motion Bowling.
#
# Prints one line per check: "OK <what>" or "MISSING <what> — <fix>", where the fix names the
# matching section of docs/dev-setup.md. Exits 1 if anything is missing, otherwise 0.
#
# Environment:
#   UNITY_VERSION           Unity editor version to look for. Overrides tools/ci/unity-version.txt.
#   UNITY_HUB_EDITORS_DIR   Where Unity Hub installs editors (default /Applications/Unity/Hub/Editor).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/../ci/unity-version.txt"
DEFAULT_UNITY_VERSION="6000.3.26f1"   # used until tools/ci/unity-version.txt exists
UNITY_HUB_APP="/Applications/Unity Hub.app"

EDITORS_DIR="${UNITY_HUB_EDITORS_DIR:-/Applications/Unity/Hub/Editor}"

version="${UNITY_VERSION:-}"
if [ -z "$version" ] && [ -f "$VERSION_FILE" ]; then
  version="$(tr -d '[:space:]' < "$VERSION_FILE")"
fi
version="${version:-$DEFAULT_UNITY_VERSION}"

EDITOR_APP="$EDITORS_DIR/$version/Unity.app"
EDITOR_BIN="$EDITOR_APP/Contents/MacOS/Unity"
MAC_VARIATIONS_DIR="$EDITOR_APP/Contents/PlaybackEngines/MacStandaloneSupport/Variations"

missing_count=0

ok() {
  printf 'OK %s\n' "$1"
}

# missing <what> <dev-setup section>
missing() {
  printf 'MISSING %s — see docs/dev-setup.md, section "%s"\n' "$1" "$2"
  missing_count=$((missing_count + 1))
}

check_xcode() {
  local developer_dir
  developer_dir="$(xcode-select -p 2>/dev/null || true)"
  if [[ "$developer_dir" == */Xcode.app/Contents/Developer ]] && xcodebuild -version >/dev/null 2>&1; then
    ok "Xcode"
  else
    missing "Xcode" "Xcode"
  fi
}

check_git_lfs() {
  if git lfs version >/dev/null 2>&1; then
    ok "Git LFS"
  else
    missing "Git LFS" "Git LFS"
  fi
}

check_unity_hub() {
  if [ -d "$UNITY_HUB_APP" ]; then
    ok "Unity Hub"
  else
    missing "Unity Hub" "Unity Hub and editor"
  fi
}

check_unity_editor() {
  if [ -f "$EDITOR_BIN" ] && [ -x "$EDITOR_BIN" ]; then
    ok "Unity $version"
  else
    missing "Unity $version" "Unity Hub and editor"
  fi
}

# The Mac IL2CPP module installs a Variations/*il2cpp* directory next to the Mono ones.
check_mac_il2cpp_module() {
  local variation
  for variation in "$MAC_VARIATIONS_DIR"/*il2cpp*; do
    if [ -d "$variation" ]; then
      ok "Mac IL2CPP module"
      return 0
    fi
  done
  missing "Mac IL2CPP module" "Unity Hub and editor"
}

check_xcode
check_git_lfs
check_unity_hub
check_unity_editor
check_mac_il2cpp_module

if [ "$missing_count" -gt 0 ]; then
  exit 1
fi
