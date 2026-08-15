"""Builds the itch.io page art for Dungeon Leaderboard from the game's own assets.

Nothing here is drawn by hand or fetched from anywhere. The cover and the banner are
crops of `Assets/Art/Resources/scenes/end-screen.png` -- the key art the game itself
shows -- with the title set over them in the palette from the moodboard, and the
screenshots are the real frames the Play Mode tests capture into `Screenshots/`.

That is the point rather than a shortcut: a store page assembled from separate art
promises a game the player does not get, and this one is made of the same pixels.

Run:  python Tools/make-itch-art.py
Out:  Marketing/
"""

import os
import shutil

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = os.path.join(ROOT, "Assets", "Art", "Resources", "scenes")
SHOTS = os.path.join(ROOT, "Screenshots")
OUT = os.path.join(ROOT, "Marketing")

# The moodboard palette, which is the authority on colour everywhere in this project.
VIOLET_BLACK = (0x25, 0x1B, 0x31)
ROYAL_PURPLE = (0x50, 0x27, 0x5E)
MAGENTA = (0xD7, 0x52, 0x68)
BURNT_ORANGE = (0x85, 0x43, 0x2A)
BLUE_GREY = (0x50, 0x4D, 0x63)
PALE = (0xE8, 0xE2, 0xF2)

TITLE_FONT = "C:/Windows/Fonts/ariblk.ttf"
BODY_FONT = "C:/Windows/Fonts/arialbd.ttf"


def font(path, size):
    """Loads a font, falling back to PIL's default if Windows has moved it."""
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default()


def key_art():
    """The end-screen key art, which is the most striking image the game owns."""
    return Image.open(os.path.join(ART, "end-screen.png")).convert("RGB")


def crop_to(image, width, height, focus_y=0.5):
    """Crops to an aspect ratio around a focal row, then resizes."""
    target = width / height
    w, h = image.size
    if w / h > target:
        new_w = int(h * target)
        left = (w - new_w) // 2
        box = (left, 0, left + new_w, h)
    else:
        new_h = int(w / target)
        top = int((h - new_h) * focus_y)
        box = (0, top, w, top + new_h)
    return image.crop(box).resize((width, height), Image.LANCZOS)


def vignette(image, strength=0.75):
    """Darkens the edges so text sits on something quiet."""
    w, h = image.size
    mask = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((-w * 0.25, -h * 0.35, w * 1.25, h * 1.35), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(radius=max(w, h) * 0.09))

    dark = ImageEnhance.Brightness(image).enhance(1.0 - strength)
    return Image.composite(image, dark, mask)


def glow_text(image, xy, text, typeface, fill, glow, spacing=0, glow_radius=10):
    """Draws letter-spaced text with a coloured bloom behind it."""
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)

    x, y = xy
    for char in text:
        draw.text((x, y), char, font=typeface, fill=glow + (255,))
        x += draw.textlength(char, font=typeface) + spacing

    bloom = layer.filter(ImageFilter.GaussianBlur(glow_radius))
    image.alpha_composite(bloom)
    image.alpha_composite(bloom)

    sharp = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(sharp)
    x, y = xy
    for char in text:
        draw.text((x + 3, y + 3), char, font=typeface, fill=(6, 4, 10, 220))
        x += draw.textlength(char, font=typeface) + spacing

    x, y = xy
    for char in text:
        draw.text((x, y), char, font=typeface, fill=fill + (255,))
        x += draw.textlength(char, font=typeface) + spacing

    image.alpha_composite(sharp)
    return image


def text_width(text, typeface, spacing):
    """Width of letter-spaced text."""
    probe = ImageDraw.Draw(Image.new("RGB", (8, 8)))
    return sum(probe.textlength(c, font=typeface) + spacing for c in text) - spacing


def cover():
    """630x500 -- the tile itch shows in browse, search and on the page itself."""
    base = crop_to(key_art(), 630, 500, focus_y=0.45)
    base = vignette(base, 0.62).convert("RGBA")

    title = font(TITLE_FONT, 74)
    tag = font(BODY_FONT, 25)
    sub = font(BODY_FONT, 23)

    width = text_width("DUNGEON", title, 3)
    glow_text(base, ((630 - width) / 2, 92), "DUNGEON", title, PALE, MAGENTA, 3, 18)

    width = text_width("LEAGUE", title, 3)
    glow_text(base, ((630 - width) / 2, 168), "LEAGUE", title, PALE, MAGENTA, 3, 18)

    width = text_width("KEEP THEM ALIVE.", tag, 2)
    glow_text(base, ((630 - width) / 2, 268), "KEEP THEM ALIVE.", tag, PALE, ROYAL_PURPLE, 2, 8)

    width = text_width("KEEP THEM BLEEDING.", tag, 2)
    glow_text(base, ((630 - width) / 2, 300), "KEEP THEM BLEEDING.", tag, MAGENTA,
              ROYAL_PURPLE, 2, 8)

    # Sized for the tile itch actually draws, which is 315x250 -- half of this. A line that reads
    # comfortably here comes out at nine pixels there, which is where a store page loses people.
    width = text_width("YOU ARE THE DUNGEON", sub, 4)
    glow_text(base, ((630 - width) / 2, 402), "YOU ARE THE DUNGEON", sub,
              (0xC9, 0xBE, 0xD8), VIOLET_BLACK, 4, 6)

    return base.convert("RGB")


def background():
    """1920x1080 -- page furniture, deliberately too dark to compete with the text."""
    base = crop_to(key_art(), 1920, 1080, focus_y=0.5)
    base = ImageEnhance.Brightness(base).enhance(0.42)
    return ImageEnhance.Color(base).enhance(0.85)


def banner():
    """1600x400 -- the wide strip at the top of the page."""
    base = crop_to(key_art(), 1600, 400, focus_y=0.5)
    base = vignette(base, 0.5).convert("RGBA")

    # Darken the left third so the title has somewhere to live without hiding the core.
    shade = Image.new("RGBA", (1600, 400), (0, 0, 0, 0))
    draw = ImageDraw.Draw(shade)
    for x in range(900):
        alpha = int(190 * (1 - (x / 900.0)) ** 1.4)
        draw.line([(x, 0), (x, 400)], fill=(6, 4, 12, alpha))
    base.alpha_composite(shade)

    title = font(TITLE_FONT, 82)
    tag = font(BODY_FONT, 27)

    glow_text(base, (70, 108), "DUNGEON LEAGUE", title, PALE, MAGENTA, 4, 20)
    glow_text(base, (76, 214), "THEY CHARGE IN.  YOU CHARGE UP.", tag, MAGENTA,
              ROYAL_PURPLE, 3, 9)
    glow_text(base, (76, 256), "KILLING THEM IS LOSING.", tag, (0xC9, 0xBE, 0xD8),
              VIOLET_BLACK, 3, 7)

    return base.convert("RGB")


# The captures worth showing, in the order a page should tell the story.
SCREENSHOTS = [
    ("01-raid-opening-hud.png", "screenshot-1-a-party-charges-in.png"),
    ("03-engaged-hud.png", "screenshot-2-hold-them-and-the-rate-climbs.png"),
    ("06-shop-with-menu.png", "screenshot-3-build-between-raids.png"),
    ("08-mid-season-standings.png", "screenshot-4-the-league-is-an-elimination.png"),
    ("07-collapse.png", "screenshot-5-finish-last-and-you-are-gone.png"),
]


def main():
    os.makedirs(OUT, exist_ok=True)

    cover().save(os.path.join(OUT, "cover-630x500.png"))
    banner().save(os.path.join(OUT, "banner-1600x400.png"))
    background().save(os.path.join(OUT, "page-background-1920x1080.png"))

    for source, destination in SCREENSHOTS:
        path = os.path.join(SHOTS, source)
        if not os.path.exists(path):
            print(f"MISSING {source} -- run the Play Mode captures first")
            continue
        shutil.copyfile(path, os.path.join(OUT, destination))

    for name in sorted(os.listdir(OUT)):
        if name.endswith(".png"):
            print(name, Image.open(os.path.join(OUT, name)).size)


if __name__ == "__main__":
    main()
