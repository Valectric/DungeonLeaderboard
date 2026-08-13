#!/usr/bin/env python3
"""Reduce generated art to a disciplined pixel-art palette.

Sprite Studio's ImageGen masters are clean pixel art, but the normalise step that shrinks a master
to the logical canvas leaves thousands of near-identical colours behind -- 18,648 in the 512x512
terrain atlas, where the top twelve differ by one or two per channel. That is render noise, not
shading, and it is the same failure that produced 610-746 colours in a 32x32 on 2026-08-12.

The tool's own guidance is "3-6 main colours plus outline and highlight for small sprites". This
script is the deterministic half of that promise: generation is a gamble, but normalisation is ours
to control and verify.

Usage::

    python Tools/quantize-art.py <in.png> <out.png> [--colors N] [--compare cmp.png]

Alpha is preserved exactly and never quantised -- it is already binary by the time art reaches here,
and blending it would reintroduce the soft edges that point filtering exists to avoid.
"""

from __future__ import annotations

import argparse
from collections import Counter
from pathlib import Path

from PIL import Image


def unique_opaque(image: Image.Image) -> int:
    """Count distinct colours among pixels that are actually visible."""
    return len({p[:3] for p in image.getdata() if p[3] > 200})


def quantize(source: Image.Image, colours: int) -> Image.Image:
    """Median-cut the RGB channels to a fixed palette, leaving alpha untouched.

    Transparent pixels are flattened to a single colour first. Their RGB is arbitrary once alpha is
    zero, but leaving it varied would spend palette entries describing pixels nobody can see.
    """
    rgba = source.convert("RGBA")
    alpha = rgba.getchannel("A")

    flattened = [
        (0, 0, 0) if a <= 200 else (r, g, b)
        for r, g, b, a in rgba.getdata()
    ]
    rgb = Image.new("RGB", rgba.size)
    rgb.putdata(flattened)

    # dither=NONE is essential: dithering scatters pixels between palette entries to fake extra
    # shades, which is the exact opposite of what pixel art wants and would undo the whole point.
    reduced = rgb.quantize(colors=colours, method=Image.MEDIANCUT, dither=Image.Dither.NONE)

    out = reduced.convert("RGBA")
    out.putalpha(alpha)
    return out


def main() -> int:
    """Quantise one image and report the colour count before and after."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("destination")
    parser.add_argument("--colors", type=int, default=32)
    parser.add_argument("--compare", default=None,
                        help="write a side-by-side before/after PNG for eyeball review")
    args = parser.parse_args()

    source = Image.open(args.source).convert("RGBA")
    before = unique_opaque(source)

    result = quantize(source, args.colors)
    after = unique_opaque(result)

    Path(args.destination).parent.mkdir(parents=True, exist_ok=True)
    result.save(args.destination)

    if args.compare:
        gap = 8
        sheet = Image.new("RGBA", (source.width * 2 + gap, source.height), (26, 20, 38, 255))
        sheet.paste(source, (0, 0), source)
        sheet.paste(result, (source.width + gap, 0), result)
        sheet.save(args.compare)

    print(f"{args.source}")
    print(f"  unique opaque colours: {before} -> {after}  (target <= {args.colors})")
    print(f"  wrote {args.destination}")
    if args.compare:
        print(f"  comparison {args.compare}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
