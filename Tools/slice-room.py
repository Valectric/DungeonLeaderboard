"""Cuts a restyled room screenshot back into named tiles.

The generator is good at painting a room and bad at drawing forty-eight independent tiles: two
attempts proved it, the second measured at 38 of 42 cell seams running continuously. So the room is
rendered by the game at exactly 64 pixels per cell, repainted by the generator at the same size, and
cut back up here at coordinates the game already recorded.

Every cell's identity comes from the `.cells.txt` manifest written alongside the screenshot, so a
wall tile is named by the neighbour mask `DungeonScenery.WallMask` will ask for -- no eyeballing
which piece is which.

Run:  python Tools/slice-room.py <restyled.png> <cells.txt> [--install]
"""

import os
import sys
from collections import defaultdict

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TILES = os.path.join(ROOT, "Assets", "Art", "Resources", "tiles")
CELL = 64


def read_manifest(path):
    """Parses the manifest into (pixel_x, pixel_y, kind, detail) rows."""
    rows = []
    for line in open(path, encoding="utf-8").read().splitlines():
        if not line or line.startswith("#") or line.startswith("image"):
            continue
        parts = line.split()
        if len(parts) < 6:
            continue
        rows.append((int(parts[0]), int(parts[1]), parts[4], parts[5]))
    return rows


def slice_room(image_path, manifest_path):
    """Returns {tile name: image}, choosing one representative per wall mask."""
    sheet = Image.open(image_path).convert("RGBA")
    rows = read_manifest(manifest_path)

    by_mask = defaultdict(list)
    floors = []

    for px, py, kind, detail in rows:
        if px + CELL > sheet.width or py + CELL > sheet.height:
            continue

        tile = sheet.crop((px, py, px + CELL, py + CELL))

        if kind == "Wall" and detail.startswith("mask="):
            by_mask[int(detail.split("=", 1)[1])].append(tile)
        elif kind == "Floor":
            floors.append(tile)

    tiles = {}

    # One tile per mask. The middle of each run is taken rather than the first, because the first
    # is usually against the frame edge where the generator's brushwork runs out.
    for mask, found in by_mask.items():
        tiles[f"wall-{mask}"] = found[len(found) // 2]

    for i, name in enumerate(["floor-plain", "floor-cracked", "floor-rubble", "floor-drain"]):
        if floors:
            tiles[name] = floors[(i * 7 + 3) % len(floors)]

    # The flat variants the old selection path still asks for, so nothing falls back to a
    # missing sprite while both paths exist.
    if 15 in by_mask:
        tiles["wall"] = by_mask[15][len(by_mask[15]) // 2]
        tiles["wall-cracked"] = by_mask[15][0]
        tiles["wall-moss"] = by_mask[15][-1]
    elif by_mask:
        any_mask = sorted(by_mask)[0]
        tiles["wall"] = by_mask[any_mask][0]
        tiles["wall-cracked"] = by_mask[any_mask][0]
        tiles["wall-moss"] = by_mask[any_mask][0]

    return tiles, sorted(by_mask), len(floors)


# The moodboard ramp, darkest to lightest, shared with import-tileset.py.
RAMP = np.array([
    (0x0E, 0x0A, 0x14), (0x1B, 0x13, 0x25), (0x25, 0x1B, 0x31), (0x33, 0x22, 0x42),
    (0x44, 0x2C, 0x55), (0x50, 0x27, 0x5E), (0x55, 0x45, 0x6B), (0x63, 0x5D, 0x7C),
    (0x7C, 0x76, 0x93), (0x9A, 0x93, 0xB0),
], dtype=np.float32)


def normalise(tiles, targets):
    """
    Brings a class of tiles to one luminance, then snaps every pixel to the ramp.

    ORDER MATTERS, and not in the obvious direction. Measured on a sliced sheet: quantising first --
    which is what every other tool here does -- took the wall spread from 2.12x to 2.20x, slightly
    WORSE. Normalising the mean luminance per class first and quantising after took it to 1.12x,
    with the standard deviation falling from 10.0 to 1.4 and the colour count from 25,104 to 10.

    Without this the slice path ships whatever the generator felt like that run: wall-11 measured
    26.0 against floor-drain at 28.7, a wall reading darker than the floor beside it.
    """
    out = {}
    for name, image in tiles.items():
        klass = "wall" if name.startswith("wall") else "floor"
        target = targets.get(klass)
        a = np.asarray(image.convert("RGBA"), dtype=np.float32)
        rgb, alpha = a[..., :3], a[..., 3:4]

        lum = (rgb[..., 0] * 0.2126) + (rgb[..., 1] * 0.7152) + (rgb[..., 2] * 0.0722)
        if target and lum.mean() > 1:
            rgb = np.clip(rgb * (target / lum.mean()), 0, 255)

        # Snap to the ramp by nearest luminance, which keeps the value structure and drops the
        # thousands of intermediate colours a painted source carries.
        stop = ((rgb[..., 0] * 0.2126) + (rgb[..., 1] * 0.7152) + (rgb[..., 2] * 0.0722))
        ramp_lum = (RAMP[:, 0] * 0.2126) + (RAMP[:, 1] * 0.7152) + (RAMP[:, 2] * 0.0722)
        idx = np.abs(stop[..., None] - ramp_lum[None, None, :]).argmin(axis=-1)

        snapped = np.concatenate([RAMP[idx], alpha], axis=-1).astype(np.uint8)
        out[name] = Image.fromarray(snapped, "RGBA")

    return out


def contact_sheet(tiles, path):
    """Writes every cut tile onto one image, for a person to look at."""
    names = sorted(tiles)
    columns = 8
    rows = (len(names) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * 72, rows * 72), (0x15, 0x10, 0x1D))

    for i, name in enumerate(names):
        cell = Image.new("RGBA", (CELL, CELL), (0x25, 0x1B, 0x31, 255))
        cell.alpha_composite(tiles[name])
        sheet.paste(cell.convert("RGB"), ((i % columns) * 72 + 4, (i // columns) * 72 + 4))

    sheet.save(path)
    return path


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return

    image_path, manifest_path = sys.argv[1], sys.argv[2]
    tiles, masks, floor_count = slice_room(image_path, manifest_path)

    # Walls a little over twice the floor, which is the measured relationship in the reference
    # tileset: the lit cap carries the separation, not the face.
    tiles = normalise(tiles, {"wall": 42.0, "floor": 19.0})

    print(f"cut {len(tiles)} tiles: masks {masks}, {floor_count} floor cells available")

    sheet = contact_sheet(tiles, os.path.join(ROOT, "Screenshots", "sliced-tiles.png"))
    print("contact sheet:", sheet)

    if "--install" in sys.argv:
        os.makedirs(TILES, exist_ok=True)
        for name, tile in tiles.items():
            tile.save(os.path.join(TILES, f"{name}.png"))
        print(f"installed {len(tiles)} tiles into {TILES}")
    else:
        print("dry run -- pass --install to write into Assets/Art/Resources/tiles")


if __name__ == "__main__":
    main()
