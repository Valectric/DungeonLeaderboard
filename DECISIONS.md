# Decision log

Dated, reasoned decisions. **Read this before reversing anything** — entries exist because the
obvious alternative was considered or tried and rejected. Add new entries at the bottom; do not
rewrite history, supersede it.

Format: what was decided, why, what it rules out, and how to tell if it was wrong.

`SPEC.md` outranks this file for questions of *what the game is*. This file records how it gets
built, and any place where the build has deliberately departed from the spec.

---

## 2026-08-12 — D1. Unity 6000.3.17f1, 2D URP

**Decided:** the project Unity Hub already created — Unity 6000.3.17f1 from the **2D (URP)**
template, keeping `Renderer2D` and the 2D package set.
**Why:** it matches the sister project's editor version exactly, so the toolchain, the MooseRunner
build and every trap already written down apply unchanged. URP 2D also brings 2D lights, which a
dungeon wants more than most genres — a torch-lit room and a dark corridor are most of the atmosphere
for nearly no authoring cost.
**Rules out:** built-in render pipeline. Switching later is a whole-project change, so this is
effectively permanent.
**Wrong if:** WebGL build size or frame rate turns out to be dominated by URP overhead on a 2D scene.

## 2026-08-12 — D2. MooseRunner from the first commit

**Decided:** MooseRunner 2.2.5 and UniTask are in the manifest before any game code is written.
**Why:** the alternative is an agent writing code it cannot compile, run, or look at. In the sister
project three whole classes of bug were invisible to every assertion and were caught only by a
rendered frame, and one broken *rate* passed every unit test while ruining the game — none of that is
findable without the runner. A jam makes this more valuable, not less: the time it saves is time
otherwise spent on round trips through a human pressing Play.
**Cost:** ~30 minutes of setup, and the CLI binaries are gitignored so a fresh clone must resolve them
from the registry.
**Wrong if:** the licence or the daemon proves unreliable enough to cost more than it saves.

## 2026-08-12 — D3. itch.io only; no GitHub Pages

**Decided:** the WebGL build is published to **itch.io** via butler. No `gh-pages` branch, no Pages
site.
**Why:** the audience is jam voters, and they are on itch. The sister project maintains both and the
second one is pure overhead here — an orphan branch to force-replace and a Pages build to wait on,
for a URL nobody will visit.
**Rules out:** the `Tools/publish-pages.sh` machinery. `Tools/publish-itch.sh` does the one job.
**Wrong if:** the jam wants a playable link that is not on itch.

## 2026-08-12 — D4. Fixed rooms on a grid, mouse input

**Decided:** the dungeon is rooms placed on a **grid**; the three verbs are driven by **mouse clicks
on dungeon elements**.
**Why:** both are the spec's own recommendation (§10) and both are the faster build. A grid makes
pathfinding a BFS over cells rather than a navmesh, and — more importantly — makes "which room is
this mob in" a lookup rather than a geometry question. Mob pursuit must stop at a room threshold for
the retreat valve to exist at all, so cheap room membership is load-bearing.
**Rules out:** freeform placement, navmesh pathing, keyboard verb bindings as the primary scheme.
**Wrong if:** the grid makes the dungeon read as a puzzle board rather than a place.

## 2026-08-12 — D5. The spec is recorded verbatim and outranks the code

**Decided:** `SPEC.md` holds the author's design word for word, and is the authority on what the game
is. Departures from it are recorded here as dated entries rather than made silently.
**Why:** the spec is unusually opinionated in exactly the places that matter — three verbs and no
more, no direct mob control, no HP numbers, killing is bad play. Every one of those is a rule an
implementer would plausibly "improve" while building something adjacent, and each would quietly
remove the reason the game is interesting. Writing it down where it can be diffed makes drift
visible.
**Wrong if:** it ossifies — a spec that cannot be argued with is worse than none. Supersede it here,
deliberately.

## 2026-08-13 — D6. 64x64 pixel tiles, PPU 64

**Decided:** the art grid is **64x64 pixels per tile**, imported at **64 pixels per unit**, so one
tile is exactly one world unit.
**Why:** the author asked for 64px tiles, and the moodboard turns out to agree — its sprites measure
58-73px natively, so 64 is the grid the art was already drawn on. Making PPU equal the tile size is
what lets the dungeon grid (D4) sit on integer world coordinates, which keeps "which room is this
mob in" an integer lookup rather than a float comparison. That lookup is load-bearing for the
retreat valve.
**Follows from this:** a 1280x720 canvas shows 20 x 11.25 tiles, so the camera's orthographic size
is `720 / 2 / 64` = **5.625**. `PixelArtImportPostprocessor` enforces the import side.
**Rules out:** 32px tiles, which the sprite generator defaults to and which would have halved the
moodboard art's detail for nothing.
**Wrong if:** 64px sprites make the readable dungeon too small on a 720p WebGL canvas — the fix is
the camera, not the art.

## 2026-08-13 — D7. Moodboard extraction is the primary art path

**Decided:** game art is **cut from `Assets/Art/referance/MoodBoard.png`** by
`Tools/extract-moodboard.py` (38 sprites: party 4 roles x 3 wound states, 7 mobs, 3 doors,
3 spawners, chest, 4 trap impacts, 8 props). **Sprite Studio is reserved for what the moodboard
lacks** — chiefly the terrain tileset atlas, later animation frames via the deterministic rig.
**Why:** extraction is deterministic, free, and on-style *by construction* — the moodboard is the
style authority, so a cut from it cannot drift. It also already contains the three wound states the
spec demands, drawn by the author. Generation is a non-deterministic agent run; using it where a
deterministic cut works trades certainty for nothing.
**Extracted sprites are also the best generation reference available** — better than the moodboard
crops, because they are clean cutouts with real alpha, no UI chrome or typography, and are already
at the 64px target so they anchor *scale* too. `Assets/Art/referance/style-stone.png` is that
anchor. This follows the tool's own guidance that approved output beats a moodboard.
**Rules out:** hand-drawing, fetching art from the web, and generating the character set.
**Wrong if:** the moodboard runs out of coverage — then generate, but promote each approved result
to a reference before generating the next thing.

### Why the one prior generation run failed — do not rediscover this

The 00:55 run on 2026-08-12 produced six warm-tan 32x32 props. Three independent causes, all
driving errors rather than tool faults; the ImageGen master itself was clean, competent pixel art:

1. **Wrong harness.** It used `--command pack`, so a tileset request was built by the pack harness
   and landed as six separate files in `assets/props/`. The router sends a tileset to the terrain
   harness, whose contract is *one atlas PNG, never one file per tile*.
2. **No palette steering.** The palette string and the cropped references were created at 01:06 —
   *after* that run. They had never been applied to anything.
3. **The downscale destroyed pixel discipline, not ImageGen.** The master renders each tile at
   ~330px of clean art; normalising 330 -> 32 with anything but exact integer nearest-neighbour
   yields mush. Measured: **610-746 unique colours in a 32x32 image**, i.e. ~65% of pixels unique,
   against the tool's own gate of "3-6 main colours plus outline and highlight".

The practical consequence is that **normalisation is ours to own**: masters are kept under
`.sprite-studio/imagegen-sources/`, so take the master and downscale it deliberately rather than
accepting whatever the harness emitted. Verify with a unique-colour count, not by eye alone.
