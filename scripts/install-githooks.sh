#!/usr/bin/env bash
# One-time setup: activates the repo's shared git hooks (secret-scanning pre-commit and pre-push).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOOKS_DIR="$REPO_ROOT/githooks"

if [ ! -d "$HOOKS_DIR" ]; then
    echo "Error: githooks directory not found at $HOOKS_DIR" >&2
    exit 1
fi

chmod +x "$HOOKS_DIR"/* 2>/dev/null || true
git -C "$REPO_ROOT" config core.hooksPath githooks

echo "Git hooks successfully installed: core.hooksPath -> githooks/"
echo "Pre-commit and pre-push hooks are now active."
