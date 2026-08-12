# Handover

**State: foundation only. No game code exists yet, by design.**

The project is set up, the toolchain is verified working, and the design is written down. The next
session builds Milestone 1.

Last updated: 2026-08-12.

---

## Read these first, in this order

1. **`SPEC.md`** — the author's design, verbatim. It is the authority on what this game is.
2. **`CLAUDE.md`** — architecture, working loop, and the toolchain traps carried over from the
   sister project (BackroomsDemo). Several of those cost a day each there. They are not
   hypothetical.
3. **`PLAN.md`** — milestone order, and what Milestone 1's gate actually gates.
4. **`DECISIONS.md`** — D1–D5. Read before reversing anything.

---

## What is done

- Unity **6000.3.17f1**, **2D URP** (Renderer2D), from the Hub's 2D template.
- `Packages/manifest.json` → Valectric + OpenUPM scoped registries, **MooseRunner 2.2.5**, UniTask,
  `com.unity.recorder`. All resolved.
- **`mooserunnerCli ping` answers `PONG`.** The licence is active machine-wide; no UI step needed.
- `TestingGuidelines.md` and `ArchitectureGuidelines.md` generated from this project's own CLI, and
  `@`-imported by `CLAUDE.md`.
- `.gitignore`, git repo, clean history.
- `Assets/Dungeon/Editor/` — WebGL builder, sentinel poller, build assembly filter. `Dungeon.Editor`
  deliberately references **nothing**, so it does not constrain how the game is structured.
- `Tools/publish-itch.sh` — butler upload. Needs `Tools/itch_target` written once.

## What is deliberately NOT done

- **No game code.** Not a line. The next session writes it.
- **No scene.** `Assets/Scenes/SampleScene.unity` is the template's and is untouched.
- **`Assets/Dungeon/Modules/*`** contains empty `.asmdef` files for five speculative modules
  (DungeonManager, PartyManager, MobManager, RaidManager, UIManager) with no code in them. They were
  scaffolded before the "foundation only" instruction landed and a delete was declined, so they are
  still there. **Treat them as a suggestion, not a decision** — rename, restructure or delete them
  freely. Nothing depends on them.
- No GitHub remote. The repo is local only.
- No itch.io project or `Tools/itch_target`.

---

## First moves for the next session

```bash
cd C:/Users/JohanHoltby/Documents/GitHub/DungeonLeaderboard
./MooseRunner/mooserunnerCli.exe ping          # expect PONG
```

Then start Milestone 1 from `PLAN.md`. The one thing worth deciding before writing code:

**Make the energy rate a pure C# function and test the curve, not just the total.** The rate is the
entire game — `base × engagement × wound`, and the spec wants the last sliver of a health bar to be
where most of the money is. In the sister project a *rate* bug survived a fully green suite: pathing,
states and catching were each individually correct while the thing that actually mattered — how often
the player met a monster — was measured by nothing. A test that asserts "energy went up" would pass
with a flat curve and a ruined game.

One trap that will bite immediately if it is not known: **a brand-new test `.asmdef` needs TWO
`force-recompile` passes** before `test --assembly` finds it. The first reports "Assembly not found
in loaded assemblies". That is normal, not a broken setup.

---

## Open questions for the author

- **GitHub remote** — create one? Public or private? Nothing has been pushed anywhere.
- **itch.io page** — needs creating, then `echo 'user/dungeon-leaderboard:html5' > Tools/itch_target`.
  The `BUTLER_API_KEY` must be added by the author; no agent should handle it.
- **Those empty module asmdefs** — keep as a starting structure, or delete?
