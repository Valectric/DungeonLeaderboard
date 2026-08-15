# The visual rework — plan and execution

Authorised 2026-08-15: *"go with the top pick of 32x32 px… make a plan and then execute the plan for
reworking the complete visuals of the game… free to use Sprite Maker to update animations of avatars
and enemies… for UI utilise what's in the GitHub repository… add a mentioning about this GitHub
repository in credits."*

## The correction that shapes the plan

The search's nominated 32×32 pick, Stealthix's *Dungeon Tileset 32x32 px*, does not survive being
downloaded. It is **256×256 — sixty-four cells at 32px, 23 colours**: two framed wall panels, a warm
brown room border, stairs, a cave mouth, and a handful of props. No sixteen-piece wall system, no
open/closed door pair, and warm brown stone, which is the one thing the moodboard vetoes. The itch
page's contents list was wrong; the search had flagged that entry as page-text-only and it was right
to.

The verified 32×32 option with a real wall system and both door states is **Dungeon Crawl Stone Soup**
(`github.com/crawl/tiles`, CC0, no attribution required). It was rejected once — but what was
rejected was **my violet recolour of it**, which read as tinted stone rather than as art.

**The moodboard's stone is cold. The magenta is the light.** Violet-black `#251B31` is the
*backdrop*; `#D75268` is arcane glow and `#85432A` is candlelight. Painting the masonry purple was a
misreading. DCSS's catacomb stone is already dark blue-grey — the `#504D63` in the palette — so it
goes in **as drawn**, gently darkened, and the colour comes from what is lit.

## Order of work

| # | Step | State |
|---|---|---|
| 1 | Tileset in its own cold stone: floors, walls, the sixteen wall shapes, door pair | in progress |
| 2 | Entrance archway from `dngn/gateways`, so the join stops being visible | |
| 3 | Props and furniture — chest, spawners, torches — from `dngn` and `item` | |
| 4 | UI frames and icons from the repository's `gui/` folder | |
| 5 | Party and monster animations regenerated against the new stone (Sprite Maker) | |
| 6 | `CREDITS.md` and an in-game credit naming the repository | |
| 7 | Photograph every screen, publish to itch | |

## Rules this rework runs under

- **Look at every step.** The metric that said the first attempt was correct (wall/floor ratio 1.74)
  said nothing about it looking like a sweet shop. Every step ends in a screenshot.
- **Stone stays cold; colour comes from light.** Any pass that tints the masonry is wrong.
- **Nothing enters `Assets/Art/` without its licence recorded** in `CREDITS.md` at the same time.
- **One command rebuilds it.** `Tools/import-tileset.py` is the whole pipeline; if a choice cannot be
  expressed there it is a choice we cannot repeat.
