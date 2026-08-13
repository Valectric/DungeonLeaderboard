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

## 2026-08-13 — D8. Health bars on adventurers, superseding SPEC.md §3

**Decided:** each adventurer carries a **continuous health bar**, colour-coded green / amber / red.
This directly supersedes SPEC.md's "**Never show a number for adventurer HP**", and goes further than
the spec's own fallback of "coarse three-state indicators".
**Why:** the author played it and could not read the party's state. The spec anticipated this exact
outcome — *"if this proves unreadable in playtesting, fall back to coarse three-state"* — and the
playtest verdict was stronger than that: deaths were arriving unseen. The author's words were that it
must not be a *"wtf, I did not see that"*.
That reasoning is sound and it is not a matter of taste. A party wipe is the single worst outcome in
the design, it is the player's fault by construction, and the player is the only one who can prevent
it. Information the player needs to avoid the losing state cannot be the information the game hides.
The spec's instinct was that ambiguity between "nearly dead" and "dead in one hit" creates tension;
in play it created unfair surprise instead, which is a different thing.
**Rules out:** the hidden-HP reading of §3. Wound sprites and limping still carry the state too — the
bar is added information, not a replacement, so the readable-from-behaviour work stays.
**Wrong if:** the raid starts to feel like a spreadsheet rather than a place, or players optimise
against the bar instead of watching the party. The colour bands exist so the bar can be read at a
glance without measuring it; going further, to numbers or percentages, would be the actual mistake.

## 2026-08-13 — D9. Per-role adventurer AI, and disarmable traps

**Decided:** each archetype runs its own behaviour rather than the party sharing one brain. The tank
leads and charges the nearest enemy it has line of sight to, otherwise walking to the next door and
routing around armed traps; the mage focuses whatever the tank picked; the ranged attacker takes the
closest enemy and, when nothing is attacking, walks to an armed trap and defuses it on a visible
timer; the healer flees anything within a cell and casts by a priority equation.
**Why:** four sprites sharing one decision read as one object. Distinct behaviour per role is also
what makes party composition the spec's "primary source of run-to-run variation" — a party AI cannot
express that, because every composition plays identically.
**The healer refuses to cast unless a full heal would land without overflowing**, so a limited mana
pool is never frittered on topping someone up, and it weights the tank above squishier allies at the
same health fraction because the tank's survival is what keeps the rest alive.
**Traps are now disarmable**, each with its own timer. That turns a trap from free damage into a
decision with a clock: spend it before the rogue reaches it, or lose it. A rogue crouched over a
plate is also several seconds the party is not advancing, which is itself worth energy.
**Rules out:** the single party-level movement brain, and traps as a permanent fixture.
**Wrong if:** roles scatter and stop reading as a party. The formation slot is the guard against
that — any role with nothing to do falls back to it, so travel still looks like a column.

## 2026-08-13 — D10. The shop spends *leftovers*, and Ready buys starting energy

**Decided:** the shop's purse is whatever the previous raid left **unspent**, unspent purse is lost
when the shop closes, and the **Ready** bonus is carried into the next raid as extra *starting*
energy rather than being paid into the purse of a shop that is shutting. Purchases are permanent for
the rest of the season.
**Why:** the alternative — shopping with the harvest — would have made the score and the currency the
same number, and the league would then rank shopping restraint instead of ranking how well a party
was milked. Keeping them separate means `EnergyHarvested` stays exactly what SPEC.md says it is.
Spending leftovers gives restraint *during* a raid a use without letting it score: a player who
hoards buys a permanent spawner with it, and a player who spends everything on stalling scores higher
this round and starts the next one with nothing.
The bonus had to leave the shop or it would be worth nothing at all: paying it into a purse that is
about to be discarded is a number that goes up and is then thrown away, which is worse than no button.
**Rules out:** shopping out of the harvest; a bonus that rolls into the purse; carrying the leftover
purse forward (which would make Ready indistinguishable from simply not buying anything).
**Wrong if:** "unspent energy is lost" reads as a punishment rather than a deadline. It is stated on
the shop screen for that reason. If players still hoard and feel robbed, the fix is to carry the
purse and cut the Ready bonus, not to soften both.

## 2026-08-13 — D11. Chests are a stall, and the reach that makes them one

**Decided:** a bought chest is a detour target the party leader walks to, opens over three seconds,
and then ignores. The reach at which the leader starts opening it is **0.8 cells** — deliberately
generous.
**Why:** the tank stops the instant its *cell* equals its objective, which on a diagonal approach can
leave it two thirds of a cell from the centre. At the first, tighter reach of 0.45 the leader parked
next to the chest without ever opening it; the chest therefore stayed the objective, and the party
stood there until the clock ran out. Every assertion about placement, price and drawing passed. The
raid was deadlocked and it looked, from the outside, like an extremely effective chest.
Measured after the fix: one chest costs an unopposed party **5.6s** of a 60s raid — 26.9s to cross
without, 32.5s with.
**Rules out:** cell-exact loot triggers, and any test of a chest that does not measure seconds.
**Wrong if:** the party visibly opens a chest it is not standing on. Three cells of margin would look
wrong; 0.8 does not, and the alternative failure mode is a game that stops.

## 2026-08-13 — D12. The energy curve reads the party's *worst* survivor

**Decided:** `energyRate` takes the health of the single most wounded living adventurer, not any
average of the party. `Party.WoundFraction` is the input; `Party.HealthFraction` (now pooled hit
points rather than a mean of fractions) survives only for the HUD and the review.
**Why:** found by an exploratory sweep, not by a failing feature test. Measured across every roster
and every player policy, **the rate never once exceeded 4.1/s in a game whose curve is built to reach
32/s**, and **a wipe out-earned every raid in which the party lived (106 against 91)** — the exact
inversion SPEC.md forbids, present in the shipped build.
The cause is structural. The tank carries 220 of the party's 500 hit points and, since damage began
landing only on whoever is in melee reach, it soaks nearly everything. So it arrives at death's door
with three untouched allies: the mean read 77%, pooled health read 63%, and the wound curve is a
*fifth power* — it barely stirs above 60%. The party went from "fine" to "members dying" without ever
passing through the badly-wounded band where the money is, and average health then *rose* as members
died, because the average is over the living.
Reading the worst survivor implements CLAUDE.md's own sentence — *"most of the money is in the last
sliver of a health bar"*, one bar, not the mean of four — and it punishes killing exactly as intended:
let the nearly-dead tank die and the reading leaps to whoever is next worst, usually somebody healthy,
and the rate collapses.
After the change: peak rate 4.1 → **37.2/s**, ambushed harvest 105 → **209**, and survival (193) beats
the best wipe (179) again.
**Rules out:** mean-of-fractions and pooled-HP as curve inputs. Both are unreachable while a tank
soaks.
**Wrong if:** parking one member at 5% and farming becomes the whole game. It should be hard — the
healer prioritises the worst-off, so holding one bar on the edge means out-damaging the healer on a
single target — but if it proves trivial, the fix is a floor on the multiplier's sensitivity, not a
return to averaging.

### Open balance question for the author — not fixed unilaterally

The same sweep found **THE SKIRMISHERS and THE GLASS CANNONS cap at 4.1/s and harvest ~30**, against
120–209 for the rest. They kill mobs so fast that income collapses to the idle 0.05/s, and the player
can then no longer afford the 25 it costs to spawn anything — an income death spiral that ends the
raid at 100% party health. Two honest readings: glass cannons are *supposed* to be poor customers, or
a roster the player cannot profit from at all is a dead minute. The measurement came from a bot that
only spams spawns and never uses traps or doors, so it is a floor rather than a verdict. Left for the
author.

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
