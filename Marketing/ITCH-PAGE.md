# The itch.io page — what to upload and what to type

Everything in `Marketing/` was built by `python Tools/make-itch-art.py` from the game's own
assets: the cover and banner are crops of the key art the game shows on its loading and review
screens, and the screenshots are real frames captured by the Play Mode tests. Re-run the script
after any visual change and the page art follows the game rather than drifting from it.

---

## 1. Images — which file goes in which slot

| itch field | File | Notes |
|---|---|---|
| **Cover image** (required) | `cover-630x500.png` | itch draws it at **315×250**, so it was laid out to survive being halved. This is the one that sells the game in browse and search. |
| **Banner** (Edit theme → *Banner*) | `banner-1600x400.png` | itch crops banners to the page width, so nothing important is within ~60px of either end. |
| **Background** (Edit theme → *Background image*) | `page-background-1920x1080.png` | Deliberately dark — it is furniture, not art. Set **Background repeat: no-repeat**, **position: fixed/center**, or the page text stops being readable. |
| **Screenshots** | `screenshot-1..5-*.png` | Upload in the numbered order; the filenames say what each one is for. |

If you only have time for three screenshots, use 1, 2 and 3 — a party charging in, the rate
climbing while they are held, and the shop. Those are the loop.

---

## 2. Colours — paste these into Edit theme

Straight from the moodboard palette the whole game is drawn in, so the page and the build match.

| Theme field | Hex | What it is |
|---|---|---|
| Background | `#15101D` | The game's own camera background, so the embed dissolves into the page |
| Background 2 (panels) | `#251B31` | Violet-black |
| Text | `#E8E2F2` | Pale, not pure white — white glares against this background |
| Text 2 / secondary | `#C9BED8` | |
| Link | `#D75268` | The arcane magenta. It is the game's accent everywhere |
| Link hover | `#F07A8C` | |
| Border | `#50275E` | Royal purple |
| Button background | `#50275E` | |
| Button text | `#E8E2F2` | |

Also worth setting: **Layout → Frame**, and turn the **embed background** to `#15101D` so there is
no white flash while the WebGL build loads.

---

## 3. Embed settings

- **Kind of project:** HTML
- **Embed:** *Click to launch in fullscreen* is the wrong choice here — use **Embed in page**
- **Viewport:** `960 × 600`, **Fullscreen button: yes**, **Mobile friendly: yes** (the game reads
  taps and pinch, and the HUD scales down to the embed size)
- **Automatically start on page load:** off. It is a 17 MB build and jam voters scroll fast.

---

## 4. Text to fill in

**Title:** `Dungeon League`

**Short description / tagline** (the line under the title, and what shows in search):

> Sixty seconds. A party charging in. Killing them is losing.

**Genre:** Strategy · **Made with:** Unity · **Platforms:** HTML5

**Tags:** `management`, `pixel-art`, `dungeon`, `singleplayer`, `arcade`, `short`, `gamejam`,
`monsters`, `2d`, `webgl`

**Description** (paste as-is; it is written for someone deciding in ten seconds whether to click):

> **You are the dungeon.**
>
> Adventurers charge in. Your core charges up. You have sixty seconds, three verbs — open a door,
> spawn a monster, fire a trap — and one rule that runs against every instinct you have:
>
> **killing them is losing.**
>
> A dead party stops paying. So does one that walks out early. Everything you earn comes from a
> party that is *alive, in combat, badly wounded and still inside* when the clock stops — and the
> money is in the last sliver of a health bar, so the best raid of your life is the one where the
> healer is out of mana and nobody quite dies.
>
> You cannot call your monsters off. Your only mercy is a door: open one behind a losing party and
> let them retreat, heal, and come back for more.
>
> Between raids you spend what they left behind — another hall, a slime pit, a chest to make them
> greedy — and then the standings update, and the bottom two dungeons in the league are destroyed.
>
> Twenty dungeons. Ten rounds. One survivor.
>
> **Controls** — click or tap a door, a spawner or a trap. Scroll or pinch to zoom, right-drag or
> two fingers to move. That is the whole game.

**Community:** comments on. **Ratings:** on. If it is jam-entered, link the jam in the devlog.

---

## 5. Two things worth knowing before you publish

- **Bump the build first if you have changed anything.** The version is stamped from the clock at
  build time and browsers cache the old one hard.
- **The first raid is now a coached one.** New players get a headline over the opening room and a
  tag on every tappable thing, and it disappears from the second raid on. That is deliberate — the
  page copy above leans on "killing them is losing" being surprising, and the tutorial line is what
  stops it being merely confusing.
