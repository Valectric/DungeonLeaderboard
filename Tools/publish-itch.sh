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

# REFUSE AN UNFINISHED BUILD. On 2026-08-17 this script uploaded a wasm of zero bytes: the build was
# still running, the file had just been truncated, and nothing here looked. The game could not load
# for anyone who opened it. Two independent checks, because either alone missed it.
#
# 1. Both big artefacts must be present and substantial. They are written in DIFFERENT PHASES, so
#    checking one proves nothing about the other, and a truncated file exists at zero bytes.
BIG_FILES="Build/Builds.data.unityweb Build/Builds.wasm.unityweb"
for f in $BIG_FILES; do
  if [ ! -f "$BUILD_DIR/$f" ]; then
    echo "REFUSING TO PUBLISH: $BUILD_DIR/$f is missing — the build has not finished." >&2
    exit 1
  fi
  bytes="$(wc -c < "$BUILD_DIR/$f" | tr -d '[:space:]')"
  if [ "$bytes" -lt 1000000 ]; then
    echo "REFUSING TO PUBLISH: $f is only $bytes bytes." >&2
    echo "That is a build mid-write, not a small build. Wait for it to finish." >&2
    exit 1
  fi
done

# 2. Nothing may still be growing. A file can be large and incomplete.
before="$(wc -c < "$BUILD_DIR/Build/Builds.data.unityweb")$(wc -c < "$BUILD_DIR/Build/Builds.wasm.unityweb")"
sleep 4
after="$(wc -c < "$BUILD_DIR/Build/Builds.data.unityweb")$(wc -c < "$BUILD_DIR/Build/Builds.wasm.unityweb")"
if [ "$before" != "$after" ]; then
  echo "REFUSING TO PUBLISH: the build artefacts are still changing size." >&2
  exit 1
fi

# The version butler records is the one the player's browser caches against, so it comes from the
# build rather than from the clock — two uploads of the same build must not look like two versions.
VERSION="$(grep -o '0\.1\.[0-9]*' "$BUILD_DIR/index.html" | head -1 || true)"

# 3. index.html is written at the START of a build, so a version here that lags the one in
#    ProjectSettings means a LATER build has begun and not yet reached this file. That mismatch is
#    exactly what was visible, and missed, when the empty wasm went out: butler reported the previous
#    version string while ProjectSettings had already moved on.
SETTINGS_VERSION="$(grep -oE 'bundleVersion: [0-9.]+' ProjectSettings/ProjectSettings.asset   | head -1 | awk '{print $2}' || true)"
if [ -n "$VERSION" ] && [ -n "$SETTINGS_VERSION" ] && [ "$VERSION" != "$SETTINGS_VERSION" ]; then
  echo "REFUSING TO PUBLISH: Builds/index.html says $VERSION, ProjectSettings says $SETTINGS_VERSION." >&2
  echo "A newer build has started and has not finished writing. Wait for it." >&2
  exit 1
fi
if [ -z "$VERSION" ]; then
  echo "Could not read a version out of $BUILD_DIR/index.html; uploading without one." >&2
  "$BUTLER" push "$BUILD_DIR" "$TARGET"
else
  echo "publishing $VERSION to $TARGET"
  "$BUTLER" push "$BUILD_DIR" "$TARGET" --userversion "$VERSION"
fi

echo "done — check https://itch.io/dashboard"
