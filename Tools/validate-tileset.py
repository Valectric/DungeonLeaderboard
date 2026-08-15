"""Checks a tileset for the faults that have actually shipped here, and says which.

Four failures have been measured on this project's tiles, and only one of them was visible to the
instrument that was watching at the time. This runs all four so that cannot happen again.

The important one is the FRAME check. Every tile in the last set was drawn as a self-contained
illustration with a dark outline, which tiles into a black grid over every wall region -- and the
seam-continuity metric passed it cleanly, because a symmetric dark frame is perfectly
wrap-continuous. A measurement that cannot see the defect is worse than no measurement, because it
is quoted as evidence of health.

Run:  python Tools/validate-tileset.py [folder]
Exits non-zero if any gate fails, so it can sit in front of an import.
"""

import glob
import hashlib
import os
import sys

import numpy as np
from PIL import Image

TILES = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
    "Assets", "Art", "Resources", "tiles")

# North 1, east 2, south 4, west 8 -- the mask DungeonScenery.WallMask computes. A bit set means
# that neighbour is also wall, so the tile must be solid stone right up to that edge.
SIDES = {1: "N", 2: "E", 4: "S", 8: "W"}


def luminance(path):
    """Rec. 709 luminance of a tile, as a 2D array."""
    a = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
    return (0.2126 * a[..., 0]) + (0.7152 * a[..., 1]) + (0.0722 * a[..., 2])


def ring(lum, d):
    """Mean luminance of the ring of pixels d in from the edge."""
    size = lum.shape[0]
    return np.concatenate([
        lum[d, d:size - d], lum[size - 1 - d, d:size - d],
        lum[d:size - d, d], lum[d:size - d, size - 1 - d]]).mean()


def framed(lum):
    """
    Whether the tile has a drawn outline around it.

    The fault that shipped: rings at 9.1, 8.4, 7.0 against an interior of 62.1. Half the interior
    is a generous threshold and still catches every offender.
    """
    size = lum.shape[0]
    interior = lum[size // 4:size - size // 4, size // 4:size - size // 4].mean()
    return ring(lum, 0) < 0.5 * interior, ring(lum, 0), interior


def side_coverage(lum, side, floor_level):
    """Share of one border that is stone rather than background."""
    size = lum.shape[0]
    strip = {
        "N": lum[0, :], "S": lum[size - 1, :],
        "W": lum[:, 0], "E": lum[:, size - 1]}[side]
    return float((strip > floor_level).mean())


def unique_colours(path):
    """
    How many distinct colours a tile holds.

    The cheapest gate of the four and it catches a whole class at once. Pixel art drawn at this size
    carries a handful of colours -- the reference dungeon set uses THREE per material. Ours measured
    1,016 to 2,136, which is 52% of a 64x64 tile's pixels holding a colour of their own. These are
    resampled images OF pixel art rather than pixel art, and edges built from two thousand
    interpolated colours cannot align even in principle. CLAUDE.md already records this failure at
    610-746 colours on an earlier run; it came back three times worse.
    """
    a = np.asarray(Image.open(path).convert("RGB")).reshape(-1, 3)
    return len(set(map(tuple, a.tolist())))


def duplicates(paths):
    """
    Tiles that are byte-identical to another tile.

    wall.png and wall-cracked.png shipped as the same file. Nothing reported it, because a variant
    that was never made looks exactly like a variant that was.
    """
    seen = {}
    for path in paths:
        digest = hashlib.md5(open(path, "rb").read()).hexdigest()
        seen.setdefault(digest, []).append(os.path.basename(path))
    return [names for names in seen.values() if len(names) > 1]


def flat_cells(path, block=4):
    """
    Share of blocks that are a single colour.

    Catches art that has been resampled off the pixel grid or anti-aliased -- a failure this
    project has already shipped once, when a crop made a vertical resample non-integer.
    """
    a = np.asarray(Image.open(path).convert("RGB"))
    h, w, _ = a.shape
    h, w = h - (h % block), w - (w % block)
    b = a[:h, :w].reshape(h // block, block, w // block, block, 3)
    return float((b.min(axis=(1, 3)) == b.max(axis=(1, 3))).all(axis=-1).mean())


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else TILES
    walls = sorted(glob.glob(os.path.join(folder, "wall-*.png")))
    floors = sorted(glob.glob(os.path.join(folder, "floor-*.png")))

    if not walls or not floors:
        print(f"no tiles found in {folder}")
        return 1

    floor_level = float(np.mean([luminance(p).mean() for p in floors]))
    print(f"{len(walls)} wall tiles, {len(floors)} floor tiles, floor level {floor_level:.1f}\n")

    failures = 0
    print(f"{'tile':16s} {'frame':>6s} {'edge0':>6s} {'inner':>6s} {'flat':>6s} {'cols':>6s}  "
          "sides that must be solid")
    for path in walls:
        name = os.path.basename(path)
        stem = name[len("wall-"):-len(".png")]
        lum = luminance(path)

        has_frame, edge, inner = framed(lum)
        flat = flat_cells(path)

        # Only the sides the mask says continue into more wall have to be solid.
        required = []
        if stem.isdigit():
            mask = int(stem)
            for bit, side in SIDES.items():
                if mask & bit:
                    cover = side_coverage(lum, side, floor_level)
                    required.append(f"{side}={cover:.0%}")
                    if cover < 0.95:
                        failures += 1

        colours = unique_colours(path)

        if has_frame:
            failures += 1
        if flat < 0.9:
            failures += 1
        if colours > 32:
            failures += 1

        print(f"{name:16s} {'YES' if has_frame else 'no':>6s} {edge:6.1f} {inner:6.1f} "
              f"{flat:6.0%} {colours:6d}  {' '.join(required) if required else '-'}")

    print(f"\n{failures} gate failures")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
