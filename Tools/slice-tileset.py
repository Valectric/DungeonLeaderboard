#!/usr/bin/env python3
"""Cut the generated atlas into the 64x64 tiles the dungeon grid actually draws.

**The atlas is not on a 64px grid.** ImageGen drew a convincing tileset but its internal block pitch
is roughly 27px and irregular (measured pieces: 45x42, 47x42, 49x44, 36x36), and the harness's
normalise step cannot correct that because the master's grid was never 64 to begin with. Asking for
"sixty-four pixels square" steers the composition, not the pixel ruler.

So tiles are produced by cropping 32x32 and upscaling x2 with nearest-neighbour. That is deliberate:
64/27 is not an integer, and resampling pixel art by 2.37 destroys the crisp blocks that point
filtering exists to preserve. An integer x2 keeps every edge hard. The cost is that a tile holds
about 1.2 stones rather than exactly one, which is invisible on floor and wall texture.

Run from the repo root after the atlas exists::

    python Tools/slice-tileset.py

Writes 64x64 tiles into ``Assets/Art/Tiles/`` and a test-room mosaic to ``Tools/`` -- **look at the
mosaic**, it is the only check that the tiles actually sit together without a seam.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parent.parent
ATLAS = REPO / "Assets" / "Art" / "Tiles" / "dungeon-tileset.png"
OUT_DIR = REPO / "Assets" / "Art" / "Resources" / "tiles"
MOSAIC = REPO / "Tools" / "tileset-room-preview.png"

TILE = 64
SOURCE = 32
"""Crop size in atlas pixels. Doubled to reach TILE, so it must be exactly TILE // 2."""

# Source crops, chosen by eye from the atlas macro-regions after measuring them.
# Room region is x 8..225, y 8..238 with a ~27px wall perimeter; floor texture panels sit along the
# bottom at y 382..502.
CROPS = {
    "floor-plain": (100, 100),
    "floor-cracked": (100, 410),
    "floor-rubble": (360, 410),
    "wall-top": (100, 10),
    "wall-bottom": (100, 208),
    "wall-left": (10, 100),
    "wall-right": (196, 100),
    "corner-tl": (10, 10),
    "corner-tr": (193, 10),
    "corner-bl": (10, 206),
    "corner-br": (193, 206),
}


def cut(atlas: Image.Image, x: int, y: int) -> Image.Image:
    """Crop a SOURCE-square patch and upscale it to TILE with nearest-neighbour."""
    patch = atlas.crop((x, y, x + SOURCE, y + SOURCE))
    return patch.resize((TILE, TILE), Image.NEAREST)


def build_mosaic(tiles: dict[str, Image.Image]) -> Image.Image:
    """Lay the tiles out as a small room so seams and mismatched edges become obvious.

    A tile set that passes every automated check can still show a bright join or a doubled outline,
    and only an assembled room reveals it.
    """
    cols, rows = 7, 5
    room = Image.new("RGBA", (cols * TILE, rows * TILE), (26, 20, 38, 255))
    for row in range(rows):
        for col in range(cols):
            top, bottom = row == 0, row == rows - 1
            left, right = col == 0, col == cols - 1
            if top and left:
                key = "corner-tl"
            elif top and right:
                key = "corner-tr"
            elif bottom and left:
                key = "corner-bl"
            elif bottom and right:
                key = "corner-br"
            elif top:
                key = "wall-top"
            elif bottom:
                key = "wall-bottom"
            elif left:
                key = "wall-left"
            elif right:
                key = "wall-right"
            else:
                key = "floor-cracked" if (col + row) % 4 == 0 else "floor-plain"
            room.paste(tiles[key], (col * TILE, row * TILE), tiles[key])
    return room


def run() -> int:
    """Slice the atlas, write the tiles, and render the review mosaic."""
    if SOURCE * 2 != TILE:
        raise ValueError("SOURCE must be exactly half of TILE to keep the x2 integer upscale")
    if not ATLAS.exists():
        print(f"missing atlas: {ATLAS}")
        return 1

    atlas = Image.open(ATLAS).convert("RGBA")
    # The atlas has transparent gutters; tiles must be opaque, so flatten onto the dungeon's own
    # ground colour rather than leaving holes that would show the camera background through a floor.
    ground = Image.new("RGBA", atlas.size, (0x25, 0x1B, 0x31, 255))
    ground.alpha_composite(atlas)

    tiles = {}
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for name, (x, y) in CROPS.items():
        tile = cut(ground, x, y)
        tile.save(OUT_DIR / f"{name}.png")
        tiles[name] = tile
        colours = len({p[:3] for p in tile.getdata()})
        print(f"  {name:14s} from ({x:3d},{y:3d})  {colours:2d} colours")

    build_mosaic(tiles).resize((7 * TILE * 2, 5 * TILE * 2), Image.NEAREST).save(MOSAIC)
    print(f"\n{len(tiles)} tiles -> {OUT_DIR}")
    print(f"room preview -> {MOSAIC}")
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
