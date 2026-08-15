"""Grades the installed wall tiles to the two ratios the moodboard was measured at.

The author's report was "the walls don't look like walls from slight angle but just pattern tiles in
different colours". Three attempts to answer it drew relief, and all three failed. TILESET-NOTES.md
had already recorded why, and it was never applied:

    the moodboard does NOT separate wall from floor by value -- they sit at the same mean.
    It separates them with the rim highlight, which is ~90% brighter than the floor.

Measured on what ships today:

    wall / floor   1.46   against a target of 0.98
    rim  / wall    3.04   against a target of 1.93

So the wall body is 46% brighter than the floor where the reference has them equal. Two adjacent
patterned areas at different values read as exactly what the author said they read as. The wall is
not failing to look three-dimensional because it lacks relief; it is failing because the value
structure that carries "mass with a lit edge" was replaced by "lighter area next to darker area".

That makes this a GRADE, not a redraw -- which is the same note's other warning, since both earlier
attempts drew a bright lit slab instead and neither survived contact.

Run:  python Tools/grade-walls.py [--install]
Writes a before/after room preview to Screenshots/ always; only touches Assets/Art with --install.
"""

import glob
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets", "Art", "Resources", "tiles")
CELL = 64

# From TILESET-NOTES.md, measured off the moodboard rather than chosen.
WALL_OVER_FLOOR = 0.98
RIM_OVER_WALL = 1.93

# How many rows from the top of a tile count as the lit rim.
RIM_ROWS = 6

NORTH, EAST, SOUTH, WEST = 1, 2, 4, 8


def luminance(rgb):
    """Rec. 709 luminance of an RGB array."""
    return (0.2126 * rgb[..., 0]) + (0.7152 * rgb[..., 1]) + (0.0722 * rgb[..., 2])


def load(path):
    """Loads a tile as float RGBA."""
    return np.asarray(Image.open(path).convert("RGBA"), dtype=np.float32)


def mask_tiles():
    """The sixteen mask tiles, by mask."""
    out = {}
    for path in sorted(glob.glob(os.path.join(TILES, "wall-*.png"))):
        stem = os.path.basename(path)[len("wall-"):-len(".png")]
        if stem.isdigit():
            out[int(stem)] = load(path)
    return out


def floor_tiles():
    """Every floor tile."""
    return {os.path.basename(p)[:-4]: load(p)
            for p in sorted(glob.glob(os.path.join(TILES, "floor-*.png")))}


def measure(walls, floors):
    """Returns (floor mean, wall body mean, rim peak) in luminance."""
    floor_mean = float(np.mean([luminance(f[..., :3]).mean() for f in floors.values()]))
    body = float(np.mean([luminance(w[..., :3])[RIM_ROWS + 2:, :].mean()
                          for w in walls.values()]))
    rim = float(np.mean([luminance(w[..., :3])[:RIM_ROWS, :].max() for w in walls.values()]))
    return floor_mean, body, rim


def grade(walls, floors, floor_mean, body, rim):
    """
    Brings the wall body to the floor's value and the rim to 1.93x that, per tile.

    Scaling rather than offsetting, so the masonry's own texture -- the mortar lines, the chipped
    blocks, the variation that makes it stone rather than a swatch -- survives at the same relative
    contrast. An offset would flatten exactly what is worth keeping.
    """
    target_body = floor_mean * WALL_OVER_FLOOR
    target_rim = target_body * RIM_OVER_WALL

    body_gain = target_body / body if body > 0 else 1.0

    def apply(gain):
        """Grades every tile at a given rim gain, so the result can be measured rather than solved."""
        graded = {}
        for mask, tile in walls.items():
            rgb, alpha = tile[..., :3].copy(), tile[..., 3:4]
            rgb *= body_gain

            # Ramp the rim gain out so there is no seam where it stops.
            for row in range(RIM_ROWS):
                fade = 1.0 - (row / RIM_ROWS)
                rgb[row, :] *= 1.0 + ((gain - 1.0) * fade)

            graded[mask] = np.concatenate([np.clip(rgb, 0, 255), alpha], axis=-1)
        return graded

    # SOLVED BY BISECTION, not by algebra, and the reason is worth keeping. The gain needed here is
    # BELOW one -- the rim is too loud at 3.04 and has to come down -- and ramping a reduction out
    # over six rows dims the deeper rows LESS than the top one. So the brightest pixel simply
    # migrates down the ramp, and any closed form written against the original peak overshoots:
    # 2.56 on the first attempt, 2.16 on a second that tried to correct for the ramp's mean.
    #
    # Measuring what was actually produced sidesteps all of it, costs a handful of passes over
    # sixteen 64x64 tiles, and stays correct if the ramp shape ever changes.
    low, high = 0.05, 3.0
    for _ in range(30):
        mid = 0.5 * (low + high)
        _, mid_body, mid_rim = measure(apply(mid), floors)
        if mid_rim / mid_body > RIM_OVER_WALL:
            high = mid
        else:
            low = mid

    return apply(0.5 * (low + high))


def room(walls, floors, path):
    """Lays tiles out as a room, which is the only view that shows whether it reads as a wall."""
    plan = ["########", "#......#", "#..##..#", "#..##..#", "#......#", "#......#", "########"]
    h, w = len(plan), len(plan[0])
    canvas = Image.new("RGB", (w * CELL, h * CELL), (0x1B, 0x13, 0x25))
    floor = Image.fromarray(
        list(floors.values())[0].astype(np.uint8), "RGBA").convert("RGB")

    def solid(x, y):
        return not (0 <= x < w and 0 <= y < h) or plan[y][x] == "#"

    for y in range(h):
        for x in range(w):
            if plan[y][x] != "#":
                canvas.paste(floor, (x * CELL, y * CELL))
                continue

            mask = ((NORTH if solid(x, y - 1) else 0) | (EAST if solid(x + 1, y) else 0)
                    | (SOUTH if solid(x, y + 1) else 0) | (WEST if solid(x - 1, y) else 0))
            tile = walls.get(mask, walls.get(15))
            canvas.paste(Image.fromarray(tile.astype(np.uint8), "RGBA").convert("RGB"),
                         (x * CELL, y * CELL))

    canvas.save(path)
    return canvas


def main():
    walls, floors = mask_tiles(), floor_tiles()
    if not walls or not floors:
        print(f"no tiles in {TILES}")
        return 1

    floor_mean, body, rim = measure(walls, floors)
    print(f"BEFORE  floor {floor_mean:5.2f}  wall {body:5.2f}  rim {rim:5.2f}   "
          f"wall/floor {body / floor_mean:4.2f} (target {WALL_OVER_FLOOR})  "
          f"rim/wall {rim / body:4.2f} (target {RIM_OVER_WALL})")

    graded = grade(walls, floors, floor_mean, body, rim)
    floor_mean2, body2, rim2 = measure(graded, floors)
    print(f"AFTER   floor {floor_mean2:5.2f}  wall {body2:5.2f}  rim {rim2:5.2f}   "
          f"wall/floor {body2 / floor_mean2:4.2f}  rim/wall {rim2 / body2:4.2f}")

    shots = os.path.join(ROOT, "Screenshots")
    os.makedirs(shots, exist_ok=True)
    before = room(walls, floors, os.path.join(shots, "grade-before.png"))
    after = room(graded, floors, os.path.join(shots, "grade-after.png"))

    pair = Image.new("RGB", (before.width * 2 + 24, before.height), (0x0E, 0x0A, 0x14))
    pair.paste(before, (0, 0))
    pair.paste(after, (before.width + 24, 0))
    pair = pair.resize((pair.width * 2, pair.height * 2), Image.NEAREST)
    pair.save(os.path.join(shots, "grade-compare.png"))
    print("comparison:", os.path.join(shots, "grade-compare.png"), "(before | after)")

    if "--install" in sys.argv:
        for mask, tile in graded.items():
            Image.fromarray(tile.astype(np.uint8), "RGBA").save(
                os.path.join(TILES, f"wall-{mask}.png"))
        print(f"installed {len(graded)} graded tiles")
    else:
        print("dry run -- pass --install to write into Assets/Art/Resources/tiles")

    return 0


if __name__ == "__main__":
    sys.exit(main())
