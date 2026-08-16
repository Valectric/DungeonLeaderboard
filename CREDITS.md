# Credits

Dungeon Leaderboard is released under the MIT licence. The art it ships is not all ours, and this
file records where every piece came from and under what terms. Nothing enters `Assets/Art/` without
an entry here.

## Dungeon tiles, doors and dungeon furniture

**Dungeon Crawl Stone Soup** — <https://github.com/crawl/tiles>

Public domain (**CC0**). The repository's own words: *"Many of those artists making these changes
have signed off their copyright, returning these tiles to a license similar to 'public domain', CC
Zero."* No attribution is required; it is given here because the work deserves it and because a game
that takes art without saying so is a game that cannot be audited.

Used: the `catacombs` wall family, the `cobble_blood` floor family, and the `dngn/doors` set — the
only free source found with a genuine open **and** closed door drawn as a pair. Tiles are darkened
and point-scaled from 32×32 to the 64×64 this project imports at, and the sixteen wall shapes are
generated from them; `Tools/import-tileset.py` is the whole pipeline and can reproduce it.

The sixteen shapes do differ from one another, and measurably: an edge facing open floor sits at
luminance 51–59 where the same edge continuing into wall sits at 30–36, with the south edge carrying
shadow the same way. `Tools/validate-tileset.py` checks exactly that, per side, conditioned on the
mask bit.

The Crawl project maintains a `TILES_UNDER_UNKNOWN_LICENSE.md` listing tiles whose provenance is
unclear. Nothing on that list is used here.

## Pipoya RPG Tileset 32x32 — requested, NOT yet in the repo, and it cannot be committed

**Pipoya RPG Tileset 32x32** — <https://pipoya.itch.io/pipoya-rpg-tileset-32x32>

Free (name-your-own-price). The licence, in the author's own words:

> "For commercial or personal use." · "Use and edit freely." · **"Not redistribute or resell this
> assets."** · "It can be used for game development and other productions."

Attribution is not demanded; it is given here anyway, for the same reason as everything else in this
file. Pipoya tag the pack **"No generative AI was used"** — that is a statement about how *they* made
it, not a restriction on what it may sit beside, which is the opposite of the Mana Seed problem
recorded in `HANDOVER.md`. So the licence is compatible with this project.

**But it cannot live in `Assets/Art/`.** This repository is **public**, `Assets/Art/` is tracked, and
publishing the raw PNGs on GitHub is redistribution however the game itself uses them. Shipping them
compiled inside the WebGL build on itch is "use in a production" and is fine; committing the source
tiles is not.

So the arrangement is:

- the pack is downloaded by hand into `Assets/Art/Pipoya/`, which is **gitignored**;
- the WebGL build embeds it, which the licence allows;
- a fresh clone therefore has no Pipoya tiles and falls back to the Crawl set above, and `Tools/`
  should say so if a build ever depends on them.

That last point is the cost of the arrangement and is worth knowing before it surprises somebody.

## Everything else

| What | Source | Terms |
|---|---|---|
| Adventurers, monsters, effects, props, key art | Generated for this project with the local Sprite Studio pipeline | Ours, MIT with the rest of the repo |
| Interface | Immediate-mode Unity IMGUI, no third-party assets | — |
| Code | Written for this project | MIT |

## If you are reusing this repository

The MIT licence covers **our** code and our generated art. The Crawl tiles above are CC0 and carry no
conditions at all, so the repository as a whole is safe to fork, modify and ship commercially. No
asset in this project is under a share-alike, non-commercial, or no-redistribution licence — that was
a hard constraint of the search that chose them, and packs failing it were rejected however good they
looked.
