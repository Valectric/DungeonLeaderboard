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

## M4 — Everything else

Party composition variation (the spec's primary source of run-to-run variety — exploit it before
adding dungeon content), mob variety, art pass, end-of-raid star reviews.

---

## Not until the three verbs are proven

The spec is explicit: **do not add a fourth verb.** Anything below is off the table until M1's gate
is answered yes.

- Named recurring adventurers
- Tiered mobs unlocked by core charge
- More dungeon content of any kind
