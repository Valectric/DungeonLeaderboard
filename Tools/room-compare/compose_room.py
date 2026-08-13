#!/usr/bin/env python3
"""Compose a single dungeon room from the project's existing 64x64 pixel-art tiles.

Renders `mine-room.png` (exactly 480x480 RGB) next to this script. The reference the
composition is aimed at is `moodboard-room.png` in the same folder: a rectangular
chamber, one-tile-thick wall ring, props hugging the walls, warm flame pools on the
floor and a heavy violet-black vignette eating the corners.

Everything here is deterministic. Tile variation comes from an FNV-1a hash of the cell
coordinate, never from `random`, so two runs produce byte-identical output.

Pixel-art rules honoured: tiles are blitted 1:1 (no scaling, ever), the only resampling
allowed anywhere is nearest-neighbour, and the composite is padded rather than resized
to reach 480x480. Light is the one thing that is allowed to be smooth -- it is generated
procedurally as a radial falloff, the same way the game does it at runtime.
"""

from __future__ import annotations

import os
from typing import Iterable

import numpy as np
from PIL import Image

# --------------------------------------------------------------------------------------
# Geometry
# --------------------------------------------------------------------------------------

TILE = 64
"""Edge length in pixels of every source tile. Nothing is ever scaled off this."""

GRID = 7
"""Room size in tiles: a 7x7 chamber is a 1-tile wall ring around a 5x5 floor."""

ROOM_PX = TILE * GRID  # 448
"""Composite size in pixels before the outer frame is added."""

OUT_PX = 480
"""Required output size. 480 - 448 = 32, i.e. a 16px dark surround on every side."""

MARGIN = (OUT_PX - ROOM_PX) // 2  # 16
"""Thickness of the dark surround. Matches the moodboard's own outer dark margin."""

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Resources"))

# --------------------------------------------------------------------------------------
# Palette (project art doctrine)
# --------------------------------------------------------------------------------------

WARM = np.array([255.0, 140.0, 46.0])
"""Candlelight / flame colour. Burnt-orange family, per the doctrine."""

ARCANE = np.array([214.0, 82.0, 219.0])
"""Cool magenta arcane glow, used only under crystals."""

VOID = np.array([9.0, 7.0, 15.0])
"""Violet-black the room sits in. The outer frame and the darkest shadows land here."""

AMBIENT = np.array([0.92, 1.02, 1.08])
"""Unlit surface gain.

Blue leads, so unlit stone stays in the violet-black family the art doctrine asks for and
the wall ring reads blue-grey the way the moodboard's does. All the warmth in the room
comes from the flames, not from a global tint.
"""

WALL_GAIN = 1.52
"""Extra exposure on the wall ring.

In the moodboard the wall blocks are the brightest thing in frame and the floor sits
darker inside them. Straight ambient does not do that on these tiles, so the ring is
lifted explicitly.
"""

DESATURATE = 0.32
"""How far the source tiles are pulled towards their own luminance before lighting.

The moodboard's stone is a desaturated grey; these tiles are a far more saturated violet.
A light grade closes some of that gap without repainting the art or tipping it into the
warm brown the doctrine forbids.
"""


# --------------------------------------------------------------------------------------
# Deterministic hashing
# --------------------------------------------------------------------------------------


def cell_hash(x: int, y: int, salt: int = 0) -> int:
    """Return a stable 32-bit hash of a cell coordinate.

    FNV-1a over the three inputs. Used instead of `random` so that the scatter of floor
    variants and wall moss is identical on every run and on every machine.

    :param x: Cell column.
    :param y: Cell row.
    :param salt: Stream selector, so one cell can drive several independent choices.
    :return: Hash in [0, 2**32).
    """
    h = 2166136261
    for value in (x + 0x9E37, y + 0x85EB, salt + 0xC2B2):
        for _ in range(4):
            h ^= value & 0xFF
            h = (h * 16777619) & 0xFFFFFFFF
            value >>= 8
    return h


# --------------------------------------------------------------------------------------
# Asset loading
# --------------------------------------------------------------------------------------


def load_rgba(kind: str, name: str) -> Image.Image:
    """Load one 64x64 art asset as RGBA.

    :param kind: Subfolder under `Assets/Art/Resources` -- "tiles" or "props".
    :param name: File stem without extension.
    :return: The image, asserted to be exactly TILE x TILE so nothing needs scaling.
    """
    path = os.path.join(ART, kind, name + ".png")
    img = Image.open(path).convert("RGBA")
    if img.size != (TILE, TILE):
        raise ValueError(f"{path} is {img.size}, expected ({TILE}, {TILE})")
    return img


def content_crop(img: Image.Image, x0: int = 0, x1: int = TILE) -> Image.Image:
    """Crop a sprite to a column band and then tightly to its opaque content.

    Several props are drawn as a pair inside one 64x64 cell (lanterns.png holds two
    lanterns). Taking a column band and trimming the transparent surround lets a single
    item be placed on its own without scaling anything.

    :param img: Source RGBA sprite.
    :param x0: First column of the band, inclusive.
    :param x1: Last column of the band, exclusive.
    :return: The trimmed sprite. Never resized.
    """
    band = img.crop((x0, 0, x1, TILE))
    box = band.getbbox()
    return band.crop(box) if box else band


# --------------------------------------------------------------------------------------
# Tile map
# --------------------------------------------------------------------------------------


def pick_floor(x: int, y: int, drain_cell: tuple[int, int]) -> str:
    """Choose which floor variant a given interior cell uses.

    Variants are sparse on purpose -- the moodboard floor is mostly one texture with a
    little damage breaking it up. One drain exists per room, at the interior cell with
    the highest hash, so the feature reads as deliberate rather than as noise.

    :param x: Cell column.
    :param y: Cell row.
    :param drain_cell: The single cell that gets the drain.
    :return: Tile file stem.
    """
    if (x, y) == drain_cell:
        return "floor-drain"
    roll = cell_hash(x, y, salt=1) % 100
    if roll < 10:
        return "floor-rubble"
    if roll < 22:
        return "floor-cracked"
    return "floor-plain"


def build_tilemap() -> Image.Image:
    """Blit the wall ring and floor field into the ROOM_PX composite.

    :return: RGBA image of the untouched, unlit room at 1:1 tile scale.
    """
    tiles = {
        name: load_rgba("tiles", name)
        for name in (
            "floor-plain",
            "floor-cracked",
            "floor-rubble",
            "floor-drain",
            "wall",
            "wall-moss",
        )
    }

    interior = [(x, y) for y in range(1, GRID - 1) for x in range(1, GRID - 1)]
    drain_cell = max(interior, key=lambda c: cell_hash(c[0], c[1], salt=7))

    room = Image.new("RGBA", (ROOM_PX, ROOM_PX), (0, 0, 0, 255))
    for y in range(GRID):
        for x in range(GRID):
            on_border = x in (0, GRID - 1) or y in (0, GRID - 1)
            if on_border:
                mossy = cell_hash(x, y, salt=2) % 100 < 28
                tile = tiles["wall-moss" if mossy else "wall"]
            else:
                tile = tiles[pick_floor(x, y, drain_cell)]
            # Mirroring is a free extra variant: it costs no art, is exact (no resampling
            # at all), and stops six tiles across 49 cells from reading as wallpaper.
            flip = cell_hash(x, y, salt=3) % 4
            if flip & 1:
                tile = tile.transpose(Image.FLIP_LEFT_RIGHT)
            if flip & 2:
                tile = tile.transpose(Image.FLIP_TOP_BOTTOM)
            room.paste(tile, (x * TILE, y * TILE))
    return room


# --------------------------------------------------------------------------------------
# Props
# --------------------------------------------------------------------------------------


class Prop:
    """One placed prop plus the light it casts.

    :ivar sprite: Trimmed RGBA art, blitted 1:1.
    :ivar x: Left edge in composite pixels.
    :ivar y: Top edge in composite pixels.
    :ivar light: Either None, "warm" for flame, or "arcane" for crystals.
    :ivar strength: Scales this prop's light. A wall lantern burns harder than a candle.
    :ivar pool_dy: How far below the flame the light pool lands, in pixels.
    """

    def __init__(
        self,
        sprite: Image.Image,
        x: int,
        y: int,
        light: str | None,
        strength: float,
        pool_dy: float,
    ) -> None:
        """Store the placement.

        :param sprite: Trimmed RGBA art.
        :param x: Left edge in composite pixels.
        :param y: Top edge in composite pixels.
        :param light: None, "warm" or "arcane".
        :param strength: Multiplier on this prop's light contribution.
        :param pool_dy: How far below the flame the light pool lands, in pixels.
        """
        self.sprite = sprite
        self.x = x
        self.y = y
        self.light = light
        self.strength = strength
        self.pool_dy = pool_dy

    def emitter(self) -> tuple[float, float]:
        """Locate the light source inside the sprite.

        Rather than hand-tuning an offset per prop, the emitter is found from the art:
        the centroid of the brightest opaque pixels, which is the flame for a lantern and
        the crystal core for a cluster. The pool is then pushed a little further down the
        screen so it reads as light landing on the floor in front of the prop.

        :return: (x, y) in composite pixel coordinates.
        """
        arr = np.asarray(self.sprite).astype(float)
        rgb, alpha = arr[..., :3], arr[..., 3]
        lum = rgb.sum(axis=2)
        lum[alpha < 32] = 0.0
        cutoff = np.percentile(lum[lum > 0], 97) if (lum > 0).any() else 0.0
        mask = lum >= max(cutoff, 1.0)
        ys, xs = np.nonzero(mask)
        if len(xs) == 0:
            return self.x + self.sprite.width / 2, self.y + self.sprite.height / 2
        return self.x + float(xs.mean()), self.y + float(ys.mean()) + self.pool_dy


def place(
    sprite: Image.Image,
    cell: tuple[int, int],
    light: str | None = None,
    dy: int = 0,
    dx: int = 0,
    strength: float = 1.0,
    pool_dy: float = 12.0,
) -> Prop:
    """Place a sprite centred in a cell, sitting on that cell's floor line.

    :param sprite: Trimmed RGBA art.
    :param cell: (column, row) in tile coordinates.
    :param light: None, "warm" or "arcane".
    :param dy: Extra vertical nudge in pixels; negative pushes the prop into the wall.
    :param dx: Extra horizontal nudge in pixels.
    :param strength: Multiplier on this prop's light contribution.
    :param pool_dy: How far below the flame the light pool lands, in pixels.
    :return: The placed prop.
    """
    cx, cy = cell
    x = cx * TILE + (TILE - sprite.width) // 2 + dx
    y = cy * TILE + (TILE - sprite.height) + dy
    return Prop(sprite, x, y, light, strength, pool_dy)


def build_props() -> list[Prop]:
    """Lay props against the walls, echoing the moodboard's arrangement.

    Two flames high in the room and two low, a wall banner on the centre of the far wall,
    crystals and a portal frame breaking up the side walls. The middle of the floor is
    left clear -- that is where the party walks.

    :return: Props in draw order, back to front.
    """
    lanterns = load_rgba("props", "lanterns")
    lantern_l = content_crop(lanterns, 0, 33)
    lantern_r = content_crop(lanterns, 33, TILE)

    candle = content_crop(load_rgba("props", "candle-skull"))
    noticeboard = content_crop(load_rgba("props", "noticeboard"))
    crystals_l = content_crop(load_rgba("props", "crystals-large"))
    portal = content_crop(load_rgba("props", "portal-frame"))
    books = content_crop(load_rgba("props", "books"))

    last = GRID - 2  # 5, the last interior column/row

    return [
        # Far wall, dead centre: the room's focal mass, standing in for the moodboard's
        # chest. Nudged down so it straddles the wall foot and reads as leaning on it.
        place(noticeboard, (3, 0), dy=18),
        # Side walls, halfway down -- one dark silhouette each, as in the moodboard.
        place(portal, (last, 3), dy=-6),
        place(books, (1, 3), dy=-4),
        # Four flames, one per interior corner. This is the lighting skeleton: their
        # halos overlap into a warm pool across the middle of the floor. The pair on the
        # far wall burn hardest, so the room falls away towards the near wall the way the
        # moodboard's does.
        place(lantern_l, (1, 1), "warm", dy=-12, strength=1.15, pool_dy=34.0),
        place(lantern_r, (last, 1), "warm", dy=-12, strength=1.15, pool_dy=34.0),
        place(candle, (1, last), "warm", dy=-2, strength=0.38, pool_dy=16.0),
        place(
            candle.transpose(Image.FLIP_LEFT_RIGHT),
            (last, last),
            "warm",
            dy=-2,
            strength=0.38,
            pool_dy=16.0,
        ),
        # A single cool accent, against the near wall between the two candles.
        place(crystals_l, (3, last), "arcane", dy=-2, pool_dy=14.0),
    ]


# --------------------------------------------------------------------------------------
# Lighting
# --------------------------------------------------------------------------------------


def radial(shape: tuple[int, int], cx: float, cy: float, radius: float) -> np.ndarray:
    """Build a soft radial falloff field.

    Smoothstep on normalised distance, squared. Zero outside the radius, so a light has
    a finite reach and the corners of the room can stay genuinely black.

    :param shape: (height, width) of the field.
    :param cx: Centre x.
    :param cy: Centre y.
    :param radius: Reach in pixels.
    :return: Float field in [0, 1].
    """
    h, w = shape
    ys = np.arange(h, dtype=float)[:, None]
    xs = np.arange(w, dtype=float)[None, :]
    d = np.sqrt((xs - cx) ** 2 + (ys - cy) ** 2) / max(radius, 1.0)
    t = np.clip(1.0 - d, 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def surface_gain(shape: tuple[int, int]) -> np.ndarray:
    """Per-pixel exposure that separates the wall ring from the floor.

    Two effects in one field. The wall ring is lifted so the stone reads bright, the way
    it does in the moodboard. The floor is darkened in the gutter where it meets that
    ring, which is what makes the chamber feel enclosed instead of like a flat tile grid.

    :param shape: (height, width) of the field.
    :return: Multiplier field.
    """
    h, w = shape
    ys = np.arange(h, dtype=float)[:, None]
    xs = np.arange(w, dtype=float)[None, :]
    inner_lo, inner_hi = TILE, ROOM_PX - TILE
    dx = np.minimum(xs - inner_lo, inner_hi - xs)
    dy = np.minimum(ys - inner_lo, inner_hi - ys)
    dist = np.minimum(dx, dy)

    reach = 26.0
    t = np.clip(dist / reach, 0.0, 1.0)
    gutter = 0.62 + 0.38 * (t * t * (3.0 - 2.0 * t))

    # The moodboard's floor is brightest along the far wall and falls away towards the
    # near one. Half of that comes from where its torches are, half is just depth; this
    # is the depth half, and it is what stops the interior reading as a flat plane.
    depth = np.clip((ys - inner_lo) / float(inner_hi - inner_lo), 0.0, 1.0)
    gutter = gutter * (1.06 - 0.22 * depth)

    # On the wall ring, distance runs negative outwards. Rolling the exposure off towards
    # the outer edge turns a flat grey band into stone that curves away into the dark, and
    # lets the room dissolve into the surround instead of stopping at a hard rectangle.
    u = np.clip(-dist / float(TILE), 0.0, 1.0)
    ledge = WALL_GAIN * (1.0 - 0.42 * (u * u * (3.0 - 2.0 * u)))

    is_floor = np.broadcast_to(dist >= 0.0, shape)
    return np.where(is_floor, gutter, ledge)


def tile_jitter(shape: tuple[int, int]) -> np.ndarray:
    """Give each tile a slightly different exposure.

    Six source tiles laid over 49 cells repeat visibly. A small deterministic brightness
    offset per cell breaks that up the way the moodboard's patchy, hand-toned floor does,
    without touching a single pixel of the art itself.

    :param shape: (height, width) of the field. Must be a whole number of tiles.
    :return: Multiplier field around 1.0.
    """
    h, w = shape
    cells = np.empty((h // TILE, w // TILE), dtype=float)
    for cy in range(cells.shape[0]):
        for cx in range(cells.shape[1]):
            cells[cy, cx] = 0.93 + 0.14 * ((cell_hash(cx, cy, salt=5) % 1000) / 999.0)
    return np.kron(cells, np.ones((TILE, TILE), dtype=float))


def vignette(shape: tuple[int, int]) -> np.ndarray:
    """Build the corner-eating vignette.

    Separable, not radial, because that is what the moodboard actually does: the middle of
    each wall run stays clearly lit while the four corners drop away to near black. A plain
    radial vignette dims the mid-edges just as hard as the corners and flattens the whole
    wall ring, which is exactly the wrong read.

    :param shape: (height, width) of the field.
    :return: Multiplier field, 1.0 at the centre falling to roughly 0.35 in the corners.
    """
    h, w = shape
    ey = np.abs(np.arange(h, dtype=float)[:, None] - (h - 1) / 2.0) / ((h - 1) / 2.0)
    ex = np.abs(np.arange(w, dtype=float)[None, :] - (w - 1) / 2.0) / ((w - 1) / 2.0)

    def inward(e: np.ndarray) -> np.ndarray:
        """Return 1 well inside the frame, 0 at the frame edge.

        :param e: Normalised distance from the centre along one axis, 0 at the centre.
        :return: Smoothstepped inwardness.
        """
        t = np.clip((1.0 - e) / 0.55, 0.0, 1.0)
        return t * t * (3.0 - 2.0 * t)

    tx, ty = inward(ex), inward(ey)
    corners = 1.0 - 0.58 * (1.0 - tx) * (1.0 - ty)
    edges = 1.0 - 0.24 * (1.0 - tx * ty)
    return corners * edges


def light_room(base: np.ndarray, props: Iterable[Prop]) -> np.ndarray:
    """Apply the full lighting model to the flat tile composite.

    The model is deliberately the one the game uses: surfaces are *modulated* by the light
    reaching them, so texture survives inside a bright pool, plus a small additive bloom
    right at the flame so the source itself reads as hot. Each light has a tight core and
    a wide, weak halo; the overlapping halos are what produce the moodboard's warm middle
    without ever painting a visible coloured disc.

    :param base: RGB float array of the unlit room, shape (ROOM_PX, ROOM_PX, 3).
    :param props: Placed props; those with a light contribute a pool.
    :return: Lit RGB float array, unclipped.
    """
    shape = base.shape[:2]
    gain = np.zeros(shape + (3,), dtype=float)
    bloom = np.zeros(shape + (3,), dtype=float)

    gain += AMBIENT[None, None, :] * surface_gain(shape)[..., None]

    for prop in props:
        if prop.light is None:
            continue
        cx, cy = prop.emitter()
        s = prop.strength
        # A dimmer flame is also a smaller one: reach scales with strength, so a candle
        # throws a tight pool at its own feet while a wall lantern washes half the floor.
        r = 0.58 + 0.42 * s
        if prop.light == "warm":
            # Three lobes, not two. A squared smoothstep drops away fast, so a bare
            # core+halo pair leaves a dead ring at mid distance; the middle lobe is what
            # carries the light out across the floor the way the moodboard's torches do.
            colour = WARM / 255.0
            core = radial(shape, cx, cy, 96.0 * r)
            mid = radial(shape, cx, cy, 180.0 * r)
            halo = radial(shape, cx, cy, 320.0 * r)
            gain += (
                colour[None, None, :]
                * (s * (0.55 * core + 0.34 * mid + 0.26 * halo))[..., None]
            )
            # Part of the light has to be additive. These floor tiles are a saturated
            # violet with almost no green in them, and a purely multiplicative light can
            # never put green back -- a warm pool on them stays stubbornly purple. The
            # additive term is what actually makes the floor read as lit by fire.
            flame = radial(shape, cx, cy, 34.0 * r)
            bloom += (
                colour[None, None, :]
                * (s * (40.0 * flame + 16.0 * core + 11.0 * mid + 6.0 * halo))[..., None]
            )
        else:
            # Deliberately weaker than the flames. A magenta pool as bright as a torch
            # stops reading as light and starts reading as a coloured disc laid on the
            # floor, so the arcane glow gets reach but not intensity.
            colour = ARCANE / 255.0
            core = radial(shape, cx, cy, 62.0)
            halo = radial(shape, cx, cy, 150.0)
            gain += colour[None, None, :] * (s * (0.16 * core + 0.10 * halo))[..., None]
            bloom += (
                colour[None, None, :]
                * (s * (9.0 * radial(shape, cx, cy, 24.0) + 7.0 * core + 4.0 * halo))[
                    ..., None
                ]
            )

    lit = base * gain * tile_jitter(shape)[..., None] + bloom
    return lit * vignette(shape)[..., None]


# --------------------------------------------------------------------------------------
# Assembly
# --------------------------------------------------------------------------------------


def compose() -> Image.Image:
    """Build the finished 480x480 room.

    :return: RGB image, exactly OUT_PX square.
    """
    room = build_tilemap()
    props = build_props()
    for prop in props:
        room.alpha_composite(prop.sprite, (prop.x, prop.y))

    base = np.asarray(room.convert("RGB")).astype(float)
    grey = base @ np.array([0.299, 0.587, 0.114])
    base = base * (1.0 - DESATURATE) + grey[..., None] * DESATURATE
    lit = np.clip(light_room(base, props), 0.0, 255.0)

    # The room is padded, never resampled, so every tile stays 1:1 and pixel-perfect.
    # The surround is the same violet-black the vignette is already heading towards, so
    # the wall ring dissolves into the dark rather than stopping at a hard frame.
    canvas = np.zeros((OUT_PX, OUT_PX, 3), dtype=float)
    canvas[:, :] = VOID[None, None, :]
    canvas[MARGIN : MARGIN + ROOM_PX, MARGIN : MARGIN + ROOM_PX] = lit

    out = Image.fromarray(np.clip(canvas, 0, 255).astype(np.uint8), "RGB")
    if out.size != (OUT_PX, OUT_PX):
        raise ValueError(f"output is {out.size}, expected ({OUT_PX}, {OUT_PX})")
    return out


def main() -> None:
    """Render the room and write it next to this script."""
    dest = os.path.join(HERE, "mine-room.png")
    compose().save(dest)
    print(f"wrote {dest}")


if __name__ == "__main__":
    main()
