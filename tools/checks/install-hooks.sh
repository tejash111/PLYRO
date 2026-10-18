#!/usr/bin/env bash
# Installs the git pre-commit hook that runs the source-of-truth checks on the staged files.
# Run it once per clone: bash tools/checks/install-hooks.sh
# Running it again overwrites the hook, which is harmless.
#
# The hooks folder comes from `git rev-parse --git-path hooks`, so this also works from a git
# worktree. A worktree shares the hooks folder of its main checkout, so the hook it writes also
# runs there. That is why the hook skips itself in a checkout that doesn't have the checks yet.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
HOOKS="$(git -C "$REPO" rev-parse --git-path hooks)"
case "$HOOKS" in
  /*) ;;
  *) HOOKS="$REPO/$HOOKS" ;;
esac
mkdir -p "$HOOKS"

cat > "$HOOKS/pre-commit" <<'EOF'
#!/bin/sh
# Source-of-truth checks on the staged files. Written by tools/checks/install-hooks.sh;
# run that script again to update this hook.
root="$(git rev-parse --show-toplevel)" || exit 1
cd "$root" || exit 1
checks="tools/checks/check_sources_of_truth.sh"
if [ ! -f "$checks" ]; then
  echo "source-of-truth checks not present in this checkout; skipping"
  exit 0
fi
exec bash "$checks" --staged
EOF
chmod +x "$HOOKS/pre-commit"
echo "installed $HOOKS/pre-commit"
