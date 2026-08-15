"""Builds sixteen wall tiles that differ from each other, from stone we already have.

The set in Assets/Art/Resources/tiles fails for one reason above all others: the sixteen mask tiles
are the same picture. Measured, they separate by 1.47x against their own texture grain, so wall-0 (an
isolated pillar) and wall-15 (fully enclosed) render identically and the tilemap cannot express a
wall boundary at all. DungeonScenery.WallMask computes the right mask and then picks between sixteen
crops of one texture.

So this does not try to draw stone. The Crawl catacombs texture is good and stays exactly as it is;
what gets added is the one thing no tile in the set has -- an EDGE where the wall stops and floor
begins. A side whose mask bit is clear faces open floor, and that is where the boundary goes.

Light comes from above, which the moodboard and TILESET-NOTES:18 both say and which decides the
whole scheme:

    north edge facing floor -> bright cap, the lit top course. This is the cue that reads as height.
    east/west facing floor  -> dim rim, much darker than the cap. Lighting these equally is what
                               made the earlier runtime attempt read as a neon outline.
    south edge facing floor -> shadow only, no highlight at all. The underside of a wall is dark.

Every value lands on the moodboard ramp, so the output is palette-locked by construction rather than
by a later pass, and modulated per 4px block so a long wall run catches light per block instead of
as one unbroken bar -- the second thing the runtime attempt got wrong.

Run:  python Tools/shape-walls.py [--install]
Writes a contact sheet to Screenshots/ always; only touches Assets/Art with --install.
"""

import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets", "Art", "Resources", "tiles")
CELL = 64

# North 1, east 2, south 4, west 8 -- the mask DungeonScenery.WallMask computes. A bit SET means that
# neighbour is also wall, so the tile continues; a bit CLEAR means that side faces floor and is where
# the boundary has to be drawn.
NORTH, EAST, SOUTH, WEST = 1, 2, 4, 8

# The moodboard ramp, darkest to lightest. Shared with slice-room.py and import-tileset.py.
RAMP = np.array([
    (0x0E, 0x0A, 0x14), (0x1B, 0x13, 0x25), (0x25, 0x1B, 0x31), (0x33, 0x22, 0x42),
    (0x44, 0x2C, 0x55), (0x50, 0x27, 0x5E), (0x55, 0x45, 0x6B), (0x63, 0x5D, 0x7C),
    (0x7C, 0x76, 0x93), (0x9A, 0x93, 0xB0),
], dtype=np.float32)

RAMP_LUM = (RAMP[:, 0] * 0.2126) + (RAMP[:, 1] * 0.7152) + (RAMP[:, 2] * 0.0722)

# How deep each edge treatment cuts into the tile, in pixels at 64. The cap is the thick one because
# it is doing the work; the others are accents and go wrong when they compete with it.
CAP = 10
RIM = 4
SHADE = 8


def load_stone():
    """The stone face every tile is built on, taken from the existing set."""
    for name in ("wall-15.png", "wall.png", "wall-0.png"):
        path = os.path.join(TILES, name)
        if os.path.exists(path):
            image = Image.open(path).convert("RGB").resize((CELL, CELL), Image.NEAREST)
            return np.asarray(image, dtype=np.float32), name
    raise SystemExit(f"no wall texture found in {TILES}")


def block_noise(seed, blocks=CELL // 4):
    """
    Per-4px-block brightness jitter, upsampled to the tile.

    Masonry catches light block by block. A single unbroken bright line along a wall run is the exact
    giveaway the first runtime attempt was reverted for, so the cap is modulated on the same 4px grid
    the art already sits on -- and by NEAREST upsampling, so it stays on the pixel grid.
    """
    rng = np.random.default_rng(seed)
    small = rng.uniform(0.82, 1.18, size=(blocks, blocks)).astype(np.float32)
    return np.asarray(
        Image.fromarray((small * 127).astype(np.uint8)).resize((CELL, CELL), Image.NEAREST),
        dtype=np.float32) / 127.0


def ramp_snap(rgb):
    """Snaps every pixel to the nearest ramp entry by luminance."""
    lum = (rgb[..., 0] * 0.2126) + (rgb[..., 1] * 0.7152) + (rgb[..., 2] * 0.0722)
    idx = np.abs(lum[..., None] - RAMP_LUM[None, None, :]).argmin(axis=-1)
    return RAMP[idx]


def shape(stone, mask, seed):
    """
    Draws one mask's tile: stone everywhere, with a boundary on each side facing floor.

    The gain figures are deliberately far apart. The cap is the only thing allowed to be bright,
    because a wall reads as having height when its top catches light and its sides do not.
    """
    out = stone.copy()
    noise = block_noise(seed)

    # South first, so a corner tile's cap can overwrite its shadow rather than the reverse -- the lit
    # top course wins every argument about a shared pixel.
    if not mask & SOUTH:
        for d in range(SHADE):
            fade = 0.45 + (0.55 * d / SHADE)
            out[CELL - 1 - d, :] *= fade

    if not mask & WEST:
        for d in range(RIM):
            out[:, d] *= 1.0 + (0.22 * (RIM - d) / RIM)

    if not mask & EAST:
        for d in range(RIM):
            out[:, CELL - 1 - d] *= 0.80 + (0.06 * d / RIM)

    if not mask & NORTH:
        for d in range(CAP):
            # Brightest at the very top edge and falling away fast, which is what a bevelled stone
            # course does. Linear all the way down reads as a gradient, not as a lip.
            lift = 1.0 + (1.35 * ((CAP - d) / CAP) ** 1.7)
            out[d, :] *= (lift * noise[d, :])[:, None]

    return ramp_snap(np.clip(out, 0, 255))


def contact_sheet(tiles, path):
    """Writes every generated tile onto one image, for a person to look at."""
    names = sorted(tiles, key=lambda n: int(n.split("-")[1]))
    columns = 4
    rows = (len(names) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * 72 + 8, rows * 72 + 8), (0x15, 0x10, 0x1D))

    for i, name in enumerate(names):
        sheet.paste(tiles[name], ((i % columns) * 72 + 8, (i // columns) * 72 + 8))

    sheet = sheet.resize((sheet.width * 2, sheet.height * 2), Image.NEAREST)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path)
    return path


def room_preview(tiles, path):
    """
    Lays the tiles out as an actual room, which is the only view that shows whether they join.

    A contact sheet shows sixteen tiles; it does not show a wall. This walks a small grid, computes
    the same mask DungeonScenery would, and places the matching tile -- so what comes out is what the
    game would draw.
    """
    plan = [
        "########",
        "#......#",
        "#..##..#",
        "#..##..#",
        "#......#",
        "#......#",
        "########",
    ]
    h, w = len(plan), len(plan[0])
    canvas = Image.new("RGB", (w * CELL, h * CELL), (0x1B, 0x13, 0x25))

    floor_path = os.path.join(TILES, "floor-plain.png")
    floor = (Image.open(floor_path).convert("RGB").resize((CELL, CELL), Image.NEAREST)
             if os.path.exists(floor_path) else None)

    def solid(x, y):
        return not (0 <= x < w and 0 <= y < h) or plan[y][x] == "#"

    for y in range(h):
        for x in range(w):
            if plan[y][x] != "#":
                if floor:
                    canvas.paste(floor, (x * CELL, y * CELL))
                continue

            mask = ((NORTH if solid(x, y - 1) else 0) | (EAST if solid(x + 1, y) else 0)
                    | (SOUTH if solid(x, y + 1) else 0) | (WEST if solid(x - 1, y) else 0))
            canvas.paste(tiles[f"wall-{mask}"], (x * CELL, y * CELL))

    canvas.save(path)
    return path


def main():
    stone, source = load_stone()
    print(f"stone face from {source}")

    tiles = {f"wall-{m}": Image.fromarray(shape(stone, m, m).astype(np.uint8), "RGB")
             for m in range(16)}

    sheet = contact_sheet(tiles, os.path.join(ROOT, "Screenshots", "shaped-walls.png"))
    room = room_preview(tiles, os.path.join(ROOT, "Screenshots", "shaped-walls-room.png"))
    print("contact sheet:", sheet)
    print("room preview: ", room)

    arrays = {n: np.asarray(t, dtype=np.float32) for n, t in tiles.items()}
    import itertools
    pairs = [np.abs(a - b).mean() for a, b in itertools.combinations(arrays.values(), 2)]
    grain = np.mean([np.abs(v[1:] - v[:-1]).mean() for v in arrays.values()])
    print(f"\nshape separation: tiles differ {np.mean(pairs):.1f} against grain {grain:.1f} "
          f"= {np.mean(pairs) / grain:.2f}x  (gate wants >= 3.0)")

    if "--install" in sys.argv:
        for name, tile in tiles.items():
            tile.save(os.path.join(TILES, f"{name}.png"))
        print(f"installed 16 tiles into {TILES}")
    else:
        print("dry run -- pass --install to write into Assets/Art/Resources/tiles")


if __name__ == "__main__":
    main()
