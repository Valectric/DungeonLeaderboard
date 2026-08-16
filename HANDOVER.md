# Handover

## Six decisions waiting for you — nothing else is blocked

Every milestone is built, tested and shipped, and the suite is green at **353 tests** across five
assemblies. These are judgements rather than work, and each is now as well-evidenced as measurement
can make it.

**The two newest are from your 2026-08-16 pass and are the ones to read first.**

**A. The permanent room bonus is built but NOT shipped — `room-bonus-permanent`, D40.** You asked for
"+2/s for the rest of the run per room" and it is implemented faithfully. It takes the
stall-versus-stroll ratio to **2.09x against a 2.5x floor**: the party that walks through and leaves
gains **3x** where the party that stays and bleeds gains 2x, because paying per room pays for
*advancing* — the behaviour the door verb exists to prevent. That test was already narrowed once for
this exact cause and its comment ends *"do not lower it twice"*, so it was not lowered. Three options
in D40; the recommendation is **pay the bonus only while the party is engaged**, which is the one that
leaves both rules standing. One small edit either way.

**B. A late-season raid now lands on the rivals' ceiling — D42.** Growing the party to nine lifts a
worked raid from **240 to 433**, and `LeagueTable.GoodRun` is **430**. D20 handicaps rivals a tenth
below your range so a good raid cannot be beaten by luck; that holds at raid one and has no room left
by raid eighteen. Levers: raise `GoodRun`, cap growth below nine, or let it stand because a player who
survived seventeen raids has earned an easy finish. Nothing applied — how the endgame should *feel* is
not settleable from a test.

**1. Buy a tileset, or keep grading this one.** `Tools/grade-walls.py` hits both moodboard ratios
exactly — wall/floor 0.98, rim/wall 1.94 — and the result **looks worse**, flatter and less legible
(`Screenshots/grade-compare.png`). That is the evidence that the ratio is necessary and not
sufficient: the reference holds wall and floor at one value *and* keeps them readable, using hue and
drawn masonry this stone does not carry. See **D32**. Candidates, both verified by downloading and
looking: **Szadi Rogue Fantasy Castle, $3.20**, 16x16, PSD, licence explicitly public-domain so the
PNGs can live in this repo; or **0x72 `dungeontileset-ii`, CC0**, whose file list carries named
boundary pieces (`wall_outer_front`, `wall_edge_left`, `doors_leaf_open/closed`). **Do not buy
Seliel** — best art in the survey, and the Mana Seed licence forbids use alongside AI-generated
content, which this project is.

**2. What to do about two inert halls.** The party reaches exactly **three rooms in sixty seconds**
whatever the dungeon's size and whoever walks in — confirmed across three seeds, identical harvest to
the pound at 3, 4, 5 and 6 rooms. `MaxRooms` is 5 against one starting room, so the shop sells four
halls and two of them cannot affect the score. See **D29** and its addendum. The three fixes point in
opposite directions: `MaxRooms` down to 3 makes the shop honest and shrinks the game; a faster party
or longer clock changes the sixty seconds the title is about; a depth multiplier rewards building
forward but is a **new rule**, which SPEC.md forbids until the three verbs are proven.

**3. Is a quarter the right win rate?** Across three seeded seasons and four play-styles the bot wins
**3 of 12** and reaches round 9 or 10 in every one. That number was chosen by nobody — it fell out of
pricing the rivals against what the game can actually produce (**D27**, re-proved properly in
**D31**). The bot is explicitly "a floor on competence, not a good player", so a human should beat a
quarter. Whether that is the shape you want is yours.

**4. Should round one be able to punish a player at random?** Measured across five seeds
(**D34**): the survival threshold is 35–75, an opening board played competently is worth 342 and
survives on **5 of 5**, and doing nothing banks 51 and survives on **3 of 5**. So D20's "round one is
sudden death" is half right — it is not death for anyone who touches a spawner, with a 4.5x margin.
What it is instead is an unreliable lesson: the player who most needs to learn that an untouched
dungeon earns almost nothing learns it on a coin toss. If the first round is meant to teach, the
lever is raising the floor slightly rather than protecting the player from it. `OpeningRoundTests` is
the instrument.

---

**State: M1–M13 built, tested and SHIPPED. `main` is green at 338 tests and itch is serving
`0.1.2608161145`. Nothing is held on a branch.**

The whole loop runs: a six-second `DUNGEON LEAGUE` title, standings, a raid, the adventurers'
review over generated key art, a thirty-second spatial shop, the next raid — and the league ends
in a winner.

Since M8: walls are solid (bodies inside a wall fell from 11.7% of samples to 1.8%, shots through
one from 13.7% to 0.5%); monsters prefer the tank, which is a rule now rather than a side effect of
the marching order; the rate pays for variety — a disarm, a new room, a crowd — and decays through
a long grind, with a HUD line naming which of those is currently moving it; a tired party slows to
80%; and wounded bodies back off, tanks giving ground below 30% of their own health.

**D30's first item is now done, the second still stands.** The rival earnings HAVE been retuned —
`GoodRun` read 500 and no raid has ever harvested it, so the ceiling sat above the game's own maximum
and inverted the promise D20 and D25 rest on. Corrected to the measured 430; the best of four
play-styles went from round 7 to round 9 and now contests the final. See **D27**. Unchanged:
`EarlyEscape`'s threshold must not be lowered a second time — if it needs it, the room bonus is too
strong and that is what to fix.

**The newest finding, and the biggest open one: D29.** The party reaches exactly three rooms in sixty
seconds whatever the dungeon's size, so harvest saturates at 446 and every hall after the third earns
nothing. `MaxRooms` is 5 against one starting room, which means the shop sells four halls and **two
are inert**. `RoomsPayTests` is the instrument. Not fixed, because the three candidate fixes point in
opposite directions and one of them is a new rule SPEC.md forbids until the three verbs are proven.

**A season is reproducible at last — D31.** `GameController.SeedOverride`. The seed was threaded
correctly through the league, the party chain and combat, and there was no way to *set* it, so every
season-long measurement was a different season while reading as if it were not: unchanged code
returned best-of-four rounds of **7, 9, 9, 10 and 10** across five runs. **Treat any single-season
figure written before D31 as an anecdote.** Re-measured properly on three seeded seasons, D27 holds —
`GoodRun` 500 gives 8/7/8 and no wins in twelve, 430 gives 10/9/10 and three.

**Three corrections were made to this file's own claims on 2026-08-15, and they share one shape.**
D28 (a tileset gate that ranked the fix below the fault, "confirmed" by a second method that was the
same method), the closed pale-bands section at the bottom (a defect measured out of a screenshot the
suite was overwriting between reads), and D31 above. In each, **the instrument was never asked to
prove it could tell two known-different cases apart** — which costs one command and would have caught
all three.

**And the same for fixes: fix the class, not the screen you photographed.** The raid's world-space
overlays were found lying across the standings, and it took three passes to finish — first the
party's bars on the league screen, then the collapse screen the first fix had named its way past,
then four monster health bars on the winning ending. That last one hid because the widened check
lived in `PhaseLookTests` and the winning ending is only reachable from `RunProgressionTests`, so the
check never ran there. `DungeonView.HideRaidOverlays` now takes every collection there is.

**Read first, before diagnosing anything:** the RaidManager and ShopManager suites now need ~1600s,
not 800s, because raids that used to end early run the full clock. **`Dungeon.Game.Tests` takes 5m07s
for its 99 tests** — measured, 2026-08-16, after D31 tripled `RunProgressionTests` to twelve seasons.

**A silent run is not a slow one, and this note said otherwise for an hour.** A client sat waiting 47
minutes; the tempting story was that the tripled sweep had made the suite enormous, and that went
into this file as "~2400s". It was wrong by a factor of eight. What actually happened is that the
test request was **lost after the recompile** — `status` read `RECOMPILE_COMPLETED`, so nothing had
started.

The diagnostic that settles it costs nothing and does not touch the daemon (which matters, because a
concurrent `status` is consumed by a waiting `test` and hangs it):

```
sample the Unity process's CPU over 20 seconds
  delta ~0s  -> idle, the run never started or already died
  delta >1s  -> genuinely working, wait
```

Recover by killing the stranded `mooserunnerCli` client, then `ping`, then re-running. No `reset` was
needed. And when a "performance
regression" or a "hang" appears, check whether the SIMULATION cost changed before believing it —
three separate toolchain faults wore that disguise in one session (Safe Mode after a compile error
that `force-recompile` reported as `[PASS]`, a CPU affinity left pinned after a build, and plain
editor wear). None of them had.

**Verify the shipped build, not just the suite.** On 2026-08-15 a green run of 98 Game tests, a full
E2E pass and every Look test all missed a green health bar and a blue mana bar lying across the
league standings — the title screen, and the first thing a jam voter sees. It was found by opening
the itch page. Nothing photographed `Phase.Standings`; `PhaseLookTests.TheLeagueScreen_ShowsNoHealthBars`
now does.

Last updated: 2026-08-15 (evening).

> **Latest pass (M8), directed by the author after playing M7:**
>
> - **Bought halls arrive empty.** A hall used to come with a spawner and a trap in it, bundling two
>   fittings the player never chose. Only the opening corridor is furnished now — an entirely bare
>   dungeon has no verb to press and earns the idle rate, so round one would be unplayable.
> - **Spawning is a loan.** The 25 leaves the core while the monster lives and returns when the party
>   kills it, so the player is only out of pocket for monsters still standing at the bell. It returns
>   to the **purse only, never the score**, so the league's balance is untouched and only spending
>   power changes. Shown as a `+25` rising off the corpse, not a particle burst — a burst there would
>   stack on the death effect, and an unhandled effect kind falls through to the *door* visual.
> - **The survivors get better.** Only the rivals' floor rises as the field shrinks; the ceiling stays
>   at ninety per cent of a good raid in every round, so a good run stays unbeatable in the final
>   exactly as in round one. The worst round a rival has climbs from 33 to 440.
>
> **And a real bug the soak found**, invisible to the dedicated containment test that passed 5694
> assertions beside it: *"a Skeleton left room 1 for room 0"*. Monsters chase the nearest party
> member, but the room check was on the party **leader** — a straggler across a threshold could be
> the nearest body and pull a mob straight out of its room. That is the retreat valve failing at the
> one moment it exists for. Bounded now at both the quarry choice and the landing cell, because
> charging straight at a quarry skips the cell-by-cell path that was doing the only checking.
>
> **What the league actually asks**, measured because the soak only asserted a competition
> *resolves*: the player needs **400 a round** to win and **never wins below 375**.

> **Read D12 and D13 in `DECISIONS.md` first.** Two bugs were found overnight by exploratory sweeps,
> both of which had shipped, and both of which broke a rule SPEC.md states outright. Neither was
> visible to any feature test.
>
> - **The energy curve was unreachable.** The rate never passed **4.1/s in a game built to reach 32**,
>   and **a wipe out-earned every raid the party survived** — SPEC's central inversion, backwards in
>   the live build. The curve now reads the party's *worst* survivor rather than an average.
> - **The league was unwinnable.** Ten seasons across ten seeds all finished in **exactly 18th**,
>   however well the bot played, because rivals earned 830 a round against an achievable 292. Rivals
>   now average ~200 and the table answers performance.
>
> Both are balance-adjacent and both are yours to re-tune. The tests pin the *ordering*, not the
> numbers.

> **Since then (D14–D16), three more that had shipped:**
>
> - **The game was completely silent.** Every sound synthesised correctly and played into a scene
>   with no `AudioListener` in it — the play scene is generated, and a code-built camera does not
>   bring one. Six green audio tests could not see it; the mandatory console sweep after a *passing*
>   run did.
> - **Combat was invisible.** Two health bars changed length while every sprite stood perfectly
>   still. Sprites now walk, face where they are going, and attack in a shape particular to their
>   role.
> - **The shop sold *what* and never *where*.** It is spatial now: buy onto the dungeon you are
>   looking at. This is the author's design, and it earns its keep — aiming purchases harvests 397
>   against 242 for scattering them.
>
> The shop rework surfaced two layout bugs no model test could see: a tile menu opened low on screen
> put its bottom row over the **Ready** button, which is hit-tested first, so buying the last item
> started the raid instead; and the itch embed's 0.4 UI scale made menu rows **twelve pixels tall**,
> drawn correctly and impossible to hit.

---

## What changed in the 2026-08-14 session, and why it matters

The author played the build and directed a redesign. Six things landed; two are still on their desk.

**The energy curve is per person, per action** (D-notes in `EnergyCurve`). It used to ask one
yes-or-no question of the whole party — is it in combat? — and multiply by its worst survivor. That
made *being wounded* the only thing that paid, and everything else followed from it. Now every living
member earns for what it is doing (walking lowest, then fleeing, working, shooting, melee highest)
times its own health. A corpse earns nothing and costs **50 points**.

| | before | after |
|---|---|---|
| central invariant (wipe vs survival) | 161 vs 169 | **4 vs 214** |
| roster spread | 9.3x | **2.9x** |
| the two tankless rosters | 4.1/s | **8.4/s** |

**Two changes the author asked for had to wait for that curve.** Faster archers and weaker monsters
both *inverted the game* under the old one, because anything keeping the party healthier made killing
it relatively more attractive. **Both are live now**: archers measure 4 vs 190, and monsters with two
and a half times less health measure 14 vs 231. A skeleton holds a party 7 seconds rather than 13, so
it is something you field several of.

Clearing the seven tests that blocked the nerf is worth reading (D22). **Not one needed its claim
weakened.** Five were measuring through a window sized on the old monster — including two about
*positioning*, which cannot depend on health at all, and which sampled at a hardcoded eight seconds
against a monster that now dies at six and a half. The tank and healer tests shared a helper that
spawned one monster and ticked thirty seconds, which is a short fight followed by twenty seconds of
walking. Only one was a genuine bound.

**The standoff is gone.** A player reported a skeleton "wagging back and forth" beside the glass
cannons without landing a blow. Reproduced exactly: every fragile role runs at anything within 1.7
cells, melee reach is 1.15, so the distance settled at 1.71 and neither side could touch the other —
48 seconds, 46 reversals, no damage either way. Monsters chase the *nearest* member now and close
straight in. That was the root cause of the 4.1/s rosters.

**The league is an elimination.** Everyone starts on zero, the bottom dungeon leaves each round and is
not replaced, and the last one standing wins — the game's first winning ending. Rivals roll between a
bad run and a good one then lose a tenth, so their ceiling is 450 against a good run's 500: *a
genuinely good raid cannot be beaten by luck*.

**The dungeon is a lattice.** Rooms grow in any of four directions from any room that is not boxed
in; the shop offers eight places to build on a three-room corridor. Growing left or down re-anchors
the grid, so purchases are translated to stay in their rooms.

**The party explores.** It heads for the nearest room it has not seen and leaves by the way it came
in once it has seen them all. That is what makes chests and monsters steer anything.

### Drawn animation (M7) — how the sprite pipeline actually behaves

**66 frames, ten cycles**: walk and attack for each party role, walk and attack for the skeleton, a
hop for the slime. Six frames each at twelve a second.

The `character` harness does **not** ask ImageGen to draw frames. It takes the sprite you attach as a
**source master and rigs it**, so the output is your own art articulated and cannot drift off-model.
That single fact is why verification works: compare the generated frame's **average colour and
opaque pixel count** against the source sprite. They should match within a few pixels. Nothing else
in the pipeline checks anything, and the agent's prose reports success either way.

Three traps, all caught by that comparison and all now in `CLAUDE.md`:

1. **A second character into the same workspace reuses the first one's rig.** A healer request
   returned six files named `tank_adventurer_march_down_*` measuring (96,81,68)/1115 — the tank
   exactly, against the healer's (102,95,69)/1254. **One `--workspace` per character.**
2. **Pasting the palette string recolours a rigged sprite.** The green-hooded archer came back purple
   with a pink face. When rigging, the source master *is* the palette — omit it.
3. **`--command` accepts only `sprite | animate | character | effect | pack`.** `creature` is a
   harness the router infers, not a command, and passing it fails the whole batch.

**The bug that mattered most had nothing to do with art.** `FrameFor` was never called: the edit
meant to redirect `view.sprite` matched nothing after an earlier refactor and silently did nothing.
Four cycles were imported, correctly named and loadable, and every adventurer showed one static pose.
`WalkingAdventurers_CycleThroughDrawnFrames` samples the **renderer** during a real raid and demands
more than one distinct sprite reach it — 1 before, 7 after.

### Three tests were found cementing bugs by asserting their symptoms

Worth internalising, because it happened three times in one session:

- `EveryComposition_EventuallyGetsThrough` asserted 5% progress on a door while its name promised
  everyone got through — two rosters never do.
- `TheLeader_RoutesAroundArmedTraps` never checked whether the trap was **armed**.
- `ClosingDoor_StallsTheParty` and `Clock_ExpiresAfterSixtySeconds` both passed *because of* a freeze:
  once the party forced its own door open, the next door was not on its room's threshold, no path to
  the boss existed, and it **stood still for the rest of the raid**. See D21.

---

## Open question for the author

**Answered: input works on itch.** The author confirmed a single click starts the raid. The earlier
suspicion was automation failing to reach itch's cross-origin frame, not a defect — and the
speculative canvas-focus patch was correctly reverted, since it *ate the first click* in the case
that already worked.

**Still open, both balance judgements (D20):**

1. **Round one is sudden death.** Everyone starts on zero, so one weak opening raid puts the player
   last and out with no banked score to absorb it. Levers: seed a small opening score, exempt the
   first round, or leave it as a sharp lesson.
2. **A full run is nineteen raids** — about nineteen minutes plus shops. Fine for a campaign, long
   for a jam voter. The lever is `LeagueTable.Size`.

---

## Read these first, in this order

1. **`SPEC.md`** — the author's design, verbatim. The authority on what this game is.
2. **`CLAUDE.md`** — architecture, working loop, and the toolchain traps. Several cost a day each in
   the sister project, and the sprite-generation section now carries four more that were paid for
   here.
3. **`PLAN.md`** — milestone order.
4. **`DECISIONS.md`** — D1–D16. Read before reversing anything.

---

## What exists now

**The whole loop runs.** The standings open the game, a key starts a raid, the raid banks into the
league and the positions shift, then thirty seconds of shop, then the next party. Purchases are
permanent for the season, so the dungeon grows across a run.

```
Assets/Dungeon/
  Application/Game/   GameController (phases, clicks, HUD), DungeonView (all rendering),
                      LeagueScreen, ShopScreen, Scenes/Raid.unity (generated -- never hand-edit),
                      Tests/ (E2E + verb clicks + shop clicks)
  Modules/
    RaidManager/      EnergyCurve (the formula), Raid (clock, verbs, combat, end conditions)
    DungeonManager/   DungeonGrid (cells, rooms, doors, BFS), DungeonLayout (the corridor)
    PartyManager/     Adventurer (roles, wounds), Party (march, chests), AdventurerAI (per role)
    MobManager/       Mob, MobPack (spawning and room-bounded pursuit)
    LeagueManager/    LeagueTable (standings, rivals, relegation), DungeonNames
    ShopManager/      Shop (thirty seconds, six items, Ready bonus), Loadout
    AudioManager/     SfxSynth (every sound synthesised; no audio assets ship), AudioFacade
  Editor/             scene builder, WebGL builder, sentinel poller, pixel-art importer
```

**201 tests green**, console clean.

Two numbers worth carrying: an unopposed party crosses the corridor in **26.9s** of the 60, and one
bought chest adds **5.6s** to that. Both are asserted, because both are rates and rates are what this
game is made of.

**49 sprites**, all 64x64 at PPU 64 so one tile is one world unit. 38 were cut from the moodboard by
`Tools/extract-moodboard.py`; 11 tiles were sliced from a generated atlas by `Tools/slice-tileset.py`.
Both are deterministic and rerunnable.

---

## Seven bugs that a fully green suite did not catch

Worth reading before trusting any future green run. Each was found by looking at the game or the
console, never by an assertion.

1. **Every verb was dead.** The project runs the Input System package, so each legacy
   `UnityEngine.Input` call threw once per frame. The E2E passed because it called
   `raid.SpawnMob(...)` directly instead of clicking. A test that reaches past the input layer
   cannot fail when the input layer is what broke. `VerbClickTests` now drives all three verbs
   through `GameController.ClickAt` at real screen positions, and one test asserts the frame loop
   logs nothing.
2. **The game was unplayable from frame one.** Starting energy was zero, the cheapest verb cost 25,
   and an idle party earns 0.05/s — five hundred seconds to afford the first action, inside a
   sixty-second raid. Fixed by `Raid.StartingEnergy`; guarded by
   `Player_CanAffordAVerb_OnTheFirstFrame`.
3. **Mobs evaporated.** The party dealt 41 dps into a 90 hp skeleton, so a fight lasted 2.2 seconds
   and a whole raid harvested 11.7 energy. Fight length is a *rate*, and rates are what this game is
   made of. Guarded by `AFight_LastsLongEnoughToEarn`.
4. **A prop was drawn on top of a bought spawner.** Decoration spots are `(centre.x-1, centre.y+2)`,
   which is exactly where the first shop-bought spawner lands, and props sort above spawners. The
   slime pit was paid for, present in the layout and tappable — and completely invisible under a
   banner. Nothing in the model was wrong, so nothing in the model could catch it. `Decorate` now
   skips occupied cells; `NoPropIsDrawnOverSomethingTappable` guards it, and that guard was checked
   by reverting the fix and watching it go red.
5. **The shop's item descriptions were invisible.** On affordable cards only — those render far
   lighter than their colour values suggest, so the grey used everywhere else became grey on grey. It
   was legible only on the *unaffordable* cards, i.e. exactly backwards. **IMGUI never appears in an
   editor camera screenshot**, so no `Look` test can ever see this class of defect: the shop has to
   be opened in a browser.
6. **The energy curve could not be reached.** The rate never passed 4.1/s in a game whose curve tops
   out near 32, and a wipe out-earned every raid the party survived. A tank carries 220 of the
   party's 500 hit points and soaks nearly everything, so it hits death's door beside three untouched
   allies — and a fifth-power curve barely stirs at a 77% average. See D12. Found by
   `ExploratoryTests`, which plays every roster against every policy.
7. **The league could not be won.** Ten seasons across ten seeds finished in *exactly* 18th, because
   rivals earned 830 a round against an achievable 292. The standings were a backdrop. See D13. Found
   by `SeasonSweepTests`, which plays whole twelve-raid seasons — a class of bug that is invisible in
   any single raid.

---

## Working loop

```bash
mooserunnerCli ping
mooserunnerCli test --assembly Dungeon.RaidManager.Tests
mooserunnerCli test --class Dungeon.Game.Tests RaidE2E        # E2E: --class, never --method
mooserunnerCli console --types error,warning --count 50        # ALWAYS, green or not
```

Scene and build are driven by sentinels, and **`force-recompile` first, then touch** — the poller
defers while the editor is in Play Mode, which is where every test run leaves it:

```bash
mooserunnerCli force-recompile && touch .dungeon-build-scene   # regenerate Raid.unity
mooserunnerCli force-recompile && touch .dungeon-build-webgl   # build into Builds/
bash Tools/publish-itch.sh                                     # push to itch
```

Set the Unity process affinity to ~4 cores before a WebGL build (`(Get-Process -Id <pid>).ProcessorAffinity = [IntPtr]0xF`)
— IL2CPP's child compilers inherit it and stop exhausting Windows commit memory.

---

## What to do next

M1–M4 are shipped. What remains is judgement rather than construction: the game is feature-complete
against `PLAN.md`, so the next work is playing it and tuning what feels wrong.

**Two balance questions are waiting for you, deliberately not decided:**

- **D12/D13's constants.** Both fixes restore an ordering the spec demands; neither figure has been
  validated against a human playing. The tests pin the ordering, not the numbers.
- **THE SKIRMISHERS and THE GLASS CANNONS cap at 4.1/s and harvest ~30**, against 120–209 for the
  other four rosters. They kill monsters so fast that income collapses to the idle rate and the
  player can no longer afford the 25 a spawn costs — an income death spiral ending at full party
  health. Either glass cannons *should* be poor customers, or that is a dead minute. Recorded at the
  end of D12.

**The itch embed crop is fixed in code — you no longer need to change anything on itch.** Unity's
stock template hardcodes `canvas.style.width = "960px"`, so a 960x600 canvas inside a 523x293 embed
simply overflowed and was clipped. `DungeonWebGLBuilder` now patches the built page to fill whatever
frame it is given, and the whole standings screen — all twenty rows, the relegation line, the
next-party line and the prompt — fits inside 523x293. Changing the embed to 960x600 would still be
*nicer* — at 523x293 the standings rows are legible but small — and nothing is cropped either way.

The whole loop was played through in a real 523x293 iframe after the fix: standings, raid, the
adventurers' review, and the shop. Every screen fits with margin, and the review does its job
unprompted — doing nothing for a minute earns two stars, *"walked straight through"*, and the line
"THEY LEFT EARLY AND STOPPED PAYING. SHUT A DOOR IN FRONT OF THEM."

Known rough edges:

- **The shop is balanced by guess, not by play.** Items cost 75-125 against a raid that harvests
  roughly 55 when badly played and several hundred when milked, and the Ready bonus is 4/s. Nobody
  has played a full season to find out whether that curve climbs or stalls.
- **No animation yet.** The spec wants wounded state read from limping and panicking, and only the
  three static wound sprites carry it today. The path is proven: `--command character` with an
  extracted sprite as the master routes to the deterministic rig, which renders identical frames
  from the same rig JSON. ImageGen never touches the party.
- **The HUD is immediate-mode.** Chosen so a missing dynamic font cannot black out a WebGL build. It
  works but looks nothing like the moodboard's HUD.
- **`Assets/Art/` is committed now** — regenerating art overwrites tracked files, so check
  `git status` after any rerun of the art tools.

## 2026-08-15 — the one-room opening, the pinch fix, and the retreat door

Live on itch as **0.1.2608150904**. 299 tests green (Raid 149, Game 77, Shop 49, League 24).

Four author requests, all shipped, with the reasoning in **D31–D33**:

- **The run opens on one room with one slime pit and one chest.** The kit is placed through the
  loadout, not stamped in by the builder, so it previews, counts toward the dungeon's value, blocks
  its tile and moves when a hall bought to the left re-anchors the grid.
- **A tap is decided on release**, so the first finger of a pinch is no longer a click.
- **A retreating party forces the door barring its exit** — the safety valve is a valve again.
- **First-raid hints** over the opening room and on each tappable thing, off from round two.

### What to look at next

1. **The league has not been retuned for the one-room opening, and this is the one real risk.**
   A player who touches nothing earns **51**; rivals in round one earn a mean of about **236**
   (uniform 22.5–450). That is roughly 15th of 16 with the bottom two relegated — a coin flip on
   being eliminated in the first minute for doing nothing. Playing the room earns **342** and lands
   comfortably mid-table, and the hints now say so out loud, so the lesson is legible rather than
   unfair. It is still the author's dial: `LeagueTable.BadRun`/`GoodRun`/`RivalHandicap`, untouched
   since D13.
2. **The opening board cannot reach the old rate figures, by design.** One slime pit peaks around
   8–9/s where three rooms of skeletons reached 27+. Nearly all the rate is in the wound curve and
   slimes barely wound. `RaidE2E.Step3` now asserts a lift off the idle floor rather than 5×, and
   measures the **peak** rather than a single sample.
3. **The tileset arch is still the open art item** — see `TILESET-NOTES.md`. Unchanged advice: cut it
   by hand from `tileset-final.png`, whose material already matches by measurement. Only the geometry
   is wrong, and geometry is what a human fixes faster than a generator.
4. **The camera pans to keep the dungeon's own centre reachable** now, so a small dungeon sits in the
   middle of the frame instead of against the right edge. Worth a look on a phone in portrait, which
   is the aspect that framing rule is hardest on.

## 2026-08-15 (later) — can it be played, and can it be won?

Live on itch as **0.1.2608150956**. The page art and copy are in `Marketing/`, built from the
game's own pixels by `python Tools/make-itch-art.py` — re-run it after any visual change.

Nothing had ever *played* the game. `TenWholeRounds_LeaveTheGameIntact` walks the loop without
spawning a monster, so the suite measured robustness and never playability, and `Phase.Won` had no
public accessor — a state a test cannot observe is a state no test asserts.
`RunProgressionTests` now plays whole seasons and sweeps the player's one judgement call.

### The one thing that needs the author

**A season played well is not winnable.** Best of four cease-fire settings reaches round 9 of 10;
none of eight runs won. It is arithmetic rather than bad luck:

- `FieldStrength` climbs to `FinalistPressure` (0.9) as rivals are eliminated, so the rivals' floor
  rises to `22.5 + 427.5 × 0.9 = 407` against a ceiling of 450 — every survivor averages **~428 a
  round** in the closing rounds.
- The player's measured harvest across ~40 played raids is **246–435**, typically 320–400, with one
  596 outlier. A dungeon capped at five rooms and sixty seconds cannot reliably beat 428.

The dials are `LeagueTable.RivalHandicap`, `FinalistPressure`, and `GameController.MaxRooms`. D25
reasons about that exact number, so nothing was changed; `RunProgressionTests` is the instrument to
check any change against, and takes about a minute to run.

### Fixed since the last note

- **The winning ending had never been rendered by anything.** It draws now — and was announcing
  "1 DUNGEONS LEFT. THE BOTTOM 1 ARE DESTROYED" with a red relegation line above the winner's own
  row. Photograph: `Screenshots/05-the-winning-ending.png`.
- **A "+ HALL" marker sat across the purse and the countdown.** A one-room dungeon has a free side
  in all four directions, so the upward marker is offered from the first shop of every run and
  clamped to the top of the screen. Markers and tile menus now clamp below the header.
- **The collapse line was drawn in the player's green**, so "YOUR DUNGEON COLLAPSED IN 20th" read
  as congratulation.
- **The resolution sweep had never checked a portrait screen** despite existing to check small ones:
  every case computed scale as `height / 720`, which matches the game in landscape and is four times
  too large upright. Corrected, and phones added. Everything fits; what does not is tile size, which
  is arithmetic — 31 cells across 360px is 11px each, and aiming there needs the pinch that only
  started working today.
- **Placement is not a refinement.** Buying onto the first buildable tile stacked every purchase
  beside the entrance and produced raids harvesting *exactly zero* — the party met four monsters on
  the threshold and died before the rate accrued anything. Buying deep removed wipes almost
  entirely. That is D29 with the sharpest evidence yet, and it is worth saying somewhere the player
  can hear it.

### Known, deliberate, and worth a second opinion

- **The opening dungeon has no door**, because one room has no threshold to put one in. The retreat
  valve SPEC calls the player's only mercy therefore does not exist in round one, and a player who
  simply mashes the slime pit spawns twenty-five slimes and wipes the party. The hint line now
  carries the restraint — *TAP THE SLIME PIT TO HOLD THEM — TOO MANY AND THEY DIE* — rather than the
  game removing the mistake, because being able to make it is the mechanic. If playtesting says new
  players still drown their first party, the next lever is a door on the entrance, not a cap.

  **Updated 2026-08-16 — read this before acting on that last sentence.** The entrance wall is now
  carved through (D39), but deliberately as a `Doorway` with **no `Door`**. Putting a real door there
  is not the small step it sounds like: the player could shut it and lock the party out for the whole
  minute, losing the raid by pressing the thing the tutorial tells them to press. The doorless carve
  is also what keeps the opening off the walkable grid, so it is load-bearing for containment, not
  cosmetic — `EntranceOpeningTests` will fail if a door is registered on it. A retreat door belongs on
  an interior threshold, which is what round two onward already has.

## 2026-08-15 (evening) — the tileset thread, and what it actually taught

Main is at `ce5e8e6`, 96/96 green, working tree clean. **Itch is unchanged at `0.1.2608151041`** —
nothing was published today, deliberately, because nothing in the art thread reached a shippable
state.

### Start here

`TILESET-SEARCH.md` §7–13 is the record, with sources. The conclusion five independent searches
converge on: **the tileset is the wrong problem.** We spent the day asking a generator for sixteen
images whose correctness lives in their *relationships* — which is the one thing an image model
cannot hold — and `TILESET-NOTES.md:18` had already named the real cue days earlier: the moodboard
*"does not separate wall from floor by value… it separates them with the rim highlight"*. A rim is a
pure function of the grid. Code draws it perfectly; a generator draws it differently every run.

### The route, if picking this up

1. Get **one** seamless full-bleed stone fill. Gates: border/body luminance 1.0 ± 0.05, wrap-seam
   ratio ≈ 1.0, unique colours ≤ 32.
2. Derive the sixteen pieces from it by compositing quadrants — joining becomes arithmetic.
3. Draw the depth in code: rim first, then front face, shadow, AO. `DungeonScenery.DrawRelief` on
   branch `tiles-from-room` is a working first cut; §13 says exactly what to change (thinner, much
   lower alpha, modulated per block, only the north rim bright).
4. Generate at **16 or 32 px per cell**, never 64 — that is our true logical resolution, and it is
   inside every pixel-art model's limit where 64 is not.

### Three traps that cost real time today

- **Every generation ran blind.** `grep -c referenced_image_paths` returns 0 on every run log. The
  references were shown to the agent, which described them back in prose that reads exactly like it
  used them. CLAUDE.md documents this in capitals; the instruction was in every prompt and was
  ignored every time. **The instruction is worthless; the check is everything.**
- **`git push origin main` from a branch pushes the local main ref** and reports success while
  shipping nothing. It did that four times before I noticed. Run `git branch --show-current` first.
- **A measurement that cannot see the defect is worse than none**, because it gets quoted as
  evidence of health. Seam continuity passed a tile carrying a 3px black frame, because a symmetric
  frame is perfectly wrap-continuous.

### The state of the art, measured

`python Tools/validate-tileset.py` — **main's own tiles score 18 gate failures** (the branch's 79 was
measured on the old gate and is not comparable). Main's art does not pass main's gate. The gate is
new and the art predates it, but that is the honest number and the baseline any replacement must
beat.

**This read 68 until 2026-08-16, and 50 of those 68 were the instrument, not the art.** Two gates
were miscalibrated and neither had ever been checked. `side_coverage` asked luminance a question about
transparency, so every wall pixel darker than the mean floor counted as missing art — about 32 of
those 68 were that one false positive, and it is quoted as evidence in D28. The gate now measures
alpha and is calibrated both ways: a transparent 4px margin scores 0% and fails, an opaque tile with
a drawn shadow scores 100% and passes.

`flat_cells` was the second. It used a fixed 4px block, which asks whether the art is drawn at 16px
in a 64px tile rather than whether it is on a grid at all — and these tiles are a clean x2 point-scale
of 32px source, so they measured **0% at block 4 and 100% at block 2**. Eighteen tiles were failing
for being drawn at the wrong size rather than for being wrong. It detects the native block now, and
fails only when no integer grid fits: bicubic-resampled art lands at 1px, 1% flat, 916 colours.

The 18 that remain are real — 187 to 272 colours against a target of 32, which is what art resampled
from DCSS carries. **Treat any validator figure written before 2026-08-16 as inflated.**

### Branches

`tiles-from-room` carries the sampler experiments, the sliced tiles and the relief prototype.
`tileset-dcss` is the violet recolour, superseded. Neither should be merged as-is.

## Closed 2026-08-15: the "pale bands" were not reproducible, and why

A section here reported two pale bands rendering across every room in world space, with exact pixel
extents and a list of things ruled out. **It could not be reproduced and the current build is
correct.** Measured in a capture taken inside the dump test, so the pixels and the renderer list are
one frame:

```
wall-11 region peak   47.0    source wall-11.png peak   47.0
wall-14 region peak   63.0    source wall-14.png peak   63.4
floor                 18.8    source floor-plain.png    19.5
```

Rendering is pixel-faithful. `Screenshots/09-scenery-dump.png` shows a normal room.

### The trap, which is the part worth keeping

**Everything in `Screenshots/` is overwritten by every test run.** `01-raid-opening.png` is rewritten
by `RaidE2E` on each pass of the Game suite. The bands were measured out of that file across several
turns while the suite kept rewriting it, so "the bands are identical in all five screenshots" and the
later contradiction — a band at luminance 100 against a brightest tile pixel of 63 — were readings of
*different frames* presented as readings of one.

The contradiction was the tell and it was visible for a while before it was believed: no tile can
render as 100 at 1:1, and rendering had already been shown to be 1:1. The right response to an
impossible measurement is to distrust the measurement's provenance, not to look harder for an exotic
renderer.

**When analysing a screenshot, capture it in the same test that reads it, or copy it out of
`Screenshots/` under a unique name first.** `SceneryDumpTests` now does the former, which is why it
settled this in one run after several turns of guessing.

The author's original report — walls reading as pattern tiles rather than walls — remains open and is
about how the art *looks*, not about a renderer fault. See D28 before starting a fourth attempt.

