# Handover

**State: M1–M5 built, tested and published. The whole loop runs: standings, a raid, the adventurers'
review, standings, a thirty-second spatial shop, the next raid.**

Last updated: 2026-08-14.

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

## Open question for the author

**Does the game take input on the itch.io page?** Automated clicks never reached it there and a key
press scrolled the itch page instead — but the identical build takes the first click in a same-size
local iframe and standalone, so this is most likely the automation failing to reach itch's
cross-origin frame. A canvas-focus hardening patch was tried and **measured to make it worse** (it
ate the first click in the case that already worked), so it was reverted rather than shipped on
speculation. Open the page, click once on the standings, and see whether the raid starts.

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
