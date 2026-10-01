#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

TARGET="${1:-Streams/SchoolBoard}"
SUBMODULE_FULL_PATH="$ROOT_DIR/$TARGET"

echo "=== Submodule Update: $TARGET ==="

if [ ! -d "$SUBMODULE_FULL_PATH" ]; then
    echo "Submodule folder does not exist. Initializing..."
    git -C "$ROOT_DIR" submodule update --init --recursive "$TARGET"
fi

if [ -d "$SUBMODULE_FULL_PATH" ]; then
    echo "Checking for uncommitted changes in $TARGET..."
    if [ -n "$(git -C "$SUBMODULE_FULL_PATH" status --porcelain 2>/dev/null)" ]; then
        echo "Stashing uncommitted changes in $TARGET..."
        git -C "$SUBMODULE_FULL_PATH" stash --include-untracked -m "auto-stash before update $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    else
        echo "Working tree is clean in $TARGET."
    fi

    echo "Fetching and pulling latest revision for $TARGET..."
    current_branch="$(git -C "$SUBMODULE_FULL_PATH" rev-parse --abbrev-ref HEAD 2>/dev/null || echo "")"
    if [ -n "$current_branch" ] && [ "$current_branch" != "HEAD" ]; then
        echo "Pulling branch '$current_branch' from origin..."
        git -C "$SUBMODULE_FULL_PATH" pull --autostash origin "$current_branch"
    else
        echo "Submodule is in detached HEAD or unbranched. Updating via git submodule update..."
        git -C "$ROOT_DIR" submodule update --init --recursive --remote "$TARGET"
    fi
fi

echo "=== Submodule update finished ==="
