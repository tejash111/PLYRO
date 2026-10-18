#!/usr/bin/env bash
# Fast text checks for the sources of truth (SOT-1 no gradients, SOT-2 modular structure).
# They run in the pre-commit hook and in tools/ci/verify.sh. Rules: foundation spec, section 8.
#
#   check_sources_of_truth.sh --all | --staged | --root <AssetsDir> [--rules <id,id>] [--reviewed <file>]
#
#   --all       scan unity/MotionBowling/Assets of this checkout
#   --staged    scan the staged files under unity/MotionBowling/Assets, as they are in the index
#   --root DIR  scan a folder that stands in for Assets (the test fixtures)
#   --rules     run only these rules, for example G-1,G-2 (default: every rule)
#   --reviewed  the reviewed-shaders list for G-3b. Default: tools/checks/reviewed-shaders.txt
#               (with --staged, the staged copy of it; with --root, <DIR>/../reviewed-shaders.txt)
#
# Output, one line per violation:   <rule-id> <path>[:<line>] <message>
# Paths start at Assets/. A line that starts with "WARN " is a warning and doesn't fail the run.
# Exit codes: 0 clean (warnings allowed), 1 violations, 2 usage or internal error.
#
# Adding a rule: write rule_<id with - as _> in a lib/rules_*.sh file, append the id to ALL_RULES
# there, and source that file below. A rule prints its violation lines. It finds files with
# list_files and scans them in batches with run_awk or scan_tokens.
#
# Written for macOS system bash 3.2 and BSD grep/awk/sed: no bash 4 features, no GNU-only flags.
set -u
export LC_ALL=C

CHECKS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$CHECKS_DIR/../.." && pwd)"
UNITY_ASSETS="unity/MotionBowling/Assets"
# The file types the rules look at. Staged mode only copies these out of the index.
SCANNED_EXTENSIONS="cs uss uxml shader hlsl shadergraph shadersubgraph"

die() {
  echo "check_sources_of_truth.sh: $*" >&2
  exit 2
}

usage() {
  echo "usage: check_sources_of_truth.sh --all | --staged | --root <AssetsDir> [--rules <id,id>] [--reviewed <file>]"
}

ALL_RULES=""
# shellcheck source=lib/rules_gradients.sh
source "$CHECKS_DIR/lib/rules_gradients.sh"

# ---------------------------------------------------------------------------------------------
# Arguments
# ---------------------------------------------------------------------------------------------
MODE="" ROOT_ARG="" RULES_ARG="" REVIEWED_ARG="" RULES_GIVEN=0

set_mode() {
  [ -z "$MODE" ] || die "choose one of --all, --staged and --root"
  MODE="$1"
}

while [ $# -gt 0 ]; do
  case "$1" in
    --all) set_mode all ;;
    --staged) set_mode staged ;;
    --root)
      [ $# -ge 2 ] || die "--root needs a folder"
      set_mode root
      ROOT_ARG="$2"
      shift
      ;;
    --rules)
      [ $# -ge 2 ] || die "--rules needs a list such as G-1,G-2"
      RULES_GIVEN=1
      RULES_ARG="$2"
      shift
      ;;
    --reviewed)
      [ $# -ge 2 ] || die "--reviewed needs a file"
      REVIEWED_ARG="$2"
      shift
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *) die "unknown argument: $1 ($(usage))" ;;
  esac
  shift
done
[ -n "$MODE" ] || die "$(usage)"

SELECTED=" $ALL_RULES "
if [ "$RULES_GIVEN" -eq 1 ]; then
  SELECTED=" $(printf '%s' "$RULES_ARG" | tr ',' ' ') "
  [ "$SELECTED" != "  " ] || die "--rules needs a list such as G-1,G-2"
  for id in $SELECTED; do
    case " $ALL_RULES " in
      *" $id "*) ;;
      *) die "unknown rule: $id (rules:$ALL_RULES)" ;;
    esac
  done
fi
if [ -n "$REVIEWED_ARG" ]; then
  [ -f "$REVIEWED_ARG" ] || die "--reviewed: no such file: $REVIEWED_ARG"
  REVIEWED_ARG="$(cd "$(dirname "$REVIEWED_ARG")" && pwd)/$(basename "$REVIEWED_ARG")"
fi

TMP="$(mktemp -d)" || die "cannot create a temporary folder"
trap 'rm -rf "$TMP"' EXIT

# ---------------------------------------------------------------------------------------------
# Scope: sets ROOT (the folder that plays Assets) and REVIEWED (the reviewed-shaders list, which
# may not exist; that means an empty list).
# ---------------------------------------------------------------------------------------------

# In staged mode the rules scan a copy of the staged files taken from the index, so a file that
# was edited after `git add`, or deleted, is never read from the working tree.
snapshot_staged() {
  local top ext
  top="$(git rev-parse --show-toplevel 2>/dev/null)" || die "--staged needs a git repository"
  cd "$top" || die "cannot enter $top"
  set --
  for ext in $SCANNED_EXTENSIONS; do
    set -- "$@" "$UNITY_ASSETS/*.$ext"
  done
  git diff --cached --name-only -z --diff-filter=ACMR -- "$@" > "$TMP/staged.list" || die "git diff failed"
  [ -s "$TMP/staged.list" ] || exit 0
  mkdir "$TMP/snapshot"
  git checkout-index --prefix="$TMP/snapshot/" -z --stdin < "$TMP/staged.list" || die "git checkout-index failed"
  ROOT="$TMP/snapshot/$UNITY_ASSETS"
  git show ":tools/checks/reviewed-shaders.txt" > "$TMP/reviewed-staged.txt" 2>/dev/null || : > "$TMP/reviewed-staged.txt"
  REVIEWED="$TMP/reviewed-staged.txt"
}

case "$MODE" in
  all)
    ROOT="$REPO/$UNITY_ASSETS"
    if [ ! -d "$ROOT" ]; then
      echo "nothing to scan: $UNITY_ASSETS not found"
      exit 0
    fi
    REVIEWED="$REPO/tools/checks/reviewed-shaders.txt"
    ;;
  root)
    [ -d "$ROOT_ARG" ] || die "--root: not a folder: $ROOT_ARG"
    ROOT="$(cd "$ROOT_ARG" && pwd)"
    REVIEWED="$ROOT/../reviewed-shaders.txt"
    ;;
  staged)
    snapshot_staged
    ;;
esac
[ -z "$REVIEWED_ARG" ] || REVIEWED="$REVIEWED_ARG"

# FILE_LIST: every file under ROOT, relative to it, minus the excluded folders: Tooling holds the
# banned patterns as data, FMOD is vendor code. (tools/checks and Packages are outside Assets.)
FILE_LIST="$TMP/files.txt"
(cd "$ROOT" && find . -type f | sed 's|^\./||' | sort) \
  | grep -v -e '^_Project/Modules/Tooling/' -e '^Plugins/FMOD/' > "$FILE_LIST"

# ---------------------------------------------------------------------------------------------
# Helpers for the rules
# ---------------------------------------------------------------------------------------------

# list_files <scope> "<ext> <ext>...": the files with these extensions, relative to ROOT.
# Scope "project" is Assets/_Project only; "assets" is all of Assets.
list_files() {
  local scope="$1" exts="$2" ext re=""
  for ext in $exts; do
    re="$re${re:+|}$ext"
  done
  if [ "$scope" = project ]; then
    grep -E "^_Project/.*[.]($re)\$" "$FILE_LIST"
  else
    grep -E "[.]($re)\$" "$FILE_LIST"
  fi
}

# run_awk <scope> "<exts>" <awk args...>: run awk over those files, in batches. awk runs from ROOT,
# so FILENAME is relative to it and a rule prints "Assets/" FILENAME.
run_awk() {
  local scope="$1" exts="$2" files
  shift 2
  files="$(list_files "$scope" "$exts")"
  [ -n "$files" ] || return 0
  printf '%s\n' "$files" | tr '\n' '\0' | (cd "$ROOT" && xargs -0 awk "$@")
}

# scan_tokens <rule> <scope> "<exts>" <skip // comment lines: 0|1> <message> <patterns>
# <patterns> is one extended regular expression per line (write a literal dot as [.], a literal
# parenthesis as [(]). Prints one violation per matching line: the first pattern that matches.
IFS= read -r -d '' AWK_SCAN_TOKENS <<'EOF' || true
BEGIN { n = split(ENVIRON["SOT_PATTERNS"], pat, "\n") }
skipc == 1 && $0 ~ /^[[:space:]]*\/\// { next }
{
  for (i = 1; i <= n; i++) {
    if (pat[i] != "" && match($0, pat[i])) {
      print rule " Assets/" FILENAME ":" FNR " " msg ": '" substr($0, RSTART, RLENGTH) "'"
      break
    }
  }
}
EOF

scan_tokens() {
  local rule="$1" scope="$2" exts="$3" skipc="$4" msg="$5"
  export SOT_PATTERNS="$6"
  run_awk "$scope" "$exts" -v rule="$rule" -v skipc="$skipc" -v msg="$msg" "$AWK_SCAN_TOKENS"
}

# ---------------------------------------------------------------------------------------------
# Run the selected rules, print what they found, and set the exit code
# ---------------------------------------------------------------------------------------------
run_rules() {
  local id
  for id in $ALL_RULES; do
    case "$SELECTED" in
      *" $id "*) ;;
      *) continue ;;
    esac
    "rule_${id//-/_}" || {
      echo "check_sources_of_truth.sh: rule $id failed to run" >&2
      : > "$TMP/failed"
    }
  done
}

OUT="$(run_rules)"
[ ! -f "$TMP/failed" ] || exit 2
if [ -n "$OUT" ]; then
  printf '%s\n' "$OUT"
  if printf '%s\n' "$OUT" | grep -qv '^WARN '; then
    exit 1
  fi
fi
exit 0
