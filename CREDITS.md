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

`dungeon/door-top.png` is **cut from `door-a.png`** — its top seventeen rows of stone arch plus a
five-row alpha ramp — and is therefore the same CC0 art, carrying the same absence of conditions. It
is drawn over the party so adventurers pass behind the doorframe instead of over it. The author
asked for that effect with the two-part doors in a Pipoya set; **that pack is not used, and none of
it is in this repository**, because its licence forbids redistribution and this repo is public.
Cutting the band out of the door we already ship gets the same effect, matches both door states pixel
for pixel because the pixels underneath are the same pixels, and keeps the guarantee below true.

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
