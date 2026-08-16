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

### Second open balance question — a fully-bought dungeon outlasts the clock

Measured by `LayoutSweepTests`: an unopposed party crosses a plain corridor in 24.8s at two rooms,
38.2s at three, 56.5s at four — and a **fully-bought five-room dungeon runs out the sixty seconds
without reaching the boss room**. Chests and halls stack until the party simply cannot finish.

Since purchases are permanent, once a player buys their way there the *party escapes early* ending is
gone for the rest of the season. Two honest readings, and it is the author's call:

- **Coherent progression.** The player spent real energy to guarantee the full minute, which is what
  SPEC.md says they want — "alive, engaged, badly wounded and still inside when the timer expires".
  The tension simply moves from *will they escape* to *can I hold one bar at 5% without tipping it*.
- **Too strong.** A permanent purchase that deletes one of the three endings removes a decision
  rather than rewarding one, and `GameController.MaxRooms` exists specifically to stop this — its
  comment says a corridor that cannot be crossed "stops being a purchase and starts being a
  guarantee". By that reasoning the cap belongs at four, not five.

Left as measured. `AnUnboughtDungeon_CanAlwaysBeCrossedInTime` pins the baseline every new player
meets; `HowLongAFullyBoughtDungeonTakes_IsRecorded` reports the trend without picking a side.

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

## 2026-08-14 — D13. Rival dungeons earn on the scale the player can actually reach

**Decided:** a rival dungeon earns `90 + rand*220` a round, averaging about 200, replacing
`380 + rand*900` which averaged 830.
**Why:** found by a season sweep, not by a raid test. Playing twelve-raid seasons across ten seeds,
**every single season finished in exactly 18th place** — with a competent bot averaging 292 harvest a
raid and buying thirty-five items. The player shed roughly 540 points of ground every round no matter
what they did. The standings were a backdrop, and SPEC.md's entire ten-second hook — *"I am 14th, 16th
is death, I need to climb"* — was **unwinnable by construction**.
The old figure also contradicted its own comment, which claimed rivals "earn on the same scale the
player does". They never did; nothing measured what the player could actually harvest.
Adjacent places in the opening table sit 500–800 points apart, so the margin has to be wide enough
that a season of good raids is felt. After: harvesting 20 a raid finishes 17th, 150 finishes 14th,
300 finishes 13th, 450 finishes 11th, and 700 finishes **7th**.
**Rules out:** rival earnings picked without reference to measured player harvest. If the raid economy
is retuned, these two constants must be re-measured against it — they are downstream of it.
**Wrong if:** climbing feels too easy, or relegation stops being a threat. Both are one constant away;
the assertion to keep is `WhereYouFinish_DependsOnHowYouPlay`, which pins the ordering rather than the
numbers.

## 2026-08-14 — D14. Sprites animate procedurally, not from drawn frames

**Decided:** walking, attacking, casting and shooting are procedural motion computed in
`SpriteMotion` — bob, squash-and-stretch, per-role attack shapes, and facing — rather than drawn
animation frames.
**Why:** combat was invisible. Two health bars changed length, a number popped, and every sprite
stood perfectly still throughout; the only moving parts were the projectile and the particles.
Drawn cycles would need sprite-maker `--command character` runs for four roles times three wound
states times three actions, each one non-deterministic and needing hand review against the
moodboard. That is a large, uncertain job against a jam clock, and it would leave the wound states
— which SPEC.md requires be read from movement, since HP may never be shown — depending on
whichever cycles got rigged in time.
Procedural motion applies to every sprite at once, is a pure function of state (so a seeded run
photographs identically, which SPEC.md requires for bug reports), and costs one multiply per sprite.
The shapes are deliberately different per role: a tank lunges bodily, an archer recoils *backwards*
from its own shot, a mage rises and leans back. Same-shaped motion would have made the roles read as
one thing.
**Rules out:** treating this as a placeholder to be swapped for frames later. Frames can be added on
top, but the wound-state legibility must not become dependent on them.
**Wrong if:** it reads as floaty at the shipped zoom. The lever is the constants in `SpriteMotion`;
`AnimationOnScreenTests` pins that the motion reaches the real sprites in the shipped scene, which is
the part a unit test cannot see.

## 2026-08-14 — D15. The audio module brings its own AudioListener

**Decided:** `AudioFacade.Awake` adds an `AudioListener` when the scene has none.
**Why:** the shipped game was **completely silent**. Every clip synthesised correctly, every cue
dispatched correctly, and nothing was audible, because the play scene is generated from code and a
camera created with `AddComponent<Camera>()` does not bring a listener the way the editor's default
Main Camera does. Unity does not treat this as an error — it logs one warning and plays nothing.
Six green audio tests covered synthesis and dispatch and none of them could see it. It surfaced only
from the mandatory `console --types error,warning` sweep after a passing run, which is exactly the
case that habit exists for.
Putting it in the module rather than the scene builder keeps the module self-bootstrapping, so audio
works in any scene that touches the facade, including the stripped scenes tests build.
**Rules out:** relying on the scene to provide the listener. The scene is regenerated from code and
would discard it.
**Wrong if:** a scene ever wants a listener somewhere specific (positional audio on the camera). The
facade yields to an existing one, and `TheFacade_DoesNotAddASecondEar` pins that.

## 2026-08-14 — D16. The shop is spatial: buy onto the dungeon, not off a list

**Decided:** the six-card grid is gone. During the shop the player looks at the dungeon the next
party will walk into and buys onto it — a marker past the last hall extends the corridor, and
tapping any empty tile opens a small menu of the five things that can stand there. Purchases carry
a cell, and the dungeon is furnished exactly where they were placed.
**Why:** the author asked for it, and the reason it is right is that the old shop sold *what* and
never *where*. The dungeon scattered purchases across the rooms past the first by a formula, so a
player bought a bone pile and found out afterwards where it had landed. Placement is the more
interesting half of the decision and the half that makes a layout theirs — and it is the half that
interacts with the one idea the game is built on, since where a spawner sits decides how long the
party is held and therefore how much the raid earns.
Mechanically: `Loadout` carries `Placement`s; `DungeonLayout.BuildCorridor` takes a `Furnishings`
that replaces the four count parameters; `GameController` translates between them, because it is the
only layer that may know both modules. The shop tints the dungeon at 34% instead of 86% — the thing
being shopped for has to be visible — and rebuilds a live preview raid after every purchase, so a
new hall or spawner appears where it was put rather than on the next loading screen.
**Rules out:** selling counts. `BuildCorridor`'s `extraSlimeSpawners` family still exists for tests
that want a formula-built dungeon, but the game never uses it, and a purchase without a cell now
builds nothing.
**Wrong if:** thirty seconds is not enough to place things thoughtfully. The lever is `ShopSeconds`,
not a return to the list.

Two things the rework surfaced that were not visible from the model:
- **A menu opened on a low tile put its last row over the Ready button**, which is hit-tested first,
  so buying the bottom item started the raid instead — losing the purchase, the rest of the clock,
  and any chance of understanding why. `NothingIsDrawnOverReady` now sweeps every anchor at every
  canvas size.
- **The itch.io embed is 523x293**, a UI scale of 0.4, which gave 12-pixel menu rows: on screen,
  drawn correctly, and untappable. Popup and marker metrics are floored in absolute pixels now.

## 2026-08-14 — D17. Why the tankless rosters earn a ninth of the others (diagnosed, not decided)

**Found:** peak energy rate by roster, played with the spawn verb pressed every tick —
THE UNSHRIVEN 37.8/s, THE IRONCLADS 30.0, THE PILGRIMAGE 29.4, THE BALANCED PARTY 25.8,
**THE GLASS CANNONS 4.1, THE SKIRMISHERS 4.1**. A **9.3x spread**, decided by a roll the player does
not make, on a third of all raids.

**It is not a constant, it is a feedback loop.** Measured for the two tankless rosters:

| | tankless | the rest |
|---|---|---|
| deepest wound (worst survivor) | 0.76–0.79 | 0.02–0.07 |
| ticks in combat | 10% | 44–74% |
| monsters the player could afford | **5** | 9–11 |
| deaths | none | 1–4 |

A fragile party kills a monster before it lands enough blows to wound anyone, so its worst survivor
sits near three-quarters health and the wound multiplier stays near 1. The rate stays near idle.
Spawning is gated **only** by energy (`Raid.SpawnMob` refuses below `SpawnCost` and has no other
guard), so a poor raid can afford five monsters in sixty seconds — and with no monster in the room
the party is scored as walking a corridor. Less income buys fewer monsters, which earns less income.
The player's own verb becomes unaffordable exactly when they most need it.

**Not fixed here, because every way out is a balance decision the author owns:**

1. **Cheaper spawns**, or a spawn cost that scales with the rate. Directly breaks the loop; risks
   making the strong rosters trivial.
2. **A floor on income while any monster is alive**, so one purchase always buys engagement.
3. **Sturdier monsters against fragile parties** — a skeleton that survives long enough to land
   blows. Closest to the fiction, most work.
4. **Accept it.** A party that cannot be hurt *is* a poor customer, and the wound curve is the game.
   `TheLeastWoundedRoster_IsTheWorstPaying` asserts exactly that correlation, so this outcome is the
   design working, not misbehaving. The objection is not that it is wrong but that it is *invisible*:
   the player is not told why this raid is worth a ninth of the last one.

**Recommendation:** 4 plus a readability pass, or 2 if playtesting says a dead raid is dead time.
Option 2 is the smallest change that keeps the player's verbs live.

**Pinned by:** `RateReachabilityTests` — the steep end stays reachable, no roster falls below an
empty corridor, and the spread may not exceed 14x. It pins the measured 9.3x rather than a target,
so it catches the gap widening without pretending 9.3x was chosen.

## 2026-08-14 — D18. The retreat valve never fires before a death (fix attempted, measured, reverted)

**Found:** across every roster, the party **never retreats before losing a member**. Traced at the
moment of each first death:

| roster | pooled health | worst survivor | retreated first? |
|---|---|---|---|
| THE BALANCED PARTY | 0.48 | 0.07 | no |
| THE IRONCLADS | 0.55 | 0.04 | no |
| THE PILGRIMAGE | 0.41 | 0.07 | no |
| THE UNSHRIVEN | 0.54 | 0.02 | no |

`Party.ChooseGoal` reads `HealthFraction` — the **pool** — against `RetreatThreshold` 0.28. Three
healthy members mask one at 0.02, so the pool never approaches the threshold and the party fights on
until somebody dies. CLAUDE.md calls this valve load-bearing: *"open a door behind a losing party and
let them retreat and heal… the central regret."* It cannot fire until after the death it exists to
prevent.

D12 made exactly this correction for the energy curve, which now reads `WoundFraction` (the worst
survivor). The AI was left on the pool.

**The obvious fix was tried and is much worse than the bug.** Switching `ChooseGoal` to
`WoundFraction`, plus a clause letting a healer-less party stop retreating once safe:

- the valve fired correctly — every roster that took damage retreated before any death;
- and **the economy collapsed**. Peak rate fell from 25.8–37.8/s to **4.1–9.4/s** across the board,
  engagement from 44–74% down to 10–12%, because one member is below 28% almost all the time, so the
  party spends the raid running instead of fighting. Two rosters that previously kept a survivor
  wiped instead.

Caught by `RateReachabilityTests.TheSteepEndOfTheCurve_IsReachable`, added hours earlier for this
exact class of failure. Reverted.

**Why it is not fixed:** the two thresholds were tuned against pooled semantics, and swapping the
measure without retuning them is what produced the collapse. Retuning them is a balance decision the
author owns, and one guess already made the game strictly worse by every measure.

**The lever, if you want it:** keep `WoundFraction` in `ChooseGoal` but drop `RetreatThreshold` far
lower — the pool sat at 0.41–0.55 while the dying member was at 0.02–0.07, so something near **0.12**
would fire only when a death is genuinely imminent rather than continuously. `RecoverThreshold` needs
to come down with it or the party still never turns around. Re-measure with
`RateReachabilityTests` and the season sweep; both will tell you immediately.

**Related:** D17, which is the same shape — a real problem whose every fix is a balance judgement.

## 2026-08-14 — D19. Two rosters cannot get through a shut door at all

**Found:** timed every roster against one shut door over a full sixty seconds.

| roster | archer | result |
|---|---|---|
| THE BALANCED PARTY | yes | lock picked at 9.0s |
| THE GLASS CANNONS | yes | picked at 7.8s |
| THE UNSHRIVEN | yes | picked at 9.0s |
| THE SKIRMISHERS | yes | picked at 7.8s |
| **THE IRONCLADS** | no | **never** — 64% of the door's health after 55.7s |
| **THE PILGRIMAGE** | no | **never** — 43% after 55.7s |

Those two spend the entire raid hitting a door while unengaged, which pays the 0.05/s idle floor, so
the raid harvests **3 energy**. Shutting one door in front of them destroys the raid outright.

**Every part of this is working as specified**, which is what makes it interesting. The door's 520
health is the author's own figure — "twice as much health as a skeleton" — and a battering party is
not in combat, so SPEC.md's rule that "an unengaged party walking a corridor must earn almost
nothing" pays them almost nothing, correctly. The trap is emergent: **a door alone is not a stall, it
is a wall**, and the player cannot see which rosters can open one.

**Not changed, because every lever is a balance decision:** door health, those rosters' damage, or
letting a party give up and turn around after a while. The last is the most interesting — a party
that abandons a door it cannot break would turn a soft lock back into a cost.

**Also fixed here:** the test covering this was named `EveryComposition_EventuallyGetsThrough` and
asserted only that each roster made 5% progress. Its name claimed a property that is false and that
it never checked. It is now `OnlyRostersWithAnArcher_GetThroughAShutDoor`, asserts what is actually
true, and fails if a stuck roster ever stops making progress — which would mean the party is standing
still rather than paying a cost.

## 2026-08-14 — D20. The league is an elimination, and the game can now be won

**Decided** (author's design): every dungeon starts on **zero**. At the end of each round the bottom
dungeon leaves the competition and is not replaced, so twenty become nineteen, then eighteen, down to
one — and the last dungeon standing is the winner. That is the goal of the game.

Rival earnings are priced against what the player can actually do: each rolls uniformly between a bad
run (25) and a good run (500), then **loses a tenth**. Their best possible round is therefore **450
against a good run's 500**, so *a genuinely good raid can never be beaten by a rival's luck*. The
league answers skill directly instead of statistically.

**Why the old table had to go:** it opened with scores descending from 16,000, relegated the bottom
two of a fixed twenty, and refilled the gaps with fresh names. It ran forever, it had no winner, and
the first raid could not move the player through a field already spread over 12,000 points. SPEC.md
gave the game a losing ending and never a winning one; `Phase.Won` is that ending.

**Measured:** playing a good raid every round takes the field from 20 to 1 over 19 rounds and wins.
Harvesting nothing is eliminated immediately.

**Both consequences were raised with the author and both are now settled.**

1. **Round one is sudden death — intended.** Everyone starts on zero, so a single weak opening raid
   puts the player last and out with no banked score to absorb it. Later rounds are far more
   forgiving. The author confirmed this is the design: the first raid is the sharpest lesson the game
   teaches, and softening it would take the teeth out of a competition that is meant to bite from the
   first round. **Do not "fix" this.** If a future change makes round one survivable by accident —
   seeding opening scores, exempting the first elimination — it is a regression, not a kindness.
2. **Run length — answered by the pacing.** Two dungeons leave each round rather than one, so twenty
   reach a winner in ten rounds rather than nineteen. See the elimination pacing in `LeagueTable`.

## 2026-08-14 — D21. A second shut door froze the party for the rest of the raid

**Found:** with every door shut, all six rosters forced the first door open at six or seven seconds
and then **stood on cell (5,3) in room zero for the remaining fifty-three**, goal `Advancing`, all
four alive, earning the idle floor.

`Party.BlockingDoor` only ever considered doors **on the current room's threshold**. Once the party
forced its own way out of room zero, the next door along joined rooms one and two — not room zero —
so it was skipped and the method returned null. `NextObjective` then fell through to a path toward
the boss cell, which was still unreachable behind that second shut door, so `FindPath` came back
empty, `MoveAlongPath` had nowhere to go, and the party stopped moving permanently.

**Two green tests were protecting it.** `ClosingDoor_StallsTheParty` asserted the party was still in
the first room after twenty seconds, and `Clock_ExpiresAfterSixtySeconds` expected the clock to run
out — both true, and both true *because of the freeze*. This is the third time in this session a test
has cemented a bug by asserting a symptom.

**Fixed:** when nothing shut is on the current threshold and the boss room is still unreachable, the
party heads for the nearest shut door it can actually walk to. Reachability is the point — pathing at
a door behind two more shut doors would strand them exactly as before.

**After:** twenty seconds behind shut doors leaves the party at x=11 against x=17 with them open. A
door is a cost now rather than a wall. The two tests assert that delay instead of the freeze, and the
clock test uses THE IRONCLADS, who genuinely cannot pick a lock and batter a door to only 66% in a
full minute (D19).

**Wrong if:** a player wants a door to hold a party indefinitely. It never did — it froze them, which
looked identical from the outside and paid the player almost nothing.

## 2026-08-14 — D22. The monster health nerf: measured, viable, and what still blocks it

**Status:** the author asked for monsters with two and a half times less health, damage untouched.
It is **not applied**, and the reason has changed twice.

**First blocker (gone).** Under the old energy curve it inverted the design's one rule — a wipe
earned 215 against 213 for a raid the party survived, and at a gentler 2x it was worse still, 322
against 170. The per-action curve removed that cause: measured at 48/104 the same test now reads
**14 against 231**, comfortably the right way round.

**Second blocker (current).** It invalidates seven tests. One is a stale figure and has been dealt
with: `ASkeleton_StillHoldsThePartyAboutThirteenSeconds` was named for a number, and a skeleton now
holds a party for **6.5 seconds** rather than 13. It is renamed and bounded on the property — worth
more than four seconds, less than twenty-two — so it holds either side of the change and will not
need rewriting when the nerf lands.

The other six are not bounds to widen, and at least three look like real questions about whether the
game still behaves as intended once a monster dies in a couple of seconds:

- `CombatReachTests.TheTank_TakesTheDamage` — a monster that dies quickly may never land enough
  blows for the tank to be clearly the one bleeding. If so, the tank stops being a tank in any
  observable sense.
- `CombatReachTests.TheHealer_HealsDuringALongFight` — there may no longer be a long fight to heal
  during.
- `RaidRulesTests.Mobs_StopBesideTheParty_NotOnTopOfIt` and `Healer_RunsFromAnythingThatGetsClose` —
  positioning, which should not depend on health at all. Worth understanding before touching.
- `RaidRulesTests.AFight_LastsLongEnoughToEarn` and `Trap_CostsEnergyWoundsThePartyAndThenCoolsDown`
  — probably fight-length bounds, but unconfirmed.

**Resolved the same day.** All seven cleared and the nerf is applied. None of the six needed its
claim weakened; five were measuring through a window sized on the old monster:

- The two **positioning** tests were the interesting ones, because where a mob comes to rest cannot
  depend on its health. Both sampled at a hardcoded time — one carried the comment *"eight seconds:
  long enough to close and settle, short enough that the skeleton is still alive. It dies around
  thirteen"* — so with a monster dying at 6.5s they were reading an empty sequence. They now sample
  while the mob lives.
- **TheTank_TakesTheDamage** and **TheHealer_HealsDuringALongFight** share a helper that spawned one
  monster and ticked for thirty seconds. That is a short fight followed by twenty-odd seconds of
  walking, during which a healer tops everyone up and the tank stops being the one bleeding. The
  helper keeps the pressure on now, which is what a player does and what "several weaker monsters"
  means.
- **Trap_CostsEnergy...** needed twenty-five seconds of held fight to bank a trap's price; same fix.
- **AFight_LastsLongEnoughToEarn** was a genuine bound sized on 260 health, lowered from eight
  seconds to four. The property is that one purchase buys a meaningful stretch, not that it buys the
  old figure.

**After:** 136 raid tests green with the nerf live. A skeleton holds a party **7.0s** and harvests
108. The central invariant reads **14 against 231**, and the roster spread is **3.7x**.

---

## 2026-08-14 — D23. Only the halls the player buys arrive empty

The author, having played M7: *"added rooms are filled with spawner and trap. Instead make them
empty and it should be possible to click in a placed room on a floor tile to get a menu to place
stuff."*

The first attempt read this as *every* room, and removed the auto-placement outright. That is what
the sentence says in isolation, and it is wrong: **34 tests went red and the shipped game opened on
a dungeon with nothing in it.** An entirely bare dungeon has no spawner to fire and no trap to
spring, so two of the three verbs are unavailable and the party walks an empty corridor at the idle
rate. Round one would be a game over screen with extra steps.

Re-reading, the complaint is specifically about **added** rooms — a hall bought in the shop. So
`Build` takes a `furnishedRooms` count: the opening corridor comes stocked, anything grown past it
arrives bare. Tests and `BuildCorridor` default to furnishing everything, so nothing that was
measuring a stocked dungeon quietly started measuring an empty one.

**The lesson is about the first fix, not the second.** Removing the placement block made 34 tests
red, and the instinct was to make the tests pass — a blind sed added `extraSkeletonSpawners: 2,
extraTraps: 2` to 47 call sites. That "fixed" the suite while the *game* stayed broken, and it
silently changed what four other tests were measuring (a shop test comparing a plain dungeon against
one with two bought spawners was handed two dungeons with two bought spawners each). All 47 were
reverted. A wave of failures after a design change is evidence about the change; it is not a list of
chores.

Click-to-place already existed and needed no work — the menu opens on any tile `CanBuildOn` accepts,
which a bare hall satisfies everywhere.

---

## 2026-08-14 — D24. Spawning is a loan against the room, not a purchase

The author: *"Spawning just temporarily consumes energy. Once the enemy is dead the value is
recovered."*

At a flat 25 the arithmetic argued against the design. A monster the party kills in four seconds had
to earn its price back before it was worth pressing, so the optimal play was to **hoard** — in a game
whose whole premise is a dungeon full of monsters the party is grinding through. The verb the design
wants pressed constantly was the one the economy punished.

As a loan the cost stops being a fee and becomes a **risk**: the stake leaves the core while the
monster lives and returns when it dies, so the player is only ever out of pocket for monsters still
standing when the clock stops. That is a bet on the party being slow, which is exactly the bet the
game wants them making.

Three things it deliberately does not do:

- **It refunds the purse, never the score.** `EnergyHarvested` is what the league ranks and spawning
  never docked it, so crediting refunds there would pay twice for one monster. Only spending power
  changes.
- **It refunds only monsters that were paid for.** A monster a test or the dungeon puts straight into
  the pack was never bought; refunding it would mint energy from nothing and every sweep that spawns
  freely would report a richer economy than the game has.
- **It draws a number, not a burst.** The refund lands on the same tick and the same spot as the
  monster's own death effect, so a second burst there is noise on the one the player is already
  reading — and an effect kind with no case of its own falls through to the **door** visual and the
  door chime, which would tell them something opened. It rises off the corpse as a `+25`, the exact
  mirror of the death penalty.

---

## 2026-08-14 — D25. The dungeons left standing get better, but never luckier

The author: *"The ones left should be better and better…"*

The dungeons knocked out each round are the ones that earned least, so a competition whose survivors
keep rolling from the same range the opening twenty rolled from gets **easier** as it goes. The
player faces a stronger average opponent while the numbers those opponents actually roll never move.

The fix is one-sided on purpose. **Only the floor rises.** The ceiling stays at ninety per cent of a
good raid in every round of the competition, so the promise D20 is built on survives intact: play a
genuinely good raid and no rival can have beaten it, in the final exactly as in round one. The player
is never eliminated from a round they played well.

What a shrinking field takes away is their **bad rounds**. Measured, the worst round a rival has
climbs from **33 to 440** between the opening round and the final — so late on a rival never has an
off day, and the player cannot coast in on one good raid and a rival's stumble.

`FinalistPressure` stops at 0.9 rather than 1. At 1 the last rival would score the same number every
round and the final would be an arithmetic check rather than a race.

**And the measurement this exposed.** The soak plays twelve competitions with a competent bot and the
bot wins all twelve, which is either a well-tuned league or no contest at all — the soak cannot tell,
because it only asserts that a competition *resolves*. Asking directly: the player needs **400 a
round** to win and **never wins below 375**. It answers skill. The soak's bot is simply good.

---

## 2026-08-14 — D26. A straggler pulled monsters out of their rooms

Found by the soak, not by any unit test: *"a Skeleton left room 1 for room 0"*.

Room-bounded pursuit is load-bearing rather than polish — the game's one safety valve is opening a
door behind a losing party so they can retreat and heal, and that only works because monsters stop at
the threshold. This broke it.

D-note in M6 changed monsters to chase the **nearest** party member rather than whoever leads, which
fixed the standoff that was costing two rosters a ninth of their income. The room check was left on
the party **leader**. So a member left behind across a threshold could be the nearest body, and the
mob would charge straight out of its room after them — the valve failing at the exact moment it
exists for.

Bounded in two places, because one is not enough:

- **Quarry selection** now only considers members standing in the mob's own room.
- **The landing cell** is checked as well. Inside 2.5 cells a mob abandons cell-by-cell pathing and
  charges directly at its target, which skips the `path[0]` room check that was doing the only
  checking. A doorway is allowed through: it belongs to no room, and a mob straddling its own
  threshold has not escaped.

`NoMonster_EverLeavesItsRoom` passed 5694 assertions beside this bug the whole time. It ticks a raid
where the party moves as a group, so it never produced the straggler that triggers it. The new test
constructs the case directly.

---

## 2026-08-14 — D27. The simulation stays plain C#. Prefabs and Unity systems go in the view.

The author, after playing M8: *"we need to rethink how the structure of the game is designed so that
we have enemies and the players as prefabs, and the shots fired as prefabs… Keep it more inside the
Unity systems. Right now it sounds like we have a lot of things handcrafted ourselves instead of
relying on the pathfinding which is offered inside Unity."*

Half of that is right and is now Phase 8 of M9. The pathfinding half is refused, and this records
why, because it will be asked again.

**Unity has no 2D navmesh.** `com.unity.ai.navigation` is not in the manifest; only the 3D, XZ-plane
`com.unity.modules.ai`. So the ask is not "switch on the thing Unity offers", it is "add
NavMeshPlus or the A* Pathfinding Project".

Four independent disqualifiers, any one sufficient:

1. **288 headless tests** tick `new Raid(layout).Tick(0.02f)` with no scene. A `NavMeshAgent` needs
   Play Mode and Unity's own update order, and cannot be stepped at a fixed dt.
2. **The dungeon is rebuilt per raid and after every shop purchase**, against a 250 ms budget. A
   runtime re-bake is a main-thread stall on single-threaded WebGL, and every door toggle — the
   cheapest and most spammable verb — would need obstacle carving.
3. **Agent avoidance is not reproducible from a seed**, and seeded reproduction is a CLAUDE.md hard
   constraint, pinned by `SoakTests.ASoakSeason_ReplaysFromItsSeed`.
4. **Zero physics symbols exist anywhere under `Assets/Dungeon`.** This is not a broken integration
   to repair, it is a first integration, and every line of it is new risk against the property that
   makes headless testing and seeded replay possible.

**And the argument that actually settles it:** a `NavMeshAgent` would path *correctly* through an
open door. That is precisely what the retreat valve forbids — mobs must not pursue past a threshold,
which CLAUDE.md calls load-bearing rather than polish. So the room bound would have to be
reimplemented on top of the navmesh anyway, leaving strictly more code enforcing the same rule
twice. The grid it would replace is about seventy lines over 133–217 cells.

**The line, for future readers:** prefabs and Unity systems for the **view** — sprites, particles,
child structure, shots as prefabs. The **simulation** stays plain C#, fixed-step, seeded and
scene-free. Amends D4 rather than reopening it.

Both wall bugs the author reported are, on inspection, *missing checks* in code that already holds
the grid one call away — `Party.Glide` is an unchecked `Vector2.MoveTowards`, and ranged targeting
has neither a range limit nor a sight test. Neither is evidence that the hand-rolled simulation is
wrong. Replacing it would have fixed them incidentally, at the cost of determinism, seeded replay,
and the entire test suite.

---

## 2026-08-14 — D28. A pinned CPU affinity fails the perf test and looks like a code regression

Operational, and it cost real time today, so it is written down rather than remembered.

CLAUDE.md prescribes limiting Unity's CPU affinity to ~4 cores before a WebGL build, because the
build otherwise exhausts Windows commit memory. It says to restore it afterwards. It did not say
what forgetting looks like.

Forgetting looks like a **code regression**. `PerformanceSweepTests.TheFrameLoop_KeepsItsBudget`
went from a mean frame time of **2.5 ms to 370–500 ms** against a 100 ms budget — red, reproducible,
and still red with the machine otherwise idle, so it survives the usual "it was contention" check
that this project has learned to apply to perf numbers.

The tell is the pair of numbers, not either one alone: the **simulation** cost was unchanged at
~310 µs/tick across the whole episode while the **frame** time exploded. The sim was not slower;
the editor simply had four cores instead of twenty-four to run it on. Restoring the full mask took
the mean to 42.3 ms and the suite to green with no code change at all.

Restore with a computed mask, not a literal — `0xF` is right for the build and `0xFF` is wrong for
the restore on a 24-core machine.

---

## 2026-08-14 — D29. Room-scoped combat inverted "aim your purchases", and the test's premise is what broke

Left **red on purpose**, because the fix is a balance decision the author owns and quietly retuning
it would be exactly the D23 mistake.

`PlacementSweepTests.AThoughtfulLayout_OutEarnsACarelessOne` is the justification for the whole
spatial shop: aiming purchases must beat scattering them, or the player is pointing at tiles for
nothing. It compares six items in the **first** room (thoughtful — the party must cross it) against
six in the **last** room (careless — "which a stalled party may never reach").

After M9 Phase 2 scoped combat per room, aimed earns **274.8** against careless's **297.3**. The gap
used to be 397 vs 242.

**Ruled out: the item mix.** The thoughtful layout bought traps and skeletons while the careless one
bought only skeletons, so the two differed in *what* was bought as well as *where* — in a test named
for where. Equalising the mix changed the numbers by **exactly nothing** (274.783478 vs 297.326233,
to the digit). The confound was real and worth removing; it was not the cause.

**So the effect is genuine, and on reflection the "careless" layout is good play.** Monsters in the
last room mean the party walks the early rooms earning almost nothing, meets them late, and is
**still fighting when the clock stops** — which is precisely SPEC's ideal outcome: alive, in combat,
badly wounded, still inside the dungeon. Monsters in the first room get killed in the opening
seconds and the party then walks the rest of the raid for free.

The premise the test was written on — *a stalled party may never reach the last room* — was true
when a fight could pull in the whole party from anywhere via the leader's room. With combat scoped
per room, fights resolve locally and the party keeps moving.

**What is NOT acceptable** is redefining "thoughtful" as whatever currently wins; that makes the test
unfalsifiable. Either the design intends depth to pay (and the test should say so, and say why), or
it intends early pressure to pay (and something must make early rooms worth more — the author's new
room-entry bonus in M9 Phase 5 does exactly that, and may resolve this for free). Both are the
author's call.

**Re-run this after Phase 5.** The +2-for-3-seconds room-entry bonus pays a party for *reaching* new
rooms, which lifts exactly the walk-through-empty-rooms stretch that currently makes the careless
layout look good. There is a real chance the inversion disappears without anyone touching the test.

---

## 2026-08-14 — D30. The author's four calls, and what each one cost

D29 was recorded as an open question left deliberately red. It is resolved, along with three others.
All four were put to the author with the options measured rather than described, and he chose the
same way each time: **accept the new behaviour and narrow the claim**. That is the right call in each
case and it is also the exact shape D23 warns about, so each is written as a *narrowed or reversed
claim carrying its evidence* — because "the author decided this" and "a threshold was nudged until
it went green" look identical in a diff a month later.

**1. The wound correlation — accepted flatter.** The positioning rules broke SPEC's "wounded pays
most" ordering: THE PILGRIMAGE finished at 94% health harvesting 408 against THE SKIRMISHERS' 354
from nothing. Two fixes were measured. Pricing retreat as `Fleeing` made it **worse** — wounded
stopped paying at all and `TheSteepEndOfTheCurve_IsReachable` broke too. Steepening the curve
(`WoundAmplitude` 9 → 25) **worked exactly**, restoring the ordering with healthy income untouched,
but turns 20% health into 9.2× against the 1× / 4× / 8× SPEC states outright. The author chose to
accept the flatter correlation rather than rewrite SPEC.

`TheLeastWoundedRoster_IsTheWorstPaying` was **deleted, not loosened**. It ranks rosters by deepest
wound, and with the positioning rules the least wounded bottoms at 5% and the most wounded at 0% —
the independent variable stopped varying, so it passed or failed by luck. A test whose variable does
not vary cannot be rescued by a looser threshold; that only hides that it measures nothing.

**Outstanding:** the league's rival earnings are downstream of measured harvest (D13) and have
**not** been retuned for the flatter correlation.

**2. Rate crossings, 4 → 6.** The limit was set against a curve with no bonuses in it. The variation
modifiers arrive and leave mid-fight, so the rate legitimately crosses its own average more often;
measured at 5 over 1003 ticks. This is not the flicker the test exists to catch — that was the number
jumping most of its range in a fiftieth of a second, which `TheRate_MovesSmoothlyBetweenFrames` still
guards and passes at 1.19/s against 1.5.

**3. Early escape, 5× → 2.5×.** The ordering is the claim and still holds: strolled 38.5, stalled
128.3, a gap of 3.3×. The new-room bonus credits the score, so a party that strolls through now
collects something. **This cost was predicted before the modifier was written** — M9-PLAN.md open
question 1 warned that paying score for entering rooms pays for *advancing*, the behaviour the door
verb exists to prevent. The author read that and chose score knowingly. **If this threshold ever
needs lowering again, the modifier is too strong — do not lower it twice.**

**4. Placement — depth pays, not proximity.** `AThoughtfulLayout_OutEarnsACarelessOne` is now
`ADeepLayout_OutEarnsAFrontLoadedOne`, and the reversal is correct rather than convenient. Monsters
placed deep mean the party walks the early rooms earning almost nothing, meets them late, and is
**still fighting when the clock stops** — SPEC's ideal outcome stated exactly. Monsters at the
entrance die in the opening seconds and the party walks the rest for free. Measured 260 against 316;
the item mix was ruled out as a confound (equalising it changed the numbers by *exactly nothing*, to
the digit). The test still asserts that *where* things go changes what a raid is worth, which is what
justifies a spatial shop. It never needed the entrance to be the right answer — that was the premise,
not the claim.

**Merged to `main`: 4 only.** 1–3 sit on `m11-positioning` and `m9-phase5-unverified`, which touch
`Raid.cs`, `Party.cs` and `AdventurerAI.cs` between them and want one verification pass each.

**Operational, and the single most useful thing to know first:** the RaidManager and ShopManager
suites now need **~1600s, not 800s**. Raids that used to end early run the full clock because parties
neither die nor escape. Twice in one session that looked exactly like a hang and cost real time.

## 2026-08-15 — D31. The run opens on one room, so "they left" had to be re-defined

The author's instruction was one line: *"the starter dungeon should just be one room with one slime
pit and one chest."* The shape is the easy half. What it broke is a **duration**, and nothing in the
suite was watching one.

With a single room, the entrance and the deepest cell share it. Every room has been visited on the
first tick and the party is standing on the entrance, so `HasExploredEverything && Cell ==
_entranceCell` — the escape condition since exploration replaced the boss-cell ending — was **true
before anybody moved**. The raid would have ended at zero seconds. Nothing would have failed: the
outcome is a legitimate `PartyEscaped`, the league would have taken the score, and the round would
simply have been over before the loading screen finished fading.

So a party now has to have gone in before leaving counts: crossed into a second room, or — in a
one-room dungeon — reached the far wall. Deliberately two clauses rather than one elegant rule. The
first is true in a corridor long before exploration finishes, so **a corridor pays nothing for
this**; only the degenerate case has to earn it. A single "walked to the deepest cell" rule read
better and cost a five-room corridor its escape ending outright: it added a there-and-back leg in
the last room and `AnUnboughtDungeon_CanAlwaysBeCrossedInTime` went red at 5 rooms.

Measured on the shipped opening board:

| | seconds | harvested | outcome |
|---|---|---|---|
| nobody touches anything | 12.2 | 51 | PartyEscaped |
| the slime pit is tapped | 60.0 | 342 | TimeExpired |

That gap is the whole first lesson, and it is why the hints over the opening room are worth their
clutter. **Round one is now genuinely losable by doing nothing** — 51 against a rival field earning
a mean of ~236 in round one puts the player around 15th of 16, with the bottom two relegated. That
is the author's dial rather than a defect (the old three-room opening earned ~70 passively, so it is
not new), but it is a coin flip on elimination for a player who watches the first minute go by, and
the league's rival calibration has not been revisited since D13.

Two things fell out of the change that were nothing to do with it, and both were literals that had
quietly encoded "the dungeon starts with three rooms":

- `CanBuyHall` counted `_boughtHalls.Count < MaxRooms - 3`. The player could buy two halls and then
  found the marker dead, with money in the purse and a cap of five rooms. Nothing failed; the offer
  simply stopped being accepted.
- `LeagueScreen.DrawStrip` walked its window against `LeagueTable.Size` — the length the table
  *started* at, not its current length. Once rivals are eliminated it indexes rows that no longer
  exist and throws `ArgumentOutOfRangeException` out of `OnGUI`, taking the clock, the rate and the
  harvest down with it. Late in a *winning* run, which is the least forgivable time for the interface
  to go.

## 2026-08-15 — D32. A tap is decided on release, because a pinch starts exactly like one

Reported as *"pinch zoom is not clicking forward when showing a click on the screen to continue"*,
which describes the code exactly. `TryReadTap` fired on `wasPressedThisFrame`, and the first finger
of a two-finger gesture is indistinguishable from a tap **at the moment it lands**. The existing
guard — ignore a tap while a second finger is down — could only ever fire a frame too late. On the
standings, starting a pinch advanced past them; in the shop it opened a build menu or spent energy on
whichever tile was under the first finger.

Touch taps now resolve on release, and only for a gesture that used one finger and stayed within
40px. A second finger at any point cancels it **for good** — including the stretch at the end of a
pinch where one finger is still on the glass, which is the case a naive fix misses. The mouse still
fires on press: there is no pinch to confuse it with, and press-to-act feels better on a desktop.

The recogniser is a plain state machine (`TapReader.Feed`) taking one frame of state at a time, so
the decision is testable with a synthetic pinch. That matters here more than usual: no headless test
in this project has a touchscreen, and the project's doctrine bans synthesising raw Input System
device events as too fragile. Without that seam the fix would have shipped unverified, which is
precisely how the original bug shipped.

## 2026-08-15 — D33. A retreating party forces the door behind it

*"Make sure a team attacks a closed door."* The door-forcing code existed and was correct — for an
**advancing** party. It returned immediately while the goal was `Retreating`, and the door it looked
for was the one on the route to the boss room. Shut the door a party has just walked through and both
answers are wrong: the route home does not exist, so the retreat pathfind returned an empty route,
and an empty route means the leader does not move. The party stood against the door until the clock
ran out.

That is worse than a stalled animation. SPEC.md makes the retreat the player's only safety valve and
their central regret — *open a door behind a losing party and let them retreat and heal*. A party
that will not use a door it can open itself turns the valve into a trap and the game's most
interesting decision into a farm.

Two changes, both narrow: a retreating party looks for the shut door on the route to the **entrance**
rather than to the boss room, and it walks to that door's threshold rather than to an entrance it
cannot reach. Fighting still comes first for an advancing party; a fleeing one works the door even
with monsters on it, because that is what fleeing looks like. Measured: 4.01 → 1.35 cells, lock
picked in 3.5s.

The test to write this against took three attempts and every failure looked like a production bug.
Hitting each member for 80% of its bar left the party at 29% against a retreat threshold of 28%, so
it advanced; hitting repeatedly until the pooled figure dropped killed them, because the pool is
measured over the **living** and each death lifts it. Taking each member *down to* 15% is the version
that measures what it claims to.

## 2026-08-15 — D34. The opening hint had to carry the restraint, not just the instruction

The first raid is coached now, and the third line said *TAP THE SPAWNER TO KEEP THEM BUSY*. Measured
what happens when a player does exactly that and nothing else:

| what the player does | slimes spawned | outcome | harvested |
|---|---|---|---|
| mashes the pit | 24 | **PartyWiped** | 202 |
| holds two at a time | 26 | TimeExpired | **345** |

Almost the same number of slimes. Opposite results. It is not how many you spawn, it is how many are
on the party at once — which is the whole game in one measurement, and the opening caption was
teaching the half of it that loses.

Three things make this worse than an ordinary mistake, and all three are new:

- The same screen says **DON'T KILL THE CHARGING TEAM** four lines above. Being walked into breaking
  an instruction by the instruction underneath it is not a lesson, it is a trap.
- A kill **refunds the spawn** (D24), so mashing is close to free. Nothing in the economy pushes back.
- The opening dungeon is **one room, so it has no door** — no threshold to put one in. SPEC.md calls
  the retreat valve the player's only mercy, and in round one it does not exist.

The line now reads *TAP THE SLIME PIT TO HOLD THEM — TOO MANY AND THEY DIE*.

**Deliberately not fixed by capping anything.** A per-room monster limit would remove the mistake,
and the mistake is the mechanic: SPEC gives the player no way to call monsters off, and the regret
is the design. It has to be a mistake the player chooses, not one the caption walks them into. If
playtesting says new players still drown their first party, the next lever is a door on the
entrance — an actual valve — rather than a cap on the verb.


## 2026-08-15 — D27. The rivals were priced against a raid that does not exist

`GoodRun` read **500** for most of the project, documented as "what a really good raid harvests".
Nothing has ever harvested it. The four play-styles in `RunProgressionTests` bank **226 to 434**
across a season for a mean of **308**, and the single best round ever recorded is **502** on a
late-season five-room dungeon.

So the rival ceiling — ninety per cent of a good raid — sat at 450, **above the game's own typical
maximum**. That quietly inverted the promise D20 and D25 are both built on. "Play a genuinely good
raid and no rival can have beaten it" was arithmetically false: there was no raid good enough.

Corrected to the measured **430**. Best of four play-styles goes from round **7 to round 9** of ten,
and now reaches the final rather than dying mid-table.

**D25 stands.** The instinct was that `FinalistPressure` was the culprit — the player's harvest is
flat across a season (341 in round one, 359 in round seven) while that dial lifts the rivals' floor
from 22 to 407, which reads exactly like the thing ending runs. It was tried at 0.55 and **reverted**:
correcting `GoodRun` alone reached round nine, and lowering `FinalistPressure` as well also reached
round nine. It bought nothing, and it would have cost D25's "late on, a rival never has an off day".

Two things worth keeping from that:

- **A plausible story about a dial is not evidence that moving it does anything.** Both changes were
  made together first and the pair looked like a success. Only measuring them apart showed one of
  them was inert.
- **Constants that describe the game go stale silently.** `GoodRun` was a measurement written down
  once and then treated as a fact forever, while the thing it measured changed underneath it — most
  recently when the run started opening on a single room. It is now labelled as measured, with
  instructions to re-measure rather than re-choose.

Still open, and the author's call: **nothing has won a season by playing it.** The bot contests the
final and loses on cumulative score. D25 measured that winning needs ~400 a round against this bot's
308, so the question is whether 400 is reachable by a human on a five-room dungeon, or whether
`MaxRooms` or the wound curve has to move. That is a design question, not a stale constant.

## 2026-08-15 — D28. A gate that ranked the fix below the fault

D-note to the tileset thread, and a correction to something committed and pushed earlier today.

`validate-tileset.py` gained a gate claiming the sixteen wall tiles were "the same picture", and
`CREDITS.md` and `TILESET-SEARCH.md` §14 were both updated to say the autotiling was decorative. **It
is not.** Measured per side, conditioned on the mask bit, an edge facing open floor sits at luminance
51–59 where the same edge continuing into wall sits at 30–36. `wall-0` and `wall-15` differ by 22
luminance along their top rows.

The gate compared **whole tiles** and divided by texture grain. Sixteen correct Wang tiles share one
interior and differ only at their edges, so whole-tile difference is near zero *by design* — the
metric could not answer the question it was asked, and its low score was the signature of a healthy
set being read as a broken one.

Two things caused it, and both are cheap to avoid:

- **The metric was never calibrated against a known-good case.** Building a set that encodes
  boundaries on purpose and re-running the gate took one command; it scored **0.33x**, worse than the
  1.47x it had just condemned. A measure that ranks a deliberate fix below the fault is measuring
  something else, and that check would have caught this before anything was written down.
- **A second opinion agreed, from the same flawed method.** A research agent independently reported
  the same conclusion, having computed the same whole-tile statistic. Reproducing its numbers felt
  like verification. It was one mistake run twice — agreement is only evidence when the methods
  differ.

The gate now asks the right question: per side, mean edge strip across tiles with the bit SET against
tiles with it CLEAR. The installed set passes at N=22.2, W=9.2, S=6.2, E=4.6.

**The author's actual report remains unexplained.** "The walls don't look like walls from slight
angle but just pattern tiles in different colours" is still true and still undiagnosed; the lit cap
is 4px on a 64px tile, under 7% of its height, which is a candidate nobody has measured. §13's three
failed attempts at drawing relief were aimed at a defect that does not exist. Before a fourth, find
out what does.

## 2026-08-15 — D29. Two of the four halls the shop sells cannot be used

Measured by `RoomsPayTests.MoreRooms_EarnMore`, same policy and same seed at every size:

```
2 rooms   harvested 330   PartyEscaped   party reached 2 of 2
3 rooms   harvested 446   TimeExpired    party reached 3 of 3
4 rooms   harvested 446   TimeExpired    party reached 3 of 4
5 rooms   harvested 446   TimeExpired    party reached 3 of 5
6 rooms   harvested 446   TimeExpired    party reached 3 of 6
```

**The party reaches exactly three rooms in sixty seconds, whatever the dungeon's size**, so harvest
saturates at 446.33 and every hall after the third earns literally nothing. `MaxRooms` is 5 with one
room to start, so the shop sells four halls and **two of them are inert** — the most expensive item in
the shop, bought first by any competent player, with no effect on the score.

This also explains the flat season curve that prompted D27: a bot buying a hall in almost every shop
harvested 341 in round one and 359 in round seven, and the recurring 246 was the same raid happening
again. The dungeon was growing into space the party never enters.

Two rooms is a different failure and worth noting beside it: the party **escapes** rather than running
the clock out, which is the losing outcome the whole design is built to avoid. So the usable range is
narrow — three rooms works, two leaks the party out the far end, four and beyond are decoration.

**Not fixed here, because every available fix is a design decision and they point in opposite
directions.** `MaxRooms` down to 3 makes the shop honest and shrinks the game. A faster party, or
smaller rooms, or a longer clock makes the later rooms reachable and changes the sixty seconds that
the title is about. Making deeper rooms pay a multiplier rewards building forward without touching
the clock, and is the only one of the three that adds a decision rather than removing one — but it is
a new rule, and SPEC.md is firm about not adding rules until the three verbs are proven.

The instrument is committed, so whichever is chosen can be measured the same way.

## 2026-08-15 — D31. The season instrument was seeded from the clock, so D27 was luck until now

`GameController.NewRun` took its seed from `System.Environment.TickCount`. Every season-long
measurement in this project was therefore a *different* season, and read as if it were not. Five
consecutive runs of **unchanged** code returned best-of-four rounds of **7, 9, 9, 10 and 10**.

D27 was drawn from one sample on each side of that spread. Its conclusion happened to be right; its
evidence did not support it, and a spread of 7-to-10 will happily contain any tuning change anyone
cares to make.

SPEC.md asked for seeded generation so a run could be reproduced from a bug report, and the seed was
threaded correctly through the league, the party chain and the combat. What was never built was a way
to **set** it — so the property existed everywhere except where a test could reach it.

Fixed with `GameController.SeedOverride`, and `RunProgressionTests` now sweeps three seasons rather
than playing one. The seed must be set and `NewRun()` called again, because `Awake` has already
started a season from the clock by the time `AddComponent` returns.

**With that, D27 re-measured properly — same seeds, same four policies, only the constant changing:**

```
GoodRun = 500   best per season  8, 7, 8    wins 0 of 12
GoodRun = 430   best per season 10, 9,10    wins 3 of 12
```

Every season improved and every policy improved. **The season is winnable**: a quarter of the
policy-and-season combinations now win outright, where none did before.

The lesson is not "the answer was right". It is that a measurement which cannot be repeated cannot
support a conclusion, and this one printed a tidy list of numbers that looked exactly like one that
could. That is the third time today a measurement rather than the code was at fault — see D28 and the
closed pale-bands note in HANDOVER.md — and all three shared a shape: the instrument was never asked
to prove it could tell two known-different cases apart.

## 2026-08-15 — D32. The wall complaint, measured at last, against a target we already had

The author's report has been open all day: *"the walls don't look like walls from slight angle but
just pattern tiles in different colours."* Three attempts answered it by drawing relief and all three
failed. D28 then retracted the diagnosis those attempts rested on, leaving it open and unexplained.

`TILESET-NOTES.md` had already recorded the answer, and nothing had ever checked the shipped tiles
against it:

> the moodboard does **not** separate wall from floor by value — they sit at the same mean.
> It separates them with the **rim highlight**, which is ~90% brighter than the floor.

Targets from that note, measured off the moodboard: **wall/floor 0.98**, **rim/wall 1.93**. Measured
on what ships:

```
wall / floor   1.46     target 0.98
rim  / wall    3.04     target 1.93
```

**The wall body is 46% brighter than the floor where the reference has them equal.** Two adjacent
patterned areas at different values are, precisely and literally, pattern tiles in different colours.
The wall does not fail to read as a wall for want of relief; it fails because the value structure
that carries "mass with a lit edge" was replaced by "lighter area beside darker area". The rim being
over-bright at 3.04 compounds it — the one cue that should be doing the work is loud enough to read
as an outline instead.

That also explains why relief could never have worked. Relief adds a highlight and a shadow to a
surface whose *relationship to the floor* is the broken part.

`Tools/grade-walls.py` applies it as a **grade rather than a redraw**, which is the same note's other
warning — both earlier attempts drew a bright lit slab and neither survived. It scales rather than
offsets, so the mortar lines and chipped blocks keep their relative contrast, and it ramps the rim
gain out over six rows so there is no seam where it stops. Dry run reaches wall/floor 0.98 exactly;
rim/wall lands at 2.56 rather than 1.93, because the rim gain is computed against a peak and applied
to a mean, and that is worth finishing before anyone installs it.

**Deliberately not installed.** The author reserved approval on this specific question — "I'll
approve once it looks good as the mood board" — and a measurement agreeing with a target is not the
same as a person liking a picture. `Screenshots/grade-compare.png` is before-and-after for that call.

### D32 addendum — the targets are now hit exactly, and the picture got worse

The rim gain is solved by bisection rather than algebra (the needed gain is *below* one, and ramping
a reduction over six rows lets the brightest pixel migrate down the ramp, so two closed forms
overshot at 2.56 and 2.16). Measured now:

```
wall / floor   0.98   target 0.98
rim  / wall    1.94   target 1.93
```

**And the room reads flatter than before.** With wall and floor at the same value the only separation
left is texture — brick against cobble — which at this darkness is very subtle, and the rim pulled
down to 1.93x no longer reads as a lit edge. `Screenshots/grade-compare.png`.

CLAUDE.md warned about exactly this, in these words: *"A run that hit every stated figure exactly
produced flat, featureless slabs with no masonry at all. Metrics constrain; they do not compose.
Always look at the output."* This is that, and the file had said so before the attempt.

What it does **not** invalidate is the diagnosis. wall/floor at 1.46 against a reference 0.98 is still
a real, measured deviation, and it still describes "pattern tiles in different colours" better than
anything else proposed. What the grade shows is that the ratio is **necessary and not sufficient**:
the reference holds wall and floor at one value while keeping them legible, and it does that with
hue, saturation and drawn masonry detail our source stone does not carry. Grading cannot add those.

So the honest reading is that this points at the **source art**, not at a filter — which is the same
conclusion §12 of TILESET-SEARCH.md reached from the other direction, and the reason the two bought
tilesets are worth the $3.20. Do not install this grade expecting it to answer the complaint.


### D29 addendum — re-measured across three seeds, and it is structural

D29 was measured on one seed, which is precisely the mistake D31 documents. Re-run with three
different parties and combat rolls, means over the three:

```
2 rooms   311    the party ESCAPES
3 rooms   421    deepest 3 of 3
4 rooms   421    deepest 3 of 4
5 rooms   421    deepest 3 of 5
6 rooms   421    deepest 3 of 6
```

**Identical to the pound at three, four, five and six rooms, across every seed.** The party reaches
exactly three rooms in sixty seconds whatever the dungeon's size and whoever walks in, so the
saturation is not a draw artefact — it is the clock meeting the walk speed. Two of the four halls the
shop sells remain provably inert, and the finding is now as solid as the instrument can make it.

### D28 addendum — the 68 it cites was inflated by about half

D28 quotes "68 gate failures" on main's own tiles as evidence of the art's state. That number was
wrong, and wrong through the same class of mistake D28 is about.

`side_coverage` exists to catch art that does not reach the canvas edge — a transparency question —
and it was asking luminance. Every wall pixel darker than the mean floor therefore counted as missing
art. With the gate measuring alpha instead, main's set scores **36**, so roughly 32 of the 68 were
that single false positive.

It would also have rejected good art on purchase day: both candidate tilesets draw base shadows, and
a shadow along a tile's bottom edge read as an unsolid border at exactly the shadow's height — 88%
against a 95% threshold on a synthetic pack, which is 8 rows of 64 after a x4 upscale.

Calibrated both directions now, which is the step D28 was written about:

```
transparent 4px margin (the real fault)   N/E/S/W = 0%    fails
opaque tile with a drawn shadow (fine)    E/W = 100%      passes
```

**The conclusion D28 draws is unaffected** — the art does not pass its own gate either way, and 36 is
still a poor score. What changes is the size of the number, and that a figure quoted as evidence had
never itself been checked. That is the third time in one day, and the shape is identical each time:
the instrument was believed because it produced a number, not because anyone asked what the number
was measuring.

## 2026-08-16 — D33. The shipped build can be played now, and the first play found something

The itch page runs the game in a **cross-origin iframe**, which synthetic input cannot reach. So
automation could confirm the build booted and photograph the title screen, and could not press a
single button — the raid loop, which is the game, had never been exercised in the shipping renderer
by anything but a human.

`Tools/serve-build.py` fixes that. The build is **brotli-compressed with no decompression fallback**,
so it needs a server sending `Content-Encoding: br`; Python's stock `http.server` does not, and the
loader fails with an error that reads like a corrupt build. Served from localhost the page is
same-origin and the whole loop is drivable. Verified end to end: loading screen, standings, a raid
with the clock running and the rate at 4.7/s, and the adventurers' review at three stars.

### What the first play found

**The party walks through the opening instruction.** At the itch viewport the three-line headline sits
low enough that the party's sprites and health bars are drawn across its third line — "TOO MANY AND
THEY DIE" struck through by an archer and a green bar.

This is not the chest-tag collision fixed earlier the same night; that one is gone. It is the party
itself, and the code comment above the block says it is placed "high enough to clear the party walking
through it", which is true at 1280x720 and false at the viewport the build actually runs in
(~1040x512, scale 0.71). **Every editor capture was 1280x720**, which is why nothing saw it.

`ResolutionSweepTests` covers ten sizes including the itch embed, but it checks geometry
analytically — that rects are on screen and big enough. It has no notion of a sprite being drawn over
a label, because nothing in it renders.

Not fixed here: the block's placement interacts with camera framing, room size and scale together, and
changing it wants a photograph at several viewports rather than one arithmetic tweak. The serving tool
now makes that possible, which it was not this morning.

### D33 addendum — fixed, and verified where it was found

The clearance is measured in world cells now and projected by the camera, so it holds at any zoom.
Verified by playing the rebuilt artefact through `serve-build.py` at the viewport that exposed it:
all three instruction lines fully legible, nothing drawn through them, and the chest's "THEY STOP TO
LOOT" visible as well now that the block is not sitting on it.

Worth knowing what the fix actually does at that size: there is **no room above**. Three cells up puts
the block at y≈70 against a HUD zone ending at y≈102, so it takes the below-the-room fallback — which
is the branch working, not a compromise. The lines land about 32px clear of the verb bar.

The residual: at the itch embed's 523x293 that fallback has much less room, and nothing has
photographed it there. `ResolutionSweepTests` will not catch it either — it checks rects
analytically, and this whole class of fault is one drawn thing landing on another.

**Tried and did not work: resizing the browser.** `resize_window` reports success and the capture
comes back at the full window size with the game unchanged, so the viewport under test never actually
changes. Two approaches in, and worth writing down so the next attempt starts somewhere else.

The route that would work: serve a modified `index.html` pinning the canvas to 523x293.
`serve-build.py` already owns the serving, so it is a small addition rather than a new idea — from a
**copy** of `Builds/`, never by editing the shipped one.

So the fix is verified at ~1040x512, the size the build runs at locally, and unverified at the embed.

### D33 closed — verified at 523x293, the size that showed both faults

`serve-build.py --canvas 523x293` pins the canvas by rewriting `index.html` in memory, so the embed
viewport is reachable without touching `Builds/`. Resizing the browser window does not work; that is
recorded above.

Photographed there on `0.1.2608160325`, all three lines clear with a gap above the verb bar:

```
DON'T KILL THE CHARGING TEAM
HURT, ALIVE AND STILL INSIDE PAYS BEST
TAP THE SLIME PIT TO HOLD THEM  -  TOO MANY AND THEY DIE
─────────────────────────────────────────────────────────
TAP A DOOR TO STALL / A SPAWNER TO AMBUSH / A TRAP TO WOUND / …
```

**Two faults lived in this one block, and neither was visible at 1280x720.** The party walked through
it at ~1040x512; the verb bar collided with it at 523x293. Every editor capture is 1280x720, which is
the one size where both looked correct — so the block was tuned against the only viewport that could
not show it wrong, twice.

The general lesson is narrower than "photograph the game", which this project already knew. It is:
**photograph it at the size it ships at.** For this game that is 523x293, and until tonight nothing
had ever rendered a frame there.

### D33 follow-through — the other screens photographed at 523x293

Having learned to look at the size the game ships at, the rest of the loop was walked there on
`0.1.2608160325`. **All clean:**

- **Standings / title** — twenty rows, relegation line and "PRESS ANY KEY" all fit. That prompt is
  the one this screen lost off the bottom once, and it is present.
- **Raid** — HUD, standings strip, and the three instruction lines clear of the verb bar.
- **Review** — stars, verdict, quote, harvest and the two-line coaching text, nothing clipped.
- **Collapse** — twenty rows with scores and the full "YOUR DUNGEON COLLAPSED IN 19th. PRESS ANY KEY
  TO BEGIN AGAIN".

**A near-miss worth recording, because it is the third of its kind tonight.** The collapse line looked
clipped — "…TO BEGIN" with "AGAIN" missing — and it was not: the capture region was 440px wide on a
523px canvas, so the crop cut it, not the game. One wider capture settled it. The other two were a
collapse-screen test that never reached the collapse screen, and a one-room board measured without
the furniture the game puts in it. Every time, the setup was wrong and looked like a finding.

Also observed, not a defect: harvesting 51 in round one finishes 19th and ends the run immediately.
That is D20's "round one is sudden death", still open and still the author's call — but it is worth
knowing it is trivially reproducible by doing nothing for sixty seconds.

## 2026-08-16 — D34. Round one measured: not sudden death, and not reliably a lesson either

D20 left "round one is sudden death" open as a worry with three offered levers — seed a small opening
score, exempt the first round, or leave it as a sharp lesson. Choosing needs the threshold, and
nobody had measured it. `OpeningRoundTests` does, across five seeds:

```
survival threshold        35, 40, 50, 65, 75      worst 75
doing nothing (51)        survives 3 of 5 seeds
playing the board (342)   survives 5 of 5 seeds
```

**So D20 is half right, and the half that is wrong is the alarming half.** Round one is not sudden
death for anyone who touches a spawner: the bar tops out at 75 against an opening board worth 342, a
4.5x margin, and playing survives on every seed. What it also is not is a reliable lesson — an
untouched dungeon banks 51 and **gets away with it three times in five**.

That reframes the choice. The risk was never that a competent opening raid could be punished; it is
that an incompetent one is punished at random, so the player who most needs to learn "an untouched
dungeon earns nothing" learns it on a coin toss. If the first round is meant to teach, the lever is
raising the floor slightly — not protecting the player from it.

**Recorded because I got it wrong first.** Having watched the shipped build bank 51 and finish 19th, I
reported that harvesting 51 ends the run. True of that seed, false in general, and I wrote a test
asserting it before measuring — which failed, correctly. One observation generalised into a rule is
the same fault as D28 and D31, in a third costume.

## 2026-08-16 — D35. Five files are over the architecture's hard cap, and nothing had said so

`ArchitectureGuidelines.md` §8 sets a **hard cap of 400 logical lines per file**, "everywhere". Nothing
in `DECISIONS.md`, `HANDOVER.md` or `PLAN.md` had ever mentioned it, so this is neither an accepted
deviation nor a known debt — it is simply unmeasured. Measured now, non-blank and non-comment as the
guideline defines it:

```
686 logical (1356 raw)   Modules/PartyManager/Party.cs
677 logical (1354 raw)   Application/Game/GameController.cs
480 logical ( 710 raw)   Modules/RaidManager/Tests/RaidRulesTests.cs
447 logical ( 764 raw)   Application/Game/DungeonView.cs
444 logical ( 899 raw)   Modules/RaidManager/Raid.cs
```

**Raw line counts overstate it roughly twofold** — this codebase carries unusually heavy XML docs and
rationale comments, so a first pass listing eight offenders by raw lines was wrong. `AdventurerAI.cs`
at 714 raw is comfortably inside the cap at logical count. The rest of the checklist passes: no
`<inheritdoc/>`, and no file references another module's `.Internal`.

**Not split, deliberately.** The two worst are `Party.cs` — which owns the party AI and the retreat
valve, the mechanic CLAUDE.md calls load-bearing — and `GameController.cs`, which owns every phase
transition. Splitting either is a real refactor with real regression risk, and doing it at four in
the morning against a shipped, green, published build is how a working game stops working. The
guideline's remedy is "a main class plus helper classes, never partials", which is a design decision
about where the seams go, and that belongs to the author.

Recorded rather than fixed or ignored. It is the only architectural rule this project breaks, and it
breaks it in five known places.

### D35 addendum — the architecture section described a project that does not exist

Auditing the rest of `ArchitectureGuidelines.md` against the code turned up worse than the file cap:
**CLAUDE.md's own architecture section was wrong in three ways**, and it is the file every agent reads
first.

- It listed a **`UIManager/` module that has never existed.** The UI is IMGUI drawn from
  `Application/Game` — `LeagueScreen`, `ShopScreen`, `ReviewScreen`, `LoadingScreen`, `Hints`,
  `DungeonView`. Anyone following that tree goes looking in a directory that is not there.
- It **omitted `AudioManager/`**, which does exist and is the only module with a Facade.
- It claimed **"Facade/Router"** as the structure. Measured: one Facade across seven modules, and no
  Routers at all. Every other module's public surface is its concrete classes, used directly.

Corrected to describe what is there, with the deviation stated rather than implied.

What the checklist *does* hold, all verified mechanically: no interfaces, no `<inheritdoc/>`, no file
reaching into another module's `.Internal`, every module inside the 2000-line cap (largest
PartyManager at 1206), every one of the 336 tests carrying an XML summary, no `Debug.Log` in test
code, and no `IEnumerator`/`[UnityTest]` coroutine tests. The only breaks are the five oversized files
above.

**Two rounds of auditing documented rules found two real problems; two rounds of hunting for bugs
before that found only my own mistakes.** Checking a claim the project makes about itself is a better
use of time than looking for something wrong.

## 2026-08-16 — D36. CLAUDE.md's headline formula described the curve M6 replaced

The section titled "The one idea this game is built on" carried:

```
energyRate = baseRate * engagementMultiplier * woundMultiplier
```

That is the **superseded** curve. Verified against `Raid.cs`: the live rate is a **sum over living
members** of `baseRate * actionRate(action) * woundMultiplier(health)` (line 838). The party-wide
`EnergyCurve.Rate(engagedCount, health)` still exists and still matches the old line, but is called
exactly once — to seed the opening display value — and drives nothing thereafter.

M6 made that change and recorded it in `PLAN.md`; the headline in CLAUDE.md was never updated. So the
file that opens every session, in the section it calls the game's central idea, described a mechanic
that had not been live for weeks.

The difference is not cosmetic. Under a party-wide multiplier only *being wounded* paid, so the
design's own instruction — alive, in combat, and hurt — was half expressed. Summing per member means a
tank fighting at 15% and a healer working at full health pay different amounts at the same instant,
which is what makes the wound curve something the player manages rather than waits for.

Corrected, with the action rates written down (idle 0.04, walking 0.06, fleeing 0.75, working 1.05,
shooting 2.1, fighting 3.0, and a corpse costing 50) so the next reader can check the claim instead of
trusting it.

**Third documentation defect in three audit rounds**, after the 400-line cap (D35) and an architecture
section listing a module that has never existed. The pattern is consistent enough to name: this
project's *code* has been kept honest by its tests, and its *prose* has not been kept honest by
anything. Everything asserted in a document here is worth checking against the thing it describes.

## 2026-08-16 — D37. A "hard constraint" that D8 had already overturned

CLAUDE.md's **Hard constraints (do not break)** listed:

> **Never show a number for adventurer HP.** … Coarse three-state (healthy/hurt/critical) is the
> fallback if playtesting says it is unreadable.

**D8 overturned that on 2026-08-13**, three days earlier, and the game has shipped continuous
colour-coded health bars ever since — visible in every screenshot taken tonight. The constraint was
still written as inviolable, with no note that it had been superseded.

This is the most dangerous of the four documentation defects found in this audit, because the others
mislead and this one **instructs**. An agent reading a section headed "do not break" would treat the
health bars as drift and remove them — deleting the fix the author asked for after playing the game
and finding deaths arrived unseen. *"wtf, I did not see that"* were the author's words.

Struck through and rewritten to say what shipped and why, including that limping, slowed movement and
panic still carry wounded state as well — the bar is in addition, not instead.

The other three constraints in that section were checked at the same time and **hold as written**:
exactly three verbs on `Raid` (`ToggleDoor`, `SpawnMob`, `FireTrap`, with no recall or call-off
anywhere), the leaderboard is the title screen, and the build ships to itch as WebGL.

**Fourth documentation defect in four audit rounds.** A file that says "do not break" earns more
scrutiny than one that says "here is the layout", and it had received less.

### D37 addendum — the rest of CLAUDE.md checked, and it holds

Every remaining factual claim in the file was verified against the thing it describes:

- **Unity `6000.3.17f1`** — matches `ProjectVersion.txt` exactly.
- **MooseRunner `2.2.5`** — matches what the CLI itself emits.
- **`Builds/` is gitignored** — `git check-ignore` confirms, at `.gitignore:72`.
- **Three verbs, no mob recall** — `Raid` exposes `ToggleDoor`, `SpawnMob`, `FireTrap` and nothing else.
- **Sprite pipeline paths** — the debug binary is present, which is the fallback the preflight already
  anticipates, and all three moodboard crops (`style-palette`, `style-tiles`, `style-mobs`) are where
  the file says.
- **The leaderboard is the title screen**, and the build ships to itch as WebGL — both photographed.

One claim was *true but unusable* and is now actionable: "a run can be reproduced from a seed in a bug
report". Until D31 there was no way to **set** the seed, so it could not be done at all. The file now
names `GameController.SeedOverride` and the trap that goes with it — `Awake` has already started a
season by the time `AddComponent` returns, so the seed must be set and `NewRun()` called again.

**So the audit ends four for four on prose and clean on everything else.** The four defects were the
400-line cap unrecorded (D35), an architecture section naming a module that never existed and claiming
a Facade/Router structure that is one Facade in seven modules (D35 addendum), the headline energy
formula describing the curve M6 replaced (D36), and a "hard constraint" that D8 had overturned three
days earlier (D37). Nothing in the code was wrong. Every defect was a document describing a project
that had moved on without it.

## 2026-08-16 — D38. The energy curve superseded SPEC §3, and this ledger never said so

SPEC.md §3 specifies the game's central mechanic as:

```
energyRate = baseRate * engagementMultiplier * woundMultiplier
```

The shipped game does not do that. It sums, per living member, `baseRate * actionRate(action) *
woundMultiplier(that member's health)` — verified in `Raid.cs:838`, and recorded properly in D36.

**The change was the author's**, directed after playing M5 and logged in PLAN.md's M6: *"The energy
curve pays per person, per action… This replaced a single party-wide 'in combat?' flag multiplied by
the worst survivor, which made being wounded the only thing that paid."* So it is authorised and it
is right — the party-wide form meant only *being hurt* paid, which expressed half of the design's own
instruction.

**What was missing is this entry.** The supersession went into the milestone log as an accomplishment
and never into the decision ledger, and the two are read for different reasons: CLAUDE.md sends a
reader to SPEC.md as "the authority on what this game is", and to DECISIONS.md "before reversing
anything". An agent comparing code against the spec would find the formula differs, find no decision
saying why, and could reasonably restore the spec's version — undoing the change the author asked for.

That is the same failure mode as D37's hard constraint, from the opposite direction: there, a
superseded rule was still written as binding; here, a live supersession was never written down at all.

D8 got this right and is the model — its title is literally *"Health bars on adventurers, superseding
SPEC.md §3"*. A change that contradicts the spec needs an entry that names the section it overrides.

### D38 addendum — the rest of SPEC.md checked, and only the curve was unrecorded

Every concrete number in the spec was verified against the code:

| SPEC says | code | |
|---|---|---|
| hard 60-second raid | `Raid.RaidSeconds = 60f` | ✓ |
| 30-second shop | `Shop.ShopSeconds = 30f` | ✓ |
| 20 dungeons | `LeagueTable.Size = 20` | ✓ |
| player starts around 14th | `PlayerStartPosition = 14` | ✓ |
| relegation line under the bottom two | `RelegationCount = 2` | ✓ |
| exactly three verbs, no mob recall | `ToggleDoor`, `SpawnMob`, `FireTrap` | ✓ |
| leaderboard is the title screen | photographed at two viewports | ✓ |

The spec has **three** places where the shipped game deliberately differs, and two were already
recorded:

- **§3 "never show a number for adventurer HP"** → D8, titled as superseding it outright.
- **§6 "a new name takes the slot"** → D20. The spec describes a persistent league that refills;
  the game is a knockout where the eliminated are not replaced and the last dungeon standing wins.
  D20 records that as the author's design.
- **§3 the energy formula** → nothing, until D38 above.

So the ledger was two-for-three, and the gap was on the mechanic the spec calls "the whole design".
Worth noting *why* that one slipped while the other two did not: D8 and D20 were both decisions taken
in response to a problem, and got written up as decisions. The curve changed as one bullet inside a
seven-item milestone the author directed in a single session — it read as work delivered rather than
as a rule overturned, and only the milestone log caught it.

### D38 addendum 2 — the remaining documents audited

**`Marketing/ITCH-PAGE.md` is accurate and needed no correction** — the first document tonight that
did not. Every file it tells the author to upload exists, the five screenshots match its
`screenshot-1..5-*.png` glob, `#15101D` is verifiably the camera background set in
`GameController.cs:155`, and `#251B31` / `#50275E` / `#D75268` are CLAUDE.md's canonical palette.

**`CREDITS.md`** was corrected earlier tonight — it claimed the sixteen wall shapes were sixteen
shapes, which the D28 retraction then made wrong in the opposite direction; it now states what is
measurable and the licensing, which was always sound.

So the audit across five documents found defects in three of them, all in the two files that describe
*how the project works* rather than *what to do with its output*. That is not a coincidence worth
ignoring: CLAUDE.md and SPEC.md are the files a session reads and acts on, and they are the ones that
drift, because the project changes underneath them while the documents that describe deliverables
only change when the deliverables do.

The cheapest guard, and the one this audit actually used: **every factual claim in a guidance document
should be checkable in one command.** A version number, a file path, a constant, a class name. Where
that was true the claim was either right or caught in seconds. Where a claim was a description — "the
architecture is Facade/Router" — it went unchecked for weeks.

## 2026-08-16 — D39. The author's pass, and what the room bonus costs elsewhere

Five changes, all requested directly.

**+2 seconds per room entered** (`Raid.NewRoomSeconds`), paid on first entry so a room the party never
reaches pays nothing. This is the author's chosen lever for D29.

It does **not**, on its own, make the fourth room reachable. Measured across three seeds: at +2 the
party still reaches three rooms and harvest goes 446 to 454; at +8 it reaches four and harvest reaches
483. Roughly six seconds buys one more room of reach, so about fourteen would open the fifth. The
author's number stands and the figures sit beside the constant.

**Two consequences worth stating plainly**, because neither was asked for and both follow:

- The party walks into its **first** room too, so every raid is now 62 seconds rather than 60, and a
  fully-bought five-room dungeon the party crosses runs **70**. That is a sixth added to the sixty
  seconds the game is named after, and it only becomes visible once somebody buys enough halls.
- **Two tests broke**, both with `Raid.RaidSeconds` baked in as the end of the raid — one advanced
  exactly 61 seconds and asserted the clock had hit zero, the other looped to the constant and then
  asserted the raid had ended, reporting a hang that was not one. Both now run until the raid actually
  stops. They were testing an arithmetic identity against a constant that has since moved.

**An open way in.** The west wall beside the entrance is carved through, so the first room is a place
a party walks into rather than a sealed box they appear inside. Carved as a `Doorway` with **no
`Door`**: a tappable door there would let the player shut the party out for the whole minute, losing
the raid by pressing the thing the tutorial tells them to press.

That carve was wrong first, and a photograph caught it. `DungeonLayout.Build` returns early whenever
`placed` is non-null — which is every raid in the real game, because furniture comes from the loadout
— so carving after that branch worked in tests and did nothing in the game.

**The tutorial** is larger (headline 24→34px, body 14→20px) and now clears when the party **loots the
chest** rather than purely on a timer. Measured: the starter chest is looted at about 4.7 seconds, so
the in-raid instruction clears after roughly five. That is exactly as asked and it pulls against "I
missed it" — the rule is therefore also stated **before** the raid, on the standings, as *"DON'T KILL
THEM — PRESS ANY KEY, THE FIRST PARTY ENTERS"*. If the in-raid text should linger, a minimum display
time is the fix and the number is the author's.

**The door sprite** was generated at 64x64, ten colours, on palette — but Sprite Studio used its
**deterministic pixel renderer, not ImageGen**, so the moodboard never reached an image model
(`referenced_image_paths` logged zero). It reads correctly in place. Only the `terrain tileset` and
`effect` harnesses forward references, so genuinely generated art here needs a different route.

### D39 addendum — the carve does not let anything out, and it is not just scenery

The opening was carved into the *outer* wall, so it belongs to no room, and a wounded party retreats
*toward* the entrance. That is a way to lose a raid which did not exist before this change, and it was
worth checking rather than assuming: if a body can path onto the opening it is off the grid, and if a
monster can, the room-bounded pursuit rule the whole retreat valve rests on is broken.

It cannot. `DungeonGrid.IsWalkable` passes a `Doorway` only when a door exists and is open —
`door != null && door.IsOpen` — so a **doorless** doorway is passable to nobody. The property that
makes the opening safe is the same one chosen to stop the player shutting the party out. That is why
no containment suite moved when the carve landed, and it is luck rather than design, so it is pinned
now: `EntranceOpeningTests`.

**The first guess after that was wrong, and measuring corrected it.** "Not walkable" reads as "the
opening is decorative", and it is not. Across twelve seeded raids, adventurers register **on** the
opening for the first **1.6 seconds** — the party spawns west of the entrance and walks east, and a
body's cell is its rounded continuous position, so the arriving party genuinely walks in through the
hole in the wall. It just cannot walk back out. Both halves are asserted.

Over the same twelve seeds: **zero** monsters on the opening at any time, and **zero** adventurers
after the arrival window. The window is set at 3s against a measured worst case of 1.6s, and it was
verified to have teeth — tightening it to 1s turns the test red, so it is a gate and not a formality.

This is the cheap version of the guard adopted after D28 and D36–D38: the failure mode in every one of
those was **a plausible story about a measurement, believed before it was checked**. "Nothing can walk
there, so nothing does" was exactly such a story, and it was half wrong.

## 2026-08-16 — D40. The permanent room bonus is built, measured, and held on a branch

The author asked for it plainly: **"after entering a new room the team gains +2/s for the rest of the
run per room."** It is implemented faithfully on `room-bonus-permanent` — a count rather than a timer,
stacking, with nothing eroding it. It is not on `main`, and this records why, because a branch with no
reason attached is just work somebody will delete.

**It breaks the invariant that stalling must beat letting the party leave.** Measured, same seed, same
board, `EarlyEscape_EarnsFarLessThanAFullRaid`:

| | before | after |
|---|---|---|
| strolled through and left | 38.5 | **121.8** |
| stalled and fought | 128.3 | **254.8** |
| ratio, floor is 2.5x | 3.3x | **2.09x** |

The party that walks through and leaves gains **three times** more; the party that stays and bleeds
only doubles. That asymmetry is the whole problem, and it is not a moved figure — paying per room pays
for **advancing**, which is the behaviour the door verb exists to prevent. `M9-PLAN.md` predicted this
in as many words before the modifier was first written.

**Why it was not simply retuned.** That test was already narrowed once, from 5x to 2.5x, by the
2026-08-14 decision — for this same cause, when the room bonus was three seconds long. That decision
ends: *"If this ever needs lowering again, the modifier is too strong — do not lower it twice."* This
is the second time. Lowering it again would convert a designed limit into a number that follows
whatever the modifier happens to do, which is how an invariant stops being one.

`TheRate_MovesSmoothlyBetweenFrames` fails as the same symptom rather than a second fault: a larger
target makes each eased step larger.

**What the author actually has to choose**, since the goal — make traversal pay — is sound and only the
size is wrong:

1. **Ship it as asked.** Depth pays, and the door verb weakens. One merge.
2. **Cap the stack** at two or three rooms, so depth pays and a five-room sprint does not.
3. **Pay it only while the party is engaged.** Depth still pays, but a party strolling an empty
   corridor earns nothing extra — which keeps SPEC's "an unengaged party must earn almost nothing"
   intact and is closest to what the bonus seems to be *for*.

Option 3 is the recommendation; it is the only one that leaves both rules standing. All three are a
small edit to `RateModifiers`.

## 2026-08-16 — D41. "Skirmish is always third" was not a bias, and measuring said so first

The author reported that THE SKIRMISHERS kept arriving as the third party. Two very different faults
produce that sentence, and they want opposite repairs:

- the run seed is **not spreading** the parties, in which case gating a roster to later rounds hides a
  broken chain and the next report will be about some other roster;
- the chain spreads fine and the roster is simply **too punishing that early**, in which case gating
  is exactly the fix.

**Measured before touching the picker**, 240 runs of the controller's own seed chain, at raid three:

```
THE BALANCED PARTY   52/240  22%
THE PILGRIMAGE       42/240  18%
THE IRONCLADS        38/240  16%
THE SKIRMISHERS      38/240  16%
THE GLASS CANNONS    35/240  15%
THE UNSHRIVEN        35/240  15%
```

Even, and THE SKIRMISHERS is unremarkable at 16%. There is no bias; the report is the second fault,
and gating masks nothing. `PartySequenceTests` keeps it that way — it fails if any roster ever owns
more than a third of a slot in the running order.

This is the cheap version of the D28 guard again. "Always third" is a strong claim about a
deterministic system, and it would have been entirely reasonable to go and find something wrong with
the LCG. Nothing was wrong with it.

**What changed instead.** THE SKIRMISHERS and the new THE COVEN carry `FirstRound = 4`, so neither can
appear before raid five. The roster table also grows from six to nine, which is the other half of
"make the teams vary more" and costs nothing but a table — a bigger pool repeats less on its own, with
no change to the picker at all.

**Reinforcements are per roster, and two of them are load-bearing.** THE UNSHRIVEN is "no healer at
all" and THE SKIRMISHERS is "no tank"; those absences are the roster and are what the on-screen
warning promises. Growing them with generic filler would have quietly made both warnings lies by raid
nine. Asserted at every size from four to nine.

## 2026-08-16 — D42. What growing the party costs the league, measured twice because the first was wrong

Party size scales the economy because the energy rate sums **per member**. The size of that effect was
guessed at 2.25x (nine over four, straight off the headcount), and guessing was wrong twice over.

**First measurement: 1.26x.** Mean harvest across the nine rosters went 35.1 at four to 44.3 at nine.
That figure is real and it is nearly meaningless, because the raids it came from used **no verbs** —
the party strolls through and leaves. 35.1 sits almost exactly on D40's un-bonused strolled figure of
38.5, which is the tell. A test named for a comparison with the rival ceiling was measuring the one
case that never approaches it.

**Second measurement, raids the player actually works** — doors shut, monsters fed in, built the way
`RaidRulesTests` builds its stalled raid:

| | raid 1, four | raid 9, six | raid 18, nine |
|---|---|---|---|
| strolled | 38 | 39 | 43 |
| **held and fought** | **240** | **305** | **433** |

**1.81x**, and the number that matters is the last cell: `LeagueTable.GoodRun` is **430**. A
late-season raid played well now lands **on the rivals' ceiling**.

**What that means, and it is the author's call.** D20 handicaps rivals a tenth below the player's own
range so that a good raid cannot be beaten by luck. That promise is intact early — 240 against 430
leaves plenty of room to lose. By raid eighteen there is none: a worked raid meets the best a rival
can possibly roll, so the closing rounds stop being a contest and become a formality for a player who
is still paying attention. Whether that is a satisfying crescendo or a flat ending is a judgement
about how the game should *feel*, which is not a thing to settle from a test.

Levers, cheapest first: raise `GoodRun` so the ceiling stays above a grown party; cap growth below
nine; or let it stand, on the grounds that a player who has survived seventeen raids has earned an
easy finish.

**The lesson is the older one.** D31, D28 and the pale bands all have the same shape, and so does this:
the first measurement was not noise, it was a **precise measurement of the wrong thing**, and it looked
authoritative. What caught it was not a better instrument but noticing that 35.1 was a number this
project had already written down somewhere else, for a case nobody had asked about.

## 2026-08-16 — D44. A recorded raid, read by Gemini, and what it says about parties of nine

The author asked for a SessionRecorder capture analysed by Gemini. Done: `RaidRecordingTests` records
a raid into `.mooserunner/Recordings/`, and `recording_extract_and_analyze` sends a segment out.

Most of the verdict is a clean bill of health, and usefully specific about things that have been
faults here before: the adventurers **walk through the doorways rather than sliding over them**, none
of them **stand inside the solid walls**, the HUD text and the review screen are legible, and there
are no rendering artefacts. Wall violations and overlay bleed have both been real defects in this
project, so a direct look confirming they are absent is worth more than the assertions that already
covered them.

**The finding that matters is the one nobody had asked about:**

> "there is significant character overlapping… their sprites merge into a single, dense cluster…
> it makes it difficult to distinguish the individual characters"
>
> "[health bars] overlap and stack on top of one another when the party bunches up, creating a
> slightly cluttered visual pile"

That is a party of **four**.

**Today's ramp sends nine** (D39, D42). Four already merges into a pile the model could not read
individual figures out of; nine will be materially worse, and it lands on the one piece of feedback
the author has already asked for twice. D8 exists because the party's state could not be read and
deaths were arriving unseen — *"wtf, I did not see that"* — and the answer was per-member health
bars. Nine bars stacked in a bunched column is that problem returning by a different route.

Nothing has been changed for it. The measurement is a four-body raid and the claim would be about a
nine-body one, and this ledger has enough entries about believing a plausible story before checking
it. **The next step is a recording of a late-season raid**, which needs the party ramp driven to nine
through the league rather than a raid built by hand.

Candidate answers, if it does reproduce: fan the formation out laterally as it grows rather than
lengthening the column; draw bars only for the wounded; or one party-wide bar with a count. All three
are cheap. Which is right depends on what the frame actually looks like.

## 2026-08-16 — D45. Nine adventurers cannot wear their health bars on their heads

D44 flagged that a party of four already bunched into a cluster. Measured at **nine**, which is what
this session's ramp sends, and it is worse than flagged. Gemini, reading a verified nine-strong
recording:

> at the worst moment only **4 or 5 of 9 figures** are distinguishable, and **3 or 4 of 9 health
> bars**; "a single bar draining to yellow or red in the middle of the stack would be masked by the
> overlapping green bars around it"; a death mid-cluster would be "completely obscured".

**That is D8 returning word for word.** The bars exist because the author played it, could not read
the party's state, and deaths were arriving unseen — *"wtf, I did not see that"*. Shipping nine-strong
parties reintroduced the exact condition the bars were added to remove.

### The fix helped, and the verdict on it was wrong

Bars are now staggered by marching rank (`PartyBars.BarPitch`), so nine read as a ladder rather than
one block. Re-analysed, Gemini reported the bars "form a dense, unreadable block" with **"no vertical
separation"**.

**That is false, and the frame proves it.** `recording_extract_frame` at t=6s shows a clear diagonal
staircase of separated bars, each with a visible gap. The model's verdict flatly contradicted the
pixels it was given.

Worth recording as its own lesson: the video analysis is a genuinely useful instrument — it found
this defect unprompted, and it was invisible to 273 green tests — but **it is a witness, not a
judge.** The same guard applies to it as to every other instrument here (D28, D31, D36–D38): when it
makes a strong claim, look at the frame.

### What the frame shows that nobody asked about

The stagger works and creates a second problem: with nine members the ladder spans about a cell and a
half, so the upper bars **drift clear of the bodies they belong to**. Readable, but no longer
attributable. Over-head bars cannot separate nine bunched sprites and stay attached to them — the
stack has to go somewhere, and there is nowhere near the owner for it to go.

So this is a design decision rather than a bug fix, and it is the author's:

1. **Keep the stagger.** Wounds and deaths become visible again, which is what D8 asked for, at the
   cost of the bars sitting above-left of their owner in a crowd. Shipped, because a readable bar in
   roughly the right place beats an unreadable one in exactly the right place.
2. **A roster panel in the HUD** — one row per member, role and health, always legible at any party
   size. Satisfies D8's "continuous, colour-coded" and never overlaps. The most robust, and the
   largest change.
3. **Fan the formation laterally as the party grows**, so nine bodies do not occupy four bodies'
   worth of floor. Fixes the sprite pile as well as the bars, and is the only option that touches
   gameplay — which is why it is not being done unilaterally.

The frame is the thing to look at before choosing.

### D43 addendum — the assumption D43 rests on has now been measured

D43 concluded that the flip's one remaining failure is the vertical geometry, and that conclusion
rested on an argument rather than a measurement: *party-only straight pathing is a no-op on a
horizontal dungeon, because the goal is due east and the old bias already pointed east, so
horizontal-plus-the-fix is just `main`.* Reasonable, load-bearing, and untested — which is the
combination this ledger keeps getting caught by.

Measured. Party-only `preferStraight` applied to the **horizontal** layout, trail and rooms
untouched:

```
best wipe = 166, best survival = 434      <- identical to plain main
7/7 ExploratoryTests green
```

The no-op holds. So the 2x2 is complete and reads:

| | horizontal | vertical |
|---|---|---|
| no straightening | 434 survival | (25% slower, cannot cross five rooms) |
| straighten everything | **0 survival** | 0 survival |
| straighten the party only | **434 survival** | **0 survival** |

The bottom row is the whole answer. Same pathfinder, same monsters, same party speed, and the only
difference left is which way the corridor runs — so parties that survive a maximal ambush going east
do not survive going north, and that is a fact about the rooms rather than about the port.

Nothing changed on `main`; this was a diagnostic and the working tree was restored.

### D43 addendum 2 — it is not crowding, it is that the party cannot break away

D43 left "why is a vertical room deadlier" unanswered. The obvious guess was that vertical geometry
lets more monsters reach the party at once. **Measured, and that guess is wrong.** Peak monsters in
contact, same seed, same rosters, maximal ambush:

```
BALANCED   7 / 7      GLASS CANNONS 5 / 5      PHALANX     10 / 10
IRONCLADS  9 / 9      ARCHERY LINE  5 / 4      SKIRMISHERS  5 / 5
PILGRIMAGE 10 / 9     UNSHRIVEN     7 / 5      COVEN        5 / 4
                                               (vertical / horizontal)
```

Near-identical. Crowding is not the difference.

**The difference is sustained contact**, and it appears in exactly the two rosters that survive the
ambush horizontally and die vertically:

| roster | mean in contact, vertical | mean, horizontal | horizontal outcome |
|---|---|---|---|
| THE IRONCLADS | 3.0 | **1.1** | TimeExpired, 434 harvested |
| THE COVEN | 1.1 | **0.3** | TimeExpired, 189 harvested |

Horizontally those two spend most of the raid **out** of contact — they break away and kite.
Vertically they never get free, and a party that cannot disengage dies to an endless stream whatever
its stats. Every other roster has near-identical means and dies on both layouts, which is what makes
this two-roster split the whole signal rather than noise.

**A concrete suspect, not yet confirmed.** `AdventurerAI.StandOff` picks the direction to back away
in as `self - target`, and when a body is standing exactly on a monster it falls back to a hardcoded
`Vector2.left`. On an east-west dungeon "left" is *back down the corridor the party came from* —
a real retreat. On a north-south one it is *into the side wall*, two cells away, where the body stays
in contact and keeps being hit.

That is a hypothesis with a mechanism and it has not been tested. The honest next step is to try a
fallback that means "away from where we are heading" rather than a compass direction, and re-measure
these two means. If IRONCLADS and COVEN come back to 1.1 and 0.3, the flip is finished.

Recorded rather than acted on because the branch is parked and this is one more change on top of a
change the author has not seen yet.

### D43 addendum 3 — the StandOff suspect was wrong, and why it was never plausible

Addendum 2 named a suspect for the vertical layout's sustained contact: `AdventurerAI.StandOff` falls
back to a hardcoded `Vector2.left` when a body is standing on a monster, which is a retreat down an
east-west corridor and a walk into the wall of a north-south one. Good story, clear mechanism.

**Tested and refuted.** Replacing the fallback with "back away from where the party is heading"
changed *nothing*: THE IRONCLADS stayed at a mean 3.0 in contact and wiped, THE COVEN at 1.1 and
wiped, every roster's figures identical to the digit.

**The reason is visible in the line itself, and should have been checked before the run.** The
fallback is guarded by `offset.sqrMagnitude < 0.0001f` — it fires only when a body is at *exactly*
zero distance from a monster. That is a measure-zero case in continuous space; it essentially never
executes. The branch was read for what it said and not for how often it runs.

That is the D28 shape once more, and cheaply caught this time because the prediction was specific
enough to fail: 3.0 and 1.1 were supposed to fall to 1.1 and 0.3, and they did not move at all. A
vaguer claim — "this should help disengagement" — would have survived the same result.

**So the mechanism is still open.** What is known: peak contact is identical across layouts, so it is
not crowding; sustained contact differs, and only for the two rosters that survive horizontally; and
it is not the standoff fallback. The next places to look are the ones that actually run every tick —
`Spacing`, the room clamp inside `StandOff` that stops a body backing out of its room, and the
retreat in `Party` that picks a refuge. The room clamp is the most promising: it walks candidate
distances inward from the requested range and takes the first that is walkable *and in the same room*,
which in a five-wide room may simply have fewer valid answers along one axis than the other.

Nothing changed on `main` or on the branch; both were restored.

### D43 addendum 4 — the party flees LESS vertically, and walks instead

Measuring behaviour rather than reading code, which is the lesson of addendum 3. Share of ticks each
roster spends in each action under a maximal ambush, same seed both layouts:

| roster | vertical | horizontal | outcome v / h |
|---|---|---|---|
| THE IRONCLADS | Fight 28, **Flee 32**, Walk 35 | Fight 19, **Flee 52**, Walk 25 | wiped / **survives** |
| THE COVEN | Fight 0, **Flee 45**, Walk 38 | Fight 0, **Flee 63**, Walk 24 | wiped / **survives** |
| THE BALANCED PARTY | Fight 13, Flee 34, Walk 30 | Fight 13, Flee 35, Walk 31 | wiped / wiped |
| THE PHALANX | Fight 35, Flee 16, Walk 47 | Fight 46, Flee 14, Walk 38 | wiped / wiped |

**The two rosters that survive horizontally flee about twenty points more there than they do
vertically, and spend precisely that time Walking instead.** The two that die on both layouts are
unchanged to within a point, which is what makes this the signal rather than noise.

So the earlier phrasing — "cannot break away" — was close but wrong in an important way. It is not
that fleeing fails. **The party enters the flee state less often** and falls back to ordinary walking,
which does not shed pursuit.

**Next place to look, and this one demonstrably runs**, unlike the standoff fallback that was refuted:
`Party` line ~425 retreats with `MoveAlongPath(leader, refuge, ...)`, and `MoveAlongPath` returns
immediately when `FindPath` comes back empty. This codebase has already been bitten by exactly that —
the comment at line ~722 records a case where it "had nowhere to go, and the party STOOD STILL FOR
THE REST OF THE RAID". If the refuge is chosen in a way that has fewer valid answers in a tall narrow
grid than a wide flat one, the retreat silently degrades into a walk, and the numbers above are what
that looks like from outside.

Not pursued further: the branch is parked, the author has not seen the flip at all yet, and this is
now three layers of inference deep. Recorded so the next session starts from a measurement instead of
from the top.

### D43 addendum 5 — addendum 4 is CONFOUNDED, and the confound is large

Addendum 4 concluded that the party "enters the flee state less often" vertically, from action shares
of 52% against 32% for THE IRONCLADS and 63% against 45% for THE COVEN. **That conclusion is not
established, and the reason is visible in one column that was never logged: raid length.**

Horizontal durations, same run:

```
THE IRONCLADS   64.0s  TimeExpired     THE BALANCED PARTY  24.4s  wiped
THE COVEN       64.0s  TimeExpired     THE PILGRIMAGE      24.3s  wiped
                                       THE PHALANX         25.7s  wiped
                                       everything else  17-19s   wiped
```

The two rosters the whole finding rests on are exactly the two whose raids run **three times longer**.
Their action shares are measured over a 64-second window; the vertical raids they die in are a
fraction of that. A party that survives to the clock accumulates a long tail of alive-and-fleeing
ticks that a party dying at twenty seconds never reaches.

So the direction of causation is unresolved. **Fleeing more may keep them alive, or living longer may
simply give them more time to flee** — the data cannot distinguish those, and addendum 4 asserted the
first.

What survives from addendum 4: fleeing shares differ, and the two rosters that die on both layouts are
unchanged. What does not survive: any claim about why.

**The fix for the measurement is to compare like windows** — the first eighteen seconds, which every
raid on both layouts has — rather than whole-raid shares. That is one run and it was not done here.

This is the third time today the same shape has appeared: D42's harvest figure was a precise
measurement of walkthroughs rather than played raids, addendum 3's suspect was a branch that never
runs, and this is a share computed over incomparable windows. The common factor is not carelessness
about the number; it is failing to ask **what else differs between the two things being compared.**

### D43 addendum 6 — the like-for-like window kills addendum 4, and reverses the picture

Addendum 5 said the repair was to compare a window both layouts reach. Done, at eighteen seconds:

| roster | fleeing, vertical / horizontal | mean health, vertical / horizontal |
|---|---|---|
| THE BALANCED PARTY | 27% / 29% | **86% / 69%** |
| THE IRONCLADS | 25% / 26% | **85% / 61%** |
| THE PILGRIMAGE | 37% / 37% | 90% / 89% |
| THE PHALANX | 13% / 14% | **86% / 67%** |

**Fleeing is identical.** Addendum 4's mechanism — "the party enters the flee state less often
vertically" — is refuted. It was an artefact of comparing a 64-second raid against a 20-second one.

**And the second column reverses the whole picture.** At eighteen seconds the vertical party is
*healthier*, by twenty points and more on three of the four rosters that are still whole. The vertical
raid is not going wrong early. It is going **better** early and failing later.

Everything said so far about the mechanism assumed the party was being ground down from the start.
That assumption is now measured and false, and every hypothesis built on it — crowding, disengagement,
the standoff fallback, the retreat path — was aimed at the wrong half of the raid.

**Where to look next, and it is a different place entirely:** whatever happens after the first twenty
seconds. The candidates are the things that only matter once the party is deep — the grind decay, the
fatigue slow at ten seconds of unbroken combat, and how far the party gets before the clock runs down.
A party that is healthier at eighteen seconds and dead by sixty is a party that went further and met
more, not one that was overwhelmed at the door.

Three hypotheses were built and killed here, and the cost was mostly in the framing rather than the
runs: each was a plausible story about the first half of a raid whose second half nobody had looked
at. The instrument that finally settled it — measure the same window on both sides — cost one run.

### D43 addendum 7 — walking the clock: it is a sudden collapse, and the 434 is one dying body

Addendum 6 said to stop guessing and look at the second half of the raid. Sampling every ten seconds
under the maximal ambush, THE IRONCLADS on both layouts:

```
vertical      10s: 4 @ 99%   20s: 4 @ 80%   -> WIPED at 26s
horizontal    10s: 4 @ 93%   20s: 4 @ 51%   30s: 1 @ a sliver   ... TimeExpired at 64s
```

Monster counts at each mark are within one of each other, so this is not a density difference.

**Two things fall out, and both change the shape of the problem.**

**1. It is not a grind, it is a collapse.** The vertical party is healthier at *every* sample — 99
against 93, then 80 against 51 — and then goes from four bodies at 80% to wiped inside about six
seconds. Nothing about "ground down from the start", "cannot break away" or "faces more monsters"
describes that. Every hypothesis in addenda 2 through 4 was a story about attrition, and this is not
attrition.

**2. The 434 the whole test hinges on is a single dying adventurer.** Horizontally the party does not
survive in any ordinary sense: three die by thirty seconds and the fourth is pinned at a sliver of
health for the remaining thirty-four. That body is worth an enormous amount — the wound curve pays
8x and more below 5% health, which is the design working exactly as intended — and it is the entire
difference between `bestSurvival = 434` and `bestSurvival = 0`.

So `KillingTheParty_NeverPaysBest`, in this scenario, turns on whether one nearly-dead body happens
to outlive the clock. That is a real property of the game and a very thin thread for a layout change
to be judged on. **The test is not wrong** — the rule it defends is the one the whole design rests on
— but the maximal-ambush scenario is a poor instrument for it, because the outcome is decided by a
single body's last few seconds rather than by whether the dungeon pays for keeping a party alive.

**What this means for the flip.** The remaining failure is real but far narrower than it has looked:
vertical parties collapse suddenly around twenty to twenty-six seconds where horizontal ones bleed out
slowly and leave a survivor. The next question is what happens in those six seconds, and it should be
asked with a *recording* rather than another aggregate — this ledger now has four dead hypotheses
built from summary statistics and one finding that came from looking at a frame.

Investigation stopped here. The branch is unchanged and unmerged; `main` is untouched.

### D43 addendum 8 — the frame, at last, and what it shows

Four hypotheses about the vertical collapse were built from summary statistics and all four died.
Addendum 7 said the next question should be asked with a recording. It was, and the answer is visible
in a single frame at nine seconds:

**Seven slimes spanning the full width of the corridor, sitting on the party, which is pinned at the
bottom against the entrance.** Damage numbers in that one moment read -98, -45, -19, -8, -8, -4, and
the HUD shows `+ CROWD x7`.

That is not attrition, not disengagement, and not pathing. It is a **wall of monsters across a
corridor the party cannot go around, with the party backed against the one wall it cannot pass
through** — and the wall behind them is the carved entrance from this same session, which is a
`Doorway` with no `Door` and therefore, by `IsWalkable`, passable to nobody. A party pushed back to
the entrance in a vertical dungeon has literally nowhere to stand.

**This is a hypothesis with a picture rather than a proof**, and the honest test is whether the
horizontal layout pins the party the same way — if it does, the collapse is about the ambush scenario
rather than the axis, and the flip is exonerated. That comparison has not been run.

But the shape of the day is worth keeping regardless. Five attempts at this mechanism:

| attempt | built from | outcome |
|---|---|---|
| crowding | peak contact counts | refuted, peaks identical |
| the standoff fallback | reading a branch | refuted, the branch never runs |
| fleeing less | whole-raid action shares | refuted, confounded by raid length |
| ground down early | shares again | reversed, the party is HEALTHIER early |
| a wall of slimes at the entrance | **one frame** | the first that explains the numbers |

The project's own doctrine says green tests hide a broken rate and to photograph the game. Four of
these five were aggregates over a simulation nobody had watched, and the fifth took one look.
