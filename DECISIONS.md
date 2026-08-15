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
