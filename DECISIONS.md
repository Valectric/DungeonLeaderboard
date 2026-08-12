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
