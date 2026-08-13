#!/usr/bin/env python3
"""Cut game tiles from the terrain-harness atlas master.

Seven attempts got here. The decisive fact is documented in CLAUDE.md: **`--command pack` never
forwards `--reference` images to ImageGen**, so six pack runs were refined against a reference the
image model had never seen. Only the terrain and effect harnesses carry the instruction "Supply
every attached visual reference through `referenced_image_paths`". Routing through terrain produced
masonry that matches the moodboard on the first try.

The terrain harness's cost is that it returns one presentation atlas of macro-regions rather than
discrete tiles, so they are cut here. Measured from the master: wall blocks repeat every 109px
horizontally and 79px vertically.

Each tile is cut at exactly one block pitch, averaged down to a 16px logical grid, made seamless by
averaging opposite edges, then blown back up by exactly 4. The logical grid is not arbitrary -- the
moodboard is a 120x120 image shown at 4x, so 100% of its 4x4 screen cells are flat and its effective
pixel is four screen pixels.

    python Tools/cut-tileset.py
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parent.parent
MASTER = Path("C:/Users/JohanHoltby/Documents/sprite-studio-sandbox/.sprite-studio"
              "/imagegen-sources/dungeon-wall-floor/tileset-master.png")
OUT_DIR = REPO / "Assets" / "Art" / "Resources" / "tiles"
PREVIEW = REPO / "Tools" / "tileset-room-preview.png"

LOGICAL = 16
SCALE = 4
TILE = LOGICAL * SCALE

PITCH_X = 109
PITCH_Y = 79
"""Masonry repeat in the master, found by autocorrelation."""

# Macro-region origins in the master, found by connected-component labelling of the non-key area.
# A sample is taken from inside each field, clear of its rounded outer corners.
SOURCES = {
    "wall": (60, 60, PITCH_X, PITCH_Y),
    "wall-moss": (528, 60, PITCH_X, PITCH_Y),
    "wall-cracked": (60, 404, PITCH_X, PITCH_Y),
    "floor-plain": (540, 410, PITCH_X, PITCH_X),
    "floor-cracked": (60, 750, PITCH_X, PITCH_X),
    # Sampled from deeper inside the cobble field. A sample nearer its top edge caught the lighter
    # rim of the region and came out at luminance 35, brighter than the walls, so scattered rubble
    # tiles popped out of the floor instead of blending into it.
    "floor-rubble": (600, 520, PITCH_X, PITCH_X),
    "floor-drain": (520, 740, 264, 196),
}


def make_seamless(cell: np.ndarray) -> np.ndarray:
    """Force opposite edges to match so a grid of copies shows no seam.

    Cutting exactly one block pitch already puts both edges at equivalent points in the pattern, so
    they are close; averaging them makes them identical with almost no visible change. Doing this at
    logical resolution means the correction is one chunky pixel wide rather than a hairline.
    """
    cell = cell.copy()
    edge = (cell[0, :] + cell[-1, :]) / 2
    cell[0, :] = edge
    cell[-1, :] = edge
    edge = (cell[:, 0] + cell[:, -1]) / 2
    cell[:, 0] = edge
    cell[:, -1] = edge
    return cell


def cut(master: Image.Image, box) -> Image.Image:
    """Cut one source region and turn it into a finished tile."""
    x, y, width, height = box
    patch = master.crop((x, y, x + width, y + height))

    # Average down to the logical grid. BOX is deliberate: it means every logical pixel is the mean
    # of the source pixels under it, which is what removes the master's fine grain rather than
    # sampling one arbitrary pixel out of each cell.
    logical = np.asarray(
        patch.resize((LOGICAL, LOGICAL), Image.BOX).convert("RGB")).astype(float)

    tile = Image.fromarray(make_seamless(logical).clip(0, 255).astype(np.uint8))
    tile = tile.quantize(colors=12, method=Image.MEDIANCUT,
                         dither=Image.Dither.NONE).convert("RGB")
    return tile.resize((TILE, TILE), Image.NEAREST)


def run() -> int:
    """Cut every tile, verify it, and render a room preview."""
    if not MASTER.exists():
        print(f"missing atlas master: {MASTER}")
        return 1

    master = Image.open(MASTER).convert("RGB")
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    tiles = {}

    for name, box in SOURCES.items():
        tile = cut(master, box)
        tile.save(OUT_DIR / f"{name}.png")
        tiles[name] = tile

        array = np.asarray(tile)
        lum = 0.299 * array[..., 0] + 0.587 * array[..., 1] + 0.114 * array[..., 2]
        flat = sum(
            1 for yy in range(0, TILE, SCALE) for xx in range(0, TILE, SCALE)
            if len({tuple(p) for row in array[yy:yy + SCALE, xx:xx + SCALE] for p in row}) == 1)
        cells = (TILE // SCALE) ** 2
        pixels = tile.load()
        seam = (all(pixels[0, y] == pixels[TILE - 1, y] for y in range(TILE))
                and all(pixels[x, 0] == pixels[x, TILE - 1] for x in range(TILE)))
        print(f"  {name:14s} lum={lum.mean():5.1f} "
              f"colours={len({tuple(p) for p in array.reshape(-1, 3)}):2d} "
              f"4x4flat={100 * flat / cells:5.1f}%  seamless={seam}")

    cols, rows = 9, 6
    room = Image.new("RGB", (cols * TILE, rows * TILE))
    for row in range(rows):
        for col in range(cols):
            if row in (0, rows - 1) or col in (0, cols - 1):
                key = ("wall-moss" if (col + row) % 9 == 0
                       else "wall-cracked" if (col + row) % 13 == 0
                       else "wall")
            else:
                spread = (col * 7) + (row * 13)
                key = ("floor-drain" if spread % 29 == 0
                       else "floor-cracked" if spread % 6 == 0
                       else "floor-rubble" if spread % 8 == 0
                       else "floor-plain")
            room.paste(tiles[key], (col * TILE, row * TILE))

    room.save(PREVIEW)
    print(f"\n{len(tiles)} tiles -> {OUT_DIR}")
    print(f"room preview -> {PREVIEW}")
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
