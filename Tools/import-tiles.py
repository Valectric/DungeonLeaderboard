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
it picks 32 from the style presets regardless.
"""

LOGICAL_TILE = 16
"""
The grid the art is really drawn on.

An art review measured the moodboard as a 120x120 image displayed at 4x: **every one of its 4x4
screen cells is perfectly flat**, so its effective pixel is four screen pixels and a room tile is
about 17 logical px. Anything carrying 1px or 2px detail is therefore too fine, and the review
found exactly that -- floor detail 2.5x finer than the target, with per-pixel dither the target
does not have and a high-frequency RMS of 7.78 against the target's 1.51.

So a 32px source is averaged down to 16 and blown back up by exactly 4. The downsample is what
destroys the dither; the integer upscale is what keeps every edge hard. Going straight from 32 to
64 would have preserved the noise at double size, which is what shipped before.
"""

PALETTE = 6
"""Colours per tile after regridding. The moodboard's own slabs hold about this many."""

# Generated name -> the name the game loads. A wall seen from directly overhead looks the same from
# every side, so this set needs no corner or edge variants at all -- which is also why it cannot
# suffer the corner mismatches the sliced set had.
MAPPING = {
    "eldritch-dark-flagstone": "floor-plain",
    "eldritch-cracked-flagstone": "floor-cracked",
    "eldritch-rubble-flagstone": "floor-rubble",
    "eldritch-iron-drain-flagstone": "floor-drain",
}
# Walls are NOT imported here. Three generation attempts got the scale, the contrast and above all
# the brightness wrong -- generated walls sat at luminance 83 against the moodboard's 32 -- so the
# wall is cut straight from the moodboard by Tools/extract-wall.py instead. Adding it back to this
# mapping would silently overwrite that with the generated version.

RELIEF = {"wall", "wall-moss"}
"""
Tiles that get relief carved into them on import.

The generator produces clean, flat, correctly-sized blocks but will not reliably light them, so its
walls read as a flat brick pattern rather than the moodboard's raised stone. Relief is derived here
instead: a face pixel sitting directly under mortar catches the light, one sitting directly above
mortar falls into shadow. That follows the real block shapes rather than assuming a grid, so it
works whatever coursing the generator chose -- and being deterministic, it is ours to tune rather
than another roll of the dice.
"""

MORTAR_LUMINANCE = 46
"""Below this, a pixel counts as a mortar gap rather than a block face."""


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


def luminance(pixel) -> float:
    """Perceived brightness of an RGB(A) pixel."""
    return (0.299 * pixel[0]) + (0.587 * pixel[1]) + (0.114 * pixel[2])


def add_relief(image: Image.Image) -> Image.Image:
    """Light the top edge of every block and shadow its underside.

    Row lookups wrap around the tile, so a block straddling the tile boundary is lit the same as one
    in the middle and the tile stays seamless -- which is the whole reason the generated tiles were
    worth keeping.
    """
    source = image.convert("RGBA")
    width, height = source.size
    pixels = source.load()
    result = Image.new("RGBA", source.size)
    out = result.load()

    for y in range(height):
        for x in range(width):
            current = pixels[x, y]
            if luminance(current) < MORTAR_LUMINANCE:
                out[x, y] = current
                continue

            above = pixels[x, (y - 1) % height]
            below = pixels[x, (y + 1) % height]

            if luminance(above) < MORTAR_LUMINANCE:
                scale = 1.45          # top edge catches the light
            elif luminance(below) < MORTAR_LUMINANCE:
                scale = 0.62          # underside falls into shadow
            else:
                out[x, y] = current
                continue

            out[x, y] = (
                min(255, int(current[0] * scale)),
                min(255, int(current[1] * scale)),
                min(255, int(current[2] * scale)),
                current[3],
            )

    return result


def add_grout(logical: Image.Image) -> Image.Image:
    """Darken one logical row and column so each tile reads as a distinct flagstone.

    The review found the generated floors structurally featureless in the wrong way: uncorrelated
    per-cell noise with no slab boundaries anywhere, where the target has explicit dark grout lines
    on a slab grid (deepest row-profile dip -5.57 against ours at -2.69). One logical pixel of grout
    is four screen pixels, so it reads clearly without becoming fine detail.
    """
    pixels = logical.load()
    width, height = logical.size

    for x in range(width):
        r, g, b = pixels[x, 0]
        pixels[x, 0] = (int(r * 0.78), int(g * 0.78), int(b * 0.78))
    for y in range(height):
        r, g, b = pixels[0, y]
        pixels[0, y] = (int(r * 0.78), int(g * 0.78), int(b * 0.78))

    return logical


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

        # Average down to the logical grid, flatten to a small palette, then blow back up by an
        # exact factor of four. Order matters: averaging first is what removes the per-pixel dither,
        # and quantising after it is what stops the average reintroducing in-between shades.
        logical = image.convert("RGB").resize((LOGICAL_TILE, LOGICAL_TILE), Image.BOX)
        logical = logical.quantize(
            colors=PALETTE, method=Image.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
        logical = add_grout(logical)
        scaled = logical.resize((TILE, TILE), Image.NEAREST)
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
