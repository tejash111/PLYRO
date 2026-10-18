#!/usr/bin/env bash
# Tests tools/ci/verify.sh without Unity. verify.sh finds the scripts it runs from its own location, so
# each test copies it into a throw-away repo tree whose scripts are stubs: a stub records its call and
# exits 0, exits non-zero when told to, and (as unity.sh) writes the editor log it is told to write.
# That tests the step order, stopping at the first failure, the exit codes, the editor-log warning
# check and the required-files check. No hook or switch in verify.sh is involved, and nothing here
# starts Unity.
# Run: bash tools/ci/test_verify.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$ROOT/tools/lib/assert.sh"

# A developer's own settings must not leak into the tests.
unset UNITY_EDITOR UNITY_HUB_EDITORS_DIR UNITY_PROJECT_DIR UNITY_LOG_NAME
unset STUB_FAIL STUB_FAIL_CODE STUB_LOG STUB_NO_LOG STUB_CALLS

# The trees live under a path with a space on purpose: verify.sh must quote its paths.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# The stub, as bash source. Environment, set per run by run_verify:
#   STUB_CALLS      file that gets one "<script> <arguments>" line per call
#   STUB_FAIL       "<script> <arguments>" of the call that must fail (with STUB_FAIL_CODE, default 7)
#   STUB_LOG        what the stub unity.sh writes to <project>/Logs/ci/$UNITY_LOG_NAME.log
#   STUB_NO_LOG     if set, the stub unity.sh writes no log
cat > "$WORK/stub.sh" <<'EOF'
#!/usr/bin/env bash
name="$(basename "$0")"
call="$name${*:+ $*}"
echo "$call" >> "$STUB_CALLS"
if [ "${STUB_FAIL:-}" = "$call" ]; then
  exit "${STUB_FAIL_CODE:-7}"
fi
if [ "$name" = unity.sh ] && [ -z "${STUB_NO_LOG:-}" ]; then
  log="$(cd "$(dirname "$0")/../.." && pwd)/unity/MotionBowling/Logs/ci/${UNITY_LOG_NAME:-run}.log"
  mkdir -p "$(dirname "$log")"
  printf '%s\n' "${STUB_LOG:-a clean editor log}" > "$log"
fi
exit 0
EOF

# make_tree <name>: a repo tree "<WORK>/<name>/mb verify" with the real verify.sh and stub steps.
# Its required-files.txt has only a comment. Prints the tree's path.
make_tree() {
  local t="$WORK/$1/mb verify" s
  mkdir -p "$t/tools/checks" "$t/tools/ci" "$t/tools/dev"
  cp "$ROOT/tools/ci/verify.sh" "$t/tools/ci/verify.sh"
  for s in tools/checks/check_sources_of_truth.sh tools/checks/test_checks.sh tools/ci/test_ci_scripts.sh \
    tools/dev/test_check_env.sh tools/ci/test_verify.sh tools/ci/unity.sh tools/ci/run_unity_tests.sh; do
    cp "$WORK/stub.sh" "$t/$s"
  done
  printf '# only a comment\n' > "$t/tools/checks/required-files.txt"
  printf '%s\n' "$t"
}

# run_verify <tree> [VAR=value...]: run the tree's verify.sh from another working directory, with the
# stub variables given, and with an empty calls file.
run_verify() {
  local t="$1"
  shift
  : > "$t/calls.log"
  (cd / && env STUB_CALLS="$t/calls.log" "$@" bash "$t/tools/ci/verify.sh")
}

# output_of <tree> [VAR=value...]: the output of a run, whatever its exit code.
output_of() {
  run_verify "$@" 2>&1 || true
}

# lacks <needle> <tree> [VAR=value...]: exit 0 if the output of a run does not contain the needle.
lacks() {
  local needle="$1" out
  shift
  out="$(output_of "$@")"
  [[ "$out" != *"$needle"* ]]
}

# calls_of <tree> [VAR=value...]: the calls the stubs recorded during a run.
calls_of() {
  local t="$1"
  output_of "$@" > /dev/null
  cat "$t/calls.log"
}

# calls_lack <script call> <tree> [VAR=value...]: exit 0 if the run didn't make that call.
calls_lack() {
  local needle="$1" calls
  shift
  calls="$(calls_of "$@")"
  [[ "$calls" != *"$needle"* ]]
}

# headings_of <tree> [VAR=value...]: the "== step N: ..." lines of a run.
headings_of() {
  local out
  out="$(output_of "$@")"
  printf '%s\n' "$out" | grep '^== step' || true
}

WARN_LINE="Assets/_Project/Modules/Gameplay/Runtime/Foo.cs(12,5): warning CS0168: The variable 'x' is declared but never used"
RUNBATCH="unity.sh exec Bowling.Tooling.Editor.SourceOfTruthValidator.RunBatch"

# --- A passing run: every step, in order, then VERIFY OK ---------------------------------------
T="$(make_tree pass)"
assert_exit 0 run_verify "$T"
assert_output_contains "VERIFY OK" run_verify "$T"
assert_output_contains "== step 1: source-of-truth text checks ==
== step 2: shell test suites ==
== step 3: Unity source-of-truth validators and compiler warnings ==
== step 4: Unity EditMode tests ==
== step 5: Unity PlayMode tests ==
== step 6: required files ==" headings_of "$T"
# The steps call the scripts of the repo they are in, with these arguments. unity.sh has to get
# UNITY_LOG_NAME=validate: the stub writes its log to that name, and step 3 reads validate.log.
assert_output_contains "check_sources_of_truth.sh --all
test_checks.sh
test_ci_scripts.sh
test_check_env.sh
test_verify.sh
$RUNBATCH
run_unity_tests.sh EditMode
run_unity_tests.sh PlayMode" calls_of "$T"
assert_exit 0 lacks "VERIFY FAILED" "$T"

# --- Arguments are not accepted -----------------------------------------------------------------
assert_exit 2 bash "$T/tools/ci/verify.sh" --skip-unity

# --- Sourcing defines the functions and runs nothing --------------------------------------------
sourcing_is_silent() {
  [ -z "$(bash -c 'source "$1"; type check_required_files check_no_project_warnings > /dev/null' _ "$ROOT/tools/ci/verify.sh" 2>&1)" ]
}
assert_exit 0 sourcing_is_silent

# --- A failing step ends the run: nothing after it runs, its exit code comes through ------------
T="$(make_tree fail)"
STEP1="check_sources_of_truth.sh --all"
assert_exit 7 run_verify "$T" STUB_FAIL="$STEP1"
assert_output_contains "VERIFY FAILED at step 1: source-of-truth text checks (exit 7)" output_of "$T" STUB_FAIL="$STEP1"
assert_exit 0 lacks "== step 2:" "$T" STUB_FAIL="$STEP1"
assert_exit 0 lacks "VERIFY OK" "$T" STUB_FAIL="$STEP1"
assert_exit 0 calls_lack "test_checks.sh" "$T" STUB_FAIL="$STEP1"

# A shell suite that fails fails step 2, but the other suites still run (one run shows every broken suite).
assert_exit 1 run_verify "$T" STUB_FAIL="test_ci_scripts.sh"
assert_output_contains "VERIFY FAILED at step 2: shell test suites (exit 1)" output_of "$T" STUB_FAIL="test_ci_scripts.sh"
assert_output_contains "test_check_env.sh
test_verify.sh" calls_of "$T" STUB_FAIL="test_ci_scripts.sh"
assert_exit 0 lacks "== step 3:" "$T" STUB_FAIL="test_ci_scripts.sh"

# The Unity validators failing (unity.sh passes the editor's exit code through).
assert_exit 3 run_verify "$T" STUB_FAIL="$RUNBATCH" STUB_FAIL_CODE=3
assert_output_contains "VERIFY FAILED at step 3" output_of "$T" STUB_FAIL="$RUNBATCH"
assert_output_contains "validate.log" output_of "$T" STUB_FAIL="$RUNBATCH"
assert_exit 0 lacks "== step 4:" "$T" STUB_FAIL="$RUNBATCH"

assert_exit 7 run_verify "$T" STUB_FAIL="run_unity_tests.sh EditMode"
assert_output_contains "VERIFY FAILED at step 4: Unity EditMode tests (exit 7)" output_of "$T" STUB_FAIL="run_unity_tests.sh EditMode"
assert_exit 0 lacks "== step 5:" "$T" STUB_FAIL="run_unity_tests.sh EditMode"

assert_exit 7 run_verify "$T" STUB_FAIL="run_unity_tests.sh PlayMode"
assert_output_contains "VERIFY FAILED at step 5: Unity PlayMode tests (exit 7)" output_of "$T" STUB_FAIL="run_unity_tests.sh PlayMode"
assert_exit 0 lacks "== step 6:" "$T" STUB_FAIL="run_unity_tests.sh PlayMode"
assert_exit 0 lacks "VERIFY OK" "$T" STUB_FAIL="run_unity_tests.sh PlayMode"

# --- Step 3: compiler warnings in Assets/_Project fail it --------------------------------------
T="$(make_tree warn)"
assert_exit 1 run_verify "$T" STUB_LOG="$WARN_LINE"
assert_output_contains "VERIFY FAILED at step 3" output_of "$T" STUB_LOG="$WARN_LINE"
assert_output_contains "$WARN_LINE" output_of "$T" STUB_LOG="$WARN_LINE"
assert_exit 0 lacks "== step 4:" "$T" STUB_LOG="$WARN_LINE"
# ... also one among many clean lines.
MANY_LINES="Compilation started
$WARN_LINE
Compilation finished"
assert_exit 1 run_verify "$T" STUB_LOG="$MANY_LINES"

# Only warning CS lines that mention Assets/_Project count.
assert_exit 0 run_verify "$T" STUB_LOG="Packages/com.unity.foo/Runtime/A.cs(1,1): warning CS0618: 'X' is obsolete"
assert_exit 0 run_verify "$T" STUB_LOG="Assets/Other/Foo.cs(1,1): warning CS0168: not ours"
assert_exit 0 run_verify "$T" STUB_LOG="Assets/_Project/Modules/Gameplay/Runtime/Foo.cs(1,1): error CS1002: ; expected"
assert_exit 0 run_verify "$T" STUB_LOG="Assets/_Project/Foo.cs: warning: a warning without a CS code"
# The note Unity prints for an assembly without scripts is expected and is not a compiler warning.
assert_exit 0 run_verify "$T" STUB_LOG="Assembly for Assembly Definition File 'Assets/_Project/Modules/App/Tests/EditMode/A.asmdef' will not be compiled, because it has no scripts associated with it."

# No log: unity.sh exited 0 but the validators wrote nothing to judge.
assert_exit 1 run_verify "$T" STUB_NO_LOG=1
assert_output_contains "Unity log not found" output_of "$T" STUB_NO_LOG=1
# A log left by an earlier run is not judged in place of this run's log: it is deleted first.
stale_log_run() {
  mkdir -p "$T/unity/MotionBowling/Logs/ci"
  printf '%s\n' "$WARN_LINE" > "$T/unity/MotionBowling/Logs/ci/validate.log"
  output_of "$T" STUB_NO_LOG=1
}
assert_output_contains "Unity log not found" stale_log_run
stale_then_clean() {
  mkdir -p "$T/unity/MotionBowling/Logs/ci"
  printf '%s\n' "$WARN_LINE" > "$T/unity/MotionBowling/Logs/ci/validate.log"
  run_verify "$T"
}
assert_exit 0 stale_then_clean

# --- Step 6: required files ----------------------------------------------------------------------
T="$(make_tree files)"
printf '%s\n' '# Required files' '' '   # an indented comment' 'README.md' 'docs/My Doc.md   ' > "$T/tools/checks/required-files.txt"
mkdir -p "$T/docs"
: > "$T/README.md"
: > "$T/docs/My Doc.md"
assert_exit 0 run_verify "$T"
assert_output_contains "2 required files present" output_of "$T"
# A missing file fails step 6, names the path, and the run does not print VERIFY OK.
rm "$T/docs/My Doc.md"
assert_exit 1 run_verify "$T"
assert_output_contains "MISSING required file: docs/My Doc.md" output_of "$T"
assert_output_contains "VERIFY FAILED at step 6: required files (exit 1)" output_of "$T"
assert_exit 0 lacks "VERIFY OK" "$T"
# The first missing file doesn't hide the others.
printf '%s\n' 'README.md' 'gone/one.txt' 'gone/two.txt' > "$T/tools/checks/required-files.txt"
assert_output_contains "2 of 3 required files are missing" output_of "$T"
# A directory counts as existing.
printf '%s\n' 'docs' > "$T/tools/checks/required-files.txt"
assert_exit 0 run_verify "$T"
# No list at all is an error, not an empty list.
rm "$T/tools/checks/required-files.txt"
assert_exit 1 run_verify "$T"
assert_output_contains "required-files list not found" output_of "$T"

finish
