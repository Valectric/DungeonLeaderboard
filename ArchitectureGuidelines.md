# Recommended Unity architecture — guidelines

> **Source of truth:** this file is emitted by `mooserunnerCli get-architecture-guidelines-md`.
> **Recommended (Claude Code):** save this as `ArchitectureGuidelines.md` at your repo root and add
>   the line `@ArchitectureGuidelines.md` to your `CLAUDE.md`. Claude Code auto-imports `@`-referenced
>   files into context every session — same effect as pasting the whole thing inline, but `CLAUDE.md`
>   stays a short index. Prefer this over pasting the full text in.
> **Other agents/IDEs:** if yours doesn't auto-import `@`-referenced files, paste this text directly
>   into your project's instructions file.
> Re-run after every MooseRunner upgrade — the `Version:` line below is the drift signal.

- **Version:** 2.2.5.0 — stamped from `MooseRunner/package.json` at CLI build time.
- **Build:** 6e482eb — short git SHA at CLI build time. `dev` if built outside a git checkout.
- **Regenerate:** `mooserunnerCli get-architecture-guidelines-md > ArchitectureGuidelines.md`

## What this is

An opinionated, test-first architecture for Unity projects: a small layer hierarchy, concrete
classes over interfaces, one-directional dependencies, and module-owned test seams. It describes
how to structure **your** project's code — it is independent of how you run tests (see
`get-testing-guidelines-md` for the MooseRunner test runner). Examples use generic placeholder
names (`Foo`, `BarModule`, `FooRouter`); substitute your own.

---

## 1. Hierarchy: Application → Module → Submodule

- **Application** — the top level; owns and wires multiple Modules.
- **Module** — a unit of behavior with a **public base namespace** (the door) and an **`.Internal`** namespace (implementation + Submodules). Has its own `Tests`.
- **Submodule** — a folder inside a Module's `Internal/`. Behaves like a module; may be tested at the parent's level.

Two named roles recur:

- **Facade** — the single public class that is the one door into a Module. Concrete. Always carries the `Facade` suffix (`SoundFacade`, `InventoryFacade`).
- **Router** — the single internal coordinator that wires a Module's submodules together and forwards calls between them. Pure plumbing, no logic. Always carries the `Router` suffix (`SoundRouter`).

---

## 2. Zero-Interface Rule

The codebase has **no interfaces by default.** Facades, routers, and submodules are **concrete classes.**

Introduce an interface **only** for genuine runtime polymorphism — when two implementations
actually coexist at runtime (per-platform backends, multiple parsers selected at runtime). The
interface is added **reactively**, at the moment the second implementation lands — never
proactively, never "in case we swap later."

**Test substitution is not a justification for an interface.** Mocking, simulation, and
external-dependency fakes are handled inside the module via the TestFacade (§7) — never by
extracting an interface. Every speculative interface is a file and a layer of indirection that
buys nothing.

---

## 3. Module anatomy

```
<ModuleName>/
  <ModuleName>Facade.cs       # public base namespace: the Facade + public DTOs/events/exceptions
  Internal/
    <ModuleName>Router.cs      # internal coordinator
    State/                     # optional: shared observable model
    UI/                        # optional: UI consumers
    <SubmoduleA>/
    <SubmoduleB>/
  Tests/
```

- **Public base namespace** = the concrete Facade plus any public DTOs/results/events/exceptions. No factory, no interfaces.
- **`.Internal`** = everything else; never referenced from outside the module.
- **Modules self-bootstrap.** A Module's Facade MonoBehaviour comes alive in a scene and wires its own Router and submodules. There is no central Bootstrap layer.
- **Scenes are the glue.** Cross-module composition happens by placing module Facades as GameObjects; the scene hierarchy + inspector references are the wiring. The module graph must be **acyclic**.
- A MonoBehaviour in `.Internal` is still declared `public` (Unity requires it for serialization / `GetComponent`). The `.Internal` namespace — not the C# access modifier — is the "do not reference from outside" signal.

---

## 4. Submodule

- **Graduation trigger.** A concern that grows past ~400 lines (§8) earns its own Facade + Router structure. Until then a submodule can be a single class with no ceremony.
- **Star topology.** Submodules talk only to their parent Router — **never** peer-to-peer. The Router isolates them from each other.
- **Nesting** is allowed when a submodule is itself a composition boundary (e.g. wrapping lifted third-party code in its own `Internal/`). No cycles between siblings.

---

## 5. One-Flow communication

- If **A→B** and **B→C**, then **A must not call C** directly; route **A→B→C**.
- Exception: a shared **Manager module** (stable public Facade) may be used by multiple peers (**A→Manager←B**).
- Keep dependency graphs **acyclic.**

**Router methods are single-line delegations** — no coordination logic, no null checks, no
conditionals:

```csharp
// GOOD — pure wiring
public RecordingInfo StopRecording()
    => _recordingManager.StopAndCreateRecording(_inputRecorder);

// BAD — business logic in the router
public void PlayRecording(string id)
{
    var rec = _recordingManager.GetRecording(id);
    if (rec?.Data == null) { Debug.LogWarning("not found"); return; }  // belongs in a submodule
    _inputRecorder.StartPlayback(rec.Data);
}
```

Three coordination patterns are allowed: (1) Router forwards A's output into B; (2) the submodule
that owns a workflow drives it internally; (3) submodules exchange data through a shared State
object — never via direct method calls.

**Self-subscription + connector.** Shared infrastructure (input sources, services) exposes
subscription points; **modules subscribe themselves** — the infrastructure holds no client list.
The wiring lives in a dedicated **connector** class owned by the module: it finds the
infrastructure, subscribes, and forwards events to the module's Facade. A connector is pure
plumbing — no business logic, no mode awareness. It decides only **how** to forward, never
**whether** to forward.

---

## 6. Unity–C# composition boundary

Plain C# classes cannot find MonoBehaviours; MonoBehaviours can find everything. So **one bridge
MonoBehaviour per boundary** does the wiring, with three jobs and no logic:

```csharp
void Awake()
{
    var rayA = GetComponentInChildren<PhysicalRay>();
    var rayB = GetComponentInChildren<UICanvasRay>();
    _router = new FooRouter(rayA, rayB);   // 1. find components  2. construct plain C# via ctor
}
void OnDestroy() => _router.Dispose();      // 3. forward lifecycle
```

- **`Awake`** initializes self (own components, defaults); **`Start`** connects to others (find references, subscribe). Use `[RequireComponent]` for hard component deps.
- Once inside plain C#, **never reach back into Unity** — no `GetComponent`, `FindObjectOfType`, `new GameObject`, or `transform` access. If a plain C# object needs something from Unity, the bridge passes it in at construction. Crossing back means the boundary doesn't exist: hidden dependencies, untestable code.

---

## 7. Module-owned test seams

A Module exposes a **TestFacade** — a concrete class in the base namespace, named
`<ModuleName>TestFacade`, that provides state inspection and test control. Its constructor takes
internal types, so only the production Facade can create it (via `GetTestFacade()`, lazily). XML
states "Not intended for production use — only for automated testing." Production code never
references it.

```csharp
var testFacade = module.GetTestFacade();
testFacade.SetSimulationMode(true);
testFacade.SimulateInput();
Assert.IsTrue(testFacade.IsActive);
```

Two test-substitution modes, **both owned inside the module** (no global "we are testing" switch):

- **Simulation mode (inbound).** The connector forwards both real and simulated input
  simultaneously; the module ignores the stream its internal mode flag doesn't select. Per-module:
  one module can simulate while peers run live.
- **Mock mode (outbound).** When a submodule wraps a slow/non-deterministic external dependency
  (network, filesystem, clock, hardware), the module exposes a mock toggle on its TestFacade; in
  mock mode the submodule returns deterministic fakes instead of hitting the real system.

```csharp
// GOOD — mock mode inside the module that owns the external call
internal async Task<UserRecord> FetchUser(string id)
    => _router.IsMockMode ? MockUsers.For(id) : await _http.GetUserAsync(id);

// BAD — extracting IUserService just so a test can swap it (violates §2)
```

---

## 8. Size limits

- **Per Module:** ≤ 2000 logical LOC (base + Internal + Submodules; excludes `Tests/`).
- **Per Submodule:** ≤ 2000 logical LOC.
- **Per File:** ≤ 400 lines (hard cap, everywhere). *Logical LOC = non-blank, non-comment.*

When a file passes 400 lines, split into a main class + helper class(es) — extract distinct
responsibilities into their own classes. **Do not use partial classes.**

---

## 9. Documentation & naming

- **XML `<summary>` on every code element** — classes, structs, records, methods, properties, fields, events, enums, **and tests** (classes & methods). State purpose, behavior, constraints, pre/post-conditions.
- **`<inheritdoc/>` is forbidden** — every member's doc is written in full and stands alone (readers and tools see files independently).
- The Facade's XML states **"This is a Module."**
- **Assembly definitions:** one asmdef per module root; the asmdef name equals the namespace root in **dotted notation** (`Product.Module`, tests `Product.Module.Tests`); the file name matches the `"name"` field. Reference only the assemblies you need; keep the asmdef graph acyclic; **verify an existing `.asmdef` before changing its references** — never assume.

---

## 10. Unity practices

- **`FixedUpdate` for gameplay** — movement, jumping, shooting, and all physics-driven logic.
- **Manager singletons** for genuinely global services; everything else is module-scoped.
- Use Unity's built-in types (`Vector3`, `Quaternion`, …) rather than re-rolling them.
- Keep a scene's physics dimension consistent (don't mix 2D and 3D colliders on the same body); verify collider/rigidbody/layer setup rather than assuming it.

---

## 11. PR checklist

- [ ] Base namespace = public API only (concrete Facade + public DTOs/events; no factory, no interfaces); XML says "This is a Module."
- [ ] Facade carries `Facade` suffix; Router carries `Router` suffix and is pure single-line wiring.
- [ ] No external references to any `*.Internal`.
- [ ] One-Flow respected (A→B→C, never A→C); module graph acyclic.
- [ ] Cross-module dependencies point at another module's public Facade only.
- [ ] Test substitution lives in a TestFacade (simulation/mock mode) — no interface added for testing.
- [ ] Module ≤ 2000 LOC; no file > 400 lines (split into helper classes, not partials).
- [ ] XML docs on all elements incl. tests; no `<inheritdoc/>`.

---

## 12. Examples — good vs bad

**Submodule communication**

```csharp
// GOOD — submodules reach each other only through the parent Router.
internal sealed class InputRecorder { /* knows nothing of its peers */ }

// BAD — a submodule holding a direct reference to a peer submodule.
internal sealed class InputRecorder
{
    private readonly CacheSubmodule _cache;             // peer reference — forbidden
    public InputRecorder(CacheSubmodule cache) => _cache = cache;
}
```

**Singleton (only the Router, only when justified)**

```csharp
// A Router may be a conceptual singleton when both UI and an external API need the same
// instance. It is a plain C# class; a thin internal "Runtime" MonoBehaviour forwards
// Awake() -> Initialize() and OnDestroy() -> Shutdown(). Never put the singleton on the Facade.
```
