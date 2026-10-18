#!/usr/bin/env bash
# Checks that this Mac can build and test Motion Bowling.
#
# Prints one line per check: "OK <what>" or "MISSING <what> — <fix>", where the fix names the
# matching section of docs/dev-setup.md. Exits 1 if anything is missing, otherwise 0.
#
# The editor is looked up like tools/ci/unity.sh does (shared code in tools/lib/unity_editor.sh):
# <version>-arm64 first, then <version>, under the Hub's editors directory. A folder counts only if
# it has Unity.app/Contents/Info.plist and an executable MacOS/Unity. The Mac IL2CPP check looks
# inside the editor that was found.
#
# Environment:
#   UNITY_VERSION           Unity editor version to look for. Overrides tools/ci/unity-version.txt.
#   UNITY_HUB_EDITORS_DIR   Where Unity Hub installs editors (default /Applications/Unity/Hub/Editor).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION_FILE="$SCRIPT_DIR/../ci/unity-version.txt"
DEFAULT_UNITY_VERSION="6000.3.26f1"   # used until tools/ci/unity-version.txt exists
UNITY_HUB_APP="/Applications/Unity Hub.app"

# shellcheck source=../lib/unity_editor.sh
source "$SCRIPT_DIR/../lib/unity_editor.sh"

EDITORS_DIR="${UNITY_HUB_EDITORS_DIR:-/Applications/Unity/Hub/Editor}"

version="${UNITY_VERSION:-}"
if [ -z "$version" ] && [ -f "$VERSION_FILE" ]; then
  version="$(tr -d '[:space:]' < "$VERSION_FILE")"
fi
version="${version:-$DEFAULT_UNITY_VERSION}"

# Sets unity_editor_bin / unity_editor_app (empty if no valid install), see tools/lib/unity_editor.sh.
unity_find_editor "$EDITORS_DIR" "$version" || true

missing_count=0

ok() {
  printf 'OK %s\n' "$1"
}

# missing <what> <dev-setup section>
missing() {
  printf 'MISSING %s — see docs/dev-setup.md, section "%s"\n' "$1" "$2"
  missing_count=$((missing_count + 1))
}

# missing_with_fix <what> <fix>
missing_with_fix() {
  printf 'MISSING %s — %s\n' "$1" "$2"
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
  if [ -n "$unity_editor_bin" ]; then
    ok "Unity $version ($unity_editor_bin)"
  elif [ -n "$unity_editor_broken_dir" ]; then
    # Only a damaged folder: macOS kills its binary at launch, so this is not an installed editor.
    missing_with_fix "Unity $version" "incomplete install at $unity_editor_broken_dir; repair or remove it in Unity Hub (see docs/dev-setup.md, section \"Unity Hub and editor\")"
  else
    missing "Unity $version" "Unity Hub and editor"
  fi
}

# The Mac IL2CPP module installs a Variations/*il2cpp* directory next to the Mono ones, inside the
# editor that was found. Without a valid editor there is nothing to look in.
check_mac_il2cpp_module() {
  local variation variations_dir
  if [ -n "$unity_editor_app" ]; then
    variations_dir="$unity_editor_app/Contents/PlaybackEngines/MacStandaloneSupport/Variations"
    for variation in "$variations_dir"/*il2cpp*; do
      if [ -d "$variation" ]; then
        ok "Mac IL2CPP module"
        return 0
      fi
    done
  fi
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
