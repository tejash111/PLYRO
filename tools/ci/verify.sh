#!/usr/bin/env bash
# The single verification entry point, for local runs and for CI (.github/workflows/verify.yml).
#
# Usage: bash tools/ci/verify.sh        (any working directory; the repo is found from this file)
#
# Runs these steps in order and stops at the first failure:
#   1. check_sources_of_truth.sh --all       text checks for SOT-1 and SOT-2
#   2. the shell suites                      test_checks.sh, test_ci_scripts.sh, test_check_env.sh, test_verify.sh
#   3. Unity batch mode                      first deletes Library/ScriptAssemblies and Library/Bee to force a full
#                                            script recompile, then SourceOfTruthValidator.RunBatch (the editor
#                                            validators), then the editor log must have no "warning CS" line from
#                                            Assets/_Project
#   4. Unity EditMode tests
#   5. Unity PlayMode tests
#   6. required files                        every entry of tools/checks/required-files.txt exists
#
# Each step prints "== step N: <name> ==" first. On success the last line is "VERIFY OK" and the exit code
# is 0. A failing step prints "VERIFY FAILED at step N: <name> (exit C)" to stderr and ends the run with
# that step's exit code (never 0).
#
# Steps 3 to 5 start the Unity editor, so no other Unity process may have the project open. The first run in
# a fresh checkout imports the whole project and takes a long time. Unity logs and test results go to
# <project>/Logs/ci/ (gitignored): validate.log, EditMode.log, PlayMode.log and the two *-results.xml files.
#
# Environment: this script reads none of its own. The Unity steps use the settings of unity.sh
# (UNITY_EDITOR, UNITY_HUB_EDITORS_DIR, UNITY_PROJECT_DIR; see docs/dev-setup.md).
#
# The functions below are written to be sourced by tools/ci/test_verify.sh, which also runs this script
# end to end against stub steps. Sourcing defines them and runs nothing.
#
# Written for macOS system bash 3.2: no bash 4 features.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
REQUIRED_FILES_LIST="$REPO_ROOT/tools/checks/required-files.txt"

# Where unity.sh writes its logs; the same default as in unity.sh.
unity_project_dir() {
  printf '%s\n' "${UNITY_PROJECT_DIR:-$REPO_ROOT/unity/MotionBowling}"
}

# check_no_project_warnings <editor log>
# Fails if the log is missing, or has a "warning CS" line that mentions Assets/_Project. Warnings from
# packages and other folders are not ours to fix, and are ignored. Prints each offending line (once).
check_no_project_warnings() {
  local log="$1" hits
  if [ ! -f "$log" ]; then
    printf 'ERROR: Unity log not found: %s\n' "$log" >&2
    return 1
  fi
  # LC_ALL=C: a log with bytes that are not valid UTF-8 must not make awk fail.
  hits="$(LC_ALL=C awk '/warning CS/ && index($0, "Assets/_Project") { print }' "$log" | LC_ALL=C sort -u)" || {
    printf 'ERROR: could not read Unity log: %s\n' "$log" >&2
    return 1
  }
  if [ -n "$hits" ]; then
    printf 'ERROR: compiler warnings in Assets/_Project (acceptance 1 needs zero):\n' >&2
    printf '%s\n' "$hits" | sed 's/^/  /' >&2
    printf 'Unity log: %s\n' "$log" >&2
    return 1
  fi
  return 0
}

# check_required_files <list file> <repo root>
# Every line of the list is a path relative to the repo root that must exist. Blank lines and lines that
# start with # are ignored; leading and trailing blanks are trimmed (a path may contain spaces).
check_required_files() {
  local list="$1" root="$2" line count=0 missing=0
  if [ ! -f "$list" ]; then
    printf 'ERROR: required-files list not found: %s\n' "$list" >&2
    return 1
  fi
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    case "$line" in
      '' | '#'*) continue ;;
    esac
    count=$((count + 1))
    if [ ! -e "$root/$line" ]; then
      printf 'MISSING required file: %s\n' "$line" >&2
      missing=$((missing + 1))
    fi
  done < "$list"
  if [ "$missing" -gt 0 ]; then
    printf 'ERROR: %d of %d required files are missing (list: %s)\n' "$missing" "$count" "$list" >&2
    return 1
  fi
  printf '%d required files present\n' "$count"
  return 0
}

step_check_sources() {
  bash "$REPO_ROOT/tools/checks/check_sources_of_truth.sh" --all
}

# Each suite runs even after an earlier one failed, so one run shows every broken suite; the step fails
# if any did.
step_shell_suites() {
  local rc=0 suite
  for suite in tools/checks/test_checks.sh tools/ci/test_ci_scripts.sh tools/dev/test_check_env.sh tools/ci/test_verify.sh; do
    printf -- '-- %s\n' "$suite"
    bash "$REPO_ROOT/$suite" || {
      printf 'ERROR: %s failed\n' "$suite" >&2
      rc=1
    }
  done
  return "$rc"
}

step_validate() {
  local project log
  project="$(unity_project_dir)"
  log="$project/Logs/ci/validate.log"
  # A log left by an earlier run must not be judged in place of this run's log.
  rm -f "$log"
  # Unity logs compiler output only for the assemblies it recompiles, so a warning already compiled into a
  # warm Library/ would never reach the log and the check below would pass without seeing it. Deleting the
  # compiled scripts (ScriptAssemblies) and the build graph that produced them (Bee) forces a full script
  # recompile in this run. Nothing else of Library/ is touched, so the asset import stays cached.
  rm -rf "$project/Library/ScriptAssemblies" "$project/Library/Bee"
  UNITY_LOG_NAME=validate bash "$REPO_ROOT/tools/ci/unity.sh" exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch || {
    local rc=$?
    printf 'Unity log: %s\n' "$log" >&2
    return "$rc"
  }
  check_no_project_warnings "$log"
}

step_edit_mode() {
  bash "$REPO_ROOT/tools/ci/run_unity_tests.sh" EditMode
}

step_play_mode() {
  bash "$REPO_ROOT/tools/ci/run_unity_tests.sh" PlayMode
}

step_required_files() {
  check_required_files "$REQUIRED_FILES_LIST" "$REPO_ROOT"
}

# run_step <n> <name> <function>: print the heading, run the function, and end the whole run if it fails.
run_step() {
  local n="$1" name="$2" fn="$3" rc=0
  printf '== step %s: %s ==\n' "$n" "$name"
  "$fn" || rc=$?
  if [ "$rc" -ne 0 ]; then
    printf 'VERIFY FAILED at step %s: %s (exit %s)\n' "$n" "$name" "$rc" >&2
    exit "$rc"
  fi
  return 0
}

main() {
  [ $# -eq 0 ] || {
    echo "usage: verify.sh (no arguments)" >&2
    exit 2
  }
  run_step 1 "source-of-truth text checks" step_check_sources
  run_step 2 "shell test suites" step_shell_suites
  run_step 3 "Unity source-of-truth validators and compiler warnings" step_validate
  run_step 4 "Unity EditMode tests" step_edit_mode
  run_step 5 "Unity PlayMode tests" step_play_mode
  run_step 6 "required files" step_required_files
  echo "VERIFY OK"
}

# Run only when executed, not when sourced.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
  main "$@"
fi
