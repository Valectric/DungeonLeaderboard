"""Builds this game's tileset from CC0 Dungeon Crawl Stone Soup tiles.

Source: https://github.com/crawl/tiles (CC Zero -- "You can use these tilesets in your
program freely. No attribution is required."). Only the families measured to read as
masonry are used: `catacombs` for walls and `cobble_blood` for floors, plus the door
pair from `dngn/doors`, which is the only free set found with a genuine open AND closed
state drawn in the same frame.

Three things happen here, and the middle one is the point:

1. **Recolour.** Each tile's own luminance is mapped onto the moodboard ramp, so the
   masonry's light-to-dark structure survives and the hue becomes ours. Recolouring by
   luminance rather than by hue rotation is what keeps a grey stone pack from turning
   into a purple smear -- the value structure is the drawing.

2. **The sixteen wall shapes.** DCSS does not ship a bitmask wall run in this release
   (checked: `dngn/wall` has 305 files and no `rock_wall_00-15`, whatever the internet
   says), so the edges are generated: a pale top-lit lip on each side that faces open
   floor and a near-black shadow just inside it. That is the whole difference between a
   wall that reads as a block of stone and one that reads as a repeated band, and it is
   the defect `TILESET-SEARCH.md` measured.

3. **Scale.** 32x32 sources point-sampled to the 64x64 the project already imports.

Run:  python Tools/import-tileset.py [--install]
Without --install it writes previews to Screenshots/ and touches nothing else.
"""

import os
import sys
import urllib.request

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(ROOT, "Tools", ".tileset-cache")
INSTALL = os.path.join(ROOT, "Assets", "Art", "Resources", "tiles")
BASE = "https://raw.githubusercontent.com/crawl/tiles/master/releases/Nov-2015/dngn"

# The moodboard ramp, darkest to lightest. Sampled from the palette the whole project is
# drawn in: violet-black, royal purple, blue-grey, and a pale lip for the top edges.
RAMP = [
    (0x0E, 0x0A, 0x14),
    (0x1B, 0x13, 0x25),
    (0x25, 0x1B, 0x31),
    (0x33, 0x22, 0x42),
    (0x44, 0x2C, 0x55),
    (0x50, 0x27, 0x5E),
    (0x55, 0x45, 0x6B),
    (0x63, 0x5D, 0x7C),
    (0x7C, 0x76, 0x93),
    (0x9A, 0x93, 0xB0),
]

WALL_SOURCES = ["catacombs1", "catacombs2", "catacombs3"]
FLOOR_SOURCES = ["cobble_blood1", "cobble_blood2", "cobble_blood3", "cobble_blood4"]
DOOR_SOURCES = ["closed_door", "open_door", "runed_door"]


def fetch(folder, name):
    """Downloads one tile, caching it so a re-run costs nothing."""
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, f"{name}.png")
    if not os.path.exists(path):
        urllib.request.urlretrieve(f"{BASE}/{folder}/{name}.png", path)
    return Image.open(path).convert("RGBA")


def luminance(pixel):
    """Rec. 709 luminance of an RGB tuple."""
    return (0.2126 * pixel[0]) + (0.7152 * pixel[1]) + (0.0722 * pixel[2])


def ramp_colour(t):
    """Samples the moodboard ramp at 0..1, interpolating between stops."""
    t = max(0.0, min(1.0, t))
    span = t * (len(RAMP) - 1)
    low = int(span)
    high = min(low + 1, len(RAMP) - 1)
    blend = span - low
    return tuple(
        round(RAMP[low][i] + ((RAMP[high][i] - RAMP[low][i]) * blend)) for i in range(3)
    )


def recolour(image, floor, ceiling):
    """
    Maps a tile's own luminance range onto the ramp, between two ramp positions.

    Vectorised, because the readable per-pixel version took minutes over twenty-six tiles
    and timed out the run that was meant to preview it.
    """
    source = np.asarray(image, dtype=np.float32)
    rgb, alpha = source[..., :3], source[..., 3]

    lum = (rgb[..., 0] * 0.2126) + (rgb[..., 1] * 0.7152) + (rgb[..., 2] * 0.0722)
    visible = alpha > 0
    if not visible.any():
        return image

    low, high = float(lum[visible].min()), float(lum[visible].max())
    spread = max(1.0, high - low)

    position = floor + (((lum - low) / spread) * (ceiling - floor))
    position = np.clip(position, 0.0, 1.0) * (len(RAMP) - 1)

    ramp = np.asarray(RAMP, dtype=np.float32)
    lower = np.clip(position.astype(np.int32), 0, len(RAMP) - 1)
    upper = np.clip(lower + 1, 0, len(RAMP) - 1)
    blend = (position - lower)[..., None]

    mixed = (ramp[lower] * (1.0 - blend)) + (ramp[upper] * blend)
    out = np.dstack([np.rint(mixed), alpha]).astype(np.uint8)
    out[~visible] = 0

    return Image.fromarray(out, "RGBA")


def shape_wall(base, mask):
    """
    Draws the lit lip and inner shadow for a wall block, given its neighbour mask.

    Bit 1 north, 2 east, 4 south, 8 west, set when that side is backed by more wall --
    the same numbering `DungeonScenery.WallMask` computes. A side that is NOT set faces
    open floor and gets the edge treatment.
    """
    tile = base.copy()
    pixels = tile.load()
    size = tile.width
    lip = max(2, size // 16)

    def paint(x, y, colour, weight):
        r, g, b, a = pixels[x, y]

        # Modulated by what is already there, so the light catches the tops of blocks and
        # skips the mortar between them. A flat band across the whole tile came out as a
        # ruler-straight pale line drawn across the dungeon -- correct by the mask, and
        # obviously not masonry.
        own = luminance((r, g, b)) / 255.0
        weight = max(0.0, min(1.0, weight * (0.45 + own)))

        pixels[x, y] = (
            round((r * (1 - weight)) + (colour[0] * weight)),
            round((g * (1 - weight)) + (colour[1] * weight)),
            round((b * (1 - weight)) + (colour[2] * weight)),
            a,
        )

    pale = ramp_colour(0.78)
    dark = ramp_colour(0.0)

    # North is up in world space, which is the TOP of the image once Unity flips it, so
    # a north-facing edge is the one that catches the light.
    if not mask & 1:
        for y in range(lip * 2):
            for x in range(size):
                paint(x, y, pale if y < lip else dark, 0.75 if y < lip else 0.5)

    if not mask & 4:
        for y in range(size - lip, size):
            for x in range(size):
                paint(x, y, dark, 0.7)

    if not mask & 2:
        for x in range(size - lip, size):
            for y in range(size):
                paint(x, y, dark, 0.45)

    if not mask & 8:
        for x in range(lip):
            for y in range(size):
                paint(x, y, dark, 0.45)

    return tile


def upscale(image, size=64):
    """Point-samples to the size the project imports at."""
    return image.resize((size, size), Image.NEAREST)


def measure(image):
    """Mean luminance of the opaque pixels."""
    pixels = image.convert("RGBA").load()
    values = [
        luminance(pixels[x, y])
        for y in range(image.height)
        for x in range(image.width)
        if pixels[x, y][3] > 0
    ]
    return sum(values) / max(1, len(values))


def build():
    """Produces every tile the game asks for, as a dict of name to image."""
    tiles = {}

    # Ramp windows, and they are the whole tuning. The first attempt used 0.30-0.92 for
    # walls and produced a vivid magenta-purple dungeon at luminance 61 -- structurally
    # right and tonally a sweet shop. The moodboard is violet-BLACK dominant with small
    # bright lights, so the windows sit low and the pale end of the ramp is reserved for
    # the lit lip on a wall's open edge, which is the only thing that should be bright.
    walls = [recolour(fetch("wall", n), 0.12, 0.58) for n in WALL_SOURCES]
    floors = [recolour(fetch("floor", n), 0.02, 0.26) for n in FLOOR_SOURCES]

    tiles["wall"] = upscale(walls[0])
    tiles["wall-cracked"] = upscale(walls[1])
    tiles["wall-moss"] = upscale(walls[2])

    tiles["floor-plain"] = upscale(floors[0])
    tiles["floor-cracked"] = upscale(floors[1])
    tiles["floor-rubble"] = upscale(floors[2])
    tiles["floor-drain"] = upscale(floors[3])

    for mask in range(16):
        tiles[f"wall-{mask}"] = upscale(shape_wall(walls[mask % len(walls)], mask))

    for name, source in zip(["door-a", "door-b", "door-gate"], DOOR_SOURCES):
        tiles[name] = upscale(recolour(fetch("doors", source), 0.10, 0.95))

    return tiles


def contact_sheet(tiles, path):
    """Writes every tile onto one image, for a person to look at."""
    names = list(tiles)
    columns = 8
    rows = (len(names) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * 72, rows * 72), (0x15, 0x10, 0x1D))

    for i, name in enumerate(names):
        cell = Image.new("RGBA", (64, 64), (0x25, 0x1B, 0x31, 255))
        cell.alpha_composite(tiles[name])
        sheet.paste(cell.convert("RGB"), ((i % columns) * 72 + 4, (i // columns) * 72 + 4))

    sheet.save(path)
    return path


def main():
    tiles = build()

    wall = measure(tiles["wall"])
    floor = measure(tiles["floor-plain"])
    print(f"wall {wall:.1f}  floor {floor:.1f}  ratio {wall / max(1, floor):.2f}")

    sheet = contact_sheet(tiles, os.path.join(ROOT, "Screenshots", "tileset-preview.png"))
    print("preview:", sheet)

    if "--install" in sys.argv:
        # Doors live under Resources/dungeon/, not Resources/tiles/ -- DungeonScenery loads
        # them as "dungeon/door-a". Writing them beside the tiles produced twenty-six files
        # and a dungeon still drawing its old doors.
        doors = os.path.join(ROOT, "Assets", "Art", "Resources", "dungeon")
        os.makedirs(INSTALL, exist_ok=True)
        os.makedirs(doors, exist_ok=True)

        for name, image in tiles.items():
            folder = doors if name.startswith("door-") else INSTALL
            image.save(os.path.join(folder, f"{name}.png"))

        print(f"installed {len(tiles)} tiles into {INSTALL} and {doors}")
    else:
        print("dry run -- pass --install to write into Assets/Art/Resources/tiles")


if __name__ == "__main__":
    main()
