#!/usr/bin/env bash
# Judges a Unity test results file (NUnit3 XML). Unity documents no process exit codes for
# test runs, so this file is the only reliable verdict.
#
# Usage: check_test_results.sh <results.xml>
#
# Exit 0 and "PASS total=N" only if the file exists and the <test-run> element has
#   total > 0, failed="0" and a result that starts with "Passed".
# Otherwise prints "FAIL <reason>", then the fullname of every failed <test-case>, and exits 1.
# Exit 2 on a usage error.
#
# Only the attributes of the <test-run> element are read, in any order. The XML may be one very
# long line or have tags spread over several lines. No XML tools needed: bash 3.2 + grep + sed.
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "usage: check_test_results.sh <results.xml>" >&2
  exit 2
fi
xml="$1"

# tags_of <element>: the opening tag of every <element ...>, one per line. Newlines are flattened
# first because a tag can span lines. Printing nothing is not an error.
tags_of() {
  tr '\r\n' '  ' < "$xml" | grep -o "<$1[[:space:]][^>]*>" || true
}

# attr <tag> <name>: the value of attribute <name> in <tag>, or nothing if it has none.
attr() {
  printf '%s' "$1" | sed -n "s/.*[[:space:]]$2=\"\([^\"]*\)\".*/\1/p"
}

# Decode the XML escapes NUnit writes into attribute values (&amp; last).
unescape() {
  sed -e 's/&quot;/"/g' -e "s/&apos;/'/g" -e 's/&lt;/</g' -e 's/&gt;/>/g' -e 's/&amp;/\&/g'
}

list_failed_cases() {
  local tag name
  tags_of test-case | while IFS= read -r tag; do
    case "$(attr "$tag" result)" in
      Failed*)
        name="$(attr "$tag" fullname | unescape)"
        printf '  %s\n' "$name"
        ;;
    esac
  done
}

# fail <reason>: print the verdict and the failed test cases, then exit 1.
fail() {
  printf 'FAIL %s\n' "$1"
  list_failed_cases
  exit 1
}

if [ ! -f "$xml" ]; then
  printf 'FAIL no results file: %s\n' "$xml"
  exit 1
fi

run_tag="$(tags_of test-run | head -n 1)"
[ -n "$run_tag" ] || fail "no <test-run> element in $xml"

total="$(attr "$run_tag" total)"
failed="$(attr "$run_tag" failed)"
result="$(attr "$run_tag" result)"

case "$total" in
  ''|*[!0-9]*) fail "<test-run> has no numeric total attribute (total=\"$total\")" ;;
esac
[ "$total" -gt 0 ] || fail "total=$total (no tests ran)"

case "$failed" in
  ''|*[!0-9]*) fail "<test-run> has no numeric failed attribute (failed=\"$failed\")" ;;
esac
[ "$failed" -eq 0 ] || fail "failed=$failed of total=$total"

case "$result" in
  Passed*) ;;
  *) fail "result=\"$result\" (expected Passed), total=$total" ;;
esac

printf 'PASS total=%s\n' "$total"
