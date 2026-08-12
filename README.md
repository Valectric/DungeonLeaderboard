# Dungeon Leaderboard

A 2D top-down dungeon management game. **Game jam entry — theme: CHARGE!**

> You're a dungeon core in a competitive league. Adventurers charge in, you charge up. Milk them for
> every drop of energy, keep them alive, and don't finish bottom 10%.

**Status: foundation only.** The project, toolchain and design are set up; the game itself is not
written yet. See [`HANDOVER.md`](HANDOVER.md).

## The idea

Killing the adventurers is **bad play**. A dead party stops generating energy, and so does one that
reaches the boss room too quickly. The best outcome is a party that is alive, in combat, badly
wounded, and still inside your dungeon when the sixty seconds run out.

The theme lands twice: the adventurers *charge in*, and the core *charges up*.

## Documents

| File | What it is |
|---|---|
| [`SPEC.md`](SPEC.md) | The design, verbatim. The authority on what this game is. |
| [`PLAN.md`](PLAN.md) | Milestone order, and what Milestone 1's gate gates. |
| [`DECISIONS.md`](DECISIONS.md) | Dated, reasoned decisions. Read before reversing one. |
| [`HANDOVER.md`](HANDOVER.md) | Current state and what to do next. |
| [`CLAUDE.md`](CLAUDE.md) | Agent guide: architecture, working loop, toolchain traps. |

## Building

Unity **6000.3.17f1**, 2D URP. Open the project and Unity resolves everything from
`Packages/manifest.json`.

```bash
mooserunnerCli force-recompile     # exits Play Mode first — order matters
touch .dungeon-build-webgl         # builds WebGL into Builds/
bash Tools/publish-itch.sh         # uploads to itch.io with butler
```

Tests run through [MooseRunner](https://www.valectric.com), which resolves from a scoped registry
declared in the manifest. It is a licensed tool; the repository does not redistribute its binaries.

## Licence

[MIT](LICENSE). Third-party Unity packages are governed by their own licences.
