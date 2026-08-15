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

A caveat that belongs here rather than only in the design notes: those sixteen shapes **do not
currently differ from one another**. Measured, they separate by 1.47× against their own texture
grain, so `wall-0` and `wall-15` render the same and the autotiling is decorative. That is a fault in
how they were derived, not in the Crawl art, which is sound — nothing about the licence or the
attribution above changes. `Tools/validate-tileset.py` now fails a set that does this.

The Crawl project maintains a `TILES_UNDER_UNKNOWN_LICENSE.md` listing tiles whose provenance is
unclear. Nothing on that list is used here.

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
