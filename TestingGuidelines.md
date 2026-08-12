# MooseRunner — testing guidelines

> **Source of truth:** this file is emitted by `mooserunnerCli get-testing-guidelines-md`.
> **Recommended (Claude Code):** save this as `TestingGuidelines.md` at your repo root and add the
>   line `@TestingGuidelines.md` to your `CLAUDE.md`. Claude Code auto-imports `@`-referenced files
>   into context every session — same effect as pasting the whole thing inline, but `CLAUDE.md`
>   stays a short index. Prefer this over pasting the full text in.
> **Other agents/IDEs:** if yours doesn't auto-import `@`-referenced files, paste this text directly
>   into your project's instructions file.
> Re-run after every MooseRunner upgrade — the `Version:` line below is the drift signal.

- **Version:** 2.2.5.0 — stamped from `MooseRunner/package.json` at CLI build time.
- **Build:** 6e482eb — short git SHA from `git rev-parse --short HEAD` at CLI build time. `dev` if the binary was built outside a git checkout.
- **Regenerate:** `mooserunnerCli get-testing-guidelines-md > TestingGuidelines.md`

## What this is

MooseRunner is a Unity test runner for AI agents. The CLI is mechanically simple; the testing patterns and helper surface are not.

**Paths:**
- Binary: `<projectRoot>/MooseRunner/mooserunnerCli` (Windows: `.exe`)
- Secrets: `<projectRoot>/MooseRunner/.env` — gitignored; verify with `git check-ignore -v <path>`

Sections: §1 CLI quick reference · §2 MooseRunnerFacade · §3 Built-in systems (LoadScene, Human Review Mode, SessionRecorder, edit-asset, Multiplaytest, Test logging, Time + speed control, Inspector Panel) · §4 Writing tests · §5 Debugging.

---

# 1. CLI quick reference

### Run tests

```
mooserunnerCli test --method <Assembly> <Class> <Method> [--timeout 300]
mooserunnerCli test --class  <Assembly> <Class>          [--timeout 300]
mooserunnerCli test --assembly <Assembly>                [--timeout 300]
```

`--method` runs one test, `--class` sweeps a class, `--assembly` runs a whole suite. `--timeout` is seconds (default 300). Concrete:

```
mooserunnerCli test --method MyGame.Tests.PlayMode PlayerHealthTests TakeDamage_ReducesHealth
mooserunnerCli test --class  MyGame.Tests.PlayMode PlayerHealthTests
mooserunnerCli test --assembly MyGame.Tests.PlayMode
```

**E2E tests must use `--class`** (or `--assembly`), never `--method` — each `[Test, Order(n)]` depends on the prior step's leftover state, so running one in isolation fails.

**After every run — pass or fail — check the console:** `mooserunnerCli console --types error,warning --count 50`. A green run with errors or exceptions in the console is **not** a pass — swallowed exceptions, broken teardown, and misconfigured assets hide there. Treat console errors after a passing run like a failure.

### Diagnostics

| Command | Purpose |
|---|---|
| `mooserunnerCli status` | Workflow state. Decorated with `[PAUSED]`, `DOMAIN_RELOAD`, or `TimeScale=0` when relevant. |
| `mooserunnerCli ping` | Daemon + Unity worker reachable. Healthy ping returns sub-second. |
| `mooserunnerCli console [--client <N>] [--types <list>] [--count <N>]` | Unity console output. All flags optional. `--client` defaults to `-1` (master); `0`, `1`, ... = clones. `--types` accepts a comma list (e.g. `error,warning`). |
| `mooserunnerCli test-log` | Per-test buffer of messages your test code emitted via `MooseRunnerFacade.Log(...)` (see §Test logging). Auto-clears between runs. **Read this after a failure — not `console`.** |

### Test lifecycle

| Command | Effect |
|---|---|
| `mooserunnerCli force-recompile [--timeout 360]` | Triggers Unity domain reload. Expect `ping` to fail transiently for 15–90s afterwards — **retry, don't `reset`**. Solid recovery path when Unity state is suspect but the editor itself is responsive. |
| `mooserunnerCli abort` | Cancels the currently running test only. Daemon keeps running. **Don't use to resume a `[PAUSED]` test** — that test is paused, not stuck; use `set-timescale 1`. |

### Daemon lifecycle

| Command | Effect |
|---|---|
| `mooserunnerCli shutdown` | Stops the daemon without restart. |

### `reset` — the big hammer

```
mooserunnerCli reset
```

Kills + restarts the daemon. No test data lost. Use when:
- `ping` hangs > 5s **and you've retried for 5 min** (the 15–90s `force-recompile` window is normal — see §5 ladder).
- A test silently never returns and `status` shows nothing useful.
- Multiplaytest hangs after a successful first run (clone deadlock — internal flag stuck).

Don't `reset` reflexively after `force-recompile` — domain reload takes 15–90s on its own; ping fails during that window even when nothing is broken.

### Unity editor lifecycle — autonomous recovery (Windows)

When the editor itself (not the daemon) is the problem — frozen, crashed, not started, or
stuck behind a blocking OS dialog — these commands let you recover it without a human:

```
mooserunnerCli unity_stop [<pid>] [--no-clones] [--timeout 10]   # stop THIS project's editor(s)
mooserunnerCli unity_start [--timeout 120]                       # launch it, wait until ping->PONG
mooserunnerCli unity_check_modal [--poll N] [--timeout 60]       # report a blocking dialog (title+buttons)
mooserunnerCli unity_modal_click "<button>"                      # click a button on that dialog
```

- `unity_stop` is **scoped by `-projectPath`** — it stops only this project's editor and its
  ParrelSync clones, never an unrelated Unity. It also clears `Temp/UnityLockfile` +
  `Temp/__Backupscenes`, so `unity_start` won't hang on Unity's "Recover scene backups?" modal.
- `unity_start` reports Safe Mode as `COMPILE_ERROR:` lines + exit 1 (fix the source, start again),
  auto-dismisses benign recovery dialogs, and only succeeds once the worker answers `ping`.
- **The big one: long commands self-diagnose a stall.** While `test` / `force-recompile` /
  `edit-asset` / the recording commands wait for the worker, if nothing arrives for 60s they
  automatically scan for a blocking modal. On a hit they exit `2` with a `[MODAL]` block naming
  the dialog, its buttons, and the exact `unity_modal_click "<button>"` to clear it — so you act
  instead of burning the whole `--timeout`. Then re-run the command. Windows-only; a no-op
  elsewhere.

### Edit assets headlessly (scenes + prefabs)

```
mooserunnerCli edit-asset --read   "Assets/path-to-asset" [--timeout 360]
mooserunnerCli edit-asset --write  "Assets/path-to-asset" [--timeout 360]
mooserunnerCli edit-asset --create "Assets/path-to-asset" [--timeout 360]
```

Asset type is auto-detected from extension: `.unity` → scene, `.prefab` → prefab. Default timeout 360s. See §edit-asset for the full JSON format and workflow.

### Recording / vision (SessionRecorder)

```
mooserunnerCli recording_extract_frame "<sessionPath>" <timeSeconds> [--out "<destPath>"] [--timeout 60]
mooserunnerCli recording_extract_and_analyze "<sessionPath>" <startT> <endT> --prompt "<text>" [--out "<destPath>"] [--timeout 240]
```

Post-mortem inspection commands against a finished recording folder. See §SessionRecorder for what a session folder contains, how to start a recording from test code, how to tag objects with `RecordableObject`, and what each layer (JSON → frame → segment + Gemini analysis) is for.

### Screenshot — capture an editor window (the "visual loop")

```
mooserunnerCli screenshot [--window <name>] [--out "<destPath>"] [--timeout 30]
```

Captures an **editor window** to a PNG so a text+image AI can *see* the UI it is
working on. Prints `[SCREENSHOT] <path> (<w>x<h>)`. Zero setup — no recording, no
ffmpeg, no Gemini. `--window` defaults to `MooseRunner` (the test-runner window);
pass any other `EditorWindow` type name (e.g. `UnityEditor.SceneView`, or a
fully-qualified custom window) to capture that window instead. `--out` writes the
PNG there (else a temp path is returned).

The intended loop: capture → read the PNG → change code → `force-recompile` →
capture again — so "looks right" is something the agent can verify directly
instead of guessing from code.

**`screenshot` vs SessionRecorder** — they don't overlap:
- **`screenshot`** = a live **editor window** (tool/inspector UI), on demand, instantly. Use it to see MooseRunner's own UI or any editor panel while developing.
- **SessionRecorder** = the **game view** during a Play Mode test, recorded to video + per-object motion for post-mortem (frame → segment → Gemini). Use it to debug *what happened in the scene* during a test.

**Caveats (read these — they explain a blank capture):**
- The window must be **open and on-screen**; the command opens it and makes it the active docked tab, but a Unity editor that is **minimized or fully in the background does not render**, so the grab comes back blank and the command reports it. Bring the editor to the foreground and retry.
- **Master only** — ParrelSync clones have no window and reject the request.
- Capture uses internal Unity editor APIs (`GUIView.GrabPixels`) via reflection; it is **re-verified on Unity upgrades** and fails with a clear message if those APIs move (see the maintainer note in the worker module docs).

### Speed / pause control

| Command | Purpose |
|---|---|
| `mooserunnerCli method-speed --set <Assembly> <Class> <Method> <multiplier>` | **Persistent** per-method speed multiplier. Stored as JSON in `<projectRoot>/MooseRunner/speedConfig.json`; survives domain reload, Unity restart, and MooseRunner upgrades. Clear by setting back to `1`. |
| `mooserunnerCli method-speed --get <Assembly> <Class> <Method>` | Read the stored multiplier. |
| `mooserunnerCli speed --set/--get ...` | **Deprecated alias** for `method-speed` — still works, prints a warning on stderr. |
| `mooserunnerCli set-timescale <N>` | Sets `Time.timeScale` globally for the current session. **Auto-broadcasts to all clones** in Multiplaytest. **At the start of each new test, the timescale is reset to that method's persisted `method-speed` (or the default speed if none is set) — not `1`.** Use `set-timescale 1` to resume a `[PAUSED]` test. |

### Config (SessionRecorder secrets)

| Command | Effect |
|---|---|
| `mooserunnerCli recording_set_gemini_key <key>` | Writes `GEMINI_API_KEY` to `<projectRoot>/MooseRunner/.env`. Read lazily by Unity on next use — no Unity restart required. |
| `mooserunnerCli recording_set_ffmpeg_path "<absolute-path>"` | Writes `FFMPEG_PATH` to the same `.env`. Path must exist on disk. |

### Live debugger (`debug_*` — optional component)

Breakpoints, stepping, eval, and stacks against the RUNNING editor. Full
workflow + rules in **§5 Live debugger**. Commands: `debug_instance_list`,
`debug_instance_attach [master|cloneN]`, `debug_instance_detach`,
`debug_instance_poll`, `debug_instance_wait_for_breakpoint [--timeout N]`,
`debug_breakpoint_add|remove <file>:<line>`, `debug_breakpoint_show`,
`debug_step_into|over|out`, `debug_continue`, `debug_eval "<expr>" [--frame N]`,
`debug_threads`, `debug_stack [--thread-id N]`,
`debug_break_on_exceptions --on|--off`. While suspended at a breakpoint, all
worker commands are rejected until `debug_continue`/`debug_instance_detach`.

### Exit codes

| Code | Meaning | First move |
|---|---|---|
| `0` | All tests passed. | Done. |
| `1` | A test failed (your test logic). **Not** a CLI/daemon error. | `mooserunnerCli test-log` to see what your test logged. |
| `2` | Timeout. Either bump `--timeout`, or check whether Unity is genuinely stuck. | `mooserunnerCli status` — confirm hang vs slow test. |
| `3` | CLI/daemon error. Plumbing is broken. | Read the `[ERROR]` line; if it's connectivity, `reset`. |
| `4` | No worker — Unity isn't running, or isn't connected to the daemon. | Open Unity with MooseRunner installed, then `reset`. |

### Output-prefix legend

Lines from `mooserunnerCli test` are prefixed for easy parsing:

- `[STATUS]` — workflow update (e.g. `DOMAIN_RELOAD`, `RUNNING`)
- `[PAUSED]` — test paused itself; resume with `set-timescale 1`
- `[FRAME]` / `[SEGMENT]` — paths emitted by SessionRecorder extract commands
- `[SCREENSHOT]` — `<path> (<w>x<h>)` emitted by the `screenshot` command
- `[ANALYSIS]` / `[MODEL]` — Gemini result + model identifier
- `[PASS]` / `[FAIL]` — terminal test outcome
- `[ERROR]` — fatal CLI/daemon error

---

# 2. MooseRunnerFacade — public API

Single entry point from test code: `MooseRunner.MooseRunnerFacade`.

- `MooseRunnerFacade.Instance` — opens the MooseRunner inspector window as a side effect. Use from human-driven scripts.
- `MooseRunnerFacade.InstanceQuiet` — same singleton, no UI side effect. **Use this from test code.**

```csharp
using MooseRunner;
var mr = MooseRunnerFacade.InstanceQuiet;
```

Every method below is on this instance unless marked `static`.

### Scene loading

| Member | Purpose |
|---|---|
| `LoadSceneFromNameAsync(string sceneName, bool forceReload = false, bool cleanDontDestroyOnLoad = false)` | See §LoadScene for parameter semantics. |

### Pause + time

| Member | Purpose |
|---|---|
| `PauseTestExecution(string label)` | Sets `Time.timeScale = 0`, emits `[PAUSED] {label}` to the CLI, keeps the test alive in Unity. Resume externally via `mooserunnerCli set-timescale 1`. |
| `SetSpeed(float f)` | Per-scope speed multiplier (broadcasts to clones in Multiplaytest). |
| `SetPauseTime(float seconds)` | Auto-pause at time T (game time). |
| `GetCurrentTimeScale()` | Current `Time.timeScale` (0 = paused). |

### Timing telemetry (for deterministic perf assertions)

| Member | Returns |
|---|---|
| `GetMeanDeltaRealTime()` | Mean wall-clock delta between FixedUpdates (seconds). |
| `GetMaxRealFixedDeltaTime()` | Worst wall-clock FixedUpdate budget (seconds). |
| `GetDuration()` | Total test duration (seconds). |
| `GetNumerOfFixedUpdates()` | FixedUpdate count for the run. *(Spelling intentional — the source has `Numer`, not `Number`. Don't autocorrect.)* |

```csharp
Assert.Less(mr.GetMaxRealFixedDeltaTime(), 0.02f, "frame budget");
```

### Status queries

`GetWorkflowStatus()`, `GetTestExecutionResult()`, `GetTestExecutionSummary()`, `GetFailedMethodNames()`, `IsTestDone()`.

### Human Review Mode

| Member | Purpose |
|---|---|
| `GetHumanReviewMode()` | `true` if the human-review toggle is on. |
| `SetHumanReviewMode(bool enabled)` | Toggle it from code. Persisted in `EditorPrefs`. |

See §Human Review Mode for the pattern.

### Test logging (static)

`MooseRunnerFacade.Log(string)` — writes to the test-log buffer the CLI reads via `mooserunnerCli test-log`. **Master and every clone write into the same shared buffer**, each entry auto-labelled with its source (`[Master ...]` / `[Client_0 ...]`). Silently no-ops when the worker isn't connected. `MooseRunnerFacade.ResetTestLog()` clears the buffer mid-test (it auto-clears between runs anyway). See §Test logging.

### AI status (static)

`MooseRunnerFacade.SetAIStatusMessage(msg)` / `GetAIStatusMessage()` / `ClearAIStatusMessage()` — surface a one-line message in the MooseRunner inspector window so a human watching can see what your agent is currently doing.

### Multiplaytest

`Multiplaytest` property → `IMultiplaytest`. See §Multiplaytest.

### Helpers (not on the facade)

- **`DoNotDestroyOnTeardown`** (`MooseRunner.helper`) — both a `MonoBehaviour` tag and a static utility. Attach the component to scene objects you want to survive between-test cleanup (Camera, Light). Call the static methods from `[SetUp]`:
  - `DoNotDestroyOnTeardown.CleanSceneImmediate(hiddenObjectsIncluded = true, includeMarkedObjects = false)` — synchronous teardown. Always preserves `TestRunnerHelper`. Preserves objects with the component unless `includeMarkedObjects: true`. **Pass `hiddenObjectsIncluded: false` if you hit SceneView cache issues with lights.**
  - `DoNotDestroyOnTeardown.CleanSceneSafeUsingUpdateAsync(hiddenObjectsIncluded, ct, includeMarkedObjects = false)` — async fallback when sync teardown causes ordering issues.
- **`TestRunnerHelper`** — auto-bootstrapped singleton marked `DontDestroyOnLoad`. Hosts `CoroutineRunner` (used internally if you `StartCoroutine` from a `[Test]`) + `TimeMonitorHelper`. **Don't reference it directly** — teardown protects it automatically so test infrastructure survives.
- Test logging is on the facade — `MooseRunnerFacade.Log(string)` (static, see above). Do **not** reach for `MooseRunner.Internal.LogDaemon` — that type is an internal bridge, hidden in the binary distribution, and not reachable from customer test asmdefs.

---

# 3. Built-in systems

## LoadScene

```csharp
await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync(
    sceneName: "TestScene",
    forceReload: false,
    cleanDontDestroyOnLoad: false);
```

Loads any scene by name — **no need to add it to Build Settings**, the loader resolves by asset path.

| Parameter | Default | Behaviour |
|---|---|---|
| `sceneName` | — | Scene asset name (no extension, no path). Matched case-sensitively. |
| `forceReload` | `false` | If `true`, reloads even when the same scene is already active. Use when prior tests mutated the scene and you want a clean copy from disk. If `false` and the scene is already active, the call returns near-instantly. |
| `cleanDontDestroyOnLoad` | `false` | If `true`, also destroys leftover `DontDestroyOnLoad` objects from previous tests/scenes (audio managers, singletons, etc.). Otherwise they persist. |

Call from `[OneTimeSetUp]` for the canonical Play Mode pattern.

## Human Review Mode

A toggle that lets a human watch a test execute at human-scannable speed. When **off** (the default), tests run at full speed — review pauses cost nothing in CI.

**Toggling:**

- **Inspector Panel:** MooseRunner window → Settings → "Human Review Mode" checkbox.
- **From code:** `MooseRunnerFacade.InstanceQuiet.SetHumanReviewMode(true)`.
- **From CLI:** there is no CLI command for this. It's an editor-side toggle (persisted in `EditorPrefs`).

**Helper to add to every test class** — call at moments worth watching:

```csharp
private async UniTask ReviewPause(string label, CancellationToken ct)
{
    if (MooseRunnerFacade.InstanceQuiet.GetHumanReviewMode())
    {
        Debug.Log($"[Review] {label}");
        await UniTask.WaitForSeconds(0.75f, cancellationToken: ct);
    }
}

[Test]
public async UniTask Shoot_HitsTarget(CancellationToken ct)
{
    var target = SpawnTarget(distance: 3f);
    await ReviewPause("Target spawned at 3m", ct);

    AimAt(target);
    await ReviewPause("Aiming at target", ct);

    Shoot();
    await ReviewPause("Verifying hit", ct);
    Assert.IsTrue(target.WasHit);
}
```

**Where to pause:**

| Moment | Duration | Example label |
|---|---|---|
| After setup/spawn | 0.5s | `"Target spawned at 3m"` |
| After aim/position change | 0.5–1s | `"Aiming at target"` |
| Before assertions | 0.5s | `"Verifying beam thickness"` |
| After state change | 0.75s | `"Laser activated"` |
| Between loop iterations | 0.5s | `"Moving to next distance"` |

## SessionRecorder

Records a Unity test session as queryable data so a text+image AI can debug failures via the escalation flow **JSON data → single frame → video segment + Gemini analysis** — each step is cheaper than the next.

### Prerequisites

- **`com.unity.recorder`** Unity package — **NOT a MooseRunner dependency; install it yourself** (Package Manager → '+' → Add package by name → `com.unity.recorder`). On the first editor load after installing it, MooseRunner syncs an internal define and triggers one extra script recompile — expected, one-time. SessionRecorder also requires an **activated MooseRunner license**.
- **`ffmpeg`** — required only by `ExtractFrame` and `AnalyzeVideoSegmentAsync` (recording itself works without it). Set once:
  ```
  mooserunnerCli recording_set_ffmpeg_path "<absolute-path-to-ffmpeg>"
  ```
  Resolution order: `FFMPEG_PATH` env var → `<projectRoot>/MooseRunner/.env` → literal `"ffmpeg"` (PATH lookup). Without it, query methods throw a clear `InvalidOperationException` that points at this CLI command.
- **Gemini API key** — required only by `AnalyzeVideoSegmentAsync`. Set once:
  ```
  mooserunnerCli recording_set_gemini_key <key>
  ```
  Written to `<projectRoot>/MooseRunner/.env` (gitignored). Read lazily — no Unity restart required.
- **Billing-enabled Gemini project** — free-tier accounts hit a clear 429 with `quotaMetric: ...free_tier_requests, limit: 0`.

### Tagging objects to record

Add `RecordableObject` to anything you want included in the per-object motion + camera-view analysis:

```csharp
using MooseRunner.SessionRecorder;

cubeGO.AddComponent<RecordableObject>();
```

Untagged objects are still visible in the video (Unity Recorder captures the whole camera view), but they don't show up in `transforms.jsonl`, `events.jsonl`, or `analysis/{id}.json`.

### Recording lifecycle (from test code)

```csharp
using MooseRunner.SessionRecorder;

var api = SessionRecorderFacade.Instance;
var cfg = new SessionRecordingConfig(
    mainCamera,
    outputPath: ".mooserunner/Recordings/run01",
    videoFrameRate: 30);

SessionInfo info = await api.StartRecordingAsync(cfg, ct);

// ... gameplay / test runs ...

api.StopRecording();   // blocks on the analysis pass (≤30s)
```

`SessionRecordingConfig` fields: `Camera`, `OutputPath`, `TransformSampleRateHz=30`, `VideoFrameRate=30`, `DefaultMovementThreshold=0.01f`, `FirstFrameTimeoutMs=10000`.

`SessionInfo` fields: `SessionPath`, `SessionId`, `T0SessionSeconds`, `T0PrecisionSeconds`, `AnalysisComplete`.

**Only one session at a time** — a second `StartRecordingAsync` throws.

### Querying the session (after stop)

```csharp
// 1. Cheapest — read per-object state from the JSONL stream
ObjectState s = api.GetObjectState(info.SessionPath, "o_001", t: 1.5);
ObjectState[] hist = api.GetTransformHistory(info.SessionPath, "o_001", 1.0, 2.0);
bool moved = api.DidObjectMove(info.SessionPath, "o_001", 1.0, 2.0);

// 2. Single frame — costs an ffmpeg invocation
string framePng = api.ExtractFrame(info.SessionPath, t: 1.5);

// 3. Video segment + Gemini analysis — costs ffmpeg + a Gemini API call
VideoAnalysisResult v = await api.AnalyzeVideoSegmentAsync(
    info.SessionPath, startSec: 1.0, endSec: 2.0,
    prompt: "what moved between t=1.0 and t=2.0?", ct);
// v.SegmentPath = cached mp4 — inspect this if Gemini's answer surprises you
```

CLI equivalents (`recording_extract_frame`, `recording_extract_and_analyze`) hit the same code path but stay out of test code — use them for ad-hoc post-mortem from the terminal.

### Analysis layer

`StopRecording` runs a synchronous analysis pass before returning. For each `RecordableObject` it computes:

- **Motion segments** — contiguous moving / resting intervals (rest-dwell threshold from `DefaultMovementThreshold`).
- **Camera-view classification** — `FullyInView` / `PartiallyOut` / `FullyOut` per tick (world-AABB projected into clip space).
- **Enter / leave events** — when an object entered or left the camera frustum.

Output goes to `analysis/summary.json` (per-session index) and `analysis/{objectId}.json` (per object).

### Session folder layout

The session folder **is** `cfg.OutputPath` — flat, no `Session_<timestamp>/` subfolder; wiped on next `StartRecordingAsync` to the same path.

```
{OutputPath}/
  video.mp4             — Unity Recorder output (one file per session)
  metadata.json         — schemaVersion, camera intrinsics, objectRegistry snapshot, fps, timestamps
  transforms.jsonl      — one cam + N obj records per tick (baseline + delta)
  events.jsonl          — object_added / object_destroyed lifecycle
  analysis/summary.json — per-session index (objectIds + timeline)
  analysis/{id}.json    — per-object motion segments + camera-view classification
  frames/               — cached single-frame PNGs from ExtractFrame (cache key: rounded ms)
  segments/             — cached clip mp4s from AnalyzeVideoSegmentAsync (cache key: ms range)
```

### URP gotcha

Primitives created in tests need URP-compatible materials (`Universal Render Pipeline/Lit` + `_BaseColor`). `Shader.Find("Standard")` returns `null` in URP → Unity falls back to magenta missing-material → Gemini describes everything as "magenta" / "pink". Set materials explicitly in URP projects.

### Gemini model

Default: `gemini-3.5-flash`. Retries 5x on 5xx/429, honouring Google's `RetryInfo.retryDelay` (capped 60s).

## Scene + Prefab serializer (edit-asset)

Round-trip scenes and prefabs through structured JSON so an agent can read what's in a scene, modify fields, and write changes back.

**Edit Mode only — DO NOT call during a Play Mode test.** If Unity is in Play Mode or compiling when an `edit-asset` command arrives, it auto-triggers force-compile (which exits Play Mode and causes a domain reload) before the operation runs. Use this between tests, not inside one.

### Workflow

```
1. mooserunnerCli edit-asset --read "Assets/Scenes/Main.unity"
       -> writes JSON into <projectRoot>/.mooserunner-active-edit/
2. Agent reads/modifies the JSON files in that folder
3. mooserunnerCli edit-asset --write "Assets/Scenes/Main.unity"
       -> applies changed files back (hash-based delta — unchanged files skipped)
   OR
   mooserunnerCli edit-asset --create "Assets/Scenes/New.unity"
       -> creates a new asset from the JSON
```

### Staging folder

JSON output lands in `<projectRoot>/.mooserunner-active-edit/`:

```
.mooserunner-active-edit/
  hierarchy.json     — tree of GameObjects in the scene/prefab (name + fileID + children)
  {fileID}.json      — one file per GameObject; contains its components + serialized fields
  .broken            — sentinel written if --create validation failed; blocks --write/--create until cleared
```

Clear `.broken` by running `--read` again (which wipes the folder) or deleting the file manually.

### hierarchy.json

```json
{
  "name": "SceneName",
  "type": "scene",
  "rootObjects": [
    {
      "name": "Root",
      "fileID": "1234567890",
      "children": [
        { "name": "Child", "fileID": "2345678901", "children": [] }
      ]
    },
    {
      "name": "PrefabInstance",
      "fileID": "3456789012",
      "nestedPrefab": "Assets/Prefabs/Something.prefab",
      "children": []
    }
  ]
}
```

Prefab roots add `"type": "prefab"` and `"assetPath": "Assets/Prefabs/X.prefab"`.

| Field | Required | Meaning |
|---|---|---|
| `name` | yes | GameObject name. |
| `fileID` | yes | Unity's stable local file identifier. Links a hierarchy node to its `{fileID}.json`. |
| `children` | yes | Array (empty `[]` if no children). |
| `nestedPrefab` | no | Asset path — triggers `PrefabUtility.InstantiatePrefab` on create. |

### {fileID}.json

```json
{
  "fileID": "1234567890",
  "hash": "a3f8c2d1e9b047...",
  "components": [
    {
      "type": "UnityEngine.Transform",
      "fileID": "1234567891",
      "fields": {
        "m_LocalPosition": { "x": 0.0, "y": 1.5, "z": 0.0 },
        "m_LocalRotation": { "x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0 },
        "m_LocalScale":    { "x": 1.0, "y": 1.0, "z": 1.0 }
      }
    },
    {
      "type": "MyNamespace.PlayerController",
      "fileID": "1234567892",
      "fields": {
        "speed": 5.0,
        "targetBody": { "$ref": { "fileID": "2345678902" } }
      }
    }
  ]
}
```

**Modify field values; leave `hash` as-is** — the writer recomputes it. Files with unchanged hashes are skipped entirely. Update only the fields you want to change.

### References

```json
// Internal (same scene/prefab):
"targetBody": { "$ref": { "fileID": "2345678902" } }

// External asset on disk:
"material":   { "$ref": { "guid": "61da4d63ca179b54a97448840cdf0f41", "type": "UnityEngine.Material" } }

// Null:
"optionalRef": null
```

### Gotchas

- **`m_Children` / `m_Father`** appear in read output but are read-only on write/create — Unity manages parent/child via the hierarchy structure.
- **`m_Script`** never appears as a field; each component has a `"type"` string instead.
- **Enums** use `{"value": int, "name": "DisplayName"}` — set `"value"` for write/create.
- **`MISSING_SCRIPT`** is a sentinel for components with deleted scripts. Skip on create.
- **Component files are optional for create** — a hierarchy node without `{fileID}.json` is created with only a default Transform.

## Multiplaytest — distributed Unity-instance testing

The master Unity instance plus N clone instances (spawned via ParrelSync) run the same test simultaneously. You assert across instance boundaries via `IMultiplaytestAssert`.

Entry point: `MooseRunnerFacade.InstanceQuiet.Multiplaytest` → `IMultiplaytest`.

### Prerequisites

- **ParrelSync** package installed in the project (clones are ParrelSync instances).
- First `SetupTestAsync` call after a fresh `reset` spawns clones if none exist — that first run can take **minutes**. Subsequent runs reuse the clones and are fast.

### Lifecycle — call from test body, no attributes

```csharp
var mp = MooseRunnerFacade.InstanceQuiet.Multiplaytest;
await mp.SetupTestAsync(numberOfClients: 1, cancellationToken: ct);
// numberOfClients is the count of CLONES (excluding master). 1 = master + 1 clone.
// Determines Role (Master | Client), assigns ClientId, waits for clones to check in.
```

### Properties

`Role` (`Undetermined` | `Master` | `Client`), `ClientId`, `IsMaster`, `Port`, `IsListening`, `Assert` (`IMultiplaytestAssert`).

### Methods (interface `IMultiplaytest`)

- `SetupTestAsync(int numberOfClients, float timeoutSeconds = 600f, CancellationToken ct = default, IMultiplaytestHost host = null)` — bootstrap. Default timeout 10 min (clone-spawn on first run is slow).
- `SyncPointAsync(string name, CancellationToken ct = default, float timeoutSeconds = 60f)` — barrier; every instance must arrive before any proceed.
- `CompareTransformsAsync(int targetClientId, string rootObjectName, TransformComparisonSettings settings, CancellationToken ct)` — cross-instance transform validation.
- `ResetLogs()` — clear the daemon's test-log buffer.

### Logging from Multiplaytest tests

Use `MooseRunnerFacade.Log(...)` (see §Test logging) — the daemon auto-labels each entry with the source instance (`[Master ...]` / `[Client_0 ...]`). No separate Multiplaytest API needed.

### Assertions (`IMultiplaytestAssert`)

Assert against a specific `clientId`. Non-matching instances no-op.

- **Immediate** — value already known. Signature: `IsXxxAsync(int clientId, T value, string message = null, CancellationToken ct = default)`. Available: `IsTrueAsync`, `IsFalseAsync`, `AreEqualAsync<T>`, `AreNotEqualAsync<T>`, `IsNullAsync`, `IsNotNullAsync`, `GreaterAsync`, `LessAsync`, `FailAsync`.
- **Polling** — value arrives eventually. Signature: `IsXxxAsync(int clientId, Func<T> getValue, float timeoutSeconds = 1f, string message = null, CancellationToken ct = default)`. **Argument order matters: `timeoutSeconds` (float) comes BEFORE `message` (string).** Re-evaluates until the predicate holds or the timeout fires.

### Canonical example

```csharp
[Test]
public async UniTask Spawn_VisibleOnClient0(CancellationToken ct)
{
    var mp = MooseRunnerFacade.InstanceQuiet.Multiplaytest;
    await mp.SetupTestAsync(numberOfClients: 1, cancellationToken: ct);

    GameObject myObject = null;
    if (mp.IsMaster)
        myObject = SpawnObject();

    await mp.SyncPointAsync("ObjectsReady", ct);

    // Polling form — args: clientId, getValue, timeoutSeconds, message, ct
    await mp.Assert.IsNotNullAsync(
        clientId: 0,
        getValue: () => myObject,
        timeoutSeconds: 5f,
        message: "must exist on client 0",
        ct: ct);
}
```

### CLI integration

`mooserunnerCli console` (no flag) queries the master; `--client 0`, `--client 1`, ... query each clone. Each instance's `Debug.Log` lives only on that instance.

### Fail-fast exceptions

When a clone dies or its test method ends without responding, master's outstanding waits surface as:

- `MultiplaytestCloneErroredException` — clone state-machine errored (Play Mode exit during test, domain reload, compile error).
- `MultiplaytestCloneTestEndedException` — clone test method exited cleanly without sending the expected response (typically a clone-side `Assert.That` failure).

Both abort master's waits within ~2s instead of timing out at minutes. See the diagnostic message for which clones were missing.

### Gotchas

- **`numberOfClients` is the clone count, not the total** — `numberOfClients: 1` = master + 1 clone (2 Unity instances).
- **Master + every clone must call `SetupTestAsync`** — opt-in; no attribute magic.
- **First `SetupTestAsync` spawns clones** — can take several minutes. Subsequent runs are fast.
- **Editing MooseRunner source can recompile clones mid-test** — one or two retries usually suffices; not a regression.
- **Never delete a clone directory** to "recover" — recover in place. The project layout depends on it.
- **Any value that must match across instances must be deterministic.** Master and every clone run the same method body in *separate processes* — a `Guid.NewGuid()`, `Random`, or `TestContext.CurrentContext.Test.ID` evaluates to a **different** value per instance, so sync-point names, room names, and shared keys never rendezvous and you get a timeout instead of a barrier. Use hardcoded literals (as the demo's `SyncPointAsync("ColorsChosen")` does).

## Test logging — write from tests, read from CLI

`mooserunnerCli test-log` reads a per-test buffer the daemon maintains. **Test code writes to it via `MooseRunnerFacade.Log(...)`** (static) — that's the pair the CLI surfaces. Plain `Debug.Log(...)` calls go to the Unity console (readable via `mooserunnerCli console`), not the test-log buffer.

**Master and every clone write into the same shared buffer**, each entry auto-labelled with its source instance. This makes race conditions and cross-instance communication in Multiplaytest dramatically easier to debug — the whole conversation between instances lands in one interleaved, timestamped stream.

```csharp
using MooseRunner;   // MooseRunnerFacade

[Test]
public async UniTask DoesTheThing(CancellationToken ct)
{
    MooseRunnerFacade.Log($"[step 1] spawn target");
    var t = SpawnTarget();

    MooseRunnerFacade.Log($"[step 2] aim, distance={Vector3.Distance(player.position, t.position):F2}m");
    AimAt(t);

    Assert.IsTrue(t.WasHit);
}
```

Then after a failure:

```
mooserunnerCli test-log
[Master 2026-06-05 12:34:01.234] [step 1] spawn target
[Master 2026-06-05 12:34:01.456] [step 2] aim, distance=3.00m
```

The daemon auto-labels entries with the source instance (`[Master ...]` / `[Client_0 ...]`), so the same `MooseRunnerFacade.Log(...)` call works from master and clones in Multiplaytest — no separate API.

The buffer auto-clears between test runs. To clear manually mid-test, call `MooseRunnerFacade.ResetTestLog()`.

## Time + speed control

Three independent mechanisms — pick by use case.

| Mechanism | Scope | Lifetime | Use for |
|---|---|---|---|
| `mooserunnerCli set-timescale <N>` | Global `Time.timeScale` | Session — at each new test start, the timescale is re-applied from that method's persisted `method-speed` (or default), **not** reset to `1` | Slowing the whole engine ad-hoc; resuming a `[PAUSED]` test with `set-timescale 1` |
| `mooserunnerCli method-speed --set <Asm> <Class> <Method> <mult>` (or `MooseRunnerFacade.SetSpeed(f)` from code) | Per-method multiplier | **Persistent** — stored in `<projectRoot>/MooseRunner/speedConfig.json`; survives reload, restart, upgrade | Slowing one test that has intentionally long real-time waits |
| `MooseRunnerFacade.PauseTestExecution(string label)` | Whole engine, from test code | Until external `set-timescale 1` | Letting a human inspect mid-test state without aborting |

`PauseTestExecution` sets `Time.timeScale = 0`, emits `[PAUSED] {label}`, and keeps the test alive in Unity. The daemon does **not** clear `_testInProgress` — the test sits paused until you `set-timescale 1`.

**Multiplaytest auto-sync:** every `set-timescale` and `SetSpeed(f)` call on the master broadcasts to all clones via the daemon relay — timescale is always synchronised across instances, you never need to apply it per-clone.

## Inspector Panel + IDE workflow

- **License key on first run** — **the Inspector Panel must be opened once** (via **Tools → MooseRunner → Open MooseRunner**) to enter a license key. The trial activates automatically; the key persists. CLI-only workflows still need this one-time UI step before any `test` command will run.
- **Inspector Panel** — docked Unity UI for clicking Run / Stop, time control, test selection. Human counterpart to the agent CLI.
- **Auto-Rerun on Save** — toggle in the Inspector Panel. Selected tests re-execute on source-file save.
- **Test grouping** — tests organised into named groups (`PlayerFlowTests`, `MultiplayerTests`) selectable from the Inspector Panel.

---

# 4. Writing tests

## Referencing MooseRunner from your asmdef

One rule: **reference `"MooseRunner"`** — the single source asmdef carries the entire public API (`MooseRunnerFacade`, the Multiplaytest interfaces and DTOs, `SessionRecorderFacade` + its DTOs, `RecordableObject`). It resolves identically in every install and under any `overrideReferences` setting. The canonical test asmdef:

```json
{
    "name": "MyGame.Tests.PlayMode",
    "references": [
        "MooseRunner",
        "MooseRunner.helper",
        "UniTask",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ]
}
```

(`MooseRunner.helper` provides `DoNotDestroyOnTeardown`; `UniTask` is needed for the recommended `async UniTask` test signatures; `nunit.framework.dll` must be in `precompiledReferences` because `overrideReferences: true` disables auto-referencing.)

**Never reference `MooseRunner.Runtime` or `MooseRunner.Editor`** — they are internal packaging wrappers, and with `overrideReferences: true` their contents don't propagate anyway. Don't list `MooseRunner.*.dll` files in `precompiledReferences` either; every public type is reachable through the `MooseRunner` reference.

### When to write what

- **Play Mode (isolated)** — independent **white-box** test methods, scene reset between each, direct state assertions; may build stands, call facades, use test seams. **The default.** MooseRunner runs **everything** through its Play Mode runner, so all tests execute in Play Mode regardless of where they live in the project.
- **E2E** — chained, **black-box** gameplay flows where order matters. Use `[Order(n)]`; each test depends on prior tests' leftover state. The runner still executes it in Play Mode, but it's a distinct discipline: real shipped scene, physical-input only, read-only assertions. See §E2E tests for the full definition.

## Play Mode tests — required structure

```csharp
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MooseRunner;              // MooseRunnerFacade (incl. static Log)
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

public class PlayerHealthTests
{
    // NUnit lifecycle methods take NO parameters. Return Task (not UniTask).
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("TestScene");
    }

    [SetUp]
    public void SetUp()
    {
        // MANDATORY: clean BEFORE each test. Guarantees a known starting state.
        DoNotDestroyOnTeardown.CleanSceneImmediate();
    }

    // Test methods CAN take a CancellationToken — MooseRunner injects it.
    // Use UniTask for the return type; thread `ct` into every await.
    [Test]
    public async UniTask TakeDamage_ReducesHealth(CancellationToken ct)
    {
        MooseRunnerFacade.Log("spawning player");
        var player = Object.Instantiate(playerPrefab);
        var health = player.GetComponent<PlayerHealth>();
        int before = health.CurrentHealth;

        health.TakeDamage(1);

        Assert.AreEqual(before - 1, health.CurrentHealth);
    }

    // No [TearDown] cleanup — next test's [SetUp] handles it.
}
```

### Rules

1. **NUnit lifecycle hooks (`[OneTimeSetUp]`, `[SetUp]`, `[TearDown]`, `[OneTimeTearDown]`) take NO parameters** — async ones return `Task`, not `UniTask`. Test methods themselves DO take an optional `CancellationToken` (injected by the runner) and return `UniTask`.
2. **Thread `ct` into every `await`** inside test bodies — on timeout / abort / domain-reload the token cancels; without it the test hangs.
3. **Clean in `[SetUp]`, not `[TearDown]`** — failure state is preserved for inspection, and the next test still gets a clean slate.
4. **Protect persistent objects** (Camera, Light) by adding the `DoNotDestroyOnTeardown` component.
5. **Tests verify production code — they do NOT fix it.** If a test reveals broken behavior, fix the production code. No workarounds, compensations, or stabilization logic inside the test body.
6. **Organize by functionality** (`Tests/Movement/WalkingTests.cs`), not by story (`Tests/PRD198_*.cs`).
7. **Deterministic only** — fixed clock, fixed seed, no flaky timing dependencies.
8. **Log to the test buffer** with `MooseRunnerFacade.Log(...)`, not `Debug.Log(...)`. The CLI surfaces the former via `test-log`.
9. **`Assert.Ignore` in a coroutine-style `[UnityTest]` reports as failure** — use `yield break` instead.
10. **Run `mooserunnerCli force-recompile` after adding a new `[Test]` method** — NUnit discovers tests at domain load; new methods are invisible until then.
11. **All suites stay green — a red test is a regression, not a footnote.** Fix the production code the failure exposed; if a test needs infrastructure that isn't always present, mark it `[Explicit("reason")]` (e.g. `[Explicit("Requires 2 ParrelSync clones")]`) so the runner skips it deliberately; otherwise delete it. Never write a failure off as "pre-existing" or "flaky" and move on.
12. **One behavior per test.** Don't bundle assertions about several features into one method — split them so a failure names the broken feature. (The shipped Multiplaytest demo "zoo" follows this: one public surface per test method.)
13. **Document intent.** Give every test class and method an XML-doc `<summary>` stating the scenario and expected outcome. Add negative and idempotency cases where they apply.
14. **Prefer `async UniTask Method(CancellationToken ct)` signatures.** Avoid `IEnumerator` / `[UnityTest]` coroutine style where you can — it silently swallows exceptions thrown off the main path (and `Assert.Ignore` inside one reports as a failure — use `yield break`, per rule 9).
15. **Check the console after every run — green is not enough.** After each run (pass or fail), read `mooserunnerCli console --types error,warning --count 50`. Errors or exceptions logged during a passing run are real defects — swallowed exceptions, broken teardown, missing references — and must be fixed like failures.
16. **Repeated runs must be idempotent — no GameObject buildup.** Cleanup happens in `[SetUp]` (rule 3), not after the test — but running the same test three times in a row must end in the same scene state every time. If each run leaves more objects than the last (leaked `DontDestroyOnLoad` objects, static registries holding spawns, event subscriptions re-creating objects), the test or the production code is leaking — fix it. Quick check: run the method 3× and compare object counts / hierarchy after each run; the state after run 3 must equal the state after run 1.

## Simulating user input

When a test drives gameplay, *how* you inject the action decides whether you tested what a real player triggers. There is one line that matters, and it sorts every technique into one of three buckets.

**The faithful path — simulated *physical* input (the only thing an E2E may use):** drive the action the same way real hardware does, so the **full chain runs**: input → UI hit-test → handler → game state. Phrase the action by **outcome/intent**, not by widget internals:

- *"scroll the list until the level button is visible, then click it"*
- *"move forward until at the door, then press use"*

In VR that means: point the controller ray at the **real** button and pull the trigger — that's *one example*, not the rule. The faithful path is the **highest-fidelity simulation your input stack exposes that still runs the full chain** — an XR pose+trigger sim, an engine UI pointer/click event, or your input layer's simulated-action hook.

**White-box shortcuts — fine for Play Mode determinism, NEVER in an E2E:** calling a logical control method that jumps straight to the handler and skips the real chain (`playerApi.PressJump()`, `uiApi.ClickButton("Start")`, `menu.ConfirmStart()`), or mutating state / using test seams directly (`ForceHit()`, `AddScore()`, `health.Kill()`, `Instantiate`/`Wire`/`DestroyImmediate`). These are legitimate in white-box Play Mode tests; they are **not** E2E actions because the player can't perform them.

**Never, in *any* test — raw Unity Input System device events:** do not synthesize device state (`InputSystem.QueueStateEvent`, `Press(...)` on virtual devices, mock `InputDevice`s). This is a separate *fragility* axis, not a faithfulness one:

- Input-System-level simulation is fragile across devices, platforms, and binding changes — a rebound key silently breaks every test.
- It couples tests to *bindings* instead of *behavior*. The contract under test is "jump happens", not "Space maps to jump".

Whichever bucket you use, the handler downstream must be identical for real and simulated input. See §E2E tests for the black-box definition that makes the faithful path mandatory there.

## Expected-to-fail tests (`_ShouldFail`)

A test whose job is to prove a *guard fires* — a divergence detector that should throw, an assert that should reject a bad value — is named with a `_ShouldFail` suffix and is **expected to report red** when run. A green `_ShouldFail` is the real regression: the guard stopped catching its failure case. The shipped `FailureZoo` and `ErrorHandling` demos are the precedent.

## E2E tests — chained, ordered

### What makes a test E2E (vs Play Mode)

An **E2E test is black-box, from the player's seat:**

- It **loads the real, shipped scene** — the one end users actually run. It does **not** build a stand, `Wire()` modules together, or `Instantiate` gameplay objects in code; the scene/level creates them.
- It performs **every action only by simulating *physical* user input** (the faithful path — see §Simulating user input), so the full input → UI hit-test → handler → state chain executes. Drive by outcome: *"scroll until the level button is visible, then click it"*, *"move forward until at the door, then press use"*.
- It may **only read production state to assert.** No test seams (`ForceHit`, `ForceWin`, `AddScore`), no `Instantiate`/`Wire`/`DestroyImmediate`, no calling a facade/controller method to *cause* an effect. Reading is fine; creating, editing, or triggering through code is not.

A **Play Mode test is white-box:** it may build stands, call facades, and use test seams for determinism — that's where logical-API shortcuts and `ForceX` belong, **never in an E2E.**

### Mechanics — three load-bearing differences from Play Mode

```csharp
public class JoinAndPlayE2E
{
    // The FIRST [Test, Order(0)] loads the REAL scene — NOT [OneTimeSetUp].
    [Test, Order(0)]
    public async UniTask Step0_LoadScene(CancellationToken ct)
    {
        await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("MainMenu", forceReload: true);
        // The scene/menu builds itself — the test does not Instantiate or Wire anything.
    }

    // Each later step acts ONLY through simulated physical input, phrased by outcome,
    // and asserts by READING production state. (`input` here is illustrative — your
    // game's physical-input simulation surface, e.g. an XR/recipe sim or UI pointer.)
    [Test, Order(1)]
    public async UniTask Step1_StartGame(CancellationToken ct)
    {
        await input.ScrollUntilVisibleThenClickAsync("StartButton", ct);  // real button, real raycast
        Assert.IsTrue(GameStateApi.IsInGameplay, "clicking Start should enter gameplay");
    }

    [Test, Order(2)]
    public async UniTask Step2_ReachDoor(CancellationToken ct)
    {
        await input.MoveForwardUntilAsync(() => PlayerApi.IsAtDoor, timeout: 30f, ct);
        await input.PressUseAsync(ct);                                    // physical "use" input
        Assert.IsTrue(DoorApi.IsOpen, "pressing use at the door should open it");
    }
}
```

### E2E rules

1. **NO `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, or `[OneTimeTearDown]`** — the carry-over state between steps IS the test. NUnit fixture hooks reset state and break the chain. The first `[Test, Order(0)]` does the scene load.
2. **Run with `--class` or `--assembly`, never `--method`** — `[Order(n)]` only takes effect when NUnit runs the whole fixture. Running one step in isolation will see no prior state and fail.
3. **Number explicitly with `[Order(n)]`** — name-based ordering is unreliable across IDEs/CLI.
4. **Cancellation-token threading, `MooseRunnerFacade.Log`, and "tests don't fix production" still apply** — but the white-box affordances of Play Mode do **not**: no test seams, no facade method called to cause an effect, no stand-building. If a step can only be made to pass by triggering something in code, it isn't an E2E step.

### Visual validation

E2E is the **only** test that exercises the real rendered scene, so it's the natural home for SessionRecorder visual validation — record the run, then extract/inspect frames to confirm it "looks right." See §SessionRecorder.

## Edit Mode tests — not supported

**MooseRunner does not run Edit Mode tests.** The CLI runner enters Play Mode for every test; there is no Edit Mode execution path. If you have NUnit fixtures you want MooseRunner to see, the asmdef must use `includePlatforms: []` and `defineConstraints: ["UNITY_INCLUDE_TESTS"]` so they're treated as Play Mode tests. `includePlatforms: ["Editor"]` makes the asmdef invisible to MooseRunner — you'll see `[PASS] 0/0` even though Unity's own Test Runner shows the tests.

If you need true Edit Mode tests (no Unity runtime, pure unit tests), run them through Unity's built-in Test Runner — not MooseRunner.

---

# 5. Debugging

## Live debugger — breakpoints in the running editor (`debug_*`)

OPTIONAL component (like the HotReload integration): if `mooserunnerDebugHost`
isn't built, every `debug_*` command says so clearly and nothing else is
affected. The debugger attaches to the editor's built-in Mono debugger agent —
no code runs inside Unity for this, so it keeps working even while the editor
is frozen at a breakpoint.

### The workflow

```
mooserunnerCli debug_instance_list                  # who can I debug? (master / cloneN, pid, port)
mooserunnerCli debug_instance_attach master         # exclusive attach (detach Rider/VS Code first)
mooserunnerCli debug_breakpoint_add Foo.cs:42       # bare name, Assets/-relative, or absolute path
mooserunnerCli test --method Asm Class Method &     # trigger the code path (use a GENEROUS --timeout:
                                                    #   the test clock keeps ticking while suspended)
mooserunnerCli debug_instance_wait_for_breakpoint   # blocks; prints [STOP] + [SOURCE] window
mooserunnerCli debug_eval "this._field.Property"    # inspect state (see eval contract below)
mooserunnerCli debug_stack                          # call stacks; debug_threads for the roster
mooserunnerCli debug_step_over                      # or step_into / step_out / continue
mooserunnerCli debug_breakpoint_remove Foo.cs:42
mooserunnerCli debug_continue                       # reason=running = success, it's executing
mooserunnerCli debug_instance_detach                # ALWAYS detach when done (frees the agent for Rider)
```

### Rules that keep you out of trouble

- **One debugger per editor.** Unity's agent is single-client: Rider/VS Code
  attached = our attach fails ("another debugger may be attached"), and vice
  versa. `debug_instance_detach` releases the slot.
- **While suspended, every worker command is rejected** (`test`, `ping`,
  `console`, `recording_*`, `edit-asset`, …) with "suspended at a breakpoint" —
  the editor literally cannot respond. Other terminals see
  `[STATUS] DEBUG_SUSPENDED <file>:<line>` / `DEBUG_RESUMED` in their streams.
- **Code Optimization must be Debug** (bug icon, bottom-right of the editor).
  In Release mode the agent isn't listening; attach tells you exactly this.
- **Stops print `[SOURCE]` only when the stopped method changes** — a ±20-line
  numbered window (`NNN→` = stop line). Read the file for more; set follow-up
  breakpoints straight from the line numbers. `debug_instance_poll` is
  status-only: never source, never stacks.
- **Unverified breakpoints are normal** for types not loaded yet (Mono loads
  classes lazily) — they bind automatically on first load;
  `debug_breakpoint_show` shows the current state.
- **Session lifetime:** the session dies on editor restart, daemon
  rebuild/`reset`, and domain reload — re-attach is one command. A domain
  reload recompiles every assembly and silently unbinds breakpoints, so the
  session ends rather than leave you waiting on a breakpoint that can never
  hit; the next `debug_*` command tells you *why* it ended ("Debug session
  ended: the editor domain-reloaded …"). Re-attach and re-add your
  breakpoints. Don't `force-recompile` while attached. Don't debug during a
  Multiplaytest run (refused while a test is in progress; don't start one
  while a clone is suspended).
- **break_on_exceptions is first-chance:** it stops on ANY thrown exception,
  caught or not (editor-internal `ExitGUIException` is auto-skipped). Turn it
  `--off` before resuming long-running work.

### debug_eval contract (v1)

Member paths + parameterless calls, evaluated in the stopped frame
(`--frame N` to pick a caller frame):

- WORKS: locals, parameters, `this`, private/public field + property chains
  (`this._state.RetryCount`), fully-qualified statics
  (`UnityEngine.Time.frameCount`), parameterless invokes (`x.ToString()`).
- NOT in v1: operators (`1+1`), method arguments, indexers, lambdas/LINQ,
  `new`. The error message restates this.
- Property getters and `()` invokes EXECUTE REAL EDITOR CODE on the suspended
  thread — side effects are yours; a 5s watchdog stops runaway invokes.

## Failure table — symptom → diagnose → fix

| Symptom | Likely cause | Fix |
|---|---|---|
| `ping` hangs > 5s, Unity alive, **5 min has passed** | Daemon split-brain (multiple daemons / stale `daemon.json`) | `mooserunnerCli reset`, then ONE `force-recompile`. Don't run concurrent CLIs during recovery. |
| `ping` fails immediately after `force-recompile` | Domain reload in progress (15–90s — sometimes longer) | Retry every 15s for up to 5 min. **Not an error.** |
| `[PASS] 0/0 passed` | Test asmdef is `includePlatforms: ["Editor"]` — invisible to the CLI runner | Set `includePlatforms: []` and `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. |
| New `[Test]` method not picked up | NUnit discovers at domain load | `mooserunnerCli force-recompile`. |
| Same error spamming the console | Recompile loop or `[InitializeOnLoad]` throw | `console --types error --count 50`, fix the source. Don't `reset` first. |
| `[PAUSED] <name>` exit 0 | Test self-paused via `PauseTestExecution(...)` | Resume with `mooserunnerCli set-timescale 1`. **Don't `abort` — the test is alive.** |
| `set-timescale 0.5` reset on next test | Session-scoped: each new test re-applies the method's persisted `method-speed` (or default) | Use `mooserunnerCli method-speed --set` for a persistent per-method multiplier (see §Time + speed control). |
| Multiplaytest hangs after passing once | Clone deadlock — internal flag stuck | `mooserunnerCli reset` clears it. |
| Multiplaytest fails with `MultiplaytestCloneErroredException` | A clone hit a state-machine error (Play Mode exit, compile error, domain reload) | Check `console --client <N>` for the clone that errored. |
| Multiplaytest fails with `MultiplaytestCloneTestEndedException` | A clone's test body exited cleanly without sending the expected response (often a clone-side `Assert` failure) | Check `console --client <N>` and `test-log` for the clone-side failure. |
| `edit-asset` triggered an unexpected domain reload mid-test | `edit-asset` is Edit-Mode-only; it force-compiled to exit Play Mode | Don't run `edit-asset` during a test. Run it between tests. |
| `method-speed --set` value disappeared | `speedConfig.json` IS persistent — the assembly/class/method args are case-sensitive | `method-speed --get` with the same args to confirm; check `<projectRoot>/MooseRunner/speedConfig.json`. |

## Recovery escalation ladder

Stop at the first step that resolves the issue:

1. **`mooserunnerCli status`** — cheap; shows `DOMAIN_RELOAD` / `PAUSED` / `TimeScale=0`.
2. **`mooserunnerCli ping`** — retry every 15s for up to 5 min before escalating.
3. **`mooserunnerCli reset`** — kills + restarts the daemon. Safe.
4. **Manual:** `taskkill /F /IM mooserunnerCliDaemon.exe` → delete `<projectRoot>/MooseRunner/daemon.json` → ONE `force-recompile`.

## Staying current

Run `mooserunnerCli get-testing-guidelines-md > TestingGuidelines.md` after every MooseRunner upgrade. The `Version:` line at the top tells you whether the paste is current.
