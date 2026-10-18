#!/usr/bin/env bash
# Tests tools/ci/check_test_results.sh, unity.sh and run_unity_tests.sh without a Unity install:
# results files are fixtures, and the editor is a fake script that records its arguments.
# Run: bash tools/ci/test_ci_scripts.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$ROOT/tools/lib/assert.sh"

CI="$ROOT/tools/ci"
FIXTURES="$CI/fixtures/results"
CHECK="$CI/check_test_results.sh"
UNITY_SH="$CI/unity.sh"
RUN_TESTS="$CI/run_unity_tests.sh"
# Empty (instead of aborting the test) while unity-version.txt doesn't exist yet.
VERSION="$(tr -d '[:space:]' 2>/dev/null < "$CI/unity-version.txt" || true)"

# A developer's own settings must not leak into the tests.
unset UNITY_EDITOR UNITY_HUB_EDITORS_DIR UNITY_PROJECT_DIR UNITY_LOG_NAME UNITY_VERSION

# Paths contain spaces on purpose: the scripts must quote them.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# in_dir <dir> <cmd...>: run a command from another working directory.
in_dir() {
  local dir="$1"
  shift
  (cd "$dir" && "$@")
}

# make_editor <hub dir> <version>: an executable that stands in for the editor binary.
make_editor() {
  local bin="$1/$2/Unity.app/Contents/MacOS/Unity"
  mkdir -p "$(dirname "$bin")"
  printf '#!/bin/sh\nexit 0\n' > "$bin"
  chmod +x "$bin"
}

# write_project_version <project dir> <version>: a ProjectVersion.txt like the editor writes.
write_project_version() {
  mkdir -p "$1/ProjectSettings"
  printf 'm_EditorVersion: %s\nm_EditorVersionWithRevision: %s (0123456789ab)\n' "$2" "$2" \
    > "$1/ProjectSettings/ProjectVersion.txt"
}

# ---------------------------------------------------------------------------------------------
# unity-version.txt: one line holding the pinned version, nothing else.
# ---------------------------------------------------------------------------------------------
assert_exit 0 grep -Eqx '[0-9]+\.[0-9]+\.[0-9]+f[0-9]+' "$CI/unity-version.txt"
assert_exit 0 test "$(grep -c '' "$CI/unity-version.txt" 2>/dev/null)" = 1

# ---------------------------------------------------------------------------------------------
# check_test_results.sh
# ---------------------------------------------------------------------------------------------
assert_exit 0 "$CHECK" "$FIXTURES/passed.xml"
assert_output_contains "PASS total=1" "$CHECK" "$FIXTURES/passed.xml"

# failed.xml is one line with reordered attributes; every failed test case is listed.
assert_exit 1 "$CHECK" "$FIXTURES/failed.xml"
assert_output_contains "FAIL" "$CHECK" "$FIXTURES/failed.xml"
assert_output_contains "Bowling.Foo.Tests.Breaks" "$CHECK" "$FIXTURES/failed.xml"
assert_output_contains "Bowling.Foo.Tests.AlsoBreaks" "$CHECK" "$FIXTURES/failed.xml"

# zero.xml reports result="Passed" with total="0": no tests ran, so it fails.
assert_exit 1 "$CHECK" "$FIXTURES/zero.xml"
assert_output_contains "total=0" "$CHECK" "$FIXTURES/zero.xml"

assert_exit 1 "$CHECK" "$WORK/missing-results.xml"
assert_output_contains "no results file" "$CHECK" "$WORK/missing-results.xml"
assert_exit 2 "$CHECK"

# No <test-run> element at all.
printf '<?xml version="1.0"?><something-else total="5" failed="0" result="Passed"/>\n' > "$WORK/no-test-run.xml"
assert_exit 1 "$CHECK" "$WORK/no-test-run.xml"
assert_output_contains "no <test-run>" "$CHECK" "$WORK/no-test-run.xml"

# Nothing failed, but the run itself did not pass.
printf '<test-run id="2" result="Failed(Error)" total="3" passed="3" failed="0"/>\n' > "$WORK/bad-result.xml"
assert_exit 1 "$CHECK" "$WORK/bad-result.xml"
assert_output_contains "result=" "$CHECK" "$WORK/bad-result.xml"

# One line, attributes in any order; the nested suite's numbers must not be read by mistake.
printf '%s\n' '<?xml version="1.0"?><test-run failed="0" result="Passed" testcasecount="7" total="7" passed="7"><test-suite total="99" failed="99" result="Failed"/></test-run>' \
  > "$WORK/reordered.xml"
assert_exit 0 "$CHECK" "$WORK/reordered.xml"
assert_output_contains "PASS total=7" "$CHECK" "$WORK/reordered.xml"

# The <test-run> tag itself spread over several lines.
printf '<test-run id="2"\n          total="4"\n          failed="0"\n          result="Passed">\n</test-run>\n' > "$WORK/multiline.xml"
assert_exit 0 "$CHECK" "$WORK/multiline.xml"
assert_output_contains "PASS total=4" "$CHECK" "$WORK/multiline.xml"

# XML escapes in a failing test's name are decoded for display.
printf '<test-run result="Failed" total="1" failed="1"><test-case fullname="A.B.C(&quot;x&quot;, &lt;1&gt;)" result="Failed"/></test-run>\n' > "$WORK/escaped.xml"
assert_output_contains 'A.B.C("x", <1>)' "$CHECK" "$WORK/escaped.xml"

# ---------------------------------------------------------------------------------------------
# unity.sh path
# ---------------------------------------------------------------------------------------------
NOPROJ="$WORK/no project"   # a project dir that does not exist, so a real project can't interfere
HUB="$WORK/hub editors"

assert_exit 2 env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path
assert_output_contains "/nonexistent" env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path

# No editor under the Hub directory: the message names the pinned version and the path it tried.
mkdir -p "$WORK/empty hub"
assert_exit 2 env UNITY_HUB_EDITORS_DIR="$WORK/empty hub" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path
assert_output_contains "Unity $VERSION not found at $WORK/empty hub/$VERSION/Unity.app/Contents/MacOS/Unity" \
  env UNITY_HUB_EDITORS_DIR="$WORK/empty hub" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path

# Editor installed under the Hub directory.
make_editor "$HUB" "$VERSION"
assert_exit 0 env UNITY_HUB_EDITORS_DIR="$HUB" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path
assert_output_contains "$HUB/$VERSION/Unity.app/Contents/MacOS/Unity" \
  env UNITY_HUB_EDITORS_DIR="$HUB" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path

# UNITY_EDITOR wins over the Hub directory, but only if it is an executable file.
printf '#!/bin/sh\nexit 0\n' > "$WORK/exec-editor"
chmod +x "$WORK/exec-editor"
printf 'not executable\n' > "$WORK/plain-file"
assert_exit 0 env UNITY_EDITOR="$WORK/exec-editor" UNITY_HUB_EDITORS_DIR="$WORK/empty hub" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path
assert_output_contains "$WORK/exec-editor" env UNITY_EDITOR="$WORK/exec-editor" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path
assert_exit 2 env UNITY_EDITOR="$WORK/plain-file" UNITY_PROJECT_DIR="$NOPROJ" "$UNITY_SH" path

# ProjectVersion.txt must agree with the pin (m_EditorVersionWithRevision must not confuse the parser).
write_project_version "$WORK/stale project" "6000.3.1f1"
assert_exit 2 env UNITY_HUB_EDITORS_DIR="$HUB" UNITY_PROJECT_DIR="$WORK/stale project" "$UNITY_SH" path
assert_output_contains "ProjectVersion.txt (6000.3.1f1) != unity-version.txt ($VERSION)" \
  env UNITY_HUB_EDITORS_DIR="$HUB" UNITY_PROJECT_DIR="$WORK/stale project" "$UNITY_SH" path
write_project_version "$WORK/good project" "$VERSION"
assert_exit 0 env UNITY_HUB_EDITORS_DIR="$HUB" UNITY_PROJECT_DIR="$WORK/good project" "$UNITY_SH" path
# The mismatch is an error even when UNITY_EDITOR overrides the path.
assert_exit 2 env UNITY_EDITOR="$WORK/exec-editor" UNITY_PROJECT_DIR="$WORK/stale project" "$UNITY_SH" path

# Usage errors.
assert_exit 2 "$UNITY_SH"
assert_exit 2 "$UNITY_SH" bogus

# ---------------------------------------------------------------------------------------------
# The scripts find the repo from their own location: a copy of tools/ci in a path with a space,
# run from an unrelated working directory.
# ---------------------------------------------------------------------------------------------
COPY="$WORK/repo copy"
mkdir -p "$COPY/tools/ci" "$COPY/unity/MotionBowling" "$WORK/elsewhere"
cp -R "$CI/." "$COPY/tools/ci/"

assert_exit 1 in_dir "$WORK/elsewhere" "$COPY/tools/ci/unity.sh" create
assert_output_contains "already exists" in_dir "$WORK/elsewhere" "$COPY/tools/ci/unity.sh" create
assert_exit 0 in_dir "$WORK/elsewhere" "$COPY/tools/ci/check_test_results.sh" "$COPY/tools/ci/fixtures/results/passed.xml"
assert_output_contains "$HUB/$VERSION/Unity.app/Contents/MacOS/Unity" \
  in_dir "$WORK/elsewhere" env UNITY_HUB_EDITORS_DIR="$HUB" "$COPY/tools/ci/unity.sh" path

# Without unity-version.txt, the version comes from ProjectVersion.txt in the default project dir.
COPY2="$WORK/repo copy two"
mkdir -p "$COPY2/tools/ci"
cp -R "$CI/." "$COPY2/tools/ci/"
rm -f "$COPY2/tools/ci/unity-version.txt"
write_project_version "$COPY2/unity/MotionBowling" "6000.3.5f1"
make_editor "$HUB" "6000.3.5f1"
assert_output_contains "$HUB/6000.3.5f1/Unity.app/Contents/MacOS/Unity" \
  in_dir "$WORK/elsewhere" env UNITY_HUB_EDITORS_DIR="$HUB" "$COPY2/tools/ci/unity.sh" path

# ---------------------------------------------------------------------------------------------
# unity.sh create / run / exec, with a fake editor.
# FAKE_UNITY_EXIT: exit code. FAKE_UNITY_CREATE=0: don't create the project on -createProject.
# FAKE_UNITY_RESULTS: file to copy to the -testResults path.
# ---------------------------------------------------------------------------------------------
FAKE="$WORK/fake unity"
cat > "$FAKE" <<'EOF'
#!/bin/sh
echo "FAKE-UNITY $*"
prev=""
for arg in "$@"; do
  case "$prev" in
    -createProject)
      if [ "${FAKE_UNITY_CREATE:-1}" = 1 ]; then
        mkdir -p "$arg/ProjectSettings"
        printf 'm_EditorVersion: %s\n' "$FAKE_UNITY_VERSION" > "$arg/ProjectSettings/ProjectVersion.txt"
      fi
      ;;
    -testResults)
      if [ -n "${FAKE_UNITY_RESULTS:-}" ]; then cp "$FAKE_UNITY_RESULTS" "$arg"; fi
      ;;
  esac
  prev="$arg"
done
exit "${FAKE_UNITY_EXIT:-0}"
EOF
chmod +x "$FAKE"

PROJ="$WORK/my project"
write_project_version "$PROJ" "$VERSION"
FAKE_ENV=(UNITY_EDITOR="$FAKE" UNITY_PROJECT_DIR="$PROJ" FAKE_UNITY_VERSION="$VERSION")

# create: refuses an existing project dir, without needing an editor.
assert_exit 1 env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$PROJ" "$UNITY_SH" create
# create: needs a working editor.
assert_exit 2 env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$WORK/new project" "$UNITY_SH" create
# create: success runs the editor on the project path, and keeps the log inside the project.
NEWPROJ="$WORK/new project"
assert_exit 0 env "${FAKE_ENV[@]}" UNITY_PROJECT_DIR="$NEWPROJ" "$UNITY_SH" create
assert_exit 0 test -f "$NEWPROJ/ProjectSettings/ProjectVersion.txt"
assert_exit 0 test -f "$NEWPROJ/Logs/ci/create.log"
rm -rf "$NEWPROJ"
assert_output_contains "-batchmode -nographics -createProject $NEWPROJ -logFile" \
  env "${FAKE_ENV[@]}" UNITY_PROJECT_DIR="$NEWPROJ" "$UNITY_SH" create
rm -rf "$NEWPROJ"
# create: the editor's failure code is passed on; "exit 0 but no project" is a failure too.
assert_exit 4 env "${FAKE_ENV[@]}" UNITY_PROJECT_DIR="$NEWPROJ" FAKE_UNITY_EXIT=4 "$UNITY_SH" create
rm -rf "$NEWPROJ"
assert_exit 1 env "${FAKE_ENV[@]}" UNITY_PROJECT_DIR="$NEWPROJ" FAKE_UNITY_CREATE=0 "$UNITY_SH" create
rm -rf "$NEWPROJ"

# run: fixed headless flags, project and log paths, then the caller's arguments.
assert_exit 0 env "${FAKE_ENV[@]}" "$UNITY_SH" run -quit
assert_output_contains "FAKE-UNITY -batchmode -nographics -projectPath $PROJ -logFile $PROJ/Logs/ci/run.log -quit" \
  env "${FAKE_ENV[@]}" "$UNITY_SH" run -quit
assert_output_contains "-logFile $PROJ/Logs/ci/validate.log" \
  env "${FAKE_ENV[@]}" UNITY_LOG_NAME=validate "$UNITY_SH" run -quit
assert_exit 0 test -d "$PROJ/Logs/ci"
# run: the editor's exit code is passed through.
assert_exit 3 env "${FAKE_ENV[@]}" FAKE_UNITY_EXIT=3 "$UNITY_SH" run -quit
# run: errors (exit 2) for no arguments, a missing editor and a missing project.
assert_exit 2 env "${FAKE_ENV[@]}" "$UNITY_SH" run
assert_exit 2 env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$PROJ" "$UNITY_SH" run -quit
assert_exit 2 env UNITY_EDITOR="$FAKE" UNITY_PROJECT_DIR="$WORK/no such project" "$UNITY_SH" run -quit
assert_output_contains "not found" env UNITY_EDITOR="$FAKE" UNITY_PROJECT_DIR="$WORK/no such project" "$UNITY_SH" run -quit

# exec <Method> is run -executeMethod <Method>.
assert_exit 0 env "${FAKE_ENV[@]}" "$UNITY_SH" exec Bowling.Tooling.Editor.Thing.RunBatch
assert_output_contains "-executeMethod Bowling.Tooling.Editor.Thing.RunBatch" \
  env "${FAKE_ENV[@]}" "$UNITY_SH" exec Bowling.Tooling.Editor.Thing.RunBatch
assert_exit 1 env "${FAKE_ENV[@]}" FAKE_UNITY_EXIT=1 "$UNITY_SH" exec Bowling.Tooling.Editor.Thing.RunBatch
assert_exit 2 env "${FAKE_ENV[@]}" "$UNITY_SH" exec

# ---------------------------------------------------------------------------------------------
# run_unity_tests.sh, with the fake editor writing (or not writing) a results file.
# ---------------------------------------------------------------------------------------------
assert_exit 0 env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/passed.xml" "$RUN_TESTS" EditMode
assert_output_contains "PASS total=1" env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/passed.xml" "$RUN_TESTS" EditMode
assert_output_contains "-runTests -testPlatform EditMode -testResults $PROJ/Logs/ci/EditMode-results.xml" \
  env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/passed.xml" "$RUN_TESTS" EditMode
assert_output_contains "-logFile $PROJ/Logs/ci/EditMode.log" \
  env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/passed.xml" "$RUN_TESTS" EditMode
assert_output_contains "-testPlatform PlayMode -testResults $PROJ/Logs/ci/PlayMode-results.xml -assemblyNames Bowling.A;Bowling.B" \
  env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/passed.xml" "$RUN_TESTS" PlayMode "Bowling.A;Bowling.B"

# Failures are judged from the results file; Unity's exit code (here 0 or 2) doesn't matter.
assert_exit 1 env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/failed.xml" FAKE_UNITY_EXIT=2 "$RUN_TESTS" EditMode
assert_output_contains "Bowling.Foo.Tests.Breaks" env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/failed.xml" "$RUN_TESTS" EditMode
assert_exit 1 env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/zero.xml" "$RUN_TESTS" EditMode
assert_exit 0 env "${FAKE_ENV[@]}" FAKE_UNITY_RESULTS="$FIXTURES/passed.xml" FAKE_UNITY_EXIT=2 "$RUN_TESTS" EditMode

# A stale results file from an earlier run must not pass when Unity writes nothing.
stale_run() {
  mkdir -p "$PROJ/Logs/ci"
  cp "$FIXTURES/passed.xml" "$PROJ/Logs/ci/EditMode-results.xml"
  env "${FAKE_ENV[@]}" "$RUN_TESTS" EditMode
}
assert_exit 1 stale_run
assert_output_contains "no results file" stale_run

# Usage and environment errors are exit 2, not a test failure.
assert_exit 2 "$RUN_TESTS"
assert_exit 2 "$RUN_TESTS" NotAPlatform
assert_exit 2 env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$PROJ" "$RUN_TESTS" EditMode
assert_output_contains "/nonexistent" env UNITY_EDITOR=/nonexistent UNITY_PROJECT_DIR="$PROJ" "$RUN_TESTS" EditMode

finish
