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
- **Restoring that affinity is not optional, and forgetting it looks exactly like a code regression.**
  A Unity left pinned to 4 of 24 cores took `PerformanceSweepTests.TheFrameLoop_KeepsItsBudget` from
  a mean frame time of **2.5 ms to 370–500 ms** against a 100 ms budget — a red test, reproducible,
  and still red with the machine otherwise idle, so it survives the usual "it was contention" check.
  The tell is that the *simulation* cost is unchanged (310 µs/tick before and after) while the
  *frame* time explodes: the sim is not slower, the editor simply has fewer cores to run it on.
  Restore with the full mask, not `0xFF` — this machine has 24 cores:
  `Get-Process Unity | %{ $_.ProcessorAffinity = [IntPtr]([int64][Math]::Pow(2,[Environment]::ProcessorCount)-1) }`

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

## Sprite generation (Sprite Studio)

Pixel art is generated by a local tool, **not** drawn by hand and **not** fetched from the web.
Full reference: `C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/AGENTS.md`.

**Preflight — run this once per session, before the first generation.** It fails fast
with a clear message instead of a confusing one mid-run:

```powershell
$SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/release/sprite-maker.exe"
if (-not (Test-Path $SPRITE)) {
  $SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/debug/sprite-maker.exe"
}
$STAGE = "C:/Users/JohanHoltby/Documents/sprite-studio-sandbox"
$REF   = "$STAGE/worktrees/dungeonassets/references"

# REQUIRED. The agent's tool shell does not inherit the user PATH, so codex is
# invisible without this. Must be set in the SAME command that runs the binary --
# the lookup is cached per process and cannot be fixed afterwards.
$env:Path = "$env:LOCALAPPDATA\Programs\OpenAI\Codex\bin;" + $env:Path

if (-not (Test-Path $SPRITE)) { "BLOCKED: binary missing - build it, see below" }
elseif (-not (Get-Command codex -ErrorAction SilentlyContinue)) { "BLOCKED: codex not installed" }
else { & $SPRITE help | Select-Object -First 2 }
```

If the binary is missing, build it — needs Bun and Rust:
`cd C:/Users/JohanHoltby/Documents/GitHub/sprite-maker; bun install --frozen-lockfile; bun run tauri build`

Then generate:

```powershell
& $SPRITE generate --workspace $STAGE --prompt "a stone dungeon door, closed" `
  --command sprite --width 32 --height 32
```

**Shell note:** this machine defaults to PowerShell — use `& $VAR` to invoke and
backtick for line continuation. In Git Bash use `"$SPRITE"` and `\`. Do not mix
the two; bash `VAR=value` assignment silently does nothing in PowerShell.

Output lands in `$STAGE/assets/{characters,creatures,terrain,props,effects}/`, plus a manifest at
`$STAGE/.sprite-studio/last-generation.json`. **Read the filesystem to verify a run — never trust the
agent's prose summary.** Enumerate PNGs by mtime and check the manifest.

Then copy approved PNGs into `Assets/Art/` as a deliberate second step.

`--command` accepts exactly **`sprite | animate | character | effect | pack`**. There is no
`creature` — that is a *harness* the router infers from the prompt, not a command, and passing it
fails the whole run with `unsupported --command "creature"` after the batch has already started.

Other flags: `--command animate --frames 6 --fps 12` for animation, `--command pack` for a
coordinated multi-item set, `--print-prompt` for a **dry run** that composes the prompt and launches
nothing. Ranges: width/height 8–512, frames 1–32, fps 1–60.

### Holding the art style — mandatory

The moodboard is the style authority: `$STAGE/worktrees/dungeonassets/references/`.
Style consistency is **engineered, not given**. Use both levers on every run.

**1. Paste this palette string into every prompt, verbatim.** Rewording it causes
drift. Values sampled from the moodboard swatches:

```
Palette: violet-black #251B31, royal purple #50275E, magenta arcane glow #D75268,
burnt orange #85432A candlelight, blood red #6D222F, blue-grey #504D63.
Dark, eldritch, cute-but-grim. Top-down. Chunky readable pixels, dark outlines.
NOT warm brown or tan stone.
```

**2. Attach the cropped references** — already generated, next to the moodboard:

```powershell
& $SPRITE generate --workspace $STAGE --command sprite --width 32 --height 32 `
  --prompt "a dungeon chest. <palette string above>" `
  --reference-category palette   --reference "$REF/style-palette.png" `
  --reference-category art_style --reference "$REF/style-tiles.png"
```

`--reference-category` is **positional** — it applies to every `--reference` after
it until the next one. Max 5 references.

**Never attach `3bfd135a-moodboard.png` itself.** It is 1536×1024 of UI chrome,
typography and labels; the palette is a tiny fraction of its pixels. Attaching it
whole already produced **warm brown/tan** tiles instead of the game's violet —
competent art for the wrong game. Use the crops.

Once a sprite is approved, promote it to a reference
(`--reference-category art_style`) for later runs. Real output anchors style far
better than a moodboard. Generate related assets in **one `--command pack` run**,
not six separate ones — a single run shares one context and stays consistent.

**Nothing checks that output matches the style.** Open the PNGs and compare against
the moodboard before copying anything into `Assets/Art/`.

### One workspace per character, or you will animate the wrong one

Generating a second character into a workspace that already holds a rig **silently reuses the first
one**. Measured: a healer walk cycle requested with `healer-healthy.png` attached as the focused
reference produced six files still named `tank_adventurer_march_down_*`, whose average colour and
opaque pixel count matched the *tank* exactly — (96,81,68)/1115 against the healer's (102,95,69)/1254.
The prose reported success.

The harness brief itself forbids this — *"never reuse an older rig or source merely because its
filename or appearance is similar; when a focused reference is supplied, provenance must trace to
that exact reference"* — and the agent ignored it. Saying so more loudly in the prompt did not help.

**Give every character its own `--workspace`.** The same request into a fresh directory produced
`robed-healer-downward-walk_*` matching the healer exactly, first time.

Check provenance by measurement, never by filename or prose: compare the generated frame's average
colour and opaque pixel count against the source sprite. They should match within a few pixels,
because a rigged animation *is* the source art articulated.

### Do not paste the palette string when rigging an existing sprite

The palette guidance is for **new** art. When the harness rigs a sprite you already have, the source
master *is* the palette, and pasting the violet palette string on top drags it off.

Measured on the archer: the game's is green-hooded at average colour (87,77,43); the run with the
palette string attached came back **purple-hooded with a pink face** at (93,45,73). Same pose, same
bow, same silhouette — recoloured. Green is the colour furthest from that palette and got pulled
hardest, which is why the tank and healer came through unscathed and only this one showed it.

Re-running with no palette string and no palette reference, and the instruction *"preserve its exact
existing colours… do not recolour, retint or restyle any pixel; only articulate the legs and arms"*,
returned (87,77,42) — the source, articulated.

### Review every batch before import — mandatory

Nothing in the pipeline verifies output. After **every** generation run:

```powershell
python Tools/sprite-contact-sheet.py
```

It writes `$STAGE/review-sheet.png` — all recent sprites, 4× nearest-neighbour, on a
checkerboard over a violet-black backdrop so palette drift and stray alpha are
obvious at a glance. Built for reviewing over remote desktop on a phone, where
opening PNGs one at a time is unusable. **Open that one image, not the sprites.**

It also flags two defects that are invisible in a thumbnail and fatal in-engine:

- **chroma-key fringe** — leftover green/magenta pixels from the generator's
  backdrop. `.sprite-studio/terrain_cleanup.py` only strips *magenta*, so green
  survives. Fix by hand or regenerate.
- **transparent edges on tiles** — a floor/wall tile that does not reach every
  canvas edge leaves visible grid lines across the dungeon. Seen in practice: a
  "32x32" floor tile whose art was only 28x28, centred with a 2px margin. When
  asking for tiles, say **"full-bleed, art must reach all four edges, no
  transparent margin"** in the prompt.

Report the warnings to the user before copying anything into `Assets/Art/`.

### The one that cost five runs: `--reference` does NOT reach the image model on every harness

**`--command pack` shows your reference images to the *agent*, and never passes them to ImageGen.**
The agent inspects them and describes them back to you in prose — which reads exactly like it used
them — but the model actually drawing the pixels sees text only. Five tile runs were refined against
a reference the generator had never seen.

Grep the harness files to see which ones forward references:

```bash
grep -rn "referenced_image_paths" \
  C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/resources/skills/sprite-director/references/
```

| harness | forwards `referenced_image_paths`? |
|---|---|
| `terrain tileset` | **yes** — "Supply every attached visual reference through `referenced_image_paths`" |
| `effect` | **yes** |
| `pack`, `character`, `creature`, `prop` | **no — the file never mentions it** |

Two ways to fix it, and it is worth doing both:

1. **Say so in the prompt.** The agent follows instructions, so state plainly: *"When you call
   `image_gen__imagegen`, pass the attached reference images through `referenced_image_paths` so the
   image model sees them, not only you."*
2. **Verify from the log, never from the prose.** After a run:
   `grep -c referenced_image_paths Tools/<run>.log` — zero means the generator was working blind,
   whatever its summary claims.

### State targets as numbers, not adjectives

"Dark, eldritch, chunky readable pixels" produced walls at luminance 83 against a reference at 30,
three runs running. Measure the reference first and put the figures in the prompt — "averages
luminance 30 out of 255, darkest tenth near 5, brightest near 51, and do NOT come back at 83" — then
verify the output the same way. Adjectives are unfalsifiable; numbers are checkable both ways.

**But numbers alone are not sufficient.** A run that hit every stated figure exactly (walls 30.0-31.0,
floors 26.7-28.3, 100% flat on the 4px grid, 6-8 colours) produced flat, featureless slabs with no
masonry at all. Metrics constrain; they do not compose. Always look at the output.

### Always `--print-prompt` first — the routing traps are invisible otherwise

`--print-prompt` is free, deterministic and launches nothing. It prints the **routed harness**, the
**asset category**, and the **logical canvas** the run will actually use. Check those three lines
before every real generation; each of the following was caught that way, and none is guessable from
the flags you typed.

- **Prose containing `64x64` silently overrode `--width 512 --height 512`.** Any `NxN` token in the
  prompt text is read as a canvas dimension. The terrain harness then normalises the whole atlas to
  that canvas — a 512px tileset crushed into 64px. **Never write `NxN` in a prompt.** Say "sixty-four
  pixels square" instead. This is the same normalise step that produced 610-746 unique colours in a
  32x32 on the 2026-08-12 run.
- **The word "platform" flipped the preset to `pixel platformer`.** Preset inference keys off prompt
  wording. "a floor platform 4x3 tiles" read as a side-view platformer. Say "floor area".
- **`--command animate` routes to `prop`/`props`, not `character`.** The prop rig skips the character
  quality gate that holds eye line, shoulders, head size, face and costume steady across frames.
  **For anything with a face, pass `--command character`.**
- **A tileset needs no `--command`.** There is no `terrain` value; the router infers it from the
  prompt. Confirm it landed on `terrain tileset` — the 2026-08-12 run used `--command pack` and got
  six loose files in `assets/props/` instead of one atlas.
- `inferred preset` may read oddly (e.g. `inventory prop`) even when routing is correct. Harmless
  **provided** `--width/--height/--frames/--fps` are all given explicitly, since the harness brief
  outranks the preset. Do not rely on preset defaults.
- **`--command pack` ignores `--width/--height`.** Its composed prompt carries no
  `logical canvas:` line at all, and the agent picks a size from the style presets regardless —
  asking for 64 produced 32. Plan on an exact **x2 nearest-neighbour upscale** rather than fighting
  it; a non-integer resample would soften exactly what point filtering preserves.
- **A pack writes to `worktrees/<slug>/assets/`, not the workspace root.** The agent's own summary
  says `assets/props/...` and it is wrong. Enumerate the filesystem, as always.

### Attach the reference that shows the thing you are asking for

Obvious in hindsight, and the single biggest cause of bad output here. A tile request that attached
only the palette swatches and a strip of **objects** came back flat: 2-4 colours, floor and wall the
same value, no relief — because nothing in the run showed what a floor or a wall was meant to look
like. Re-running with `style-tiles.png` (the moodboard's own TILE / ROOM EXAMPLES strip) attached as
`art_style` produced 7-12 colours, a 0-133 luminance range, and walls twice as bright as floors.

Also **describe the value structure explicitly**, because it is what carries the style: "light
blue-grey blocks with a pale top-edge highlight and near-black shadow beneath, against a much darker
floor". And never ask for "flat colour blocks" or "a compact palette" unless flat is genuinely what
is wanted — that phrasing alone produced the 2-colour tiles.

### Traps — read before first use

- **NEVER generate directly into `Assets/Art/`.** Always stage outside the repo and copy in. The tool
  runs an autonomous agent that writes files; on Windows there is **no OS-enforced sandbox** (Codex
  has no Seatbelt/Landlock equivalent there), so the workspace boundary is a convention, not a wall.
  A bad turn inside `Assets/` can touch things it was never asked to.
- **`Assets/Art/` IS tracked, all 330 files, nothing untracked.** This note used to say the opposite
  and it was stale — which is worse than absent, because it invites treating committed art as
  disposable. Overwriting a sprite is therefore recoverable with `git checkout`, and the real rule is
  the ordinary one: **check `git status` before overwriting, and commit a generation you want to
  keep.** Verify rather than trust this line — `git ls-files Assets/Art | wc -l` against
  `find Assets/Art -type f | wc -l` settles it in one command.
- **`codex` must resolve on the PATH of the shell that launches the binary.** It is looked up once,
  cached for the process lifetime, and **on Windows there is no fallback** — the login-shell probe is
  Unix-only. Fixing PATH afterwards does nothing; the process must be restarted. Codex lives at
  `%LOCALAPPDATA%\Programs\OpenAI\Codex\bin` and is on the *user* PATH, which some automation
  contexts do not inherit. Symptom: `Codex CLI was not found`.
- **Unity's default import ruins pixel art** — bilinear filtering plus compression gives blurry,
  artefacted sprites. Every imported PNG needs Filter Mode **Point**, Compression **None**, and a
  Pixels Per Unit matching the art's grid. Do this with an `AssetPostprocessor` under
  `Assets/Dungeon/Editor/`, not by hand, or it will be wrong the moment someone reimports.
- **Generation is not deterministic.** Same prompt, different sprite. Do not put it in a test, a
  build step, or anything expected to reproduce. Generate once, commit the PNG, treat it as source.
- **Import needs the editor out of Play Mode**, same as every other asset operation here — see the
  sentinel-file rules above.
- The tool rewrites `$STAGE/.sprite-studio/*.py` on every run. Never edit those.

## Unity

Unity `6000.3.17f1`, **2D URP** (Renderer2D). MooseRunner `2.2.5` (Valectric npm registry), UniTask
(OpenUPM) and `com.unity.recorder` come from the manifest. WebGL Build Support module required.

## Screenshots are overwritten by every test run — copy before you analyse

`Screenshots/*.png` are rewritten in place by the Look tests and `RaidE2E` on every pass of the Game
suite. Reading one across several turns while the suite runs means comparing different frames without
knowing it.

This cost a whole investigation on 2026-08-15: two "pale bands" were measured out of
`01-raid-opening.png` over several turns, ruled against doors, props, glow, hints and post-processing,
and written up as an open defect — and were not reproducible. The tell was an impossible number, a
band at luminance 100 against a brightest tile pixel of 63 in a renderer already shown to be 1:1.
**An impossible measurement means the measurement is wrong, most often in its provenance.**

Capture inside the test that reads the pixels, as `SceneryDumpTests` does, or copy the PNG out under a
unique name first.

## Photograph every phase, not just the ones with tests

`CLAUDE.md` already said "photograph the game". What it did not say is **which** frames, and the gap
is where the bugs were. On 2026-08-15, five defects were found by looking at rendered frames and
**none** was visible to a suite of 331 green tests:

1. Party health bars lying across the **league standings** — the title screen, found by opening itch.
2. The same bars on the **collapse screen**, which the first fix had named its way past.
3. Four monster health bars on the **winning ending**, which the widened check could not reach
   because it lived in the wrong fixture.
4. A hall marker clipped behind the shop's **build menu**, drawn but not pressable.
5. The chest's hint tag drawn **through** the third instruction line of the opening raid, both
   unreadable — in a feature the author asked for by name, with an earlier fix in place that had
   moved the collision from the first line to the third rather than removing it.

Every one is a *composition* fault: two correct things drawn in the same place. Assertions check that
each thing happened; only a frame shows them together.

**So: every phase in `GameController.Phase` needs a photographed frame, and a check that runs on it.**
Loading, Standings, Raiding, Reviewing, Shopping, Won, Destroyed. Adding a phase means adding a
capture. And put the check in a fixture that actually reaches the phase — #3 above hid for hours
because `PhaseLookTests` cannot reach the winning ending, which only `RunProgressionTests` does.

**Copy the PNG before analysing it** (see the screenshots-are-overwritten note above), and prefer
asserting *renderers* over pixels: a pixel threshold on a screen that also draws torchlight passes
for the wrong reason.
