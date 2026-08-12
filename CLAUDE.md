# Dungeon Leaderboard — agent guide

A **2D top-down dungeon management game** for a game jam. Theme: **CHARGE!** — adventurers *charge
in*, the dungeon core *charges up*.

Read `SPEC.md` first — it is the design, written by the author, and it is the authority on what this
game is. Read `HANDOVER.md` for current state and what to do next.
Read `DECISIONS.md` before reversing anything — decisions are dated and reasoned.
Read `PLAN.md` for the milestone order and what each one gates.

## The one idea this game is built on

**Killing the adventurers is bad play.** A dead party stops paying. So does a party that reaches the
boss room early. The best outcome is a party that is *alive, in combat, badly wounded, and still
inside the dungeon* when the clock runs out.

Everything else follows from that. If a change makes killing the party more attractive, or makes a
wounded party worth less than a healthy one, it is wrong however well it plays.

```
energyRate = baseRate * engagementMultiplier * woundMultiplier
```

- An unengaged party walking a corridor must earn **almost nothing**.
- The wound curve is steep — full HP ≈ 1×, 20% HP ≈ 4×, 5% HP ≈ 8×+. **Most of the money is in the
  last sliver of a health bar.** That is the game.
- The rate is shown as a large pulsing number. The player has to *see* dead time costing them.

## Hard constraints (do not break)

- **Three verbs only** until they are proven fun: toggle door, spawn mob, fire trap. Do not add a
  fourth. The spec is explicit about this and it is the most likely thing to be quietly violated.
- **No direct mob control, ever.** The player may never call mobs off. The safety valve is
  environmental: open a door behind a losing party and let them retreat and heal. This is the
  central regret and it costs no new verb.
- **Mob pathing must respect room boundaries** — mobs do not pursue past a threshold. The retreat
  valve above depends on it, so it is load-bearing, not polish.
- **Never show a number for adventurer HP.** Wounded state is communicated by limping, blood,
  slowed movement, the healer panicking. Coarse three-state (healthy/hurt/critical) is the fallback
  if playtesting says it is unreadable. Ambiguity between "nearly dead" and "dead in one hit" is
  where the tension lives.
- **The leaderboard is the title screen.** No menu, no logo. The game opens on the standings with
  the player highlighted around 14th and a red relegation line under the bottom two.
- **WebGL build, published to itch.io** for jam voting traffic. No GitHub Pages for this project.
- **Deterministic where it can be** — seeded party generation and seeded AI-dungeon score
  fluctuation, so a run can be reproduced from a seed in a bug report.

## Architecture

Namespace root `Dungeon`; one asmdef per module (`Dungeon.<Module>`, tests `Dungeon.<Module>.Tests`).
Concrete classes, Facade/Router, module-owned TestFacade seams, ≤400 lines/file, XML docs on
everything including tests.

```
Assets/Dungeon/
  Application/Game/       GameController (raid/shop/league flow), Scenes/, Tests/ (E2E)
  Modules/
    RaidManager/          the 60s raid: clock, energy rate, run-end conditions
    DungeonManager/       grid layout, rooms, doors, spawners, traps
    PartyManager/         adventurers: tank/healer/ranged/mage, party AI, retreat
    MobManager/           monsters: spawn, engage, room-bounded pursuit
    LeagueManager/        standings, AI dungeon scores, relegation
    ShopManager/          30s shop, six items, Ready bonus
    UIManager/            HUD (energy, rate, clock), standings strip, shop
  Editor/                 scene builder, WebGL builder
```

## Working loop

```
mooserunnerCli ping                                  # daemon + Unity worker
mooserunnerCli test --assembly Dungeon.<M>.Tests     # unit suites
mooserunnerCli test --class Dungeon.Game.Tests <E2E> # E2E: --class, never --method
mooserunnerCli console --types error,warning --count 50   # ALWAYS after a run
```

Headless editor actions are driven by **sentinel files** at the project root, picked up by an
`EditorApplication.update` poller within ~30s:

```
touch .dungeon-build-scene       # regenerate the play scene
touch .dungeon-build-webgl       # build WebGL into Builds/
```

Deploy: build into `Builds/`, then `bash Tools/publish-itch.sh`. **`Builds/` is gitignored.**

## Traps carried over from the sister project (BackroomsDemo) — read before debugging

These cost real time there. They are properties of the toolchain, not of that game.

- **`force-recompile` prints `[PASS]` even when C# compilation FAILED.** `console --types error` also
  shows *stale* errors from earlier compiles. **The only reliable proof that code compiled is
  running a test.**
- A **brand-new test `.asmdef` needs TWO `force-recompile` passes** before `test --assembly` finds
  it; the first reports "Assembly not found in loaded assemblies".
- **Sentinel builds are a no-op while the editor is in Play Mode**, which is where every test run
  leaves it. **`force-recompile` first to exit Play Mode, then `touch` the sentinel** — touching
  first serialises the scene from a possibly stale assembly.
- **Serialized scene values beat code defaults.** Changing a `[SerializeField]` default does nothing
  to the shipped scene until it is rebuilt. A *new* field takes its code default; a *changed* default
  does not apply. Check the value landed: `grep -n '<field>' Assets/Dungeon/.../<Scene>.unity`.
- **Never run two `mooserunnerCli` commands at once.** A concurrent `status` will be consumed by a
  waiting `test`, which then reports `[WARN] Unexpected` and hangs.
- Long CLI commands report `[MODAL]` when the editor is merely busy building. **Never `unity_stop`
  during a build.**
- **MooseRunner's own runtime assemblies break player builds** — `MooseRunner.Helpers.Runtime.dll`
  references nunit, so IL2CPP fails with `Failed to resolve assembly: 'nunit.framework'`. A build
  assembly filter must strip the whole family, matched **case-insensitively**.
- **Aggressive stripping breaks URP.** Keep `ManagedStrippingLevel.Minimal`.
- **Never ship `WebGLExceptionSupport.None`** — every crash becomes `The error was: undefined`.
- **`Shader.Find` shaders are stripped from builds** unless registered in Graphics Settings; the game
  then renders **magenta**.
- **Bump `PlayerSettings.bundleVersion` every build** or browser caching serves returning players the
  old one forever.
- WebGL builds can exhaust Windows commit memory (`LLVM ERROR: out of memory`). No-admin fix: set the
  Unity process CPU affinity to ~4 cores so child compilers inherit it —
  `(Get-Process -Id <pid>).ProcessorAffinity = [IntPtr]0xF` — then restore it. This happens often
  enough to do pre-emptively before a build.

## Verification doctrine

Green tests are necessary and **not sufficient**. In the sister project three whole classes of bug
were invisible to every assertion and only a rendered frame caught them.

- **Green unit tests hide a broken *rate*.** Every Dweller test passed while the shipped game let you
  cross a floor without meeting one — pathing, states and catching were each correct, and the
  encounter rate, the thing actually broken, was measured by none of them. This game is *made of*
  rates. Assert the energy curve over a simulated raid, not just that energy increases.
- **Photograph the game.** Look tests capture the HUD and the dungeon into `Screenshots/`. Read the
  PNGs.
- **The editor is not the shipping renderer.** WebGL runs a lower quality tier than the editor.

## Testing & architecture doctrine

@TestingGuidelines.md
@ArchitectureGuidelines.md

Regenerate both after any MooseRunner upgrade:
`mooserunnerCli get-testing-guidelines-md > TestingGuidelines.md` (and the architecture equivalent).

## Unity

Unity `6000.3.17f1`, **2D URP** (Renderer2D). MooseRunner `2.2.5` (Valectric npm registry), UniTask
(OpenUPM) and `com.unity.recorder` come from the manifest. WebGL Build Support module required.
