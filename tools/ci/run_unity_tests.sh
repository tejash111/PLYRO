#!/usr/bin/env bash
# Runs the Unity tests of one platform headless, then judges the results file.
#
# Usage: run_unity_tests.sh <EditMode|PlayMode> [assemblyNames]
#
#   assemblyNames   optional, semicolon-separated test assemblies for Unity's -assemblyNames,
#                   e.g. "Bowling.Tooling.Tests.EditMode"
#
# Writes <project>/Logs/ci/<Platform>-results.xml (deleted first, so a stale file can't pass) and
# the editor log <project>/Logs/ci/<UNITY_LOG_NAME or Platform>.log. Then runs check_test_results.sh
# on the results file, which prints "PASS total=N" or "FAIL ...".
#
# Exit 0: tests ran and passed. Exit 1: the tests failed, none ran, or Unity wrote no results.
# Exit 2: bad usage, or the editor can't be resolved (see unity.sh). Unity's own exit code is
# ignored on purpose: it is not documented for test runs, and the results file is what counts.
#
# Environment: UNITY_PROJECT_DIR and UNITY_LOG_NAME as in unity.sh, plus its editor settings.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT_DIR="${UNITY_PROJECT_DIR:-$REPO_ROOT/unity/MotionBowling}"
UNITY_SH="$SCRIPT_DIR/unity.sh"

usage() {
  echo "usage: run_unity_tests.sh <EditMode|PlayMode> [assemblyNames]" >&2
  exit 2
}

[ $# -ge 1 ] && [ $# -le 2 ] || usage
platform="$1"
assemblies="${2:-}"
case "$platform" in
  EditMode|PlayMode) ;;
  *) usage ;;
esac

results="$PROJECT_DIR/Logs/ci/$platform-results.xml"
UNITY_LOG_NAME="${UNITY_LOG_NAME:-$platform}"
export UNITY_LOG_NAME

# A missing editor must be an exit 2 with its own message, not a "no results file" failure.
"$UNITY_SH" path > /dev/null

mkdir -p "$(dirname "$results")"
rm -f "$results"

if [ -n "$assemblies" ]; then
  "$UNITY_SH" run -runTests -testPlatform "$platform" -testResults "$results" -assemblyNames "$assemblies" || true
else
  "$UNITY_SH" run -runTests -testPlatform "$platform" -testResults "$results" || true
fi

rc=0
"$SCRIPT_DIR/check_test_results.sh" "$results" || rc=$?
if [ "$rc" -ne 0 ]; then
  echo "Unity log: $PROJECT_DIR/Logs/ci/$UNITY_LOG_NAME.log" >&2
fi
exit "$rc"
