#!/usr/bin/env bash
# Tests the source-of-truth text checks: check_sources_of_truth.sh against the fixture folders in
# tools/checks/fixtures/{pass,fail}/<rule-id>/, against generated files and against throw-away git
# repos (staged mode), plus install-hooks.sh and the pre-commit hook it writes.
# Run: bash tools/checks/test_checks.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$ROOT/tools/lib/assert.sh"

CHECKS="$ROOT/tools/checks"
CHECK="$CHECKS/check_sources_of_truth.sh"
FIXTURES="$CHECKS/fixtures"

# The throw-away repos must not see the developer's git setup (hooksPath, signing, identity) or
# the repo this script runs in (when it is started from a hook, git exports GIT_DIR and friends).
unset GIT_DIR GIT_INDEX_FILE GIT_WORK_TREE
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com
export GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com

# Paths contain spaces on purpose: the scripts must quote them.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SPACED="$WORK/with space"
mkdir -p "$SPACED"

# in_dir <dir> <cmd...>: run a command from another working directory.
in_dir() {
  local dir="$1"
  shift
  (cd "$dir" && "$@")
}

# check_pass <fixtures dir> <rule>: the pass fixture exits 0 and reports nothing for the rule.
check_pass() {
  local out rc=0
  out="$("$CHECK" --root "$1/pass/$2/Assets" --rules "$2" 2>&1)" || rc=$?
  if [ "$rc" -ne 0 ] || printf '%s\n' "$out" | grep -q "^$2 "; then
    printf 'exit %s\n%s\n' "$rc" "$out"
    return 1
  fi
}

# check_fail <fixtures dir> <rule>: the fail fixture exits 1 and reports the rule.
check_fail() {
  local out rc=0
  out="$("$CHECK" --root "$1/fail/$2/Assets" --rules "$2" 2>&1)" || rc=$?
  if [ "$rc" -ne 1 ] || ! printf '%s\n' "$out" | grep -q "^$2 "; then
    printf 'exit %s\n%s\n' "$rc" "$out"
    return 1
  fi
}

# output_lacks <needle> <cmd...>: the command's output does not contain the needle.
output_lacks() {
  local needle="$1" out
  shift
  out="$("$@" 2>&1)" || true
  case "$out" in
    *"$needle"*)
      printf '%s\n' "$out"
      return 1
      ;;
  esac
}

# prints_nothing <cmd...>: the command's output is empty (its exit code is not looked at).
prints_nothing() {
  local out
  out="$("$@" 2>&1)" || true
  [ -z "$out" ] || { printf '%s\n' "$out"; return 1; }
}

# run_one <rule> <file name under Assets/_Project> <file content>: run one rule on a one-file Assets folder.
run_one() {
  local dir
  dir="$(mktemp -d "$WORK/one.XXXXXX")"
  mkdir -p "$dir/Assets/_Project"
  printf '%s' "$3" > "$dir/Assets/_Project/$2"
  "$CHECK" --root "$dir/Assets" --rules "$1" < /dev/null
}

# put <repo> <path> <content>: write a file below a repo.
put() {
  mkdir -p "$(dirname "$1/$2")"
  printf '%s\n' "$3" > "$1/$2"
}

# new_repo <dir>: an empty git repo.
new_repo() {
  mkdir -p "$1"
  git -C "$1" -c init.defaultBranch=main init -q
}

# copy_checks <dir>: put the check scripts into <dir>/tools/checks, as in a checkout of the repo.
copy_checks() {
  mkdir -p "$1/tools/checks"
  cp "$CHECKS/check_sources_of_truth.sh" "$CHECKS/install-hooks.sh" "$1/tools/checks/"
  cp -R "$CHECKS/lib" "$1/tools/checks/lib"
}

BAD_USS='.a { background-image: linear-gradient(red, blue); }'
UNITY="unity/MotionBowling/Assets/_Project"

# ---------------------------------------------------------------------------------------------
# Fixtures, one pass and one fail case per rule. The second round runs from a path with a space.
# ---------------------------------------------------------------------------------------------
cp -R "$FIXTURES" "$SPACED/fixtures"
for fx in "$FIXTURES" "$SPACED/fixtures"; do
  for rule in G-1 G-2 G-3 G-3b; do
    assert_exit 0 check_pass "$fx" "$rule"
    assert_exit 0 check_fail "$fx" "$rule"
  done
done

# Each violation line is "<rule> <path>[:<line>] <message>", with paths starting at Assets/.
G1="$FIXTURES/fail/G-1/Assets"
assert_output_contains "G-1 Assets/_Project/Styles/Gradient.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Shadow.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Extra.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Extra.uss:5 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/Menus/Outside.uxml:2 " "$CHECK" --root "$G1" --rules G-1
assert_exit 0 output_lacks "Extra.uss:8" "$CHECK" --root "$G1" --rules G-1

# G-2 skips lines that start with //, but still flags the same token in code (Review Focus 4).
G2="$FIXTURES/fail/G-2/Assets"
assert_output_contains "G-2 Assets/_Project/Scripts/Bad.cs:4 " "$CHECK" --root "$G2" --rules G-2
assert_output_contains "G-2 Assets/_Project/Scripts/Bad.cs:5 " "$CHECK" --root "$G2" --rules G-2
assert_output_contains "G-2 Assets/_Project/Scripts/Bad.cs:6 " "$CHECK" --root "$G2" --rules G-2
assert_exit 0 output_lacks "Bad.cs:1 " "$CHECK" --root "$G2" --rules G-2
assert_exit 0 output_lacks "Bad.cs:2 " "$CHECK" --root "$G2" --rules G-2

# G-3 covers .shadergraph and .shadersubgraph.
G3="$FIXTURES/fail/G-3/Assets"
assert_output_contains "G-3 Assets/_Project/Shaders/Ramp.shadergraph:7 " "$CHECK" --root "$G3" --rules G-3
assert_output_contains "G-3 Assets/_Project/Shaders/Sub.shadersubgraph:6 " "$CHECK" --root "$G3" --rules G-3

# G-3b: .shader, .hlsl and .shadergraph under _Project need an entry in the reviewed list.
G3B="$FIXTURES/fail/G-3b/Assets"
assert_output_contains "G-3b Assets/_Project/Shaders/Flat.hlsl " "$CHECK" --root "$G3B" --rules G-3b
assert_output_contains "G-3b Assets/_Project/Shaders/Unlit.shader " "$CHECK" --root "$G3B" --rules G-3b
assert_output_contains "G-3b Assets/_Project/Shaders/Graph.shadergraph " "$CHECK" --root "$G3B" --rules G-3b
assert_exit 0 "$CHECK" --root "$G3B" --rules G-3b --reviewed "$FIXTURES/pass/G-3b/reviewed-shaders.txt"
assert_exit 1 "$CHECK" --root "$FIXTURES/pass/G-3b/Assets" --rules G-3b --reviewed "$FIXTURES/fail/G-3b/reviewed-shaders.txt"

# ---------------------------------------------------------------------------------------------
# Every banned token on its own line (the spec's lists, spelled out here on purpose).
# ---------------------------------------------------------------------------------------------
while IFS= read -r token; do
  assert_exit 1 run_one G-1 T.uss "x: ${token}red, blue);"
done <<'EOF'
linear-gradient(
radial-gradient(
conic-gradient(
EOF
assert_exit 0 run_one G-1 T.uss "x: linear-gradient;"

while IFS= read -r token; do
  assert_exit 1 run_one G-2 T.cs "var x = $token;"
  assert_exit 1 run_one G-2 T.cs "    $token // not a full-line comment"
  assert_exit 0 run_one G-2 T.cs "    // $token"
  assert_exit 0 run_one G-2 T.cs "//$token"
done <<'EOF'
new Gradient(
GradientColorKey
GradientAlphaKey
colorGradient
enableVertexGradient
VertexGradient
TMP_ColorGradient
AmbientMode.Trilight
AmbientMode.Skybox
ambientSkyColor
ambientEquatorColor
ambientGroundColor
RenderSettings.fog = true
Vignette
ScreenSpaceLensFlare
EOF
assert_exit 0 run_one G-2 T.cs "var x = AmbientMode.Flat; RenderSettings.fog = false;"

while IFS= read -r token; do
  assert_exit 1 run_one G-3 T.shadergraph "\"m_Type\": \"UnityEditor.ShaderGraph.$token\""
  assert_exit 1 run_one G-3 T.shadersubgraph "\"m_Type\": \"UnityEditor.ShaderGraph.$token\""
done <<'EOF'
GradientNode
SampleGradient
BlackbodyNode
EOF

# ---------------------------------------------------------------------------------------------
# G-1 text-shadow: only a non-zero third length (the blur radius) fails.
# ---------------------------------------------------------------------------------------------
shadow() { run_one G-1 T.uss "$(printf '.a {\n    %s\n}\n' "$1")"; }

assert_exit 1 shadow 'text-shadow: 2px 2px 4px black;'
assert_exit 1 shadow 'text-shadow: 2px 2px 0.5px black;'
assert_exit 1 shadow 'text-shadow: 2px 2px .5px black;'
assert_exit 1 shadow 'text-shadow: -1px -1px 2px #000;'
assert_exit 1 shadow 'text-shadow: black 2px 2px 4px;'
assert_exit 1 shadow 'text-shadow: rgba(10, 20, 30, 0.5) 1px 1px 3px;'
assert_exit 1 shadow 'text-shadow:2px 2px 3px red;'
assert_exit 1 shadow 'text-shadow : 2px 2px 3px red;'
assert_exit 1 shadow 'text-shadow: 1px 1px 2px red'
assert_exit 0 shadow 'text-shadow: 2px 2px 0px black;'
assert_exit 0 shadow 'text-shadow: 2px 2px 0 black;'
assert_exit 0 shadow 'text-shadow: 2px 2px 0.0px black;'
assert_exit 0 shadow 'text-shadow: 2px 2px;'
assert_exit 0 shadow 'text-shadow: 2px 2px black'
assert_exit 0 shadow 'text-shadow: -1px -1px 0 #000;'
assert_exit 0 shadow 'text-shadow: rgba(10, 20, 30, 0.5) 1px 1px 0;'
assert_exit 0 shadow 'text-shadow: none;'
assert_exit 0 shadow 'transition-property: text-shadow;'
# A value that runs to the end of the file, and one that continues on the next lines.
assert_exit 1 run_one G-1 T.uss 'text-shadow: 1px 1px 2px red'
assert_exit 0 run_one G-1 T.uss 'text-shadow: 1px 1px 0px red'
assert_exit 1 shadow 'text-shadow: 1px
        1px
        2px red;'
assert_exit 1 run_one G-1 T.uxml '<ui:Label style="text-shadow: 1px 1px 2px black;" />'
assert_exit 0 run_one G-1 T.uxml '<ui:Label style="text-shadow: 1px 1px 0px black;" />'
assert_output_contains "found 4px" shadow 'text-shadow: 2px 2px 4px black;'

# ---------------------------------------------------------------------------------------------
# Rule selection and the default rule set.
# ---------------------------------------------------------------------------------------------
assert_exit 0 "$CHECK" --root "$G2" --rules G-1
assert_exit 1 "$CHECK" --root "$G2" --rules G-1,G-2
assert_exit 1 "$CHECK" --root "$G2"
assert_exit 1 "$CHECK" --root "$G1"
assert_exit 0 "$CHECK" --root "$FIXTURES/pass/G-1/Assets"
assert_exit 0 "$CHECK" --root "$FIXTURES/pass/G-3b/Assets"

# Usage errors exit 2.
assert_exit 2 "$CHECK"
assert_exit 2 "$CHECK" --bogus
assert_exit 2 "$CHECK" --all --staged
assert_exit 2 "$CHECK" --root
assert_exit 2 "$CHECK" --root "$WORK/does not exist"
assert_exit 2 "$CHECK" --root "$G1" --rules G-99
assert_exit 2 "$CHECK" --root "$G1" --rules
assert_exit 2 "$CHECK" --root "$G1" --reviewed "$WORK/missing.txt"

# ---------------------------------------------------------------------------------------------
# The runner's rule contract, with rules injected into a copy of the lib: a warning doesn't fail
# the run, a violation does, and a rule that can't run is an error rather than a silent pass.
# ---------------------------------------------------------------------------------------------
X="$SPACED/extra rules repo"
copy_checks "$X"
cat >> "$X/tools/checks/lib/rules_gradients.sh" <<'EOF'
ALL_RULES="$ALL_RULES T-1 T-2"
rule_T_1() { echo "WARN T-1 Assets/x.cs:1 only a warning"; }
rule_T_2() { echo "T-2 Assets/x.cs:2 a violation"; }
EOF
X_CHECK="$X/tools/checks/check_sources_of_truth.sh"
X_ROOT="$FIXTURES/pass/G-1/Assets"
assert_exit 0 "$X_CHECK" --root "$X_ROOT" --rules T-1
assert_output_contains "WARN T-1 Assets/x.cs:1 only a warning" "$X_CHECK" --root "$X_ROOT" --rules T-1
assert_exit 1 "$X_CHECK" --root "$X_ROOT" --rules T-1,T-2
assert_exit 1 "$X_CHECK" --root "$X_ROOT"
assert_exit 0 "$X_CHECK" --root "$X_ROOT" --rules G-1
printf 'rule_G_1() { return 1; }\n' >> "$X/tools/checks/lib/rules_gradients.sh"
assert_exit 2 "$X_CHECK" --root "$X_ROOT" --rules G-1
assert_output_contains "rule G-1 failed to run" "$X_CHECK" --root "$X_ROOT" --rules G-1

# ---------------------------------------------------------------------------------------------
# --all: scans unity/MotionBowling/Assets of the checkout the script lives in.
# ---------------------------------------------------------------------------------------------
MINI="$SPACED/mini repo"
copy_checks "$MINI"
MINI_CHECK="$MINI/tools/checks/check_sources_of_truth.sh"
assert_exit 0 in_dir "$WORK" "$MINI_CHECK" --all
assert_output_contains "nothing to scan: unity/MotionBowling/Assets not found" in_dir "$WORK" "$MINI_CHECK" --all
put "$MINI" "$UNITY/a.uss" "$BAD_USS"
assert_exit 1 in_dir "$WORK" "$MINI_CHECK" --all
assert_output_contains "G-1 Assets/_Project/a.uss:1 " in_dir "$WORK" "$MINI_CHECK" --all
put "$MINI" "$UNITY/Shaders/Flat.hlsl" 'float4 F() { return 0; }'
assert_output_contains "G-3b Assets/_Project/Shaders/Flat.hlsl " in_dir "$WORK" "$MINI_CHECK" --all
put "$MINI" "tools/checks/reviewed-shaders.txt" 'Assets/_Project/Shaders/Flat.hlsl # reviewed by test on 2026-10-18: flat'
assert_exit 0 output_lacks "G-3b" in_dir "$WORK" "$MINI_CHECK" --all

# ---------------------------------------------------------------------------------------------
# --staged: reads the index, only unity/MotionBowling/Assets, never a deleted file (Review Focus 2).
# ---------------------------------------------------------------------------------------------
R="$SPACED/staged repo"
new_repo "$R"
put "$R" "$UNITY/a.uss" "$BAD_USS"
git -C "$R" add -A
assert_exit 1 in_dir "$R" "$CHECK" --staged
assert_output_contains "G-1 Assets/_Project/a.uss:1 " in_dir "$R" "$CHECK" --staged

# The index wins over the working tree, in both directions.
put "$R" "$UNITY/a.uss" '.a { color: red; }'
assert_exit 1 in_dir "$R" "$CHECK" --staged
git -C "$R" add -A
assert_exit 0 in_dir "$R" "$CHECK" --staged
put "$R" "$UNITY/a.uss" "$BAD_USS"
assert_exit 0 in_dir "$R" "$CHECK" --staged

# A path with spaces, and files outside the Unity Assets folder or in excluded folders.
put "$R" "$UNITY/My Styles/b b.uss" "$BAD_USS"
git -C "$R" add -A
assert_output_contains "G-1 Assets/_Project/My Styles/b b.uss:1 " in_dir "$R" "$CHECK" --staged

# Deleted file: exit 0 and nothing printed.
D="$SPACED/deleted repo"
new_repo "$D"
put "$D" "$UNITY/a.uss" "$BAD_USS"
git -C "$D" add -A
git -C "$D" commit -q -m init
git -C "$D" rm -q "$UNITY/a.uss"
assert_exit 0 in_dir "$D" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$D" "$CHECK" --staged

# Renamed file: a clean one passes, a banned one is still found under its new name.
N="$SPACED/renamed repo"
new_repo "$N"
put "$N" "$UNITY/clean.uss" '.a { color: red; }'
put "$N" "$UNITY/bad.uss" "$BAD_USS"
git -C "$N" add -A
git -C "$N" commit -q -m init
git -C "$N" mv "$UNITY/clean.uss" "$UNITY/clean2.uss"
assert_exit 0 in_dir "$N" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$N" "$CHECK" --staged
git -C "$N" mv "$UNITY/bad.uss" "$UNITY/bad2.uss"
assert_exit 1 in_dir "$N" "$CHECK" --staged
assert_output_contains "G-1 Assets/_Project/bad2.uss:1 " in_dir "$N" "$CHECK" --staged

# Nothing in scope staged: a README, a banned fixture under tools/, the Tooling folder, a PNG.
O="$SPACED/outside repo"
new_repo "$O"
put "$O" README.md 'hello'
put "$O" "tools/checks/fixtures/fail/G-1/Assets/_Project/Styles/Gradient.uss" "$BAD_USS"
put "$O" "unity/MotionBowling/Packages/x/y.uss" "$BAD_USS"
put "$O" "$UNITY/Modules/Tooling/Editor/Banned.uss" "$BAD_USS"
put "$O" "$UNITY/Art/a.png" 'not really a png'
git -C "$O" add -A
assert_exit 0 in_dir "$O" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$O" "$CHECK" --staged
# Staging only README.md.
O2="$SPACED/readme repo"
new_repo "$O2"
put "$O2" README.md 'hello'
git -C "$O2" add README.md
assert_exit 0 in_dir "$O2" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$O2" "$CHECK" --staged

# Nothing staged at all, and not a git repo.
E="$SPACED/empty repo"
new_repo "$E"
assert_exit 0 in_dir "$E" "$CHECK" --staged
mkdir -p "$WORK/not a repo"
assert_exit 2 in_dir "$WORK/not a repo" env GIT_CEILING_DIRECTORIES="$WORK" "$CHECK" --staged

# G-3b reads the reviewed list from the index, not from the working tree.
S="$SPACED/shader repo"
new_repo "$S"
put "$S" "$UNITY/Shaders/Flat.hlsl" 'float4 F() { return 0; }'
git -C "$S" add -A
assert_output_contains "G-3b Assets/_Project/Shaders/Flat.hlsl " in_dir "$S" "$CHECK" --staged
put "$S" "tools/checks/reviewed-shaders.txt" 'Assets/_Project/Shaders/Flat.hlsl # reviewed by test on 2026-10-18: flat'
assert_exit 1 in_dir "$S" "$CHECK" --staged
git -C "$S" add tools/checks/reviewed-shaders.txt
assert_exit 0 in_dir "$S" "$CHECK" --staged

# ---------------------------------------------------------------------------------------------
# install-hooks.sh and the pre-commit hook.
# ---------------------------------------------------------------------------------------------
H="$SPACED/hook repo"
new_repo "$H"
copy_checks "$H"
put "$H" README.md 'hello'
git -C "$H" add README.md
git -C "$H" commit -q -m init

# Installing twice is harmless: the hook is overwritten with the same content.
assert_exit 0 in_dir "$H" bash tools/checks/install-hooks.sh
cp "$H/.git/hooks/pre-commit" "$WORK/pre-commit.first"
assert_exit 0 in_dir "$H" bash tools/checks/install-hooks.sh
assert_exit 0 test -x "$H/.git/hooks/pre-commit"
assert_exit 0 cmp "$H/.git/hooks/pre-commit" "$WORK/pre-commit.first"

# Script present: a violation blocks the commit, a clean change passes.
put "$H" "$UNITY/a.uss" "$BAD_USS"
git -C "$H" add "$UNITY/a.uss"
assert_exit 1 git -C "$H" commit -q -m bad
assert_output_contains "G-1 Assets/_Project/a.uss:1 " git -C "$H" commit -q -m bad
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 1
put "$H" "$UNITY/a.uss" '.a { color: red; }'
git -C "$H" add "$UNITY/a.uss"
assert_exit 0 git -C "$H" commit -q -m clean
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 2

# Script absent (a checkout without the checks, such as main before the merge): skip and pass.
rm "$H/tools/checks/check_sources_of_truth.sh"
put "$H" "$UNITY/a.uss" "$BAD_USS"
git -C "$H" add "$UNITY/a.uss"
assert_output_contains "source-of-truth checks not present in this checkout; skipping" git -C "$H" commit -q -m skipped
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 3

# A git worktree shares the hooks directory of its main checkout (git rev-parse --git-path hooks).
W="$SPACED/main checkout"
WT="$SPACED/linked worktree"
new_repo "$W"
put "$W" README.md 'hello'
git -C "$W" add README.md
git -C "$W" commit -q -m init
git -C "$W" worktree add -q -b linked "$WT"
copy_checks "$WT"
assert_exit 0 in_dir "$WT" bash tools/checks/install-hooks.sh
assert_exit 0 test -x "$W/.git/hooks/pre-commit"
put "$WT" "$UNITY/a.uss" "$BAD_USS"
git -C "$WT" add "$UNITY/a.uss"
assert_exit 1 git -C "$WT" commit -q -m bad
assert_output_contains "G-1 Assets/_Project/a.uss:1 " git -C "$WT" commit -q -m bad
# The main checkout has no checks (copy_checks went to the worktree only), so its hook skips.
put "$W" README.md 'changed'
git -C "$W" add README.md
assert_output_contains "source-of-truth checks not present in this checkout; skipping" git -C "$W" commit -q -m ok

finish
