"""Cuts the upper doorframe out of the door sprite the game already ships.

The party is drawn at sorting order 20 and this band at 25, so an adventurer standing in a
doorway passes BEHIND the arch instead of sliding over it. That is the whole feature, and it
is what the author asked for when they said the doorway should cover the team "so it look
like they walked under".

Why a cut rather than new art: the band lands over the door it came from, so it matches pixel
for pixel in both states -- door-a when shut and door-gate when open, which share this arch --
and there is no seam to hide, because the pixels underneath are the same pixels. It also keeps
the licence position clean. door-a is CC0 (Dungeon Crawl Stone Soup, see CREDITS.md), so the
band is CC0 too, and nothing from a no-redistribution pack goes near this public repository.

Run from the repository root:

    python Tools/make-door-lintel.py

Re-run it after changing door-a.png, or the arch and its lintel will drift apart.
"""

import pathlib
import sys

from PIL import Image

# Rows kept at full alpha: the bright stone arch band at the top of the door. Measured on
# door-a, whose luminance runs 59, 60, 46, 40 down these rows and then settles around 34 where
# the wooden leaf starts. Seventeen covers the arch and the springing of it.
SOLID_ROWS = 17

# Rows faded out below the solid band. The cut is invisible over the door it was taken from,
# but the ramp means it stays invisible over anything else the door may become.
FADE_ROWS = 5

SOURCE = pathlib.Path("Assets/Art/Resources/dungeon/door-a.png")
TARGET = pathlib.Path("Assets/Art/Resources/dungeon/door-top.png")


def main() -> int:
    """Writes the lintel next to the door it was cut from.

    Returns 0 on success, 1 if the source sprite is missing.
    """
    if not SOURCE.exists():
        print(f"missing {SOURCE} -- run this from the repository root", file=sys.stderr)
        return 1

    source = Image.open(SOURCE).convert("RGBA")
    width, height = source.size
    lintel = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    read = source.load()
    write = lintel.load()

    for y in range(SOLID_ROWS + FADE_ROWS):
        if y < SOLID_ROWS:
            scale = 1.0
        else:
            scale = 1.0 - ((y - SOLID_ROWS + 1) / (FADE_ROWS + 1))

        for x in range(width):
            red, green, blue, alpha = read[x, y]
            write[x, y] = (red, green, blue, int(alpha * scale))

    lintel.save(TARGET)

    covered = sum(
        1
        for y in range(height)
        for x in range(width)
        if lintel.getpixel((x, y))[3] > 8
    )
    print(
        f"{TARGET} written: {width}x{height}, "
        f"{covered} opaque pixels over the top {SOLID_ROWS + FADE_ROWS} rows"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
