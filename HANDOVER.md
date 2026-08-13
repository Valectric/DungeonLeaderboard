# Handover

**State: Milestone 1 built and playable. The gate is open for the author to judge.**

Last updated: 2026-08-13.

---

## Read these first, in this order

1. **`SPEC.md`** — the author's design, verbatim. The authority on what this game is.
2. **`CLAUDE.md`** — architecture, working loop, and the toolchain traps. Several cost a day each in
   the sister project, and the sprite-generation section now carries four more that were paid for
   here.
3. **`PLAN.md`** — milestone order.
4. **`DECISIONS.md`** — D1–D7. Read before reversing anything.

---

## What exists now

**The sixty seconds runs.** One corridor of three rooms, a party of four entering left, boss room
right, a 60-second clock, and the three verbs wired to mouse clicks on dungeon elements.

```
Assets/Dungeon/
  Application/Game/   GameController (raid loop, clicks, HUD), DungeonView (all rendering),
                      Scenes/Raid.unity (generated -- never hand-edit), Tests/ (E2E + verb clicks)
  Modules/
    RaidManager/      EnergyCurve (the formula), Raid (clock, verbs, combat, end conditions)
    DungeonManager/   DungeonGrid (cells, rooms, doors, BFS), DungeonLayout (the corridor)
    PartyManager/     Adventurer (roles, wounds), Party (advance / fight / retreat AI)
    MobManager/       Mob, MobPack (spawning and room-bounded pursuit)
  Editor/             scene builder, WebGL builder, sentinel poller, pixel-art importer
```

**43 tests green**, console clean: 31 unit, 5 E2E against the shipped scene, 7 driving the verbs
through real screen coordinates.

**49 sprites**, all 64x64 at PPU 64 so one tile is one world unit. 38 were cut from the moodboard by
`Tools/extract-moodboard.py`; 11 tiles were sliced from a generated atlas by `Tools/slice-tileset.py`.
Both are deterministic and rerunnable.

---

## Three bugs that a fully green suite did not catch

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

**First, answer Milestone 1's gate: is stalling a party with doors for a full minute satisfying?**
That is the author's judgement and nothing downstream rescues a bad answer. Everything below is on
hold until it is answered yes.

If yes, `PLAN.md` M2 is the league — and it is the 10-second hook, so it is what a jam voter sees
first.

Known rough edges, none blocking the gate:

- **Rooms are bare.** Eight atmosphere props are already extracted and unused (`Resources/props/`:
  candles, crystals, banner, books, noticeboard, chest). Scattering them is a cheap, large win.
- **No animation yet.** The spec wants wounded state read from limping and panicking, and only the
  three static wound sprites carry it today. The path is proven: `--command character` with an
  extracted sprite as the master routes to the deterministic rig, which renders identical frames
  from the same rig JSON. ImageGen never touches the party.
- **The HUD is immediate-mode.** Chosen so a missing dynamic font cannot black out a WebGL build. It
  works but looks nothing like the moodboard's HUD.
- **`Assets/Art/` is committed now** — regenerating art overwrites tracked files, so check
  `git status` after any rerun of the art tools.
