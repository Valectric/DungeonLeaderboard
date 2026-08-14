# M9 — Walls, aggro, and paying for variety

*Directed by the author after playing M8. Four asks (A rate variation, B aggro, C solid walls, D structure), synthesised from five reviews and their critics. Everything below is measured against the code in `a124c5d`, not against HANDOVER prose.*

---

## What the author asked for

**(A) More variation in the energy rate — reward variety, not grinding.**

| # | Ask | Verbatim numbers |
|---|---|---|
| A1 | Disarming a trap | the rogue gets **+2 for 7 seconds** |
| A2 | Entering a new room | **+2 for 3 seconds, whole team** |
| A3 | Fighting an enemy | a bonus (exists already) |
| A4 | Fighting more than one enemy | **+2 for every extra enemy** |
| A5 | Fighting continuously past 30s | **−2, then a further −2 every 5s** |

His reason for A5, in substance: *"if you just push a lot after thirty seconds it starts to get boring, it's just enemy wave after wave after wave, that's no fun. Exploration is fun. The variation is important for the team."*

**How I converted his numbers, so he can correct them.** `+2` reads as **+2.0 energy per second, flat, added to the *team* total**, in the same place and the same units as the existing chest bonus (`EnergyCurve.ChestBonus = 6f` for 5s, consumed at `Raid.cs:649-653` *after* the per-member loop). Yardsticks from the shipped curve, so the size is legible:

- four members walking = 4 × 0.06 = **0.24/s total**
- four members idle = **0.16/s**
- one chest = **6/s for 5s** = 30 energy
- a normal fight ≈ **8/s**; measured peak across rosters **25–38/s**
- so "+2 for 3 seconds" = **6 energy**, "+2 for 7 seconds" = **14 energy**
- A5 at 60s of unbroken combat = **−14/s**, against a healthy fight's ~12/s

The three readings I rejected and why, because each is a 4×-or-worse difference: **+2 per member** would make a five-room walkthrough worth ~126 energy — a three-star review for a party that never fought, and SPEC §3's "almost nothing" dead. **+2 as a multiplier**, stacked over four modifiers, is ×8–×24 and annihilates the fifth-power wound term. **+2 inside the per-member wound multiply** pays 2 × 7.96 = 16/s off one member at 5% health, which is D12's explicit warning ("parking one member at 5% and farming becomes the whole game").

One naming note: **there is no Rogue role.** The roster is Tank / Healer / Ranged / Mage, and D9 gives disarming to the **Ranged** member. A1 is read as "the ranged member's disarm", and since it cannot be a per-member term without inventing a role, that independently confirms the team-flat reading.

**(B) Targeting and aggro.** Skeletons and slimes move to the closest party member, *unless* a tank is in that room, in which case the tank is targeted first — the tank draws aggro through its abilities. There is no aggro system today; it is hand-waved. And: any enemy in the same room as a party member attacks that party member.

**(C) Walls are not solid.** Party members walk through walls. Ranged attackers shoot through three or four walls. He wants real colliders, pathfinding that respects walls, and shots that stop ("bounce off") at a wall.

**(D) Architecture.** *"we need to rethink how the structure of the game is designed so that we have enemies and the players as prefabs, and the shots fired as prefabs. A shot pool is probably not needed, we have so few shots. Keep it more inside the Unity systems. Right now it sounds like we have a lot of things handcrafted ourselves instead of relying on the pathfinding which is offered inside Unity and such."*

---

## What we are doing, in order

Each phase ends with a shippable build. The ordering is forced, not stylistic: three of the four asks terminate in the same seven lines (`Raid.AccrueEnergy`, `Raid.ResolveCombat`), so tuning A against a resolver that still fires through walls tunes it against the wrong game. Both critics who looked at sequencing reached that conclusion independently, and I have kept it.

**Standing rules for the whole milestone**, both bought at real cost already:

- **A wave of red is evidence about the change, not a list of chores** (D23: a blind sed across 47 call sites made the suite green while the game stayed broken). Do not edit an assertion to make it pass.
- **The league constants are frozen until Phase 7.** D13 states outright that `LeagueTable`'s rival numbers are downstream of measured player harvest. Four of these phases move harvest; re-tuning per phase means chasing four different games.

---

### Phase 0 — Read the measurement that already landed *(done)*

`WallViolationTests` shipped in `a124c5d` and is exactly the right instrument: it plays every roster on a four-room corridor and logs three separate counts — ticks with a body inside a non-walkable cell, moves whose straight line crossed one, and shots whose flight line crossed one — asserting only that the probe sampled at all. `ARangedAttacker_HasNoRangeLimit` logs the longest shot of a raid.

**Do this before writing Phase 2:** `mooserunnerCli test --class Dungeon.RaidManager.Tests WallViolationTests`, then `mooserunnerCli test-log`, and paste the figures into the M9 write-up. They are the numbers Phases 2 and 3 are judged against. **If any count is zero, the diagnosis below is wrong and the fix must not be written.**

**Size:** minutes.

---

### Phase 1 — Truth up the instruments. No behaviour change.

**Goal:** stop measuring the game through code the game does not run, before five modifiers land behind it.

**Changes.**

1. **Five of the eleven `EnergyCurveTests` assert a formula the shipped game no longer executes.** `EnergyCurve.Rate` and `EnergyCurve.EngagementMultiplier` have **exactly one production caller** — `Raid.cs:148`, which only seeds the opening HUD value — while `AccrueEnergy` sums `MemberRate` instead. Verified by grep: every other reference is in `EnergyCurveTests`. Two of the five carry the names of SPEC's central invariants and **cannot fail whatever the new modifiers do**:
   - `SimulatedRaid_StallingWoundedParty_FarOutEarnsAWalkthrough` (`:133`, `:141`)
   - `SimulatedRaid_WipingTheParty_EarnsLessThanKeepingItAlive` (`:165`, `:172`)

   Rewrite all five against the shipped path — a helper that sums `MemberRate` over a described party plus the modifier stack. Keep the `WoundMultiplier` tests as they are; those are live. Consider deleting `Rate`/`EngagementMultiplier` outright and seeding `Raid.cs:148` from the per-member sum for an idle party.

   This is the shape D19, D21 and D22 each record: *a test cementing a claim it never checks*. Landing five modifiers behind two green tests named for the game's two central invariants would repeat it a fourth time.

2. **`RateReachabilityTests.cs:158` compares against a ruler that is ~5× too weak.** It asserts each roster beats `BaseRate * IdleEngagement` = 1 × 0.05 = **0.05/s**, while the shipped idle party earns 4 × 0.04 = **0.16/s** and a walking one **0.24/s**. Replace it with the measured walking total — which is also the floor A5's decay may never breach, so one number serves both purposes.

3. **Add `EconomyBaselineTests`**: log (do not pin) harvest and peak rate for four fixed scenarios — pure walkthrough; one-monster stall behind shut doors; crowd stall with spawn-every-tick; each of the six rosters. Assert only order-of-magnitude bands so it survives tuning. Harness copied from `ExploratoryTests.Play` (`:33-66`).

**Files:** `Assets/Dungeon/Modules/RaidManager/Tests/EnergyCurveTests.cs:88-178`; `RateReachabilityTests.cs:158`; new `Tests/EconomyBaselineTests.cs`; possibly `EnergyCurve.cs:165-198` and `Raid.cs:148`.

**How it is proven.** The rewrite is proven by **deliberately breaking the game and watching it catch**: revert D12's fix locally (feed `Party.HealthFraction` where `WoundFraction` goes) and confirm the rewritten `SimulatedRaid_WipingTheParty` goes red where the old one stayed green. That is the only evidence that the new tests can see what the old ones could not. Everything else must be bit-identical — `RepeatedRaids_AreIdentical` and the whole `Dungeon.RaidManager.Tests` assembly, plus `console --types error,warning`.

**Size:** small. Half a session. **Ships alone** (test-only).

---

### Phase 2 — Shots stop at walls

**Goal:** the author's most visible reported bug, fixed at the layer where the damage is decided rather than the layer that draws it.

**Root cause, verified.** `Raid.SwingParty` (`:449-491`) picks `Nearest(engaged, member.Position)` by bare distance; for Ranged and Mage the only distance check in the method is short-circuited (`:465`), so **there is no range limit and no line-of-sight test at all**. The one thing bounding it is that `engaged` is scoped to the *leader's* room (`:432`, and `Party.Cell` is the leader's cell at `Party.cs:144`). `SwingMobs` (`:496-531`) has the mirror defect: a mob hits any member within `MeleeReach` with no room check on that member.

This is **the same defect class as D26** — a room test taken from the leader rather than from the actor. The codebase has now made that mistake twice; the fix has the same shape both times.

**Changes.**

1. **Lift `ResolveCombat` / `SwingParty` / `SwingMobs` into `RaidManager/Internal/CombatResolver.cs`** first, behaviour-identical. `Raid.cs` is 687 lines and both this phase and Phase 4 land in it.
2. **Scope combat per combatant, not per leader.** A member may only swing at mobs in **its own** room; a mob may only swing at members in **its own** room. Both with the **doorway exemption** — `RoomAt(cell) == HomeRoom || RoomAt(cell) == DungeonGrid.NoRoom` — exactly the shape `MobPack.cs:316` already uses. **This clause is load-bearing and a reviewer dropped it:** `AddDoor` gives every doorway `NoRoom` (`DungeonGrid.cs:248`), so without the exemption a member standing on a threshold becomes simultaneously un-hittable and unable to attack. The column is 1.86 cells long crossing a 1-cell doorway at 0.9 cells/s, so someone sits on a threshold for roughly two seconds per room transition, and a fleeing healer or a 3.6× panicking archer can park there indefinitely.
   - **This is also the author's B3** — "any enemy in the same room as a party member attacks that party member" — delivered exactly, and bounded by construction rather than by a rule that could be forgotten.
3. **Do not add a separate range constant.** A room is 5×5 (`DungeonLayout` defaults, `PlanBuilder.Build`), so the room check *is* the range limit: max ~5.7 cells corner to corner. One rule, no new number.
4. **Confine the mage's blink.** `Party.TryBlink` (`:948-973`) teleports, and `AdventurerAI.TryFindBlink` (`:296-323`) validates **only the landing cell** — never the line travelled. `BlinkDistance` is 5 against 5×5 rooms, so a blink routinely crosses a wall into a room the mage was not in, at which point the mage falls out of `engaged` and stops fighting. Gate candidates on `HasLineOfSight(self.Position, candidate)` **and** `RoomAt(cell) == RoomAt(self.Cell)`.
   - **This is very likely what the author saw as "three or four walls".** `Raid.cs:197-200` fires a `ShotKind.Bolt` for every blink, from `BlinkedFrom` to `BlinkedTo` — the longest line the view ever draws, outside `ResolveCombat` entirely, and guaranteed to cross a wall today. A reviewer's file list stopped at `Raid.cs:430-531` and would have missed it.
5. **Give `Shot` an explicit `IsBlink` flag.** `DungeonView.cs:258-259` currently *infers* a blink from `Kind == Bolt && distance > 3f`. A room-confined blink can fall under 3 cells, which would silently downgrade it to `VfxHitMonster` with no chime — D24's exact trap ("an effect kind with no case of its own falls through to the DOOR visual and the door chime, which would tell them something opened").
6. **Do not add a line-of-sight test to the resolver.** Rooms are carved as `RectInt` (`PlanBuilder.cs:44`) and are therefore convex, so once the actor-room check lands, any two points in the same room already have clear sight. LOS would add no gating and one failure mode: `HasLineOfSight` rounds each sample to the nearest cell (`DungeonGrid.cs:393`), so a member near a room edge samples a wall cell and silently cannot fire — intermittent, invisible, and it removes damage the economy is sized on. **I am overruling the two reviewers who wanted LOS in the resolver; the critic is right that on this geometry only the redundant half can misfire.**
7. **Do not touch `Party.AssignActions` in this phase.** It prices `Shooting` at 2.1/s against `Walking`'s 0.06/s — a 35× step — from bare distance to the threat list, with no grid query. Gating it is a real balance change and belongs in Phase 7's measured pass, not folded into a geometry fix. D18 is the precedent for a correct-looking positioning fix that collapsed the economy.

**Files:** new `Assets/Dungeon/Modules/RaidManager/Internal/CombatResolver.cs`; `Raid.cs:430-531, 197-200`; `ProjectileFeed.cs` (the `IsBlink` flag); `AdventurerAI.cs:296-323`; `DungeonView.cs:252-270`.

**How it is proven.**

- **The instrument, not an assertion first.** `WallViolationTests`' shot-through-wall count must go to **zero** across every roster, and the longest-shot figure must fall inside one room's diagonal. Only then tighten it into a guard.
- **Constructed cases**, because a ticked raid did not produce D26's straggler and passed 5694 assertions beside it: (i) archer in room 0, skeleton in room 2 → `raid.Shots.Shots` empty and the skeleton untouched; (ii) a member standing **on a door cell** with a mob in the adjacent room → blows land in **both** directions.
- **Re-measure the economy rather than editing tests.** This removes damage that currently lands, so fights get longer and harvest rises. Re-run `RateReachabilityTests` (steep end > 20/s, spread < 14×, `TheLeastWoundedRoster_IsTheWorstPaying`), `ExploratoryTests.KillingTheParty_NeverPaysBest` (reads 14 vs 231 today — anything approaching parity means the change inverted the design), `AFight_LastsLongEnoughToEarn`, `MageBlinkTests` (a room-confined blink is a shorter blink and the mana economy was sized on the old one), and one season sweep. Record before/after in DECISIONS.
- **A hypothesis worth testing, not just a risk:** gating ranged fire on the shooter's own room slows exactly the rosters D17 diagnosed as earning a ninth of the rest. THE GLASS CANNONS and THE SKIRMISHERS earn nothing because they kill monsters before taking a wound (worst survivor 0.76–0.79, 10% of ticks in combat). Longer fights mean deeper wounds, which is the direction D17 wants. Run the sweep that produced the 9.3× spread and report the new figure — it may retire an open balance question for free.
- **Look at it.** A screenshot with a straggler a room behind, showing no arrow crossing a wall.

**Size:** medium. **Ships alone.**

---

### Phase 3 — Bodies stay out of walls

**Goal:** the other half of (C), fixed deterministically in plain C#.

**Root cause, verified.** `Party.Glide` (`:715-720`) is a bare `Vector2.MoveTowards` with no walkability test, and it is the funnel for the leader (`:399`), every follower (`:429`), the retreat (`:379`) and `MoveAlongPath` (`:739`). `FindPath` is **not** the bug — `DungeonGrid.Search` tests `IsWalkable` on every neighbour (`:354`) and refuses an unwalkable destination (`:334`). Pathing already respects walls and shut doors. Any plan that answers (C) by "adding pathfinding" is solving a problem that does not exist.

**Changes, in this order — the first two are cheaper than the third and remove most of the cause.**

1. **Fix the spawn placement first.** `PlaceFollowers` (`:1209-1216`, called from the constructor at `:323`) writes positions with no walkability test. The constructor seeds the trail at `entranceCell.x − step*0.25` for step 8..0, and `DungeonLayout.cs:451` sets `entrance = (margin, midY) = (1, 3)`. So `PositionBehind(0.62)` → x 0.38 → **cell 0, the margin wall**; `PositionBehind(1.24)` → x −0.24 → **cell 0**; `PositionBehind(1.86)` → x −0.86 → **cell −1, out of bounds**. **Three of the four members stand outside the walkable dungeon at tick zero of every raid.** No slide primitive touches this, and any "assert zero violations" acceptance criterion is red from frame 0 until it is fixed. Either seed the trail inside the room, or route `PlaceFollowers` through the same movement primitive — decide deliberately, because the constructor comment says the strung-out trail exists so the party "reads as walking in".
2. **Fix the leader's look-ahead.** `AdventurerAI.Advance` (`:205-216`) returns `path[Mathf.Min(1, path.Count - 1)]` — a cell up to **two** steps ahead — and `Glide` runs a straight line to it. That is the leader's corner-cutting mechanism. Take `path[1]` only when `HasLineOfSight(self.Position, path[1])`, else `path[0]`. Two lines, no new primitive, and it removes the corner-cut outright. **Measure after this and before step 4** — it may take most of the remaining count with it.
3. **Fix `StandOff`.** `AdventurerAI.StandOff` (`:539-548`) checks **nothing at all** — it returns `target + offset.normalized * range` unconditionally, and it is the destination for the tank (0.85), the mage (2.4) and the archer (3.0). Search **radius as well as angle**: try the requested range, then shrink toward `MeleeReach`, taking the first candidate that is walkable. An angle-only ring is the wrong search here — the greatest distance from a room's centre to any interior cell is **2.83**, strictly less than `RangedRange` 3.0, so with a mob near the middle of a room *no* angle on the radius-3 ring lands on interior floor. **Two fallbacks are forbidden**: "stay put" (that is the D18-shaped stall) and **any doorway cell** — a leader standing on a doorway makes `RoomAt(Party.Cell) == NoRoom`, `engaged` empty, both swing loops no-ops, and all four members priced as Walking at 0.06/s. Combat stops outright.
4. **Add one deterministic movement primitive** in a **new** file in `DungeonManager` (`GridMovement.cs`): `Slide(from, to)` — axis-separated, rejecting any component that would land the mover's cell on a non-walkable cell, sliding along the other axis so nobody sticks in a corner, and **recovering a mover that is already illegal** by stepping toward the nearest walkable cell. Route `Party.Glide` and the `mob.Position = next` write in `MobPack.Tick` (`:306-321`) through it. Leave `TryBlink` alone — it is a teleport and Phase 2 already gated it.
   - **No swept collision, no physics.** The fastest thing in the game is a panicking archer at `WalkSpeed 0.9 × ArcherPanicSpeed 3.6 × 0.02` = **0.065 cells per tick**, against a wall ring one cell thick. Endpoint checking is provably sufficient — and `GameController.FixedUpdate` (`:513-521`) ticks at `Time.fixedDeltaTime`, a constant, so the step cannot grow under a frame hitch.
5. **Harden the mob landing guard.** `MobPack.cs:314-319` tests room identity, not walkability, and **a wall has the same `RoomAt` value as a doorway** (`NoRoom`), so the guard reads a wall as permissible. Change to `IsWalkable(next) && (RoomAt(next) == HomeRoom || KindAt(next) == Doorway)`. Measured zero occurrences today — because carved rooms are convex, not because the guard works. Strictly more restrictive than today, so room-bounded pursuit cannot regress. One line, worth doing now while the reason for the zero is understood.
6. **Add a new DDA `FirstBlockedCell` if Phase 2 needed one — but do NOT rewrite `HasLineOfSight`.** A critic swept every ordered pair of walkable cells on three layouts and found a DDA and the existing sampler disagree on **2.2–2.7%** of pairs, and the sampler is not even stable under its own refinement (resampling at 8/16/32 per unit flips 1.2–2.1% of pairs, in *both* directions). `HasLineOfSight` has exactly two callers — `NearestArmedTrap` (`:393`) and `NearestVisible` (`:509`) — which decide who the tank charges, who the archer aims at, and whether the party detours to a trap. All three are positioning inputs the energy curve prices. Rewriting it is a silent AI-behaviour change dressed as a perf refactor, and a "differential test asserting they agree on every pair" cannot pass.

**Files:** `Party.cs:715-720, 1209-1216, 301-324`; `AdventurerAI.cs:205-216, 539-548`; `MobPack.cs:306-321`; new `Assets/Dungeon/Modules/DungeonManager/GridMovement.cs`.

**How it is proven.** This is the phase most likely to break the shipped game, so the proof is heavier than the change.

- **Violation count and liveness together, never alone.** `WallViolationTests`' inside-wall and crossed-wall counts must go to zero — **and** a liveness probe must simultaneously show the party still moving, because *a frozen party scores a perfect zero on every violation counter*. D11 (chest reach 0.45 parked the leader for a whole raid), D19 (a stuck roster harvests 3 energy) and D21 (all six rosters stood on cell (5,3) for 53 seconds, with two green tests protecting it) are three separate instances of exactly this. The probe: assert the party's distance to its objective strictly decreases over any rolling 5s window in which its goal is `Advancing`, across every roster and layout.
- **A new invariant the collision fix specifically threatens:** assert the fraction of ticks with `RoomAt(Party.Cell) == NoRoom` does **not** rise. A wall-clamped leader lingering on thresholds silently empties `engaged`.
- **Re-measure the pacing.** `UnopposedParty_TakesMostOfTheClockToCross` (bounded 10s–60s) and `AnUnboughtDungeon_CanAlwaysBeCrossedInTime` (a pass/fail outcome at 2/3/4/5 rooms) are the real pins. A slide that catches on corners moves both, and a crossing time is the pacing of the whole game. **Re-tune `WalkSpeed`, do not widen the bound.**
- **Re-run the containment tests unchanged:** `MobBehaviourSweepTests.NoMonster_EverLeavesItsRoom` and the D26 straggler case. A slide that clamps a mob against a wall must not push it across a threshold.
- **Look at a raid.** D11 is the precedent for a movement constraint that deadlocked a raid while every assertion passed.

**Size:** large — the largest in the milestone. **Ships alone.**

---

### Phase 4 — Aggro that the game actually runs

**Goal:** (B), built at the two sites production combat uses, not the one that looks like it.

**The trap this phase exists to avoid.** `Party.DistributeDamage` (`:785-840`) contains a 60%-to-tanks rule, and its comment says *"Tanks draw aggro, so they eat the bulk between them."* **It has no production caller.** Grep finds only three tests (`CombatReachTests.cs:187`, `MobBehaviourSweepTests.cs:171`, `PartyCompositionTests.cs:445`) using it as a hand-wounding injector. Live mob damage goes through `SwingMobs`. An implementer who edits `DistributeDamage` produces a correct-looking diff, a green suite, and an unchanged game. **Correct the comment (or move the method behind a test-only injector) in the same commit**, so the next reader is not misled.

Today's aggro is 100% emergent geometry — `Raid.cs:506-508` says so: the tank leads, closes to `TankReach` 0.85, and is therefore the nearest body inside `MeleeReach` 1.15. The author is asking for an accident to be made deliberate. That keeps the change small.

**Changes.**

1. **Compute the preference in `RaidManager`, not `MobManager`.** `Dungeon.MobManager.asmdef` references only `Dungeon.DungeonManager`, and `MobPack.Tick(float, IReadOnlyList<Vector2>)` sees **bare coordinates — no role, no identity**. A `Threat.cs` inside MobManager that says "for tanks" is unimplementable without reversing a documented One-Flow invariant. **The critic wins here and the reviewer's file placement is overruled.** Extend the signature to `Tick(dt, IReadOnlyList<Vector2> positions, IReadOnlyList<float> weight)`, filled by `Raid` (which is already the only module that knows both, `Raid.cs:176-182`). Budget for the signature change: `MobBehaviourSweepTests.cs:106` and friends call it directly.
2. **Make the preference multiplicative, not a fixed radius.** `effective = distance * (isTank ? TankFactor : 1f)`, with `TankFactor` below 1. A fixed pull of 1.2 cells does **not** survive the roster tables: `FollowSpacing` is 0.62, so rank 1 sits 0.62 behind and rank 2 sits 1.24 behind *measured along the trail* (straight-line distance is always ≤ that, and strictly less around a corner). THE BALANCED PARTY puts the **mage at rank 2**, 1.24 cells from the tank — a 0.04-cell margin against a 1.2 pull, which any curved trail erases. A multiplicative factor cannot invert an ordering at close range: a mob standing *on* the mage is at distance 0 and keeps the mage whatever the tank's factor, so the blink and the rear ambush survive by construction, while a mob choosing across a room walks to the tank.
3. **Apply it strictly *after* the existing room filter** (`MobPack.cs:264-267`) and leave the landing guard alone. This is structural, not something to test for: the pull may only reorder candidates already known to be in the mob's own room.
4. **Use the same score at the attack step** (`SwingMobs`, now in `CombatResolver`), **keeping the `MeleeReach` gate exactly as it is.** Movement and attack currently disagree in the one case that matters — a mob shoved off the tank by `Separate` (0.7 cells) can find a follower inside 1.15 and hit them while still walking at the tank.
5. **Add a crowding term** so the tank is preferred but not a monopoly: `effective += Crowd * (living mobs already within MeleeReach of that member)`. This is the guard against the two failure modes below, and it reads on screen as monsters shouldering round a blocked target rather than as a magic constant.
6. **Passive threat, not a taunt with a cooldown.** A cooldown reintroduces the on/off target flip that caused the "wagging" standoff M6 fixed; it adds hidden timed state to a game that deliberately shows no numbers; and it adds an ordering-sensitive state machine to a simulation that must reproduce exactly from a seed.

**Do NOT read B3's second sentence literally.** "Any enemy in the same room attacks that party member" as written deletes the `MeleeReach` gate — and with it `CombatReachTests.AMonster_CannotHurtWhatItCannotReach`, the healer flee (`HealerFleeRange` 1.0), the archer kite (3.6×) and the mage blink, in a single edit. Phase 2 already delivered what that sentence is actually complaining about.

**Files:** `MobPack.cs:215, 258-275`; new `MobManager/Threat.cs` (pure arithmetic over position+weight pairs, role-blind); `RaidManager/Internal/CombatResolver.cs`; `Raid.cs:176-182`; `Party.cs:785-840` (comment or injector).

**How it is proven.**

- **Build the missing measurement first.** `CombatReachTests.TheTank_TakesTheDamage` asserts only `Assert.Less(tank.HealthFraction, 1f)` — satisfied by a single scratch. It cannot distinguish aggro from a graze. Record **share of blows landed on the tank while a tank is alive and in the room**, per roster, before and after. Add a characterisation test pinning that share between 0.5 and 0.85, so a future edit that quietly returns to a monopoly fails here.
- **Predict where the delta must appear, then measure that case.** The tank is already almost always who gets hit, so an aggregate share may barely move. The case that must change is **a second mob spawned behind the party, near the healer or mage**. Test it directly.
- **Pick `TankFactor` and `Crowd` by sweeping, not by model.** The p-band table (`p=0.75→999`, "a monopoly costs ~40% of harvest") that justified `Crowd = 1.4` is reproducible from nothing in this repo — no script, no seed, no test — and one of its results (retreat before death at p=0.60) directly contradicts D18's *measured* finding that across every roster the party never retreats before losing a member. **I am overruling the constant while keeping the mechanism.** Sweep `RateReachabilityTests` across a small grid of both constants, record peak rate / deepest wound / engaged share / monsters afforded per roster, and choose from the table. Record it as a dated DECISIONS entry beside D26.
- **Measure income and outcome together, never income alone.** Concentration pays more under the per-member curve, so income can rise while raids end early. Record tank time-to-death, wipe rate, and the outcome mix. D20 makes round one deliberate sudden death, so a round-one tank loss ends the run.
- **Containment, constructed:** a tank standing one cell over the threshold in the next room, held for 400 ticks, and the mob never leaves its home room. `NoMonster_EverLeavesItsRoom` passed 5694 assertions beside the D26 bug; it ticks a party that moves as a group and cannot produce the case.
- **`MageBlinkTests.APressedMage_BlinksClearAndPaysForIt` is the canary, with a caveat**: it re-spawns its bully only when the mage's room is empty (`:148`), so an over-strong pull makes it fail *silently and slowly* — the bully walks to the tank and the raid simply runs out with `blinked == false`. Read the log, not just the colour.
- **Look at a fight with three mobs** and confirm they are not stacked on one sprite. The crowding term is as much a rendering fix as an economic one.

**Size:** medium. **Ships alone.**

---

### Phase 5 — The rate variation

**Goal:** (A). Landed last of the mechanics, because A4 counts from `engaged` — the exact set Phase 2 redefines — and because Phase 4 changes which member's health is low, which is the other input to the same sum.

**5a. Extract the modifier stack.** New `RaidManager/Internal/RateModifiers.cs` holding the windows, the strain meter and the decay, returning a **per-term breakdown** rather than a scalar. Move the existing chest bonus into it as a pure refactor. `Raid.cs` keeps only the call. **Proof: bit-identical harvest** on every `EconomyBaselineTests` scenario. A refactor of the game's central number that shifts any harvest by any amount is not a refactor.

Note this is where the breakdown for the Phase 6 HUD comes from. A reviewer proposed routing it through "the RaidManager TestFacade seam" — **there is no TestFacade anywhere in this codebase** (grep returns nothing; every test reaches `raid.Party` / `raid.Mobs` / `raid.Layout` directly). Inventing the pattern here is new project-wide infrastructure, not a seam that exists. Expose the breakdown as a plain read-only property on `Raid`; the HUD needs it anyway.

**5b. A2 (new room) and A1 (disarm) — pay the *purse*, not the rate.**

This is my one deliberate departure from the author's literal words, and it needs his sign-off. As spoken, both bonuses break two named tests and one SPEC sentence:

- **`RaidRulesTests.EarlyEscape_EarnsFarLessThanAFullRaid`** (`:241`) asserts `stalled > strolled * 5`. A2 pays for **advancing** — the exact behaviour the primary verb exists to prevent. The `strolled` raid crosses three rooms and collects every bonus (~+18); the `stalled` raid has all doors shut by construction (`:245-249`), never reaches a second room, collects **one** (+6), and A5's decay then *subtracts* from it. The 5× ratio cannot survive that, and shortening the window to 2s does not save it.
- **`RateStabilityTests.TheRate_DoesNotFlickerDuringAFight`** (`:62-87`) bounds crossings of the **1/s** line at 4. A walking party earns 0.24/s; a flat +2 lifts it to **2.24/s**. Each 3s or 7s window is one up-crossing and one down-crossing, and the harness explores the remaining rooms and disarms traps after its one skeleton dies. Two or three rooms plus a disarm is 6–8 crossings against a bound of 4, on top of the fight's own 2. Fading the window in over 0.25s changes the slope, not whether the value crosses the line — the mitigation a reviewer proposed does not address this test at all. (For the record, the *other* stability test is safe: the ease factor is `dt/RateEaseSeconds` = 0.0909, so +2 adds at most 0.18/s to a single-frame jump against a 1.5/s bound.)
- **SPEC §3**: *"an unengaged party walking a corridor must earn almost nothing."* A2's gradient points the wrong way — a party successfully held in room 1 for 60s collects one bonus; a party that strolls out collects all of them and then ends the raid.

**The fix, and it has a precedent in this codebase:** credit A1 and A2 to `TotalEnergy` (the purse) and never to `EnergyHarvested` / `CurrentRate`. D24 established exactly this pattern for the spawn refund — *"it refunds the purse, never the score"* — precisely so a mechanic can change spending power without touching what the league ranks. Shown as a rising number off the room threshold or the disarmed plate (`+6 SCOUTED`, `+14 DISARMED`), the mirror of the `+25` refund.

What this buys: exploration and disarming genuinely pay — in **more verbs the player can press**, which is D17's option 2 in another shape and directly attacks the tankless income death spiral (those rosters can afford 5 monsters against 9–11). SPEC's "almost nothing" stays literally true. `EarlyEscape` and both stability tests stay green. The league cannot be inflated.

What it costs: variety does not raise the *score* directly, only indirectly. **That is the open question for the author** — see below, with the two alternatives.

**5c. A4 (crowd) — on the rate, capped, and measured *before* the cap is chosen.**

`+2/s per extra engaged monster`, gated on `Party.Goal == PartyGoal.Fighting` (which inherits `CombatGrace` 1.4s so the bonus does not flicker between waves), capped at 3 extras (+6/s ceiling). The hook already exists and is thrown away: `Raid.cs:170` computes `int threats = Mobs.CountInRoom(partyRoom)` and passes it to `ResolveCombat`, where the parameter is **never read** (`:430`). One line of wiring — though note it is computed *before* `Mobs.Tick` and `Party.Tick`, so it is one tick stale relative to the room membership the accrual would be pricing. Harmless in play; a test asserting exact crowd counts will chase it.

**Two reviewers called A4 "a pure addition that cannot go negative" and pre-cleared it as safe. It is the single largest unmeasured exposure in the design.** It is flat and **wound-independent**, so it dilutes the fifth-power wound term that is the whole game — D12's failure re-run from the other side. And the raid that produces the most extra monsters is *the one that kills the party*: `ExploratoryTests`' harness spawns at every spawner every tick, which is the wipe policy, and since D24 the marginal monster is a refunded loan. A4 pays right up to the instant of the wipe.

So: **run `KillingTheParty_NeverPaysBest` with the spawn-everything policy and read the real ratio before choosing the cap.** It reads 14 vs 231 today, ~16×. Also consider suppressing the crowd bonus for a short window after any adventurer dies, so a crowd is never paid for the kill it just made.

**5d. A5 (monotony decay) — last, floored, and per raid.**

One float meter: +1s per second while `Goal == Fighting`; drains at 0.5× while not; each variety event (new room, chest, disarm) subtracts a relief. Penalty = 0 below 30s, then `2 + 2*floor((strain − 30) / 5)`. **Subtract from the team target and clamp the result to at least the party's walking total** (sum of `ActionRate(Walking)` × each member's wound multiplier) **before** the `Lerp` at `Raid.cs:655`.

**The floor is the invariant, not a nicety.** `AccrueEnergy` (`:658-660`) is `float earned = CurrentRate * deltaTime; TotalEnergy += earned; EnergyHarvested += earned;` — **no floor anywhere**. As literally specified the penalty reaches −14/s by t=60 against a healthy fight's 12/s, so a sustained fight banks negative score. The precise break is not "wiping becomes optimal" (a wipe costs 4 × 50 = 200 banked and forfeits the clock); it is that **a party that is "alive, in combat, badly wounded, and still inside the dungeon when the timer expires" — SPEC §1's stated optimum — would be losing score through its final thirty seconds.** That is the one rule everything else follows from, broken through the front door.

(For accuracy: `ExploratoryTests.TheRate_IsAlwaysSaneEveryTick` already asserts `CurrentRate >= 0` every tick, so an unfloored decay would go red rather than ship silently — *provided the 30s trigger fires under those policies*, which is open question 4.)

**Per raid, not per room or per bout**, because the author's reason is that the *player* gets bored; a per-room meter would be reset by the retreat valve, and opening a door to save a party must never become a money decision.

**Files:** new `RaidManager/Internal/RateModifiers.cs`; `EnergyCurve.cs` (constants + a pure `Decay(strain)` and a walking-floor helper); `Raid.cs:169-172, 204-207, 595-661`; `Party.cs:346, 1108-1119` (a `JustEnteredRoom` moment, set in `RecordVisit` and cleared at the top of `Tick` exactly as `JustLooted` is); `DungeonLayout.cs:45-60` (catch the bool `Trap.Disarm` already returns and `Raid.cs:206` discards); `EffectFeed.cs`.

**How it is proven.**

- **The composition band.** The new terms must contribute between **5% and 40%** of a well-played raid's harvest. Under 5% they are decoration; over 40% the game is no longer about wounded parties. This is the one instrument that would catch A4's dilution — **provided A4 is listed as a term to watch rather than pre-cleared.**
- **The ask itself, which nothing measures today.** Three policies on one seed and roster: grind one spawner in one room / explore unmolested / ambush-release-ambush-in-a-new-room. **Variety must out-earn grinding, or the change did not do what was asked.**
- **Does A5 ever fire?** Sweep and count. The good rosters measure 44–74% of ticks engaged (D17); with a 0.5× drain, net strain at the bell for the best roster is roughly 44 − 8 = 37s, i.e. penalty 4/s against a peak of 25–38/s — about a 10% shave, before relief. **On the real simulation the decay is plausibly too weak to punish monotony while three bonuses are unconditional adds, so the net effect of the whole stack may be inflation rather than variety.** If it never fires, the change is inert and the author's complaint is unaddressed; if it fires on most raids it is now the dominant term. Nobody has measured it, and the answer chooses the constants.
- **Anti-gaming, as arithmetic:** a policy that breaks combat for one tick every N seconds must earn *less* than one that does not — 1s out buys 0.5s of relief and costs a full second of the fight rate.
- **Swept, not spot-checked:** the rate never drops below the walking floor at any strain 0–120s × any health 1–0; `EnergyHarvested` is monotonic non-decreasing across every tick of every roster×policy run.
- **Re-run the gates:** `KillingTheParty_NeverPaysBest`, `EarlyEscape_EarnsFarLessThanAFullRaid`, `PlayingWell_AlwaysBeatsPlayingNotAtAll` **plus a door-stalling policy** (A2 pays for moving on and A5 punishes long fights — together they shift value away from stalling, which is the primary verb and the subject of M1's gate), `RepeatedRaids_AreIdentical`, both `RateStabilityTests`.
- **Write a D27 entry.** A5 narrows SPEC §3's own sentence: under the decay, being *continuously* engaged at the bell is the worst-paying way to be engaged at the bell. That is a deliberate and defensible departure and D5 requires it be recorded rather than asserted away. Record with it: the five constants and their reasoning, the measured before/after for the four baseline scenarios, and the note that the decay's drain plus A4's `Fighting` gate together give the retreat valve a **new economic upside**, weakening SPEC's "central regret".

**Size:** large. 5a/5b ship together; 5c ships alone; 5d ships alone.

---

### Phase 6 — Make the modifiers visible

**Goal:** without this, (A) is an invisible economy.

Under the big pulsing rate, a short stack of ephemeral chips — `NEW ROOM +6`, `CROWD +4`, `DISARMED +14`, `SAME FIGHT −6` — each living as long as its window, driven off `RateModifiers`' breakdown.

SPEC §3's entire justification for the rate display is that the player must *see* dead time costing them. A decay the player cannot see is an invisible punishment; four bonuses they cannot attribute are noise. D17's own recommendation was *"option 4 plus a readability pass — the objection is not that it is wrong but that it is INVISIBLE."* This is the difference between an economy and a number that wobbles, and it is why I have not put it in the cut list despite the jam clock.

**Files:** `GameController.cs:1090-1113`.

**How it is proven.** **Not by a Look test — it cannot be.** IMGUI never appears in an editor camera screenshot (HANDOVER bug 5), which is why the shop's invisible descriptions shipped. This has to be opened **in a browser at the itch embed's 523×293 with UI scale 0.4**, where D16's twelve-pixel menu rows happened. Re-run `NothingIsDrawnOverReady` and `StandingsLayoutTests`.

**Size:** small.

---

### Phase 7 — Re-measure once, then ship

**Goal:** settle everything downstream, in one pass rather than four.

1. **Decide the deferred pricing question with a number attached.** Should `Party.AssignActions` (`:1014-1062`) require line of sight before paying `Shooting`? It pays 2.1/s against `Walking`'s 0.06/s — 35× — from bare distance, with no grid query and no evidence a shot was fired. **Measure it post-Phase-2, not before**: measured today, ~43% of shooter-ticks would be vetoed, but 906 of those 2032 ticks are a shooter *standing inside a wall*, for which LOS is false by construction. The honest post-fix figure is ~21%. Anyone who measures this before the movement fix concludes the gate is twice as expensive as it is. Hand the author the roster table in D17's shape; do not decide it in the collision rework.
2. **Re-derive what the league asks.** D13 states the rival constants are downstream of measured player harvest; `LeagueTable` has `GoodRun = 500`, `RivalHandicap = 0.9` (ceiling 450), `FinalistPressure = 0.9`. D25 measured that the player needs **400 a round to win and never wins below 375**. Both figures move. Re-run `SeasonSweepTests` / `SoakTests` across ten seeds and re-derive that pair. Keep `WhereYouFinish_DependsOnHowYouPlay` — it pins the ordering, which is what survives a retune.
3. **Build, publish, play.** Bump `bundleVersion`. Set the Unity process affinity to ~4 cores before the build. `force-recompile` first, *then* touch the sentinel. Then play the whole loop — standings → raid → review → shop → raid — in a real 523×293 iframe.

**How it is proven.** Season sweeps across ten seeds must still answer skill: harvesting nothing eliminated immediately, a good raid winning, `WhereYouFinish_DependsOnHowYouPlay` green. Then `console --types error,warning --count 50` on the last editor run — that sweep, after a *passing* run, is what found D15's silent game.

**Size:** medium.

---

### Phase 8 (optional) — View prefabs

**Goal:** the half of (D) that is straightforwardly right, honestly priced.

Three prefabs — `Adventurer`, `Mob`, `Shot` — each a `SpriteRenderer` with its bar/trail children, instantiated once per simulation index by `DungeonView`. The author is also **right that no pool is needed**: `ProjectileFeed` is a plain `List<Shot>` and the view already recycles `_shotViews`.

**Two things that will otherwise waste the phase.**

1. **They must live under `Assets/Art/Resources/prefabs/`.** The play scene is code-generated and holds only a camera and the controller (`DungeonSceneBuilder`, deliberately — serialized scene values outrank code defaults), and `DungeonView` is constructed in code, so **there is no inspector field to serialize a prefab reference into**. Every existing prefab sits under `Assets/Art/Resources/vfx/` and loads via `Resources.Load<GameObject>($"vfx/{prefab}")` (`DungeonView.cs:292`) for exactly this reason. Carry the consequence forward: `Resources` content is never stripped, so these land in the WebGL build whether referenced or not.
2. **Price it honestly.** The prefab owns **sprite assignment, child structure, particle settings, materials** — things with no code equivalent. **Sorting bases, bar widths and motion constants stay in C# and are applied at `Instantiate` time**, because CLAUDE.md's "serialized values beat code defaults" trap applies to prefab assets exactly as it does to scenes. So `DungeonView` shrinks by the child-construction code, *not* by the constants. A reviewer claimed both ("prefabs delete the bar/colour/sorting construction" and "keep every gameplay-visible number in C#"); only one can be true.

**Write the boundary into DECISIONS.** A view prefab may contain `SpriteRenderer`, `Animator`, `ParticleSystem` and child transforms. It may **not** contain a `Rigidbody2D`, a `Collider2D` the simulation reads, or any MonoBehaviour holding game state.

**How it is proven.** `AnimationOnScreenTests.WalkingAdventurers_CycleThroughDrawnFrames` samples the real renderer during a real raid and demands more than one distinct sprite reach it — that is the guard that caught `FrameFor` never being called, and it must still pass. Add one assertion that no instantiated view prefab carries a `Rigidbody2D` or `Collider2D`, so the boundary is enforced rather than documented. Add one that reads a live view object's sorting order and bar width and compares them against the C# constants, so a stale serialized value fails loudly. Then screenshot, and check it in the 523×293 embed.

**Size:** small–medium. **Cut this before cutting anything above it.**

---

## What we are NOT doing, and why

**Cut from (D) — the Unity-systems half.**

1. **Unity NavMesh, `com.unity.ai.navigation`, NavMeshPlus, A\* Pathfinding Project.** Four independent disqualifiers, any one sufficient. (i) `com.unity.ai.navigation` is **not in the manifest** — only the 3D, XZ-plane `com.unity.modules.ai`; stock Unity has no 2D navmesh. (ii) ~288 headless tests tick `new Raid(layout).Tick(0.02f)` with no scene; a `NavMeshAgent` needs Play Mode and Unity's own update order and cannot be stepped at a fixed dt. (iii) The dungeon is rebuilt per raid **and after every shop purchase**, against a 250ms budget — a runtime re-bake is a main-thread stall on single-threaded WebGL, and every door toggle (the cheapest, most spammable verb) would need obstacle carving. (iv) Agent avoidance is not reproducible from a seed, and seeded reproduction is a CLAUDE.md hard constraint pinned by `SoakTests.ASoakSeason_ReplaysFromItsSeed`. **And the argument that settles it:** a NavMeshAgent would path *correctly* through an open door — which is precisely what the retreat valve forbids — so the room bound would have to be reimplemented on top of it anyway, leaving strictly more code enforcing the same rule twice. The grid it would replace is 133–217 cells and ~70 lines. **Amend D4 with a dated entry** rather than reopening it.
2. **`Rigidbody2D` / `Collider2D` / `Physics2D` in the simulation.** Grep returns **zero** physics symbols anywhere under `Assets/Dungeon`. This is not a broken integration to fix, it is a first integration, and every line of it is new risk against the property that makes 288 headless tests and seeded replay possible. A ~20-line axis-separated `Slide` gets the same visible result deterministically. **Write the line into DECISIONS: prefabs and Unity systems for the VIEW; the simulation stays plain C#, fixed-step, seeded, scene-free.**
3. **A `Physics2D` raycast on the projectile.** It would fix only the picture. Damage is applied at `Raid.cs:480`, *before* `Shots.Fire` at `:485`, and `Shot`'s own XML says it is purely presentational — so the arrow would stop at the wall while the health bar still moved.
4. **Tilemap conversion of `DungeonScenery`.** `com.unity.2d.tilemap` *is* installed and `DungeonScenery.Build` really does create one GameObject per cell (133 on a 3-room corridor, ~250 on five). It is the largest "more Unity idiom" win available and it is still **polish on a game that already ships**. Its two hazards — draw order and the 4-pixel lighting grid — are both invisible to every assertion, and `DungeonView.cs:90-113` documents a sorting bug that made a party member vanish mid-raid. Not in a jam pass.
5. **GC / allocation work.** The headline "10^5 heap allocations per second" is an arithmetic estimate presented as a measurement, and it over-counts: `MobPack.Tick` skips `FindPath` entirely for any mob whose home room the party has left (`:240`) and for any mob already inside `ContactRange` (`:284`), which is most mobs most of the time. The same review's own open question concedes nobody has measured GC in a browser. **One session with the profiler open in the itch embed decides whether this phase exists at all.** (Opportunistically, while `AdventurerAI.cs` is open in Phase 3: `Nearest` (`:531-536`) is `points.OrderBy(...).First()` — a full sort with a freshly allocated comparer closure, reached from `SpeedMultiplier`, `Cornering`, `MageGoal`, `HealerGoal` and `TryFindBlink`, i.e. several times per member per tick. Two lines. Not a phase.)
6. **Splitting `Party.cs` as a gating item.** It is 1240 physical / ~664 logical lines against a 400-line cap and it genuinely should be split. But Phase 3 barely adds to it, and a behaviour-preserving four-way split whose merge gate is bit-identical harvest is a session's work that changes nothing a player can see. Do the splits the work *forces* — `CombatResolver.cs`, `RateModifiers.cs`, `GridMovement.cs` — and leave `Party.cs` for M10. (Note: `DungeonGrid.cs` is 436 physical but **236 logical**, and ArchitectureGuidelines §8 defines the unit as non-blank, non-comment. A reviewer invoked the cap against the one file in the set that does not breach it. The new-file decision is still right, on cohesion and module ownership.)

**Cut from (C).**

7. **Ricochet.** The author said "bounce off"; the recommendation is **stop and absorb**, with a chip of masonry on the wall. Four reasons, and he should hear them: damage is already applied at shot creation, so a ricochet that could hit something else reverses that decision and breaks "the number you saw matches the bar that moved"; a bouncing arrow makes ranged damage a geometry puzzle the player has no verb to aim, and all three verbs are the dungeon's; flight is 0.22s, so a ricochet is two or three frames and unreadable; and what he actually described seeing was an arrow *crossing* a wall. **Phase 2 goes further and stops the illegal shot being fired at all** — a shot never loosed cannot be drawn crossing anything, and the view needs no change (`RefreshShots` already lerps From→To; the impact burst already fires at `shot.To`).
8. **Making `Shot` carry damage.** Follows from 7. A larger change to how damage resolves.
9. **Rewriting `HasLineOfSight` as a DDA.** See Phase 3, change 6. A 2.2–2.7% silent change to three AI positioning decisions the energy curve prices, dressed as a perf refactor — and the perf case is weak anyway, since both callers short-circuit (`if (distance >= bestDistance || !HasLineOfSight(...))`), making the expected call count the harmonic number (~4.5 at 48 threats, not 48).
10. **A line-of-sight gate inside the combat resolver.** Redundant on convex rectangular rooms once the actor-room check lands, and the only half that can misfire.
11. **Line of sight on mob targeting.** Mobs use none today; room-bounding does the whole job and CLAUDE.md calls it load-bearing. Every historical change to how a mob picks quarry has broken the valve (D26). Out of scope beyond Phase 3's one-line landing guard.
12. **Gating `AssignActions` on line of sight in the same change as the resolver fix.** Correctness and pricing must be separable in the diff, or the rate regression and the geometry fix become indistinguishable. Deferred to Phase 7 with a number attached.

**Cut from (B).**

13. **A taunt ability with a cooldown.** Reintroduces the on/off target flip behind the "wagging" standoff; adds hidden timed state to a game that shows no numbers; adds an ordering-sensitive state machine to a seeded simulation.
14. **The literal reading of B3's second sentence** (any enemy in the same room attacks that member, i.e. no reach gate). Deletes `AMonster_CannotHurtWhatItCannotReach`, the healer flee, the archer kite and the blink in one edit.
15. **Widening the global `threats` list handed to `Party.Tick`.** Every reviewer who proposed "per-member engagement" as a single global widening missed that four load-bearing gates read `threats.Count`: `ForceDoors` returns immediately when `threats > 0` (`:882`) — a mob beside a straggler stops all door work, which is D21's freeze shape; the disarm branch is `if (member.Role != Ranged || threats.Count > 0) continue;` (`:431`); `OpenChests` requires `Goal == Advancing` (`:480`), so held threats stop chest looting — D11's entire stall mechanic; and the escape condition is `if (Goal != Fighting && left)` (`:462`), so a mob walked past but still co-located could prevent the raid ever reaching `PartyEscaped`, which `MobBehaviourSweepTests.AClearedRoom_LetsThePartyThrough` asserts directly. Phase 2's **per-combatant** scoping delivers B3 without touching any of them. Widening the pricing list is a separate, separately-measured change — see open question 7.
16. **Editing `Party.DistributeDamage`'s 60/40 rule as if it were the aggro system.** It has no production caller.
17. **Switching `ChooseGoal` to `WoundFraction`** to make the retreat valve fire before a death. D18 measured this exact fix: peak rate fell from 25.8–37.8/s to 4.1–9.4/s, engagement from 44–74% to 10–12%, and two rosters that had kept a survivor wiped instead. Reverted then; do not retry it inside an aggro change. If Phase 4's sweep shows no roster retreats before losing anybody at any `Crowd` value, D18's lever (`RetreatThreshold` near 0.12 on `WoundFraction`) becomes the author's decision, not an implementation detail.

**Cut from (A).**

18. **+2 per member**, **+2 as a multiplier**, and **any modifier inside the per-member wound multiply.** Arithmetic in "What the author asked for".
19. **An unfloored A5.** SPEC §1, through the front door.
20. **An uncapped or wound-scaled A4.** Pays for the losing line at zero marginal cost.

**Cut, general.**

21. **A fourth verb.** Nothing here proposes one — the modifiers are all read-only consequences of what the party does, the aggro preference is not a player input, and the player still cannot call a mob off. Stated so the next reader can check.

---

## ANSWERED BY THE AUTHOR — 2026-08-14

These override the open questions below. Recorded verbatim in substance.

**Q1 — score or purse? SCORE.** *"the whole point is to make them traverse the dungeon. So it
should pay the score for the first."* So A2 (entering a new room) credits `EnergyHarvested`, not the
purse. The reviewers' worry — that this pays for *advancing*, which the door verb exists to prevent —
is noted and overruled by the author, who wants traversal rewarded. Their instinct is defensible:
the room bonus is small (+2/s for 3s = 6 energy) against a fight's ~8/s, and A5's decay is the
counterweight that stops a party being parked in one room forever.

**He also asked what the difference is** — *"I thought they were one and the same."* They are not,
and it is worth keeping straight: `EnergyHarvested` is the **score**, only ever rises (bar the -50
death penalty), and is what the league ranks. `TotalEnergy` is the **purse**: the same income lands
there but spending on spawns and traps removes it, and what survives funds the shop. A raid can
harvest 400 and end holding 90. Same income, different accounting. D24's spawn refund credits the
purse only, for exactly this reason.

**Q4 — what resets the combat decay? TEN SECONDS OUT OF COMBAT.**

**NEW, and not in the original request — COMBAT FATIGUE.** The author added a movement rule that
pairs with A5:

> *"the team moves slower when they are in the thirty second counting... when they have passed ten
> seconds, and then they have ten seconds more, they move slower. If they are in combat for
> seventeen seconds, then for ten seconds they would just move slower. In combat for five seconds,
> they move the same speed. It's not much — like eighty percent of normal speed — but you'll see the
> speed up after a while, which is a significant twenty percent speed up."*

Read as: after **10 seconds of continuous combat** the party drops to **80% movement speed**, and
the fatigue persists for **10 seconds after combat ends** before lifting. Under 10 seconds of combat
costs nothing. He said "thirty percent slower" first and then settled on "eighty percent of normal
speed"; the later, more specific figure wins — **80%, a 20% reduction**.

The "significant twenty percent speed up" is the fatigue *lifting* — returning to full speed reads
as a speed-up. Not a separate bonus above 100%.

This is the same idea as A5 expressed in movement rather than money, and it is the better half: a
grinding party visibly slows down, so the player *sees* the boredom the decay is pricing, instead of
only watching a number shrink. Both should share one timer — continuous-combat seconds, reset by ten
seconds clear.

---

## Open questions for the author

Only the ones that genuinely change the work.

1. **Should A1 and A2 pay the score, or the purse?** *Literal reading* (+2/s on the rate) measurably breaks `EarlyEscape_EarnsFarLessThanAFullRaid` and `TheRate_DoesNotFlickerDuringAFight`, and contradicts SPEC §3's "an unengaged party walking a corridor must earn almost nothing" — because it pays for *advancing*, the exact behaviour the door verb exists to prevent. *My reading* credits the purse (D24's spawn-refund pattern), so exploration buys more monsters rather than more score. *A third option, if it must be score:* gate the room bonus on the room **containing something** — a live monster, an unlooted chest, an armed trap — so it pays content encountered rather than ground covered. This is a 3–4× swing on two of the five modifiers and it needs deciding before Phase 5b is written.
2. **Confirm "+2" is +2.0 energy per second for the whole team**, in the same units as the chest's +6/s. And confirm **"the rogue"** means the **Ranged** member, who is who disarms traps today (D9) — there is no Rogue role.
3. **"Shots bounce off" — visual deflection, or simply stop?** Stopping is three lines and Phase 2 goes one better by never firing the illegal shot. A ricochet that can hit the party is a new damage source, a new visual, and a new way to kill the adventurers by accident — which is the losing state.
4. **What resets A5's timer?** The party's `Fighting` goal (which carries a deliberate 1.4s `CombatGrace`) or "no living monster in the party's room at all"? The two differ by whether a stream of fresh spawns is one long fight or many short ones — which is the entire substance of the request.
5. **"Entering a NEW room" — new this raid, or new ever?** Purchases are permanent for the season and the party explores toward the nearest unseen room, so "new ever" pays nothing from round three onward while "new this raid" pays every raid.
6. **Does variety have to be strictly the *best* play, or merely worth doing?** Under the literal schedule a crowd held in one room still out-earns varied play. Making variety strictly best costs one constant (the decay step). This is a design call, not a bug.
7. **Should a straggler under attack be *paid* for it?** Phase 2 makes a mob in room 1 hit a healer in room 1 while the leader is in room 2 — but `AssignActions` still prices that healer from the leader-scoped threat list, so it earns `Walking` 0.06/s while bleeding. That underpays the situation the design most wants. Fixing it means splitting the threat list in two: a per-member proximity list for pricing and AI, and a leader-scoped count for the four party gates in cut 15. Worth doing, but it is its own measured change.
8. **Prefabs: view-only, or gameplay values too?** View-only (sprites, particles, child structure) is safe and is Phase 8. Putting numbers on them reintroduces the serialized-beats-code trap that `DungeonSceneBuilder` was written to avoid and that CLAUDE.md lists as costing real time.
9. **Jam scope.** This is nine phases against a game HANDOVER calls feature-complete. If the goal is a better build for voting rather than continued development: **Phases 1, 2, 4, 5 and 6** deliver most of what was asked at a fraction of the risk, and **Phase 3 is the one worth deferring past the deadline.**

---

## The one change most likely to break the shipped game

**Phase 3 — bodies staying out of walls.**

Not because it is hard, but because its failure mode is *invisible to the fix's own acceptance test*. Clamping movement turns "walks through the wall" into "presses against the wall and stops", and a party that never moves scores a **perfect zero** on every violation counter the fix is judged by. This project has shipped that exact shape three times with a fully green suite: D11 (a 0.45 chest reach parked the leader beside a chest for the whole raid — "every assertion about placement, price and drawing passed"), D21 (all six rosters stood on cell (5,3) for 53 seconds with two green tests *protecting* the freeze), and D19 (a stuck roster harvests 3 energy over a whole minute). A fourth test, `ExploratoryTests.NobodyEverLeavesTheDungeon`, carries a docstring claiming it catches bodies "inside a wall" and asserts only map bounds ±3 cells — it is the very test a reader would assume already covers this.

Three specific edges, all verified in the code:

- **Three of four members are already outside the walkable dungeon at tick zero** (`PlaceFollowers`, arithmetic above). A `Slide` that refuses illegal moves without a recovery path pins them there permanently.
- **A leader clamped onto a doorway makes `RoomAt(Party.Cell) == NoRoom`**, which empties `engaged`, no-ops both swing loops, and prices all four members as Walking. The party becomes simultaneously invulnerable and unpaid, with the sim running and every assertion green.
- **`StandOff`'s radius-3 ring has no valid candidate** for a mob near the middle of a 5×5 room (max centre-to-interior distance 2.83), so a naive fix herds the archer into a doorway — which is the previous bullet.

Mitigation is not a better assertion; it is **pairing the violation counter with a liveness probe and looking at the game**. Assert progress, not position: the party's distance to its objective must strictly decrease over any rolling 5-second window in which its goal is `Advancing`, across every roster and layout. Assert that `RoomAt(Party.Cell) == NoRoom` does not rise as a share of ticks. Keep `UnopposedParty_TakesMostOfTheClockToCross` and `AnUnboughtDungeon_CanAlwaysBeCrossedInTime` as the pacing pins and re-tune `WalkSpeed` rather than widening either bound. Then photograph a raid.