# Handover

**State: M1, M2 and M3 built, tested and published to itch.io. M1's gate was answered yes by the
author. The whole loop runs: standings, a raid, standings, a shop, the next raid.**

Last updated: 2026-08-13.

---

## Read these first, in this order

1. **`SPEC.md`** — the author's design, verbatim. The authority on what this game is.
2. **`CLAUDE.md`** — architecture, working loop, and the toolchain traps. Several cost a day each in
   the sister project, and the sprite-generation section now carries four more that were paid for
   here.
3. **`PLAN.md`** — milestone order.
4. **`DECISIONS.md`** — D1–D11. Read before reversing anything.

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
  Editor/             scene builder, WebGL builder, sentinel poller, pixel-art importer
```

**93 tests green**, console clean: 73 unit, 5 E2E against the shipped scene, 15 driving the verbs and
the shop through real screen coordinates.

Two numbers worth carrying: an unopposed party crosses the corridor in **26.9s** of the 60, and one
bought chest adds **5.6s** to that. Both are asserted, because both are rates and rates are what this
game is made of.

**49 sprites**, all 64x64 at PPU 64 so one tile is one world unit. 38 were cut from the moodboard by
`Tools/extract-moodboard.py`; 11 tiles were sliced from a generated atlas by `Tools/slice-tileset.py`.
Both are deterministic and rerunnable.

---

## Five bugs that a fully green suite did not catch

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

M1's gate is answered and M2 and M3 are shipped. `PLAN.md` **M4** is next: party composition
variation first, because SPEC.md calls it the primary source of run-to-run variety and the shop now
gives the player something to vary *against*.

**One thing needs the author, not an agent:** the itch embed is **523x293** against a 960x600 canvas,
so it crops the HUD and the shop. Fix it in *Edit theme* — set the frame to 960x600 and the page
background to `#15101D`.

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
