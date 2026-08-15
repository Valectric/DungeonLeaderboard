"""Scales a bought pixel-art pack up to this project's 64px grid, without softening it.

Both tilesets worth buying are **16x16**. This project imports everything under `Assets/Art` at
**PPU 64** (`PixelArtImportPostprocessor`), and 127 of its 131 sprites are exactly 64x64 -- one cell,
one world unit. A 16px tile dropped in unchanged imports at a quarter of a cell and renders tiny.

There are two ways out and only one of them is sane:

- **Change PPU to 16.** Breaks all 127 existing sprites, which are 64px. No.
- **Upscale the pack x4.** Everything stays on one grid. This.

The scale MUST be an integer and the filter MUST be nearest-neighbour. A non-integer resample, or any
smoothing filter, destroys exactly the hard pixel edges that Point filtering and uncompressed import
exist to preserve -- and this project has already shipped that fault once, when a crop made a vertical
resample non-integer and the tiles came back off-grid.

Run:  python Tools/upscale-pack.py <source-dir> <dest-dir> [--factor 4]

Refuses to guess: if a file's size does not divide into the target cleanly it is reported and skipped
rather than resampled.
"""

import glob
import os
import sys

from PIL import Image

TARGET = 64


def upscale(source, dest, factor):
    """Scales every PNG in source by an integer factor, nearest-neighbour, into dest."""
    os.makedirs(dest, exist_ok=True)

    files = sorted(glob.glob(os.path.join(source, "**", "*.png"), recursive=True))
    if not files:
        print(f"no PNGs found under {source}")
        return 1

    done = skipped = 0
    for path in files:
        image = Image.open(path)
        width, height = image.size

        # A sheet is fine; a sprite that is not a whole number of cells is not, and silently
        # resampling it is how art ends up off the pixel grid.
        if width % 1 or height % 1:
            print(f"  SKIP {os.path.relpath(path, source)} -- {width}x{height} is not integral")
            skipped += 1
            continue

        scaled = image.resize((width * factor, height * factor), Image.NEAREST)

        relative = os.path.relpath(path, source)
        out = os.path.join(dest, relative)
        os.makedirs(os.path.dirname(out), exist_ok=True)
        scaled.save(out)

        done += 1
        if done <= 6:
            print(f"  {relative}: {width}x{height} -> {width * factor}x{height * factor}")

    if done > 6:
        print(f"  ... and {done - 6} more")

    print(f"\n{done} scaled by x{factor}, {skipped} skipped, into {dest}")
    return 0 if skipped == 0 else 1


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    if len(args) < 2:
        print(__doc__)
        return 1

    factor = 4
    if "--factor" in sys.argv:
        factor = int(sys.argv[sys.argv.index("--factor") + 1])

    if factor < 1 or factor != int(factor):
        print(f"factor must be a positive integer, not {factor}")
        return 1

    source, dest = args[0], args[1]
    print(f"scaling {source} by x{factor} (nearest-neighbour) -> {dest}")
    print(f"this project's cell is {TARGET}px, so a 16px pack wants --factor 4\n")
    return upscale(source, dest, factor)


if __name__ == "__main__":
    sys.exit(main())
