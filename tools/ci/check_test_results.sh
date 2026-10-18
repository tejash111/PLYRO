#!/usr/bin/env bash
# Judges a Unity test results file (NUnit3 XML). Unity documents no process exit codes for
# test runs, so this file is the only reliable verdict.
#
# Usage: check_test_results.sh <results.xml>
#
# Exit 0 and "PASS total=N" only if the file exists and the <test-run> element has
#   total > 0, failed="0", inconclusive="0" and a result that starts with "Passed".
# Otherwise prints "FAIL <reason>", then the fullname of every failed <test-case> (or, for
# "FAIL inconclusive=N", of every inconclusive one), and exits 1. Exit 2 on a usage error.
#
# An inconclusive test (a failed Assume.That, Assert.Inconclusive) proved nothing, yet NUnit still
# reports the run as Passed, so it must fail here (foundation spec section 9). A missing
# inconclusive attribute counts as 0, but an inconclusive <test-case> fails the run all the same.
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

# list_cases <result>: the fullname of every <test-case> whose result starts with <result>.
list_cases() {
  local tag name
  tags_of test-case | while IFS= read -r tag; do
    case "$(attr "$tag" result)" in
      "$1"*)
        name="$(attr "$tag" fullname | unescape)"
        printf '  %s\n' "$name"
        ;;
    esac
  done
}

# fail <reason> [<result of the cases to list>]: print the verdict and those test cases (the failed
# ones by default), then exit 1.
fail() {
  printf 'FAIL %s\n' "$1"
  list_cases "${2:-Failed}"
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
inconclusive="$(attr "$run_tag" inconclusive)"
result="$(attr "$run_tag" result)"

case "$total" in
  ''|*[!0-9]*) fail "<test-run> has no numeric total attribute (total=\"$total\")" ;;
esac
[ "$total" -gt 0 ] || fail "total=$total (no tests ran)"

case "$failed" in
  ''|*[!0-9]*) fail "<test-run> has no numeric failed attribute (failed=\"$failed\")" ;;
esac
[ "$failed" -eq 0 ] || fail "failed=$failed of total=$total"

# The attribute may be absent (that counts as 0), but not present and not a number.
if [ -n "$inconclusive" ]; then
  case "$inconclusive" in
    *[!0-9]*) fail "<test-run> has a non-numeric inconclusive attribute (inconclusive=\"$inconclusive\")" ;;
  esac
else
  inconclusive=0
fi
# A <test-case> that says Inconclusive fails the run even if the attribute says 0.
if [ "$inconclusive" -eq 0 ]; then
  inconclusive="$(list_cases Inconclusive | grep -c '' || true)"
fi
[ "$inconclusive" -eq 0 ] || fail "inconclusive=$inconclusive" Inconclusive

case "$result" in
  Passed*) ;;
  *) fail "result=\"$result\" (expected Passed), total=$total" ;;
esac

printf 'PASS total=%s\n' "$total"
