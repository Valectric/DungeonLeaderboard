#!/usr/bin/env python3
"""Cut game-ready 64x64 sprites out of the moodboard.

The moodboard (``Assets/Art/referance/MoodBoard.png``) is the project's style authority, and it
already contains finished pixel art for most of what Milestone 1 needs -- including all four
adventurer archetypes drawn in the three wound states the spec demands. Extracting from it is
deterministic and free, and the result is on-style by construction because the source *is* the
style. That makes this the default art path; the Sprite Studio generator is for what the moodboard
genuinely lacks, such as a real terrain tileset with edges and corners.

Run from the repo root::

    python Tools/extract-moodboard.py

Outputs 64x64 RGBA PNGs into ``Assets/Art/Sprites/<category>/`` plus a contact sheet at
``Tools/moodboard-contact-sheet.png``. **Read the contact sheet.** Nothing here checks that a cut
looks right, and a band whose box has drifted will happily emit 12 crops of empty floor.

Two behaviours worth knowing before changing the BANDS table:

* Sprites are only downscaled when they exceed the canvas. Most moodboard sprites are 50-73px, so
  they land at or near 1:1 and stay crisp. Upscaling is never done -- it would invent pixels.
* Characters and mobs are bottom-anchored so they share a foot line in game; props are centred.
"""

from __future__ import annotations

import sys
from collections import Counter, deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

CANVAS = 64
"""Output canvas edge in pixels. Must match PixelArtImportPostprocessor.PixelsPerUnit."""

PALETTE = 32
"""
Colours kept per sprite.

The moodboard is an AI render, not hand-placed pixels, so a 64x64 cut off it carries 600-1200
distinct colours -- roughly a quarter of its own pixels are unique. That reads as mush at game size:
no flat colour blocks, no clean outline, exactly what point filtering is supposed to preserve.
Reducing to 32 sharpens the blocks and keeps every readable detail; 12 starts eating the healer's
face. Compared side by side before choosing.
"""

REPO = Path(__file__).resolve().parent.parent
MOODBOARD = REPO / "Assets" / "Art" / "referance" / "MoodBoard.png"
# Under a folder literally named "Resources" so Resources.Load can reach it from a code-built
# scene, and still under Assets/Art/ so PixelArtImportPostprocessor claims it. Both matter.
OUT_ROOT = REPO / "Assets" / "Art" / "Resources"
SHEET = REPO / "Tools" / "moodboard-contact-sheet.png"


class Band:
    """One labelled region of the moodboard holding a row or grid of sprites."""

    def __init__(self, key, category, box, names, anchor="bottom",
                 grid=None, tol=26, dilate=3, min_area=200, min_height=20):
        """Describe a region to cut.

        :param key: short id used in log output.
        :param category: output subfolder under ``Assets/Art/Sprites``.
        :param box: ``(x0, y0, x1, y1)`` in moodboard pixels.
        :param names: sprite names, in reading order.
        :param anchor: ``bottom`` to sit sprites on a shared foot line, else ``center``.
        :param grid: ``(cols, rows)`` of explicit cell boxes, or None to auto-detect blobs.
        :param tol: per-channel distance from the sampled background that counts as foreground.
        :param dilate: radius used to merge a sprite's detached parts (a shield, a staff glow).
        :param min_area: blobs smaller than this are noise (stray glow particles).
        :param min_height: blobs shorter than this are the panel's own caption. Every band on the
            moodboard is titled ("SPAWNERS", "EXTRA MOOD"), and that text keys out as foreground
            just as readily as art does. Filtering on height rather than area is what separates
            them: captions are ~10-12px tall while the shortest real sprite is over 30px.
        """
        self.key = key
        self.category = category
        self.box = box
        self.names = names
        self.anchor = anchor
        self.grid = grid
        self.tol = tol
        self.dilate = dilate
        self.min_area = min_area
        self.min_height = min_height


PARTY_COLS = [(44, 120), (126, 212), (218, 302), (308, 404)]
PARTY_ROWS = [(436, 514), (516, 586), (588, 652)]
PARTY_NAMES = [f"{role}-{state}"
               for state in ("healthy", "hurt", "critical")
               for role in ("tank", "healer", "ranged", "mage")]

BANDS = [
    Band("party", "party", None, PARTY_NAMES, anchor="bottom",
         grid=(PARTY_COLS, PARTY_ROWS)),
    Band("mobs", "mobs", (435, 688, 960, 775), anchor="bottom",
         names=["bat", "skeleton", "slime", "spider", "assassin", "ogre", "watcher"]),
    Band("doors", "dungeon", (438, 402, 612, 488), anchor="center",
         names=["door-a", "door-b", "door-gate"]),
    # x1 reaches 862: the cauldron spawner sits at 766-820 and an earlier 772 cut it off entirely.
    Band("spawners", "dungeon", (618, 400, 862, 490), anchor="center",
         names=["spawner-crystal", "spawner-skull", "spawner-cauldron"]),
    Band("chest", "dungeon", (866, 405, 944, 486), anchor="center",
         names=["chest"]),
    # dilate=1, tol=34: the blade and fire impacts throw faint sparks toward each other, and at the
    # default radius those bridge into a single 246px-wide blob holding both effects.
    Band("traps", "effects", (435, 806, 960, 902), anchor="bottom", min_area=400,
         tol=34, dilate=1,
         names=["trap-spikes", "trap-poison", "trap-blade", "trap-fire"]),
    Band("props", "props", (876, 932, 1512, 1016), anchor="bottom", min_area=400,
         names=["crystals-small", "crystals-large", "noticeboard", "lanterns",
                "banner", "books", "candle-skull", "portal-frame"]),
]


def sample_background(region: np.ndarray) -> np.ndarray:
    """Estimate a band's background from its border pixels.

    Panels on the moodboard sit on slightly different backing colours, so a single global key
    leaves halos on some bands and eats sprite edges on others.

    :param region: HxWx3 array of the band.
    :return: the modal border colour as a length-3 array.
    """
    border = np.concatenate([
        region[0, :], region[-1, :], region[:, 0], region[:, -1],
    ])
    modal, _ = Counter(map(tuple, border.tolist())).most_common(1)[0]
    return np.array(modal, dtype=int)


def foreground_mask(region: np.ndarray, tol: int) -> np.ndarray:
    """Mark pixels that differ from the band background by more than ``tol`` on any channel."""
    bg = sample_background(region)
    return np.abs(region - bg).max(axis=2) > tol


def dilated(mask: np.ndarray, radius: int) -> np.ndarray:
    """Grow a mask so a sprite's detached parts join into one blob.

    A tank's shield and a mage's staff glow are separated from the body by background pixels, and
    without this they are found as separate sprites -- or dropped as noise.
    """
    if radius <= 0:
        return mask
    img = Image.fromarray((mask * 255).astype(np.uint8))
    grown = img.filter(ImageFilter.MaxFilter(radius * 2 + 1))
    return np.asarray(grown) > 127


def blobs(mask: np.ndarray, min_area: int, min_height: int = 0
          ) -> list[tuple[int, int, int, int]]:
    """Find bounding boxes of connected foreground regions, in left-to-right order."""
    height, width = mask.shape
    seen = np.zeros_like(mask, dtype=bool)
    found = []
    for sy in range(height):
        for sx in range(width):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            queue = deque([(sy, sx)])
            seen[sy, sx] = True
            xs, ys = [], []
            while queue:
                cy, cx = queue.popleft()
                xs.append(cx)
                ys.append(cy)
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if (0 <= ny < height and 0 <= nx < width
                                and mask[ny, nx] and not seen[ny, nx]):
                            seen[ny, nx] = True
                            queue.append((ny, nx))
            box = (min(xs), min(ys), max(xs) + 1, max(ys) + 1)
            if len(xs) >= min_area and (box[3] - box[1]) >= min_height:
                found.append(box)
    found.sort(key=lambda b: b[0])
    return found


def label_components(mask: np.ndarray) -> tuple[np.ndarray, int]:
    """Label each connected foreground region with a distinct integer, background as 0."""
    height, width = mask.shape
    labels = np.zeros(mask.shape, dtype=np.int32)
    current = 0
    for sy in range(height):
        for sx in range(width):
            if not mask[sy, sx] or labels[sy, sx]:
                continue
            current += 1
            queue = deque([(sy, sx)])
            labels[sy, sx] = current
            while queue:
                cy, cx = queue.popleft()
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = cy + dy, cx + dx
                        if (0 <= ny < height and 0 <= nx < width
                                and mask[ny, nx] and not labels[ny, nx]):
                            labels[ny, nx] = current
                            queue.append((ny, nx))
    return labels, current


def largest_component(mask: np.ndarray, dilate_radius: int) -> np.ndarray:
    """Keep only the biggest connected region of a mask.

    Explicit grid cells (the party sheet) enclose more than the sprite: the faint vertical rules
    that divide the cells, and the row captions bleeding in from the margin. Both key out as
    foreground and both stretch the bounding box, which then shrinks the sprite to fit.

    Selection is by *component membership*, not by bounding box. A caption sitting in the corner of
    the sprite's own bounding box survives a box-based filter -- that is how a red fragment of the
    "CRITICAL" label ended up welded to the tank sprite -- but cannot survive this one.
    """
    grown = dilated(mask, dilate_radius)
    labels, count = label_components(grown)
    if count == 0:
        return mask
    best = max(range(1, count + 1), key=lambda k: (mask & (labels == k)).sum())
    return mask & (labels == best)


def fit_canvas(rgb: np.ndarray, alpha: np.ndarray, anchor: str) -> Image.Image:
    """Place a cut sprite onto the square output canvas without ever upscaling."""
    height, width = alpha.shape
    rgba = np.zeros((height, width, 4), dtype=np.uint8)
    rgba[..., :3] = rgb
    rgba[..., 3] = alpha * 255
    sprite = Image.fromarray(rgba, "RGBA")

    longest = max(width, height)
    if longest > CANVAS - 2:
        scale = (CANVAS - 2) / longest
        sprite = sprite.resize(
            (max(1, round(width * scale)), max(1, round(height * scale))), Image.NEAREST)

    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    x = (CANVAS - sprite.width) // 2
    y = CANVAS - sprite.height - 1 if anchor == "bottom" else (CANVAS - sprite.height) // 2
    canvas.paste(sprite, (x, max(0, y)))
    return reduce_palette(canvas)


def reduce_palette(sprite: Image.Image) -> Image.Image:
    """Cut a sprite down to PALETTE colours without touching its alpha.

    Median cut with dithering explicitly off -- dithering scatters pixels between palette entries to
    fake extra shades, which is the exact opposite of what pixel art wants.
    """
    alpha = sprite.getchannel("A")
    flattened = [(0, 0, 0) if a <= 128 else (r, g, b) for r, g, b, a in sprite.getdata()]
    rgb = Image.new("RGB", sprite.size)
    rgb.putdata(flattened)

    reduced = rgb.quantize(colors=PALETTE, method=Image.MEDIANCUT,
                           dither=Image.Dither.NONE).convert("RGBA")
    reduced.putalpha(alpha)
    return reduced


def cut(image: np.ndarray, box, tol: int, anchor: str,
        isolate: bool = False, dilate_radius: int = 3) -> Image.Image | None:
    """Cut one sprite from an explicit box, keying out its background.

    :param isolate: drop everything but the largest region, for boxes known to enclose clutter.
    """
    x0, y0, x1, y1 = box
    region = image[y0:y1, x0:x1]
    mask = foreground_mask(region, tol)
    if isolate:
        mask = largest_component(mask, dilate_radius)
    if mask.sum() < 40:
        return None
    ys, xs = np.where(mask)
    ry0, ry1, rx0, rx1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    return fit_canvas(region[ry0:ry1, rx0:rx1], mask[ry0:ry1, rx0:rx1], anchor)


def run() -> int:
    """Extract every band, write the PNGs and the contact sheet, and report what was found."""
    if not MOODBOARD.exists():
        print(f"missing moodboard: {MOODBOARD}", file=sys.stderr)
        return 1

    image = np.asarray(Image.open(MOODBOARD).convert("RGB")).astype(int)
    results = []
    problems = []

    for band in BANDS:
        sprites = []
        if band.grid:
            cols, rows = band.grid
            boxes = [(cx0, ry0, cx1, ry1) for (ry0, ry1) in rows for (cx0, cx1) in cols]
        else:
            x0, y0, x1, y1 = band.box
            region = image[y0:y1, x0:x1]
            mask = foreground_mask(region, band.tol)
            found = blobs(dilated(mask, band.dilate), band.min_area, band.min_height)
            boxes = [(x0 + bx0, y0 + by0, x0 + bx1, y0 + by1) for bx0, by0, bx1, by1 in found]

        if len(boxes) != len(band.names):
            problems.append(
                f"  {band.key}: found {len(boxes)} regions but {len(band.names)} names")

        out_dir = OUT_ROOT / band.category
        out_dir.mkdir(parents=True, exist_ok=True)
        for index, box in enumerate(boxes):
            name = band.names[index] if index < len(band.names) else f"{band.key}-{index}"
            sprite = cut(image, box, band.tol, band.anchor,
                         isolate=band.grid is not None, dilate_radius=band.dilate)
            if sprite is None:
                problems.append(f"  {band.key}/{name}: empty cut at {box}")
                continue
            sprite.save(out_dir / f"{name}.png")
            colours = len({p[:3] for p in sprite.getdata() if p[3] > 128})
            sprites.append((name, sprite, colours))
        results.append((band, sprites))

    total = sum(len(s) for _, s in results)
    widest = max((len(s) for _, s in results), default=1)
    sheet = Image.new("RGBA", (widest * CANVAS, len(results) * CANVAS), (26, 20, 38, 255))
    for row, (_, sprites) in enumerate(results):
        for col, (_, sprite, _) in enumerate(sprites):
            sheet.paste(sprite, (col * CANVAS, row * CANVAS), sprite)
    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(SHEET)

    for band, sprites in results:
        names = ", ".join(f"{n}({c})" for n, _, c in sprites)
        print(f"{band.key:10s} -> {band.category:8s} {len(sprites):2d}  {names}")
    print(f"\n{total} sprites -> {OUT_ROOT}")
    print(f"contact sheet -> {SHEET}")
    if problems:
        print("\nPROBLEMS (band boxes likely need adjusting):")
        print("\n".join(problems))
    return 0


if __name__ == "__main__":
    raise SystemExit(run())
