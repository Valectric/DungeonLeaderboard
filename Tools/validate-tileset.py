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


def alpha_of(path):
    """
    A tile's alpha channel, or None when the image has none at all.

    Returned even when the tile is fully opaque, which is the point: an opaque tile reaches every
    edge BY DEFINITION, so its coverage is 100% and there is nothing to infer. An earlier version of
    this returned None for opaque tiles and fell back to luminance, which left the false positive it
    was written to remove — a dark shadow on an opaque tile still read as missing art.
    """
    image = Image.open(path)
    if image.mode != "RGBA":
        return None

    return np.asarray(image, dtype=np.float32)[..., 3]


def side_coverage(lum, side, floor_level, alpha=None):
    """
    Share of one border that is tile rather than nothing.

    MEASURED ON ALPHA WHERE THERE IS ANY. The fault this gate exists for is art that does not reach
    the canvas edge -- a "32x32" floor whose drawing was only 28x28, leaving grid lines across the
    dungeon. That is a question about TRANSPARENCY, and it was being asked of luminance.

    Conflating the two produces a false positive that will meet the next bought pack immediately: a
    tile with a drawn shadow along its bottom edge has border pixels darker than the floor, so a
    luminance threshold calls them absent. Measured on a synthetic pack with a 2px shadow, the east
    and west borders reported 88% against a 95% threshold -- 8 rows of 64 after a x4 upscale, which
    is exactly the shadow and not a defect at all.

    Alpha cannot make that mistake: a drawn shadow is opaque, and missing art is not. The luminance
    path stays as the fallback for fully-opaque tiles, where alpha carries no information and "darker
    than the floor" is the only signal available -- which is the case for everything currently
    installed here.
    """
    size = lum.shape[0]
    if alpha is not None:
        strip = {
            "N": alpha[0, :], "S": alpha[size - 1, :],
            "W": alpha[:, 0], "E": alpha[:, size - 1]}[side]
        return float((strip > 8.0).mean())

    strip = {
        "N": lum[0, :], "S": lum[size - 1, :],
        "W": lum[:, 0], "E": lum[:, size - 1]}[side]
    return float((strip > floor_level).mean())


def unique_colours(path):
    """
    How many distinct colours a tile holds.

    The cheapest gate of the four and it catches a whole class at once. Pixel art drawn at this size
    carries a handful of colours -- the reference dungeon set uses THREE per material. Ours measured
    1,016 to 2,136 once, and 187 to 272 now. CLAUDE.md records 610-746 on an earlier run.

    THIS IS A STYLE CHECK, NOT AN ALIGNMENT ONE, and the docstring used to claim otherwise -- that
    "edges built from two thousand interpolated colours cannot align even in principle". That claim is
    disproven by the gate next door: `native_block` measures these same tiles at **2px, 100% flat**,
    so they align perfectly while carrying 272 colours. Alignment is measured directly now and this
    gate should not be quoted as evidence for it.

    What it does measure is real and worth keeping: a small palette is what makes art read AS pixel
    art rather than as a photograph of some. That bears on the author's standing complaint about the
    walls, which is a question about how the art reads.

    **Quantising to fix it is not free.** Measured on wall-14 at 24 colours: mean luminance moves 27.4
    to 27.3 and the mean pixel changes by 0.89 of 255 -- which says harmless -- while the rim peak
    falls from 85.0 to 70.9. The rim is the cue TILESET-NOTES identifies as carrying the wall reading,
    so a 17% loss there is the opposite of harmless. The averages and the feature that matters
    disagree, which is D32's lesson in miniature.
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


def encodes_shape(walls):
    """
    Whether the sixteen mask tiles actually differ from each other.

    THE ONE THIS FILE MISSED. Every other gate here reads a tile on its own, so all sixteen can pass
    individually while being the same picture -- which is exactly what shipped. Measured on the set
    that prompted "the walls don't look like walls, just pattern tiles in different colours":

        pairwise tile-to-tile difference   11.5   (min 0.3)
        within-tile neighbour-pixel noise   7.8

    Tile-to-tile variation barely clears the texture's own grain, and the closest pair is identical
    to three decimal places. wall-0 is an isolated pillar and wall-15 is fully enclosed, and they
    render the same, so DungeonScenery.WallMask computes a correct mask and then picks between
    sixteen copies. The autotiling was decorative.

    That is why three attempts at adding relief in code went nowhere: there is no wall/floor boundary
    drawn anywhere in the set, and lighting a boundary that was never drawn cannot work. A tileset
    has to encode WHERE the wall stops, and this gate is the cheapest possible check that it does.

    Returns (ok, worst, per_side). Each per-side figure is the gap between that edge drawn as open
    floor and the same edge drawn as continuing wall, in luminance.

    MEASURE IT PER SIDE, CONDITIONED ON THE BIT. The first version of this gate compared whole tiles
    to each other and divided by texture grain, which is the wrong question twice over: a correct
    Wang set shares its interior across all sixteen tiles and differs only at the edges, so
    whole-tile mean difference is near zero BY DESIGN. That version scored a genuinely shape-encoding
    set at 0.33x -- worse than the broken set's 1.47x -- and would have rejected the fix while
    passing the fault.
    """
    edges = {1: lambda a: a[:4, :], 2: lambda a: a[:, -4:],
             4: lambda a: a[-4:, :], 8: lambda a: a[:, :4]}

    arrays = {}
    for path in walls:
        stem = os.path.basename(path)[len("wall-"):-len(".png")]
        if stem.isdigit():
            arrays[int(stem)] = luminance(path)

    if len(arrays) < 8:
        return True, 0.0, {}

    per_side = {}
    for bit, side in SIDES.items():
        closed = [edges[bit](a).mean() for m, a in arrays.items() if m & bit]
        open_ = [edges[bit](a).mean() for m, a in arrays.items() if not m & bit]
        per_side[side] = (abs(np.mean(closed) - np.mean(open_))
                          if closed and open_ else 0.0)

    # At least one side must carry a real boundary, and the worst may be zero -- a south edge that is
    # shadow-only is legitimate. Six luminance levels is roughly one step on the moodboard ramp.
    best = max(per_side.values()) if per_side else 0.0
    return best >= 6.0, best, per_side


def flat_cells(path, block=4):
    """Share of block x block cells that are a single colour."""
    a = np.asarray(Image.open(path).convert("RGB"))
    h, w, _ = a.shape
    h, w = h - (h % block), w - (w % block)
    b = a[:h, :w].reshape(h // block, block, w // block, block, 3)
    return float((b.min(axis=(1, 3)) == b.max(axis=(1, 3))).all(axis=-1).mean())


def native_block(path):
    """
    The largest pixel size the art is actually drawn at, and how flat it is there.

    ASK WHICH GRID, DO NOT ASSUME ONE. The fault worth catching is art resampled OFF the pixel grid --
    anti-aliased or scaled by a non-integer factor -- which this project shipped once when a crop made
    a vertical resample fractional. That shows up as no block size giving flat cells at all.

    A fixed block of 4 does not ask that. It asks whether the art is drawn at 16px in a 64px tile, and
    answers "no" for anything else. Measured on this project's own set: 0% flat at block 4 and
    **100% at block 2**, because the tiles are a clean x2 point-scale of 32px source. Eighteen tiles
    were failing a gate for being drawn at the wrong size rather than for being wrong.

    Returns (block, flatness). Block 1 is always trivially flat, so a set that only manages 1 is one
    where every pixel differs from its neighbours -- which is the actual defect.
    """
    for block in (8, 4, 2):
        flat = flat_cells(path, block)
        if flat >= 0.9:
            return block, flat

    return 1, flat_cells(path, 2)


def self_test():
    """
    Proves the shape gate can tell a real mask set from sixteen copies of one tile.

    THE STEP THAT WAS MISSING. The first version of this gate compared whole tiles and scored a set
    built specifically to encode boundaries at 0.33x -- WORSE than the broken set it had just
    condemned at 1.47x -- and that verdict was written into three files and pushed before anyone
    checked whether the measure could rank a known-good case above a known-bad one. It could not.

    A gate nobody has calibrated is an opinion with a number attached. This runs both cases every
    time, so the question cannot go unasked again.
    """
    import shutil
    import tempfile

    real = sorted(glob.glob(os.path.join(TILES, "wall-*.png")))
    if not real:
        print("self-test skipped: no installed tiles to calibrate against")
        return 0

    good, _, _ = encodes_shape(real)

    staging = tempfile.mkdtemp(prefix="tileset-selftest-")
    try:
        source = os.path.join(TILES, "wall-15.png")
        if not os.path.exists(source):
            source = real[0]
        for mask in range(16):
            shutil.copyfile(source, os.path.join(staging, f"wall-{mask}.png"))

        bad, bad_best, _ = encodes_shape(sorted(glob.glob(os.path.join(staging, "wall-*.png"))))
    finally:
        shutil.rmtree(staging, ignore_errors=True)

    ok = good and not bad
    print(f"self-test: installed set {'passes' if good else 'FAILS'}, "
          f"sixteen-copies set {'FAILS as it should' if not bad else f'PASSES at {bad_best:.1f} -- '
          'the gate cannot see the fault it exists for'}")
    return 0 if ok else 1


def main():
    if "--self-test" in sys.argv:
        return self_test()

    folder = sys.argv[1] if len(sys.argv) > 1 else TILES
    walls = sorted(glob.glob(os.path.join(folder, "wall-*.png")))
    floors = sorted(glob.glob(os.path.join(folder, "floor-*.png")))

    if not walls or not floors:
        print(f"no tiles found in {folder}")
        return 1

    floor_level = float(np.mean([luminance(p).mean() for p in floors]))
    print(f"{len(walls)} wall tiles, {len(floors)} floor tiles, floor level {floor_level:.1f}\n")

    failures = 0
    print(f"{'tile':16s} {'frame':>6s} {'edge0':>6s} {'inner':>6s} {'grid':>5s} "
          f"{'flat':>5s} {'cols':>6s}  sides that must be solid")
    for path in walls:
        name = os.path.basename(path)
        stem = name[len("wall-"):-len(".png")]
        lum = luminance(path)

        has_frame, edge, inner = framed(lum)
        block, flat = native_block(path)

        # Only the sides the mask says continue into more wall have to be solid.
        required = []
        if stem.isdigit():
            mask = int(stem)
            alpha = alpha_of(path)
            for bit, side in SIDES.items():
                if mask & bit:
                    cover = side_coverage(lum, side, floor_level, alpha)
                    required.append(f"{side}={cover:.0%}")
                    if cover < 0.95:
                        failures += 1

        colours = unique_colours(path)

        if has_frame:
            failures += 1

        # Block 1 means no integer grid fits at all, which is the resampling fault. A tile drawn at
        # 2px in a 64px canvas is on-grid and fine; the old fixed block of 4 called it broken.
        if block <= 1:
            failures += 1
        if colours > 32:
            failures += 1

        print(f"{name:16s} {'YES' if has_frame else 'no':>6s} {edge:6.1f} {inner:6.1f} "
              f"{block:3d}px {flat:5.0%} {colours:6d}  "
              f"{' '.join(required) if required else '-'}")

    shaped, best, per_side = encodes_shape(walls)
    if not shaped:
        failures += 1
    detail = "  ".join(f"{s}={v:.1f}" for s, v in per_side.items())
    print(f"\nshape: open-vs-closed edge gap  {detail}  best {best:.1f} "
          f"({'ok' if shaped else 'FAIL -- the mask tiles are the same picture'})")

    print(f"\n{failures} gate failures")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
