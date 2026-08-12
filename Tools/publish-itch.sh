#!/bin/bash
#
# Publishes the WebGL build in Builds/ to itch.io with butler.
#
# The build itself is never committed. A ~20MB payload committed on every deploy is what put 34
# builds into a sister project's git history before anyone noticed, so Builds/ is gitignored and
# this script uploads straight from the working tree.
#
# Requires:
#   butler on PATH, or installed to $BUTLER  (https://itch.io/docs/butler/)
#   butler login  (once, interactively — or BUTLER_API_KEY in the environment)
#   ITCH_TARGET set below, or in Tools/itch_target
#
set -euo pipefail

cd "$(dirname "$0")/.."

BUILD_DIR="Builds"
TARGET_FILE="Tools/itch_target"

if [ ! -f "$BUILD_DIR/index.html" ]; then
  echo "No build in $BUILD_DIR — run the WebGL build first:" >&2
  echo "  mooserunnerCli force-recompile && touch .dungeon-build-webgl" >&2
  exit 1
fi

# The itch target is user:game:channel, e.g. norritt42/dungeon-leaderboard:html5.
# Kept in a file rather than hardcoded so the script is the same for anyone who forks it.
if [ -n "${ITCH_TARGET:-}" ]; then
  TARGET="$ITCH_TARGET"
elif [ -f "$TARGET_FILE" ]; then
  TARGET="$(tr -d '[:space:]' < "$TARGET_FILE")"
else
  echo "No itch target. Write one to $TARGET_FILE, for example:" >&2
  echo "  echo 'yourname/dungeon-leaderboard:html5' > $TARGET_FILE" >&2
  exit 1
fi

BUTLER="${BUTLER:-butler}"
if ! command -v "$BUTLER" >/dev/null 2>&1; then
  echo "butler not found on PATH. Install it from https://itch.io/docs/butler/" >&2
  echo "or set BUTLER=/path/to/butler" >&2
  exit 1
fi

# The version butler records is the one the player's browser caches against, so it comes from the
# build rather than from the clock — two uploads of the same build must not look like two versions.
VERSION="$(grep -o '0\.1\.[0-9]*' "$BUILD_DIR/index.html" | head -1 || true)"
if [ -z "$VERSION" ]; then
  echo "Could not read a version out of $BUILD_DIR/index.html; uploading without one." >&2
  "$BUTLER" push "$BUILD_DIR" "$TARGET"
else
  echo "publishing $VERSION to $TARGET"
  "$BUTLER" push "$BUILD_DIR" "$TARGET" --userversion "$VERSION"
fi

echo "done — check https://itch.io/dashboard"
