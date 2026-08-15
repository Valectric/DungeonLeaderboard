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

## M9 — Walls, aggro, and paying for variety ▸ *done*

Directed by the author after playing M8: more variation in the rate, a real aggro rule, solid walls,
and a push toward Unity idiom (prefabs, built-in pathfinding).

**The full plan is `M9-PLAN.md`** — nine phases, the cuts, the open questions, and the one change
most likely to break the shipped game. It was produced by five independent reviewers, each stress
-tested by an adversarial critic (every one came back `needs-changes`, 4–7 broken constraints each)
and then synthesised. Read that file before starting any of it; what follows is only the shape.

**Phases, in a forced order.** Three of the four asks terminate in the same seven lines
(`Raid.AccrueEnergy`, `Raid.ResolveCombat`), so tuning the rate against a resolver that still fires
through walls tunes it against the wrong game.

| # | Phase | Why it is here |
|---|---|---|
| 0 | Measure the walls *(done, `a124c5d`)* | 11.7% of samples inside a wall, 13.7% of shots through one |
| 1 | Truth up the instruments | five `EnergyCurveTests` assert a curve the game no longer runs |
| 2 | Shots stop at walls | never fire the illegal shot, rather than drawing it and stopping it |
| 3 | Bodies stay out of walls | **the dangerous one — see below** |
| 4 | Aggro the game actually runs | there is no aggro system; the tank is hit because it walks in front |
| 5 | The rate variation | the author's five modifiers |
| 6 | Make the modifiers visible | a bonus the player cannot see is a bonus they cannot learn |
| 7 | Re-measure once, then ship | league constants stay frozen until here |
| 8 | View prefabs *(optional)* | the safe half of the architecture ask |

**Two standing rules for the milestone**, both already paid for once:

- **A wave of red is evidence about the change, not a list of chores** (D23).
- **The league constants are frozen until Phase 7** (D13): four phases move harvest, and re-tuning
  per phase means chasing four different games.

**The one most likely to break the game is Phase 3**, and not because it is hard. Clamping movement
turns "walks through the wall" into "presses against the wall and stops" — and a party that never
moves scores a **perfect zero** on every violation counter the fix is judged by. This project has
shipped that exact shape three times with a green suite (D11, D19, D21). The mitigation is a
liveness probe paired with the violation counter — assert *progress*, not position — and then
photograph a raid.

**What was cut, and the argument that settled the biggest one.** Unity NavMesh is out on four
independent grounds, but the one that ends the discussion is this: a `NavMeshAgent` would path
*correctly* through an open door, which is exactly what the retreat valve forbids — so the room
bound would have to be reimplemented on top of it, leaving strictly more code enforcing the same
rule twice. The grid it would replace is ~70 lines over 133–217 cells. Prefabs and Unity systems go
in the **view**; the simulation stays plain C#, fixed-step, seeded and scene-free.

---

## M10 — The end screen and the loading screen ▸ *done*

Asked for by the author on 2026-08-14, after M9 Phase 5:

> *"Create a great end screen. Ask codex CLI for image creation. Also do the same for loading scene
> but still keep a short two sec made with Unity."*

Read as: both screens get **generated key art** as a backdrop, produced through the sprite-maker's
Codex-backed pipeline rather than drawn by hand or fetched. The loading screen keeps a **short
Unity-rendered animation of about two seconds** on top of that art — so the art is the backdrop and
the motion is still real, not a video.

**Before touching this, read the sprite generation section of `CLAUDE.md` in full.** It is the
longest section in the file because every trap in it cost a run:

- Run the **preflight** once per session. `codex` must be on the PATH of the shell that launches the
  binary, set in the *same* command — the lookup is cached per process and cannot be fixed
  afterwards.
- **Always `--print-prompt` first.** It is free and prints the routed harness, the asset category and
  the logical canvas. Any `NxN` token in prose silently overrides `--width/--height`; the word
  "platform" flips the preset to a side-view platformer.
- **Never generate into `Assets/Art/`.** Stage outside the repo and copy in as a deliberate second
  step. `Assets/Art/` is untracked, so a mistake there is unrecoverable.
- **Paste the palette string verbatim** and attach the cropped references — but *not* when rigging
  art that already exists, where the source master is the palette.
- **Review every batch** with `python Tools/sprite-contact-sheet.py` before importing, and report the
  warnings. Nothing else in the pipeline checks the output.

**Two things specific to this request**, neither of which the existing pipeline has done before:

1. Everything generated so far is a **sprite** at 32–64px. A full-screen backdrop is a different
   ask, and `--command pack` ignores `--width/--height` entirely. Establish the canvas with
   `--print-prompt` before spending a run.
2. The end screen has to work at the **itch embed's 0.4 UI scale**, where a menu row once rendered
   twelve pixels tall and unhittable. Whatever is drawn over the art must be laid out in absolute
   pixels and checked at that scale, not just in the editor.

**Already learned, from a `--print-prompt` dry run — do not repeat it.** A backdrop prompt sent with
`--command sprite` at 480x270 routes to:

```
routed harness: character
asset category: characters
logical canvas: 480x270 pixels
```

The canvas is right; **the harness is wrong**. That run would have drawn a *character sprite*, filed
it under `assets/characters/`, and the harness brief would have held it to a character quality gate —
eye line, shoulders, head size — for a picture that has no face in it. Nothing but the dry run
reveals this: the flags are all accepted and the composed prompt looks reasonable.

The router infers the harness from the prompt wording, not from `--command`. So the next attempt
should describe the thing as an **illustrated scene or game object** rather than as art containing a
core, bones and torches — nouns that read as subjects to draw. Re-run `--print-prompt` and confirm
the harness before spending a generation. Budget two or three dry runs; they are free.

The leaderboard is the title screen and that does not change — SPEC is explicit, and a loading
screen is not a menu.

---

## M11 — Positioning, placement, and the second phone pass ▸ *done*

Wounded members back off, the healer keeps its distance, the tank holds the line, and the mage
blinks clear. Placement started paying for depth rather than proximity (D29). Portrait scaling and
the healer's flee/return flicker were fixed from a phone report.

---

## M12 — The one-room opening ▸ *done, 2026-08-15*

The author's four asks, and the three defects they uncovered. D31–D33 have the reasoning.

- [x] The run opens on **one room, one slime pit, one chest**, placed through the loadout so the kit
      previews, counts, blocks its tile and survives the grid being re-anchored
- [x] "They left" re-defined, or a one-room raid ends at zero seconds — the party has to have gone in
- [x] A **tap is decided on release**, so the first finger of a pinch is not a click
- [x] A **retreating party forces the door barring its exit** — the safety valve is a valve again
- [x] **First-raid hints** over the opening room, gone from round two
- [x] `LeagueScreen.DrawStrip` threw out of `OnGUI` once rivals were eliminated
- [x] `CanBuyHall` counted from a literal 3, killing the hall marker after two purchases
- [x] The frame-budget test was averaging in every preceding test's scene load

---

## M13 — Can the game be played, and won? ▸ *measured, one open question*

- [x] `RunProgressionTests` plays whole seasons and sweeps the player's one judgement call
- [x] The winning ending reached, drawn and photographed for the first time — and fixed, because it
      was announcing relegations at the winner
- [x] The shop, the collapse screen and the mid-season table photographed; a hall marker was sitting
      on the purse, and the collapse line was drawn in the winner's green
- [x] Portrait screens added to the resolution sweep, which had been computing scale the landscape way
- [x] itch page art and copy built from the game's own pixels (`Marketing/`)
- [x] **The rivals were priced against a raid the game cannot produce.** `GoodRun` read 500 and
      nothing has ever harvested it, so the ceiling sat above the game's own maximum and inverted the
      promise D20 and D25 rest on. Corrected to the measured 430: best of four play-styles went from
      round 7 to round 9, and it now contests the final instead of dying mid-table. See D27.
- [ ] **Still open, and the author's call: nothing has yet won a season by playing it.** The bot
      reaches the final and loses on cumulative score. D25 measured that a player needs ~400 a round
      to win and this bot averages 308, so the real question is whether 400 is reachable by a human
      on a five-room dungeon, or whether `MaxRooms` or the wound curve has to move. `FinalistPressure`
      was tried at 0.55 and reverted — measured, it changed nothing the `GoodRun` fix had not already
      done, and it cost D25's "late on, a rival never has an off day".

---

## M14 — Measured, open for the author

- [x] `RoomsPayTests` measures what a hall is worth, at every dungeon size, on one policy and seed
- [ ] **Halls beyond the third buy nothing.** The party reaches exactly three rooms in sixty seconds
      whatever the size, so harvest saturates at 446 and `MaxRooms = 5` sells two inert halls. Two
      rooms is also wrong in the other direction — the party escapes. See D29 for the numbers and the
      three candidate fixes, which point in opposite directions and are the author's call.

---

## Not until the three verbs are proven

The spec is explicit: **do not add a fourth verb.** Anything below is off the table until M1's gate
is answered yes.

- Named recurring adventurers
- Tiered mobs unlocked by core charge
- More dungeon content of any kind
