#!/usr/bin/env python3
"""Cut the whole dungeon tile set out of the moodboard.

Seven attempts at *generating* wall tiles produced art that never matched the moodboard, for a
reason now documented in CLAUDE.md: `--command pack` never forwards reference images to ImageGen, so
most of those runs were refined against a picture the image model had never seen. Even the run that
did get the reference through needed cutting afterwards.

The moodboard already contains the art. This cuts it directly, which is deterministic, free, and
on-style by construction -- the same approach that produced the 38 character sprites on the first
try.

Sources are the second TILE / ROOM EXAMPLES panel, whose masonry period measures 16px -- exactly one
logical tile. Straights and corners are cut from the room's own perimeter. T-junctions and the
crossing are composed from those pieces rather than invented, so every tile in the set is made of
the same stone.

    python Tools/extract-dungeon-tiles.py
"""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parent.parent
MOODBOARD = REPO / "Assets" / "Art" / "referance" / "MoodBoard.png"
OUT_DIR = REPO / "Assets" / "Art" / "Resources" / "tiles"
PREVIEW = REPO / "Tools" / "tileset-room-preview.png"

LOGICAL = 16
SCALE = 4
TILE = LOGICAL * SCALE
"""The moodboard is a 120x120 image shown at 4x, so its effective pixel is four screen pixels."""

# Room panel 2. Masonry period is 16px, so every box below is exactly one tile.
SOURCES = {
    "wall-h": (572, 624, 16, 16),      # horizontal run, bottom course
    "wall-v": (540, 572, 16, 16),      # vertical run, left column
    "corner-tl": (540, 528, 16, 16),
    "corner-tr": (646, 528, 16, 16),
    "corner-bl": (540, 624, 16, 16),
    "corner-br": (646, 624, 16, 16),
    "floor-plain": (596, 604, 16, 16),  # clear interior, no props
    "floor-alt": (580, 612, 16, 16),
}


def cut(moodboard: Image.Image, box) -> np.ndarray:
    """Cut one tile at logical resolution."""
    x, y, width, height = box
    patch = moodboard.crop((x, y, x + width, y + height))
    return np.asarray(patch.resize((LOGICAL, LOGICAL), Image.BOX).convert("RGB")).astype(float)


def combine(*layers: np.ndarray) -> np.ndarray:
    """Merge wall pieces by taking the brighter pixel.

    Stone sits bright against near-black mortar, so the lighter of two pieces is the one carrying
    masonry. That makes a crossing simply the union of a horizontal and a vertical run, built from
    the same stone as everything else rather than drawn fresh.
    """
    stacked = np.stack(layers)
    brightest = stacked.sum(axis=3).argmax(axis=0)
    return np.take_along_axis(stacked, brightest[None, :, :, None], axis=0)[0]


def half(tile: np.ndarray, side: str) -> np.ndarray:
    """Keep one half of a tile and blank the rest, for building junctions."""
    out = np.zeros_like(tile)
    mid = LOGICAL // 2
    if side == "up":
        out[:mid, :] = tile[:mid, :]
    elif side == "down":
        out[mid:, :] = tile[mid:, :]
    elif side == "left":
        out[:, :mid] = tile[:, :mid]
    else:
        out[:, mid:] = tile[:, mid:]
    return out


def seamless(tile: np.ndarray, axis: str) -> np.ndarray:
    """Average opposite edges so runs of this tile show no seam along the axis it repeats on."""
    tile = tile.copy()
    if axis in ("x", "both"):
        edge = (tile[:, 0] + tile[:, -1]) / 2
        tile[:, 0] = edge
        tile[:, -1] = edge
    if axis in ("y", "both"):
        edge = (tile[0, :] + tile[-1, :]) / 2
        tile[0, :] = edge
        tile[-1, :] = edge
    return tile


def save(tile: np.ndarray, name: str) -> Image.Image:
    """Quantise, upscale by exactly four, and write."""
    image = Image.fromarray(tile.clip(0, 255).astype(np.uint8))
    image = image.quantize(colors=12, method=Image.MEDIANCUT,
                           dither=Image.Dither.NONE).convert("RGB")
    image = image.resize((TILE, TILE), Image.NEAREST)
    image.save(OUT_DIR / f"{name}.png")
    return image


def run() -> int:
    """Cut, compose, verify and preview the whole set."""
    moodboard = Image.open(MOODBOARD).convert("RGB")
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    raw = {name: cut(moodboard, box) for name, box in SOURCES.items()}
    wall_h, wall_v = raw["wall-h"], raw["wall-v"]

    tiles: dict[str, np.ndarray] = {
        "wall-h": seamless(wall_h, "x"),
        "wall-v": seamless(wall_v, "y"),
        "corner-tl": raw["corner-tl"],
        "corner-tr": raw["corner-tr"],
        "corner-bl": raw["corner-bl"],
        "corner-br": raw["corner-br"],
        "floor-plain": seamless(raw["floor-plain"], "both"),
        "floor-alt": seamless(raw["floor-alt"], "both"),
    }

    # Junctions, built from the same stone rather than drawn fresh.
    tiles["wall-cross"] = combine(wall_h, wall_v)
    tiles["wall-t-up"] = combine(wall_h, half(wall_v, "up"))
    tiles["wall-t-down"] = combine(wall_h, half(wall_v, "down"))
    tiles["wall-t-left"] = combine(wall_v, half(wall_h, "left"))
    tiles["wall-t-right"] = combine(wall_v, half(wall_h, "right"))

    # The plain wall the solid mass of the dungeon is drawn with.
    tiles["wall"] = tiles["wall-h"]
    tiles["wall-moss"] = tiles["wall-h"]

    for name, tile in tiles.items():
        image = save(tile, name)
        array = np.asarray(image)
        lum = 0.299 * array[..., 0] + 0.587 * array[..., 1] + 0.114 * array[..., 2]
        flat = sum(
            1 for yy in range(0, TILE, SCALE) for xx in range(0, TILE, SCALE)
            if len({tuple(p) for row in array[yy:yy + SCALE, xx:xx + SCALE] for p in row}) == 1)
        print(f"  {name:14s} lum={lum.mean():5.1f} "
              f"colours={len({tuple(p) for p in array.reshape(-1, 3)}):2d} "
              f"4x4flat={100 * flat / ((TILE // SCALE) ** 2):5.1f}%")

    order = ["wall-h", "wall-v", "wall-cross", "wall-t-up", "wall-t-down",
             "wall-t-left", "wall-t-right", "corner-tl", "corner-tr",
             "corner-bl", "corner-br", "floor-plain", "floor-alt"]
    sheet = Image.new("RGB", (len(order) * TILE, TILE), (20, 16, 29))
    for i, name in enumerate(order):
        sheet.paste(Image.open(OUT_DIR / f"{name}.png"), (i * TILE, 0))
    sheet.resize((sheet.width * 2, TILE * 2), Image.NEAREST).save(PREVIEW)

    print(f"\n{len(tiles)} tiles -> {OUT_DIR}")
    print(f"sheet -> {PREVIEW}")
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
