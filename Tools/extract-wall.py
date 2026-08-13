#!/usr/bin/env python3
"""Build the dungeon wall tile from the moodboard's own masonry.

Three generation attempts produced walls that were the wrong scale, the wrong contrast, or -- most
tellingly -- the wrong *brightness*: the generated walls sat at luminance 83 while the moodboard's
real walls sit at 32 with highlights reaching 52. The moodboard wall reads as a wall because of its
block structure and pale top rims, not because it is bright, and describing that to a generator kept
losing it.

So this does what worked for the 38 character sprites: cut the authored pixels straight out of the
moodboard. The source is the bottom wall course of the second TILE / ROOM EXAMPLES panel, chosen by
measurement rather than eye -- it is the brightest of the clean runs, the coolest in hue (no torch
spill), and carries no props.

One block is isolated, resampled to a clean 16px unit, and laid in offset courses to build a tile
that repeats seamlessly on both axes by construction rather than by hoping.

    python Tools/extract-wall.py
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parent.parent
MOODBOARD = REPO / "Assets" / "Art" / "referance" / "MoodBoard.png"
OUT_DIR = REPO / "Assets" / "Art" / "Resources" / "tiles"
REFERENCE = REPO / "Assets" / "Art" / "referance" / "style-wall.png"
PREVIEW = REPO / "Tools" / "wall-preview.png"

# Bottom wall course of room panel 2. Measured at luminance mean 32.0, p90 52, warmth -7.4.
#
# Height is exactly 16, matching the 16px masonry period, so the block crop is square and the
# upscale to 64 is an exact 4x on BOTH axes. An earlier 17-row crop made the vertical resample
# 3.76x, which broke the 4px pixel grid -- only 43% of 4x4 cells came out flat against the
# moodboard's 100%.
COURSE = (546, 624, 662, 640)

BLOCK = 64
"""
Width and height of one masonry block in the finished tile.

**One block fills a whole tile**, and that is measured, not chosen. The moodboard's room panels run
about 17px per tile with a 16px masonry period, so the authored art puts 0.93 blocks in a tile. An
earlier version used 16px blocks -- four courses per tile -- and the wall read as fine horizontal
stripes instead of chunky stone. A review of the composed room caught it; the numbers then settled
it. 16 -> 64 is an exact 4x integer upscale, so nothing softens.
"""

TILE = 64
SCALE = 4
"""Finished tile size, matching the rest of the set and PixelArtImportPostprocessor."""


def find_period(strip: np.ndarray) -> int:
    """Find the horizontal repeat length of the masonry, in pixels.

    Compares the strip against shifted copies of itself and takes the shift with the best match.
    The blocks are hand-drawn and not perfectly regular, so this finds the dominant rhythm rather
    than an exact tiling.
    """
    grey = strip.mean(axis=2)
    width = grey.shape[1]
    best, best_score = BLOCK, float("inf")

    for period in range(10, min(30, width // 2)):
        overlap = width - period
        diff = np.abs(grey[:, :overlap] - grey[:, period:period + overlap]).mean()
        if diff < best_score:
            best_score, best = diff, period

    return best


def build_tile(blocks: list[Image.Image]) -> Image.Image:
    """Lay blocks in offset courses to fill the tile, wrapping horizontally.

    Several source blocks are alternated by position rather than one being repeated, because a wall
    of identical stones reads as wallpaper. The choice is a hash of the cell so the tile is
    reproducible -- the same in a screenshot, a test and the shipped build.
    """
    tile = Image.new("RGB", (TILE, TILE))
    courses = TILE // BLOCK

    for row in range(courses):
        # Offset every other course by half a block, the way real brickwork is laid. Because the
        # offset is exactly half of a whole divisor of the tile, the pattern still wraps.
        shift = (row % 2) * (BLOCK // 2)
        for col in range(-1, (TILE // BLOCK) + 1):
            block = blocks[((col * 5) + (row * 3)) % len(blocks)]
            x = (col * BLOCK) + shift
            tile.paste(block, (x, row * BLOCK))
            if x + BLOCK > TILE:
                tile.paste(block, (x - TILE, row * BLOCK))

    return tile


def quantise(image: Image.Image, colours: int = 14) -> Image.Image:
    """Flatten to a small palette, matching the discipline of the rest of the tile set."""
    return image.quantize(
        colors=colours, method=Image.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")


LOGICAL = 16
"""Logical grid the relief is carved on, so every band is exactly SCALE screen pixels."""


def carve_relief(tile: Image.Image) -> Image.Image:
    """Give the block real thickness: lit top rim, mid face, shadowed underside.

    A wall that is merely a *pattern* of blocks does not read as a wall -- it reads as wallpaper.
    What makes the moodboard's stone feel solid is that each block is clearly lit from above: a
    bright rim along its top edge, a flat mid-tone body, and a deep shadow underneath where the next
    course sits on it. That is a lighting cue, not a texture, and sampling alone will not reproduce
    it because the sampled course is small and low-contrast.

    Carved on the 16px logical grid, so each band is four screen pixels and the tile stays on the
    same pixel grid as everything else.
    """
    logical = np.asarray(
        tile.resize((LOGICAL, LOGICAL), Image.BOX)).astype(float)

    # A lighting ramp MULTIPLIED over the sampled stone, not painted in place of it. An earlier
    # version replaced each row with a flat computed colour, which produced exactly the right
    # luminance profile and destroyed every trace of the moodboard's texture -- the tiles measured
    # perfectly and looked like blank slabs. Generating them from scratch had the same failure for
    # the same reason: the numbers were never what made the stone look like stone.
    ramp = np.ones(LOGICAL)
    ramp[0] = 0.28      # mortar course above
    ramp[1] = 2.10      # block catches the light
    ramp[2] = 1.45
    ramp[3] = 1.12
    ramp[LOGICAL - 3] = 0.80
    ramp[LOGICAL - 2] = 0.46   # shadow the next course casts
    ramp[LOGICAL - 1] = 0.28   # mortar course below

    for y in range(LOGICAL):
        logical[y, :] *= ramp[y]

    logical[:, 0] *= 0.42      # vertical joint

    return Image.fromarray(logical.clip(0, 255).astype(np.uint8))


def run() -> int:
    """Extract the block, build the tile, and write it plus a reference and a preview."""
    moodboard = Image.open(MOODBOARD).convert("RGB")
    strip = moodboard.crop(COURSE)
    array = np.asarray(strip).astype(float)

    period = find_period(array)
    print(f"  masonry repeats every {period}px")

    # Take several consecutive periods from the middle of the run, away from the panel's rounded
    # corners, so the finished wall varies stone to stone the way the moodboard's does.
    blocks = []
    start = ((strip.width - (period * 3)) // 2) + 1
    for i in range(3):
        left = start + (i * period)
        if left + period > strip.width:
            break
        blocks.append(
            strip.crop((left, 0, left + period, strip.height)).resize(
                (BLOCK, BLOCK), Image.NEAREST))

    print(f"  sampled {len(blocks)} distinct blocks")
    tile = carve_relief(build_tile(blocks)).resize((TILE, TILE), Image.NEAREST)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    tile.save(OUT_DIR / "wall.png")

    # A mossy variant, so the wall is not perfectly uniform across a whole dungeon. The speckle is
    # placed on the 4px grid, not per pixel -- single-pixel marks are exactly the too-fine detail
    # the moodboard never has, and they would drop this tile off the pixel grid the rest sits on.
    mossy = np.asarray(tile).astype(float)
    cell = SCALE
    for y in range(0, TILE, cell):
        for x in range(0, TILE, cell):
            if (((x // cell) * 7) + ((y // cell) * 13)) % 11 != 0:
                continue
            patch = mossy[y:y + cell, x:x + cell]
            mossy[y:y + cell, x:x + cell] = (patch * 0.65) + (np.array([46, 74, 40]) * 0.35)

    Image.fromarray(mossy.clip(0, 255).astype(np.uint8)).save(OUT_DIR / "wall-moss.png")

    # Keep the clean course as a reference image for any future generation run.
    strip.resize((strip.width * 3, strip.height * 3), Image.NEAREST).save(REFERENCE)

    lum = np.asarray(tile).astype(float)
    lum = 0.299 * lum[..., 0] + 0.587 * lum[..., 1] + 0.114 * lum[..., 2]
    print(f"  wall tile luminance mean={lum.mean():.1f} "
          f"p10={np.percentile(lum, 10):.0f} p90={np.percentile(lum, 90):.0f}")
    print(f"  colours={len({tuple(p) for p in np.asarray(tile).reshape(-1, 3)})}")

    preview = Image.new("RGB", (TILE * 4, TILE * 3))
    for y in range(3):
        for x in range(4):
            preview.paste(tile, (x * TILE, y * TILE))
    preview.resize((TILE * 4 * 2, TILE * 3 * 2), Image.NEAREST).save(PREVIEW)
    print(f"  wrote {OUT_DIR / 'wall.png'} and preview {PREVIEW}")
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
