#!/usr/bin/env bash
# Minimal assertion library for the repo's bash tests. Source it; don't run it:
#
#   source "$(dirname "$0")/../lib/assert.sh"     # from a script in tools/<dir>/
#
#   assert_exit <code> <cmd...>                 the command exits with <code>
#   assert_output_contains <needle> <cmd...>    the command's stdout+stderr contains <needle>
#   finish                                      print totals; exit 1 if any assertion failed
#
# Each assertion prints "ok - <command line>" or "not ok - <command line>" and bumps
# PASSED or FAILED. A failure also prints "# ..." diagnostic lines (expected value and the
# command's output). Assertions never abort the calling script, so they are safe under
# `set -e` and one failure doesn't hide the rest.
#
# To pass environment variables to the command, use env:
#   assert_exit 1 env FOO=bar tools/dev/some-script.sh
#
# Written for bash 3.2 (macOS system bash).

PASSED=0
FAILED=0

_assert_usage() {
  echo "assert.sh: usage: $1" >&2
  exit 2
}

_assert_pass() {
  PASSED=$((PASSED + 1))
  printf 'ok - %s\n' "$1"
}

# _assert_fail <desc> <reason> <captured output>
_assert_fail() {
  FAILED=$((FAILED + 1))
  printf 'not ok - %s\n' "$1"
  printf '# %s\n' "$2"
  if [ -n "$3" ]; then
    printf '%s\n' "$3" | sed 's/^/#   /'
  fi
}

assert_exit() {
  [ $# -ge 2 ] || _assert_usage "assert_exit <code> <cmd...>"
  local expected="$1"
  shift
  local desc="$*"
  local rc=0
  local out
  out="$("$@" 2>&1)" || rc=$?
  if [ "$rc" -eq "$expected" ]; then
    _assert_pass "$desc"
  else
    _assert_fail "$desc" "expected exit $expected, got $rc" "$out"
  fi
  return 0
}

assert_output_contains() {
  [ $# -ge 2 ] || _assert_usage "assert_output_contains <needle> <cmd...>"
  local needle="$1"
  shift
  local desc="$*"
  local rc=0
  local out
  out="$("$@" 2>&1)" || rc=$?
  if [[ "$out" == *"$needle"* ]]; then
    _assert_pass "$desc"
  else
    _assert_fail "$desc" "expected output to contain: $needle" "$out"
  fi
  return 0
}

finish() {
  printf '# passed: %d, failed: %d\n' "$PASSED" "$FAILED"
  if [ "$FAILED" -gt 0 ]; then
    exit 1
  fi
  exit 0
}
