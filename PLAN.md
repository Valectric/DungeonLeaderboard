# Plan

Milestone order is from `SPEC.md` §7 and is deliberate: the game is presentable from Milestone 2
onward, and Milestone 1 carries a **gate** that can stop the whole project.

---

## M0 — Project setup ▸ *done*

- [x] Unity 6000.3.17f1, 2D URP project from the template
- [x] `Packages/manifest.json` → Valectric + OpenUPM scoped registries, MooseRunner 2.2.5, UniTask,
      `com.unity.recorder`
- [x] `.gitignore` (Unity + MooseRunner binaries + `Builds/` + recorder output), git repo, first commit
- [x] `SPEC.md` recorded verbatim, `CLAUDE.md` written with the sister project's toolchain traps
- [x] MooseRunner resolved and `mooserunnerCli ping` answering
- [x] Doctrine files regenerated from the CLI
- [x] Module asmdefs scaffolded
- [x] WebGL builder + sentinel triggers + `Tools/publish-itch.sh`
- [x] Pixel-art import rules (`PixelArtImportPostprocessor`, Point / uncompressed / PPU 64)
- [x] 38 sprites extracted from the moodboard into `Assets/Art/Sprites/` — see D6/D7

---

## M1 — The sixty seconds

One corridor. A party of four entering from the left, boss room on the right. 60s countdown. Energy
counter and rate display. Three verbs: toggle door, spawn mob, fire trap. Run ends on wipe / boss
room / 0:00 and shows total energy harvested.

**Gate: is stalling a party with doors for a full minute satisfying?**

If it is not, stop and fix it before building anything else — nothing downstream rescues a dull core
loop. This is the one point in the plan where the correct response to a bad answer is to redesign
rather than continue.

How the gate gets answered: it is a judgement, and it is the author's to make, not a test's. What the
tests owe it is the *rate* — that a wounded engaged party earns many times what a healthy walking one
does, measured over a simulated raid rather than asserted at a single instant. A green suite that
proves energy accrues while the curve is flat would pass the gate on paper and fail it in the hand.

Also in M1, because the spec calls them load-bearing rather than polish:
- Mob pursuit **bounded by room**, so the retreat valve exists from the start.
- No HP numbers on adventurers — wounded state readable from movement and behaviour.

---

## M2 — The league ▸ *shippable from here*

Standings screen **as the title screen**. 20 procedurally named dungeons, player highlighted around
14th, red relegation line under the bottom two. AI scores fluctuate plausibly each round. Standings
strip visible during play; positions shift with animation after each raid. Bottom 10% ends the run.

This is the 10-second hook, so it is the thing a jam voter sees first. It gets an art pass before
anything else does.

---

## M3 — The shop ▸ *done*

30s countdown, six items (two mobs, two traps, a door, a chest), **Ready** button granting an energy
bonus scaled to the time skipped.

Shipped as built:

- Purse is **whatever the last raid left unspent**, so restraint during a raid buys permanence
  between them. Unspent purse is lost when the shop closes — the phase has to be a decision.
- **Ready** pays 4/s of the time skipped, carried into the next raid as extra *starting* energy, not
  into the purse of a shop that is closing. It is spendable and it is **not** score: the league still
  ranks harvest only, so no one climbs by skipping shops.
- Purchases are **permanent for the season** and land in the dungeon: slime pit and bone pile become
  real spawners of their own kind, traps become more plates, a hall lengthens the corridor (capped at
  five rooms so the boss room stays reachable), and a chest is a detour the party actually takes.
- Measured: one chest costs the party **5.6s** of a 60s raid (26.9s → 32.5s unopposed crossing).

---

## M4 — Everything else ▸ *done*

Party composition variation (the spec's primary source of run-to-run variety), mob variety, art pass,
end-of-raid star reviews.

Shipped as built:

- **Six party compositions**, seeded from the run's one number, announced on the standings before the
  raid and named in the HUD during it. Never the same roster twice running.
- **An RPG stat system** — weapon, might, armour, attack interval — decomposed from the
  damage-per-second figures the game was already balanced on, with rolled damage of ±15%. Visible
  arrows and mage bolts.
- **Mage mana and blink**; healers own their mana too, and both regenerate.
- **End-of-raid star reviews.** The adventurers rate the dungeon like a restaurant, and their taste
  and the player's interest point the same way — five stars and a big number always arrive together.
- **Combat VFX and audio.** Nine particle prefabs and ten procedurally synthesised sounds, both cued
  off the same feeds as the floating numbers. All three verbs acknowledge themselves.
- **Doors the party can force** — an archer picks in 3.5s, otherwise the party batters 520 health.
  Jammed open afterwards, so a door is worth a finite number of seconds.

---

## M5 — The rework pass ▸ *done*

Not in the original plan; added after the author played M4 and asked for two things.

- **The shop became spatial.** The six-card grid is gone. During the thirty seconds the player looks
  at the dungeon the next party will walk into and buys onto it: a marker past the last hall extends
  the corridor, and tapping any empty tile opens a menu of the five things that can stand there.
  Buildable tiles are marked, because the rule is not guessable from the picture. See D16.
- **Everything animates.** Walking gained squash on the footfall and sprites face the way they are
  going; each role attacks in its own shape — a tank lunges, an archer recoils from its own shot, a
  mage rises to cast, a skeleton hits heavier than any of them. Procedural rather than drawn, and
  the reasoning for that is D14.
- **The game gained an ear.** It had been shipping completely silent. See D15.

Measured after the rework: aiming six purchases where the party arrives harvests **397** against
**242** for dumping them in a room the party may never reach, so placement is a decision rather than
a chore. Seasons finish 10th–14th instead of 13th flat.

---

## M6 — The author's redesign ▸ *done, bar two parked items*

Directed by the author after playing M5. The pieces interlock, which is why they landed together.

- **The energy curve pays per person, per action.** Walking lowest, then fleeing, working, shooting,
  melee highest, each times that member's own health. A corpse earns nothing and costs 50 points.
  This replaced a single party-wide "in combat?" flag multiplied by the worst survivor, which made
  being wounded the only thing that paid. See D-notes in `EnergyCurve` and the table in HANDOVER.
- **The standoff is fixed.** Monsters chase the nearest member rather than whoever leads, and close
  directly once inside 2.5 cells. This was the root cause of two rosters earning a ninth of the rest.
- **The league is an elimination with a winner.** Everyone from zero, bottom dungeon out each round,
  last one standing wins. Rivals are handicapped a tenth below the player's own range, so a good raid
  cannot be beaten by luck. See D20.
- **The dungeon is a lattice.** Rooms grow in four directions; the shop offers every free side.
- **The party explores** toward the nearest unseen room, then leaves the way it came in.
- **Tank defence 50%, walk speed x1.5, archers outrun a single monster, monsters two and a half
  times less health.** The last two had to wait for the curve, which is what made them safe.

Everything the author asked for in the session is in. What remains is theirs to decide: the two
balance questions in D20 — round one is sudden death, and a full run is nineteen raids.

---

## M7 — Drawn animation ▸ *done*

The author asked for true sprite animation rather than the procedural motion: real frames for
movement and for attacks, on every entity that moves.

**66 frames**, six per cycle at twelve a second, produced by the sprite-maker's `character` harness:
walk and attack for all four party roles, walk and attack for the skeleton, a hop for the slime.

The harness takes each existing sprite as its **source master and rigs it**, so ImageGen is never
called and the character cannot drift off-model — the output matches its source on average colour
and opaque pixel count. That is also the check every batch was verified with before import, and it
is the only one that catches the three traps the pipeline has: reusing another character's rig,
recolouring toward a pasted palette, and an invalid `--command` failing a whole batch. All three are
written up in `CLAUDE.md`.

The view plays attack frames off `AttackPhase` — the same phase driving the procedural lunge, so the
drawn swing and the sprite's throw at its target are one event — then walk frames, then the static
sprite. Every entity keeps rendering whatever art exists for it.

Guarded by a test that samples the **renderer** during a real raid and requires more than one
distinct sprite to reach it. It caught the animation being entirely dead on arrival: `FrameFor` was
never called, because an edit that was meant to redirect the sprite assignment matched nothing after
an earlier refactor and silently did nothing.

---

## M8 — The second redesign pass ▸ *done*

Directed by the author after playing M7. Three asks and one bug found on the way.

- **Bought halls arrive empty.** A hall used to come with a spawner and a trap already in it, which
  bundled two fittings the player never chose and could not place, and made a room they had furnished
  deliberately look identical to one the builder had stocked. Only the opening corridor is furnished
  now — a dungeon with nothing in it has no verbs to press and earns nothing, so an unfurnished round
  one would be a game over screen with extra steps. Click-to-place already existed and works on the
  bare floor unchanged.
- **Spawning is a loan, not a purchase.** The stake leaves the core while the monster lives and comes
  back when the party kills it, so the player is only ever out of pocket for monsters still standing
  when the clock stops. At a flat 25 the arithmetic argued against the design: a monster killed in
  four seconds had to earn its price back before it was worth pressing, so the optimal play was to
  hoard, in a game whose premise is a dungeon full of monsters the party is grinding through.
- **The survivors get better.** The dungeons knocked out each round are the ones that earned least,
  so a competition whose survivors keep rolling from the opening range gets *easier* as it goes. Only
  the floor rises; the ceiling stays at ninety per cent of a good raid in every round, so the handicap
  promise survives intact and a good run is unbeatable in the final exactly as in round one. Measured,
  the worst round a rival has climbs from 33 to 440.

**The bug**, found by the soak rather than by any unit test: *"a Skeleton left room 1 for room 0"*.
Monsters chase the nearest party member, but the room check was on the party **leader** — so a
straggler across a threshold could be the nearest body and the mob would charge straight out of its
room after them. That is the retreat valve failing at the exact moment it is meant to work. Quarry
selection is now bounded to the mob's own room, and the landing cell is checked as well, because
charging straight at a quarry skips the cell-by-cell path that was the only thing checking.

Also **measured what the league asks**, because the soak only asserted that a competition *resolves*
and so could not tell a tuned league from a formality: the player needs **400 a round** to win and
**never wins below 375**. It answers skill.

---

## Not until the three verbs are proven

The spec is explicit: **do not add a fourth verb.** Anything below is off the table until M1's gate
is answered yes.

- Named recurring adventurers
- Tiered mobs unlocked by core charge
- More dungeon content of any kind
