#!/usr/bin/env bash
# Resolves the pinned Unity editor and runs it headless against unity/MotionBowling.
#
# Usage:
#   unity.sh path              print the editor binary
#   unity.sh create            create the project; exits 1 if the project directory already exists
#   unity.sh run <args...>     run the editor with
#                                -batchmode -nographics -projectPath <project> -logFile <log> <args...>
#                              and exit with the editor's exit code
#   unity.sh exec <Method>     run -executeMethod <Method> (the method must call EditorApplication.Exit)
#
# Editor resolution:
#   Version: m_EditorVersion in <project>/ProjectSettings/ProjectVersion.txt if that file exists,
#            else tools/ci/unity-version.txt. If both exist and differ, that is an error.
#   Path:    $UNITY_EDITOR, if set, which wins and must be an executable file. Otherwise the first valid
#            of $UNITY_HUB_EDITORS_DIR/<version>-arm64 and $UNITY_HUB_EDITORS_DIR/<version>, each with
#            Unity.app/Contents/MacOS/Unity (UNITY_HUB_EDITORS_DIR defaults to
#            /Applications/Unity/Hub/Editor, as in tools/dev/check-env.sh). A folder is valid only if
#            it has Unity.app/Contents/Info.plist and an executable MacOS/Unity; see
#            tools/lib/unity_editor.sh, which check-env.sh shares.
#
# Environment:
#   UNITY_EDITOR            Full path of the editor binary. Overrides the Hub lookup.
#   UNITY_HUB_EDITORS_DIR   Where Unity Hub installs editors.
#   UNITY_PROJECT_DIR       Absolute path of the Unity project. Default: unity/MotionBowling in this repo
#                           (found from this script's location, so any working directory works).
#   UNITY_LOG_NAME          Log file name for run/exec: <project>/Logs/ci/<name>.log (default "run").
#
# Exit codes: 2 for a missing, incomplete or mismatched editor, a missing project, or bad usage. 1 if create finds
# the project directory already there or Unity creates nothing. run/exec pass the editor's code through.
# Errors go to stderr as "ERROR: ...".
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PIN_FILE="$SCRIPT_DIR/unity-version.txt"
PROJECT_DIR="${UNITY_PROJECT_DIR:-$REPO_ROOT/unity/MotionBowling}"
PROJECT_VERSION_FILE="$PROJECT_DIR/ProjectSettings/ProjectVersion.txt"
LOG_DIR="$PROJECT_DIR/Logs/ci"
EDITORS_DIR="${UNITY_HUB_EDITORS_DIR:-/Applications/Unity/Hub/Editor}"

# shellcheck source=../lib/unity_editor.sh
source "$SCRIPT_DIR/../lib/unity_editor.sh"

VERSION=""
EDITOR_BIN=""

# die <exit code> <message...>
die() {
  local code="$1"
  shift
  printf 'ERROR: %s\n' "$*" >&2
  exit "$code"
}

usage() {
  echo "usage: unity.sh path | create | run <args...> | exec <Class.Method>" >&2
  exit 2
}

# Sets VERSION. A ProjectVersion.txt that disagrees with the pin is an error.
resolve_version() {
  local project_ver="" pinned_ver=""
  if [ -f "$PROJECT_VERSION_FILE" ]; then
    # "m_EditorVersion:" only; "m_EditorVersionWithRevision:" is a different key.
    project_ver="$(sed -n '/^m_EditorVersion:/{s/^m_EditorVersion:[[:space:]]*//p;q;}' "$PROJECT_VERSION_FILE" | tr -d '[:space:]')"
    [ -n "$project_ver" ] || die 2 "no m_EditorVersion in $PROJECT_VERSION_FILE"
  fi
  if [ -f "$PIN_FILE" ]; then
    pinned_ver="$(tr -d '[:space:]' < "$PIN_FILE")"
  fi
  if [ -n "$project_ver" ] && [ -n "$pinned_ver" ] && [ "$project_ver" != "$pinned_ver" ]; then
    die 2 "ProjectVersion.txt ($project_ver) != unity-version.txt ($pinned_ver)"
  fi
  VERSION="${project_ver:-$pinned_ver}"
}

# Sets EDITOR_BIN, or exits 2.
resolve_editor() {
  # Checked even when UNITY_EDITOR is set: a project and a pin that disagree are a repo problem.
  resolve_version
  if [ -n "${UNITY_EDITOR:-}" ]; then
    EDITOR_BIN="$UNITY_EDITOR"
    if [ ! -f "$EDITOR_BIN" ] || [ ! -x "$EDITOR_BIN" ]; then
      die 2 "UNITY_EDITOR=$EDITOR_BIN is not an executable file."
    fi
    return 0
  fi
  [ -n "$VERSION" ] || die 2 "no Unity version: $PIN_FILE is missing or empty and there is no ProjectVersion.txt."
  if ! unity_find_editor "$EDITORS_DIR" "$VERSION"; then
    if [ -n "$unity_editor_broken_dir" ]; then
      die 2 "Unity $VERSION install at $unity_editor_broken_dir is incomplete ($unity_editor_broken_why). Repair or remove it in Unity Hub, or set UNITY_EDITOR."
    fi
    die 2 "Unity $VERSION not found at $unity_editor_tried. Install it (docs/dev-setup.md) or set UNITY_EDITOR."
  fi
  EDITOR_BIN="$unity_editor_bin"
}

cmd_path() {
  resolve_editor
  printf '%s\n' "$EDITOR_BIN"
}

cmd_create() {
  [ ! -e "$PROJECT_DIR" ] || die 1 "$PROJECT_DIR already exists; not creating a project over it."
  resolve_editor
  mkdir -p "$(dirname "$PROJECT_DIR")"
  # The log can't go into the project yet (it doesn't exist); it is moved there afterwards.
  local log rc=0
  log="$(mktemp "${TMPDIR:-/tmp}/unity-create.XXXXXX")"
  "$EDITOR_BIN" -batchmode -nographics -createProject "$PROJECT_DIR" -logFile "$log" -quit || rc=$?
  [ "$rc" -eq 0 ] || die "$rc" "Unity exited with $rc while creating $PROJECT_DIR. Log: $log"
  # Unity documents no exit codes, so check that it made what we need.
  [ -f "$PROJECT_VERSION_FILE" ] || die 1 "Unity exited 0 but $PROJECT_VERSION_FILE does not exist. Log: $log"
  mkdir -p "$LOG_DIR"
  mv "$log" "$LOG_DIR/create.log"
  echo "Created Unity project at $PROJECT_DIR"
}

cmd_run() {
  [ $# -ge 1 ] || die 2 "run needs Unity arguments (without any, Unity would idle forever), e.g. unity.sh run -quit"
  resolve_editor
  [ -f "$PROJECT_VERSION_FILE" ] || die 2 "Unity project not found at $PROJECT_DIR. Create it with tools/ci/unity.sh create."
  mkdir -p "$LOG_DIR"
  exec "$EDITOR_BIN" -batchmode -nographics -projectPath "$PROJECT_DIR" \
    -logFile "$LOG_DIR/${UNITY_LOG_NAME:-run}.log" "$@"
}

cmd_exec() {
  [ $# -eq 1 ] || usage
  cmd_run -executeMethod "$1"
}

[ $# -ge 1 ] || usage
command="$1"
shift
case "$command" in
  path)   [ $# -eq 0 ] || usage; cmd_path ;;
  create) [ $# -eq 0 ] || usage; cmd_create ;;
  run)    cmd_run "$@" ;;
  exec)   cmd_exec "$@" ;;
  *)      usage ;;
esac
