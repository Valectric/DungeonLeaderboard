#!/usr/bin/env python3
"""Import the generated tile pack into the game and prove it tiles.

Replaces the first attempt, which sliced arbitrary squares out of the terrain harness's
presentation atlas. That atlas is deliberately composed as large assembled macro-regions for a human
to read, at whatever internal pitch ImageGen chose (measured ~27px, irregular), so the slices cut
through stones mid-block and their mortar lines did not meet. These tiles were generated as tiles:
one item per 64px canvas, in a single pack run so they share projection, palette and pixel density.

Run from the repo root after ``Tools/generate-tiles.ps1``::

    python Tools/import-tiles.py

Every tile is checked for the three properties a tilemap actually needs -- opposite edges identical,
fully opaque, exactly 64 square -- and the import **fails** rather than shipping a tile that would
draw a seam across the dungeon floor. Then it writes a room preview; look at it.
"""

from __future__ import annotations


import sys
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent

# The pack lands under worktrees/<slug>/, NOT the workspace root the agent's own summary claims.
# Read the filesystem to verify a run; the prose is not reliable about where files went.
PACK = Path("C:/Users/JohanHoltby/Documents/sprite-studio-sandbox"
            "/worktrees/dungeonassets/assets/props")
OUT_DIR = REPO / "Assets" / "Art" / "Resources" / "tiles"
PREVIEW = REPO / "Tools" / "tileset-room-preview.png"

TILE = 64
"""Game tile size. Must match PixelArtImportPostprocessor.PixelsPerUnit."""

SOURCE_TILE = 32
"""
Size the pack harness actually draws at.

`--width`/`--height` do not reach the pack harness -- its brief carries no logical-canvas line and
it picks 32 from the style presets regardless. Rather than fight that, tiles are upscaled by an
exact factor of two with nearest-neighbour, which keeps every edge hard and doubles the chunkiness
in a way that suits the art. A non-integer resample to 64 would soften exactly what point filtering
exists to preserve.
"""

# Generated name -> the name the game loads. A wall seen from directly overhead looks the same from
# every side, so this set needs no corner or edge variants at all -- which is also why it cannot
# suffer the corner mismatches the sliced set had.
MAPPING = {
    "eldritch-dark-flagstone": "floor-plain",
    "eldritch-cracked-flagstone": "floor-cracked",
    "eldritch-rubble-flagstone": "floor-rubble",
    "eldritch-iron-drain-flagstone": "floor-drain",
    "eldritch-overhead-masonry-wall": "wall",
    "eldritch-mossy-masonry-wall": "wall-moss",
}


def check(image: Image.Image, name: str) -> list[str]:
    """Return the ways a tile would visibly fail in a tilemap."""
    faults = []
    if image.size != (SOURCE_TILE, SOURCE_TILE):
        faults.append(f"is {image.size[0]}x{image.size[1]}, not {SOURCE_TILE} square")

    pixels = image.load()
    width, height = image.size
    if any(pixels[0, y] != pixels[width - 1, y] for y in range(height)):
        faults.append("left and right edges differ -- vertical seam")
    if any(pixels[x, 0] != pixels[x, height - 1] for x in range(width)):
        faults.append("top and bottom edges differ -- horizontal seam")
    if any(p[3] != 255 for p in image.getdata()):
        faults.append("has transparent pixels -- floor would show the camera background")
    return faults


def preview(tiles: dict[str, Image.Image]) -> Image.Image:
    """Draw a room from the imported tiles so seams and contrast are visible at a glance."""
    cols, rows = 9, 6
    room = Image.new("RGB", (cols * TILE, rows * TILE))
    for row in range(rows):
        for col in range(cols):
            edge = row in (0, rows - 1) or col in (0, cols - 1)
            if edge:
                key = "wall-moss" if (col + row) % 7 == 0 else "wall"
            else:
                spread = (col * 7) + (row * 13)
                key = ("floor-drain" if spread % 23 == 0
                       else "floor-cracked" if spread % 5 == 0
                       else "floor-rubble" if spread % 7 == 0
                       else "floor-plain")
            room.paste(tiles[key], (col * TILE, row * TILE))
    return room


def run() -> int:
    """Copy, verify and preview. Returns non-zero if any tile would seam."""
    if not PACK.exists():
        print(f"missing pack directory: {PACK}", file=sys.stderr)
        return 1

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    tiles = {}
    problems = []

    for source_name, game_name in MAPPING.items():
        source = PACK / f"{source_name}.png"
        if not source.exists():
            problems.append(f"{source_name}: not generated")
            continue

        image = Image.open(source).convert("RGBA")
        faults = check(image, source_name)
        if faults:
            problems.extend(f"{game_name}: {fault}" for fault in faults)
            continue

        scaled = image.resize((TILE, TILE), Image.NEAREST)
        scaled.save(OUT_DIR / f"{game_name}.png")
        tiles[game_name] = scaled.convert("RGB")
        colours = len({p[:3] for p in image.getdata()})
        print(f"  {game_name:14s} <- {source_name:32s} {colours:2d} colours, seamless, x2 -> {TILE}")

    # Remove the sliced tiles this pack replaces, so nothing loads the old broken art by accident.
    for stale in ("wall-top", "wall-bottom", "wall-left", "wall-right",
                  "corner-tl", "corner-tr", "corner-bl", "corner-br"):
        for suffix in (".png", ".png.meta"):
            path = OUT_DIR / f"{stale}{suffix}"
            if path.exists():
                path.unlink()
                print(f"  removed stale {path.name}")

    if problems:
        print("\nFAILED -- these would draw seams in game:")
        print("\n".join(f"  {problem}" for problem in problems))
        return 1

    room = preview(tiles)
    room.resize((room.width * 2, room.height * 2), Image.NEAREST).save(PREVIEW)
    print(f"\n{len(tiles)} tiles -> {OUT_DIR}")
    print(f"room preview -> {PREVIEW}")
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
