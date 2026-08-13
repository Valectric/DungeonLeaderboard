#!/usr/bin/env python3
"""Build one labelled contact sheet from generated sprites.

Reviewing art over a remote desktop on a phone is the bottleneck: opening PNGs
one at a time is unusable at that screen size. This composites the most recent
sprites into a single image, scaled up with nearest-neighbour so pixel art stays
crisp, on a mid-grey checkerboard so transparency and stray fringe pixels are
both obvious.

    python Tools/sprite-contact-sheet.py [SOURCE_DIR] [-o OUT] [-n COUNT] [--scale N]

Defaults to the Sprite Studio staging workspace and writes review-sheet.png.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:
    sys.exit("Pillow is required:  python -m pip install Pillow")

STAGE = Path(r"C:/Users/JohanHoltby/Documents/sprite-studio-sandbox")
CHECKER = (56, 56, 64), (44, 44, 50)
BACKDROP = (26, 22, 34)  # violet-black, so warm-brown drift is obvious
LABEL = (214, 210, 224)
CELL_PAD = 10
LABEL_H = 14


def newest_sprites(source: Path, count: int) -> list[Path]:
    files = [
        p
        for p in source.rglob("*.png")
        # Skip tool scratch (masters, pack contact sheets), the style references
        # and moodboard, and any previous review sheet. Only generated art.
        if ".sprite-studio" not in p.parts
        and "references" not in p.parts
        and "review-sheet" not in p.name
    ]
    files.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    return files[:count]


def checkerboard(size: int) -> Image.Image:
    tile = Image.new("RGB", (size, size), CHECKER[0])
    draw = ImageDraw.Draw(tile)
    half = size // 2
    draw.rectangle([0, 0, half - 1, half - 1], fill=CHECKER[1])
    draw.rectangle([half, half, size - 1, size - 1], fill=CHECKER[1])
    return tile


def build(paths: list[Path], scale: int, columns: int) -> Image.Image:
    loaded = []
    for path in paths:
        image = Image.open(path).convert("RGBA")
        image = image.resize(
            (image.width * scale, image.height * scale), Image.Resampling.NEAREST
        )
        loaded.append((path.name, image))

    cell_w = max(i.width for _, i in loaded) + CELL_PAD * 2
    cell_h = max(i.height for _, i in loaded) + CELL_PAD * 2 + LABEL_H
    rows = (len(loaded) + columns - 1) // columns

    sheet = Image.new("RGB", (cell_w * columns, cell_h * rows), BACKDROP)
    draw = ImageDraw.Draw(sheet)
    tile = checkerboard(8)

    for index, (name, image) in enumerate(loaded):
        cx = (index % columns) * cell_w
        cy = (index // columns) * cell_h
        # Checkerboard only behind the sprite, so alpha is readable.
        patch = Image.new("RGB", (image.width, image.height))
        for y in range(0, image.height, 8):
            for x in range(0, image.width, 8):
                patch.paste(tile, (x, y))
        patch.paste(image, (0, 0), image)
        sheet.paste(patch, (cx + CELL_PAD, cy + CELL_PAD))
        label = name if len(name) <= 30 else name[:27] + "..."
        draw.text((cx + CELL_PAD, cy + CELL_PAD + image.height + 2), label, fill=LABEL)

    return sheet


def inspect(path: Path) -> list[str]:
    """Cheap defect checks. Nothing else in the pipeline verifies output, and
    both of these are invisible in a thumbnail but fatal in-engine."""
    image = Image.open(path).convert("RGBA")
    w, h = image.size
    px = image.load()
    problems: list[str] = []

    chroma = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            # Leftover green/magenta key from the generator's backdrop.
            if (g > 90 and g > r * 1.5 and g > b * 1.5) or (
                r > 90 and b > 90 and r > g * 1.5 and b > g * 1.5
            ):
                chroma += 1
    if chroma:
        problems.append(f"{chroma} chroma-key fringe pixel(s)")

    # A tile must reach every edge or it will not tile seamlessly.
    if any(word in path.stem for word in ("floor", "wall", "tile", "ground")):
        edges = {
            "top": all(px[x, 0][3] == 0 for x in range(w)),
            "bottom": all(px[x, h - 1][3] == 0 for x in range(w)),
            "left": all(px[0, y][3] == 0 for y in range(h)),
            "right": all(px[w - 1, y][3] == 0 for y in range(h)),
        }
        empty = [name for name, blank in edges.items() if blank]
        if empty:
            problems.append(
                f"transparent {'/'.join(empty)} edge(s) - will not tile seamlessly"
            )

    if w != h and "wall" not in path.stem:
        problems.append(f"non-square {w}x{h}")
    return problems


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", nargs="?", default=str(STAGE))
    parser.add_argument("-o", "--out", default=None)
    parser.add_argument("-n", "--count", type=int, default=12)
    parser.add_argument("--scale", type=int, default=4)
    parser.add_argument("--columns", type=int, default=4)
    args = parser.parse_args()

    source = Path(args.source)
    if not source.is_dir():
        return f"not a directory: {source}"

    paths = newest_sprites(source, args.count)
    if not paths:
        return f"no sprites found under {source}"

    out = Path(args.out) if args.out else source / "review-sheet.png"
    build(paths, args.scale, args.columns).save(out)

    print(f"{out}  ({len(paths)} sprite(s), {args.scale}x)")
    flagged = 0
    for path in paths:
        problems = inspect(path)
        if problems:
            flagged += 1
            print(f"  {path}")
            for problem in problems:
                print(f"      WARN  {problem}")
        else:
            print(f"  {path}")
    if flagged:
        print(f"\n{flagged} of {len(paths)} sprite(s) need attention before import.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
