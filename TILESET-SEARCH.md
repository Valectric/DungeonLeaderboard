# Replacing the tileset — what we need, and why the current one fails

Written 2026-08-15, after the author's verdict: *"the tile set is not working."* This file is the
brief for judging a replacement. The candidate packs and the recommendation are at the end.

## 1. Why the current tiles fail, measured rather than felt

The seven tiles in `Assets/Art/Resources/tiles/` are 64×64. Their average luminance:

| tile | L (of 255) |
|---|---|
| floor-plain / cracked / rubble / drain | 26.9 / 27.5 / 25.7 / 24.8 |
| wall / wall-cracked / wall-moss | 33.6 / 34.1 / 32.3 |

Three things are wrong, and only the first was ever chased:

1. **Walls are barely brighter than floors** — a ratio of 1.25. The dungeon reads flat. An earlier
   pass drove this to 1.05 to match a measured moodboard figure, which made it *worse*: the metric
   was hit and the picture got flatter. Numbers constrain; they do not compose.
2. **The floors have no readable masonry.** At 26 average luminance with no structure, they are
   noise. A floor tile has to show a flagstone grid or it is a dark rectangle.
3. **The walls are not a wall *system*.** Each one is a horizontal band — dark top, pale grey-blue
   bottom, black post — which reads as a fence rather than as a block of stone seen from above.
   There are **no corners, no edges, no directional pieces at all**.

And the entrance, which the author flagged separately as looking bolted on, is measurably from a
different picture: `scenes/entrance.png` averages **luminance 13.7** against the wall tiles' 33.6.
Two and a half times darker than the stone it is set into, so no amount of blending hides the join.

That third point is the real defect, and it is also a *code* fact rather than only an art one:

```csharp
// DungeonScenery.TileFor
return spread % 11 == 0 ? "wall-moss" : "wall";
```

Every wall cell gets the same sprite regardless of its neighbours. No amount of redrawing one tile
fixes a wall that does not know it is a corner.

## 2. What a replacement has to contain

Straight from what the game draws today (`DungeonScenery.TileFor`, `Assets/Art/Resources/`):

**Required**
- Floor, 3–4 variants that tile seamlessly, **full-bleed** — art must reach all four edges of the
  cell or the dungeon shows grid lines
- Wall top face, 2–3 variants
- **Wall edges and corners** — N/S/E/W plus four outer and four inner corners (this is the part we
  do not have and cannot fake)
- **Doors: open and closed**, on a threshold cell
- A **dungeon entrance / archway**, matching the wall stone. The current entrance is separate art
  and reads as a different game — the author's original complaint

**Nice to have**
- Torch/sconce wall variants (the game lights rooms with sprite torches today)
- Doorway threshold floor tiles
- Chest, bones, rubble props

**Not needed** — we keep our own: adventurers, monsters, spawners, effects, UI.

## 3. Judging criteria, in order

1. **Licence.** CC0 is the target. CC-BY is acceptable (one attribution line). **CC-BY-SA and GPL
   are disqualifying** — this project is going public under MIT and share-alike would infect it.
2. **A complete wall system**, because that is the actual hole. A beautiful pack with one wall tile
   buys us nothing we do not already have.
3. **Doors, both states**, plus an entrance if possible.
4. **Recolourability to the moodboard palette** — violet-black `#251B31`, royal purple `#50275E`,
   magenta `#D75268`, candle orange `#85432A`, blood red `#6D222F`, blue-grey `#504D63`. Packs drawn
   with a small flat palette and a clear light-to-dark value structure recolour cleanly; painted or
   heavily textured art does not. A grey or brown pack is fine **if** it recolours; the palette is
   fixable, the geometry is not.
5. **Grid.** 16×16 upscales ×2 to our 32-grid cleanly (point filtering, integer scale). 32×32 is a
   direct fit. Anything non-power-of-two or non-square is out.

## 4. Two levels of adoption

- **Level 1 — drop-in.** Replace the seven PNGs. No code change. Fixes the flat floors and the
  wall/floor ratio immediately, but the walls still will not turn corners.
- **Level 2 — autotiling.** Add neighbour-aware selection to `TileFor`: a four-bit mask of which
  orthogonal neighbours are wall, mapped to sixteen sprites. Roughly forty lines and a table, and it
  is what makes a dungeon look built rather than stamped. **Only worth doing against a pack that
  ships the sixteen pieces**, which is why criterion 2 outranks how pretty a pack is.

---

# 5. The search: ten candidates, rated

Three independent searches — itch.io, OpenGameArt, and GitHub/elsewhere — each required to open every
page and quote the licence rather than describe it. The four links in the top three and the palette
donor were then re-verified by hand. Ratings are out of 5 per column; **wall system is weighted
double**, because §1 established that a missing wall system, not colour, is what is actually broken.

| # | Pack | Licence | Wall system | Doors O+C | Entrance | Palette fit | Volume | Verdict |
|---|---|---|---|---|---|---|---|---|
| 1 | **DCSS — `crawl/tiles`** | CC0 ⚠️ (4) | **5** — `rock_wall_00–15`, the full sixteen | 5 — 91 door files, open/closed/runed/sealed, H+V gates | 5 — 127 gateway tiles | 3 — dark cobalt/grey, recolours per tile | 5 — 800 walls, 586 floors | **Pick for the walls** |
| 2 | **Kenney — Tiny Dungeon** | CC0 (5) | 3 — corners, not a 16-set | **5** — both states seen in the pixels | 5 — stone arch + portcullis | 3 — warm tan, but 28 colours total | 3 — 132 tiles | **Pick for the doors** |
| 3 | **Buch — Dungeon tileset (OGA)** | CC0 (5) | 3 — five room frames with corner sets | **5** — open doorway, planked door, portcullis, adjacent in one wall run | 3 — stairs, no formal arch | **4** — already cool blue-grey, avg (62,66,79) | 3 — ~250 cells, 36 colours | **Pick for the mood** |
| 4 | 0x72 — 16x16 Dungeon Tileset (the *first* one) | CC0 (5) | 4 — inner+outer autotile demo | 3 — closed seen, open unconfirmed | 2 | **5** — dark plum floor, grey-lavender brick, orange sconces | 3 | Already violet |
| 5 | hyprv — Dungeon Pack 16x16 | CC0 (5) | 4 — clean inner+outer | 3 — doors listed, split unconfirmed | 3 — portcullis seen | **5** — closest palette found | 2 — ~20–30 tiles | Palette, little volume |
| 6 | Kenney — Roguelike Caves & Dungeons | CC0 (5) | 4 — rounded + broken-edge variants | 4 — single and double wooden | 4 — several arched doorframes | 3 | 4 — 520 tiles | Solid second Kenney |
| 7 | HorusKDI — 6 Color Dungeon 16x16 | CC0 (5) | 3 | 1 — no in-wall pair | **5** — animated portcullis over stairs | **5** — measured `#16101E`/`#2E2440`/`#70579C`/`#E096A8` | 2 — ~70 cells | **The palette donor** |
| 8 | rubberduck — dungeon tileset with walls and floors | CC0 (5) | 3 — 4 walls, 15 floors | 3 — unconfirmed split | 3 — stairs | 4 — ships a clean greyscale ramp | 4 | The recolour shortcut |
| 9 | Shade — 16x16 Puny Dungeon | CC0 (5) | 4 — 2-edge Wang tiles | 3 — gate + frame, pair unconfirmed | 2 | 3 — grey, green moss to kill | 3 | Good autotiling |
| 10 | 0x72 — DungeonTileset II | CC0 (5) | 3 | 5 | 5 | **1** — warm brown/tan, the brief's one veto | 5 | Famous, wrong game |

### Rejected on licence — do not put these in a public MIT repo

| Pack | Why |
|---|---|
| **LPC base assets** | CC-BY-SA 3.0 **and** GPL 3.0. Share-alike would infect the repo. |
| **DawnLike** | Shipped `README.txt` says **CC-BY-SA 3.0** even though its OpenGameArt page says CC-BY 4.0. A licence that contradicts itself is not one to build on — and it is otherwise the best recolour target here, drawn entirely on DawnBringer-16. |
| **Minifantasy — Dungeon** | The free tier is **non-commercial only**; commercial use is a paid licence. Frequently recommended, wrong terms. |
| **CraftPix free assets** | *"You can NOT… redistribute art in a manner that would make some or all of the art files useable to another end user"* — a public repo containing the PNGs breaks this. |
| **Shattered Pixel Dungeon / Pixel Dungeon** | GPL-3.0. |
| **Anokolisa — Pixel Crawler** | Terms live in an off-site Google Doc rather than a named licence. Unresolvable. |
| **David Gervais / TomeTik** | Grants copy, distribute and transmit — but **never adaptation**, so recolouring is legally as well as visually wrong. |

## 6. Recommendation: a composite, not a pack

No free tileset satisfies the whole list. The cheapest complete route is three CC0 sources, none of
which requires attribution:

1. **Walls from DCSS.** It is the only candidate that ships `rock_wall_00–15` — the sixteen-piece run
   that maps **directly onto the mask this codebase now computes** (`DungeonScenery.WallMask`,
   N 1 · E 2 · S 4 · W 8). Our dungeons use eight of those sixteen (1, 4, 5, 7, 11, 13, 14, 15), so
   the import is eight files and a rename.
2. **Doors and the arch from Kenney's Tiny Dungeon**, palette-swapped. Twenty-eight flat colours and
   one outline colour across the whole sheet, so the swap is a lookup table, not a repaint.
3. **The palette from HorusKDI's 6 Color Dungeon**, which measures `#16101E`, `#2E2440`, `#70579C`,
   `#E096A8` against our `#251B31`, `#50275E`, `#D75268`. It is our moodboard, already drawn.

Two import traps, both already known to this project: Kenney's sheets ship on a **blue backdrop** and
Screaming Brain's on **magenta**, so both need alpha keying before they reach `Assets/Art/` —
`Tools/sprite-contact-sheet.py` flags exactly that fringe. And DCSS's CC0 comes with a maintainer's
caveat and a `TILES_UNDER_UNKNOWN_LICENSE.md` exclusion list, which must be diffed against whatever
we actually copy.

---

## 7. Why cutting tiles out of a repainted room can never work

Found by research, and it explains every measurement in section 6 at once.

**Slicing a painted image cannot produce joining edges.** Tile A's right-hand column and tile B's
left-hand column come from unrelated parts of the painting. Nothing in the prompt can fix that,
because the two columns were never drawn to agree — and that is exactly what the border-coverage
figure was measuring when it came back at 33%, and at 7–9% for the fully-enclosed pieces.

Every tool that genuinely solves this does the same thing: it **composites tiles from shared
sub-pieces**, so the pixels along a shared edge are literally the same pixels rather than two
drawings that happen to look similar. Tilesetter builds a set from a base plus an edge; Blobator
assembles a 47-piece blob from 13 quadrant templates; the principle is identical.

So the repaint-and-slice loop is the wrong shape for **wall pieces**. It remains right for floors,
doors, props and the entrance, none of which have to tile against a neighbour.

### The other correction: our mask is an edge set, and the world uses corner sets

`DungeonScenery.WallMask` computes north 1, east 2, south 4, west 8 — an **Edge Set** in Tiled's
terminology. Godot's "Match Corners and Sides", Unity's AutoTile 2×2 mask and the 47-piece blob are
all **corner** sets. Both are sixteen tiles and they are **not interchangeable**. Any tool adopted
from the list below needs a mapping layer, and there are three different bit orders in play
(ours, Tiled's wangid, Blobator's) — one tested mapping module, with a case per mask.

### The change worth making

**Unity's `RuleTile` supports `Rotated`, `MirrorX`, `MirrorY` and `RotatedMirror` transforms**, which
collapses the sixteen edge masks to about **six unique drawings**: isolated, one-edge, two-opposite,
two-adjacent, three-edge, four-edge. Six is an achievable ask from a generator. Sixteen demonstrably
is not — we have now failed at it three times, in three different ways.

`TileBase` is a `ScriptableObject`, so the assets can be authored headlessly from
`Assets/Dungeon/Editor/` exactly like the scene builder, driven by a sentinel file.

Package: `com.unity.2d.tilemap.extras`, Unity Companion Licence, free. `AutoTile` (added 4.2.0)
supports 2×2 → 16 sprites and 3×3 → 47/48 and is still marked experimental.

## 8. What the tiles actually have wrong, and why one of our metrics could not see it

Profiled from the shipped PNGs rather than described. Alpha is 100% everywhere, so this was never a
transparency margin. Classifying by luminance (floor 18.5, wall-15 interior 62.1), the ring means of
`wall-15` by distance from its edge:

```
d=0  9.1    d=1  8.4    d=2  7.0    d=3 21.4    d=4 27.5   ...   interior 62.1
```

**A three-pixel near-black frame drawn around every tile.** The generator was not stopping the
masonry short — it was drawing each tile as a self-contained illustration *with an outline*, the way
you would draw an icon. Tiled across a wall region that renders a black grid. Same on `wall-0`,
`wall-6`, `wall-cracked` and `wall-moss`, all at 0.0–0.4% border coverage.

**And the seam metric is blind to it.** On `wall-15` the wrap seam measures V=2.7 and H=5.4 against
an interior adjacent-column gradient of 9.8 — the seam is *smoother than the texture*, a clean pass.
A symmetric dark frame is perfectly wrap-continuous. Seam continuity is necessary and nowhere near
sufficient; the border-versus-interior statistic is the instrument that catches this, and it should
be a hard gate rather than a number in a log.

Per-side coverage also shows the generator had a consistent bias worth knowing: `wall-1` came back
N=73%, S=56%, W=14%, E=0% — it drew the top edge and abandoned the right.

## 9. The fix: composite the set, do not draw it

Ask the generator for **one seamless wall fill and one floor fill** — the thing it is good at — and
derive all sixteen cases mechanically from about **six 32×32 quadrants** (fill, outer corner, edge,
inner corner, plus `np.rot90` rotations, which are exact and lossless on pixel art).

```python
Q = 32
def tile_from_quadrants(tl, tr, bl, br):     # each 32x32 RGBA
    return np.block([[tl, tr], [bl, br]])
```

The fully-enclosed tile then **is** the fill texture, so 100% border coverage is true by
construction rather than by measurement, and the edge-hash validator below becomes a regression
test instead of a bug-finder. Prior art, both MIT: `HeartoLazor/autotile_generator` (Python + Pillow,
composites a bitmask set from quadrants) and `itsjavi/autotiler`. About 120 lines to own outright.

**Dual-grid rendering** is what makes six quadrants sufficient: offset the display tilemap by half a
cell so each drawn tile's four corners land on four world cells — four yes/no questions, sixteen
cases, no ambiguity. Reference implementation `jess-hammer/dual-grid-tilemap-system-unity`, MIT,
about 80 lines of C# on our side.

### The validator to add — `Tools/validate-tileset.py`, ~110 lines, no new dependencies

- **Edge hash**: `blake2b` of each tile's 1px border strip; every adjacency the mask can place must
  have identical facing strips.
- **Border coverage, per side**: where a tile's mask says wall continues, require ≥95% coverage on
  that side, plus `ring_mean(0) >= 0.6 * interior_mean` to catch the frame.
- **Flat-cell rate**: fraction of 4×4 blocks that are constant, which catches the off-grid resample
  already recorded in CLAUDE.md.
- **Palette lock**: `Image.quantize(palette=..., dither=Image.Dither.NONE)`. Dithering is wrong here
  — it fabricates high-frequency checkerboards that wreck both the flat-cell and seam metrics.

Everything above runs on what is already installed (numpy 2.5, Pillow 12.2, scipy 1.18,
scikit-image 0.26). Rejected: `texturize` (AGPL, non-deterministic), `imagequant` (libimagequant is
GPL), OpenCV `seamlessClone` (gradient-domain, invents intermediate colours and breaks a six-colour
palette), `img2texture` (hides seams with an alpha gradient, which undoes point filtering).

## 10. How artists actually do it, and the pair we have been measuring wrongly

Slynyrd, the most-cited authority on top-down tilesets, describes the workflow this pipeline has
been inverting:

> *"step one with any tileset is the base repeating texture, which must loop well on all four sides.
> After that, all side and corner variants can be made by shaving off portions of the base texture."*
> — slynyrd.com/blog/2023/3/26/pixelblog-43-top-down-tiles-part-2

Nobody draws forty-seven tiles independently. They draw **one**, and subtract. That is why a human's
borders always join: every tile's edge pixels come from the same source image. Ours were drawn as
forty-eight separate illustrations, which is the single cause of both failures — art floating inside
its cell, and art wandering across the boundary.

### Measured from 0x72's DungeonTileset II (CC0), not inferred

| | |
|---|---|
| Wall front face height | exactly **1.0 tile** |
| Lit wall top cap | exactly **0.25 tile** |
| Border opacity, all four edges | **100%** |
| Lit cap ÷ floor | **2.16×** |
| Wall front face ÷ floor | **0.93×** — the same brightness |
| Distinct colours across wall and all floors | **3** |
| Floor variants | 9, differing 6–43% of pixels, all within ±6% luminance |

**We have been measuring the wrong pair all session.** Every re-run of the brief chased wall-versus-
floor brightness, pushing it from 1.09 to 2.16. In the reference the wall *face* and the floor are
the same value — the separation comes entirely from the **bright top cap** and a one-pixel dark
outline. Chasing the face/floor ratio was optimising a quantity the reference deliberately leaves
flat, which is why the pictures kept getting brighter without getting better.

### Rules worth putting straight into a brief or a validator

- Detail sizes must **divide the tile exactly**: at 16px with a 1px grout line, brick dimensions are
  15, 7, 3, 1. At 64px: 63, 31, 15, 7, 3, 1. Side-wall bricks are shorter than top-wall bricks.
- **Variants change detail, never value** — within ±8% luminance of the base.
- Base shadow is **at most one tile long, the same length whatever the wall height**, on one or two
  faces only.
- Corners are best served by a **column or universal corner piece** rather than bespoke corner art.
- Lock a **three-colour ramp per material** and reject any pixel outside it: drift becomes
  impossible rather than merely detectable.

### Two corrections to our own setup

**64×64 is off-convention.** Every source works at 16×16; Slynyrd calls anything over 32×32 overkill
for pixel art. At 64 the generator has sixteen times the pixels to keep coherent across a seam,
which plausibly *causes* the drift and the floating we measured. Generating at 16 or 32 and
point-upscaling is the same integer-scale step this project already does for packs.

**Occlusion is an engine setting, not art.** Unity 2D URP: Transparency Sort Mode → **Custom Axis**,
Transparency Sort Axis → **(0, 1, 0)**, on the Renderer2D asset. Characters then sort behind wall
caps by Y automatically — no split lower/upper door tiles, no per-tile sorting layers. That answers
the door-occlusion question from the other direction entirely.

## 11. Two blunt conclusions, one of them about our own process

### The premise was fighting every available tool

**No hosted image API will honour "repaint this and keep it on a 64-pixel grid."** The vendors say so
themselves: OpenAI's mask *"may not follow its exact shape with complete precision"*; Black Forest
Labs lists *"expecting pixel-perfect structural matching"* under **Avoid**, because FLUX *"interprets
structure semantically"*; Gemini cannot be asked for an exact pixel size at all, only an aspect-ratio
enum. So the whole-board-repaint approach was never going to hold a grid, however the brief was
worded — which is consistent with the grid landing correctly in only **two of five** runs
(`a-cut-stone` and `c-mossy` peak at `x%64==63`; `b-worn` is 8px off, `d-ossuary` 6px, `e-arcane`
half a cell).

### Every generation this session ran blind

`grep -c referenced_image_paths` returns **0** on every run log — the tileset runs, the restyle runs
and all three panel re-runs. The style references were never passed to the image model. They were
shown to the *agent*, which described them back in prose that reads exactly like it used them.

CLAUDE.md documents this exact trap, in capitals, as the thing that cost five tile runs. The
instruction was in every prompt this session and it was ignored every time, silently. **The lesson is
that the instruction is worthless and the check is everything**: `grep -c referenced_image_paths` on
the log, after every run, before looking at the picture.

So the style drift attributed to prompting was partly a model that never saw the moodboard.

### Three fixes, measured rather than argued

1. **Order matters and it is counterintuitive.** Normalise mean luminance per class *then* quantise
   to the fixed ramp. Measured on `a-cut-stone`: wall spread 2.12× → **1.12×**, sd 10.0 → 1.4,
   25,104 colours → 10. Quantising first — which is what every tool in `Tools/` currently does —
   made it slightly **worse** (2.20×). About fifteen lines in `slice-room.py`, which today does no
   colour normalisation at all while every other tool does.
2. **Render the repaint source at 16 px per cell, not 64.** The sampler at 1664×1280 exceeds every
   pixel-art-native model's limit; at 16px/cell the same board is 416×320. Sixteen is also our true
   logical resolution — `LOGICAL = 16` in `extract-dungeon-tiles.py`, and the moodboard masonry
   period is 16px. A model asked to hold a grid across 416 pixels rather than 1664 has sixteen times
   less room to drift, and the ×4 upscale on import is exact.
3. **Our tiles are not pixel art.** 0.0% of 4×4 blocks are flat; 927–2105 unique colours per 64×64
   tile. Snapping to the 4× grid costs only 7.9/255 mean error, so the fix is nearly lossless.

Also worth knowing: `wall-11` measures 26.0 against `floor-drain` at 28.7 — a wall that reads
*darker* than the floor beside it. That is the same spread the ratio work was chasing, and
normalisation fixes it in one pass rather than by re-running a generator.

## 12. The conclusion all five searches arrive at

**The tileset is the wrong problem.** We have been asking a generator for sixteen images whose
correctness lives in their *relationships*, which is the one thing an image model cannot hold — while
the pipeline silently resampled them to two thousand colours and handed back a duplicate file as a
distinct variant.

Two verifications from the last search, both reproduced here:

- **`wall.png` and `wall-cracked.png` are byte-identical**, MD5 `128c19d9`. A variant that was never
  made looks exactly like a variant that was, and nothing reported it. (Ours, not the generator's:
  `slice-room.py` filled both from the same mask-15 list when only one candidate existed.)
- **1,016 to 2,136 unique colours per 64×64 tile** — 52% of the pixels holding a colour of their own.
  These are resampled images *of* pixel art. Edges built from two thousand interpolated colours
  cannot align even in principle, which is upstream of the frame and would sabotage a hand-drawn
  tileset just as thoroughly.

### Our own notes named the fix before any of this

`TILESET-NOTES.md:18`, written days ago: the moodboard *"does not separate wall from floor by
value… It separates them with the **rim highlight**, which is ~90% brighter than the floor."*

That settles it. The standing-up cue is the **rim**, and a rim is a pure function of the grid — *is my
neighbour open?* It is precisely what code draws perfectly and a generator draws inconsistently. The
same file also predicted the exact defect that shipped: *"I did not see the floor vignette that would
have drawn a black grid across every room."*

### The route, in the order that matters

1. **One seamless, full-bleed stone fill**, with three checkable gates: border/body luminance
   1.0 ± 0.05, wrap-seam ratio ≈ 1.0, unique colours ≤ 32. Current tiles score 0.19, 3.97× and 2,136.
2. **Derive the pieces from it** — procedurally in world space, or via Tilesetter/Wangscape, which
   composite every tile from the same source pixels so joining is arithmetic rather than luck.
3. **Draw the depth in code**: rim highlight first, then front face, drop shadow, ambient occlusion.
4. If dual-grid is adopted, keep colliders and every room/threshold rule on the **data** grid. Our
   room-bounded pursuit is load-bearing — a half-tile offset there would break the retreat valve in
   a way no rendering test would catch.

Rejected: `ShadowCaster2D` from a tilemap collider (selectable but produces no shadow, unresolved on
6.3 as of April 2026, community fixes reach into private fields by reflection) — wrong risk for a
jam when the code-drawn layers cost nothing per frame. And Rule Tile's rotation transform does not
save us six sprites the way it would elsewhere: a wall with a top face and a front face is
anisotropic, so only the horizontal mirror is available.

## 13. First attempt at drawing the relief in code — mechanism right, look wrong

Implemented `DungeonScenery.DrawRelief`: for each wall cell, a lit strip along every side facing open
floor and a cast shadow below a south-facing face, all driven by the same `WallMask` the tile lookup
uses. Photographed in the running game.

**The mechanism is correct.** Strips appear only on sides that face open floor, they land on the
pixel grid at any zoom, they cost no asset, and they cannot drift between runs — which is the whole
argument for moving the cue into code.

**The look is not.** Drawn as continuous full-length bars at 85% alpha they read as a neon frame
around the room rather than as light catching stone. Reverted from the working tree rather than left
in, because the game currently ships fine without it and a half-right effect is worse than none.

What the photograph says to change, for whoever picks this up:

- **Much lower alpha and a thinner strip.** The reference rim is a one-pixel line at roughly 90%
  brighter than the floor, not a 12%-of-a-cell bar at 85% opacity.
- **Break the bar per cell.** A single unbroken line along a whole wall run is the giveaway; real
  masonry catches light per block, so the strip wants the same 4px modulation the earlier
  `shape_wall` pass used.
- **Only the north-facing rim should be bright.** East and west should be much dimmer and south
  should carry shadow alone — the light in this dungeon comes from above, and lighting all four
  sides equally is what makes it read as an outline instead of as relief.

## 14. The diagnosis was wrong, and this is what the tiles actually are

Everything above §13 argues about texture, relief and lighting. All of it was treating a symptom.
Measured on the shipped set in `Assets/Art/Resources/tiles/`:

```
pairwise tile-to-tile difference   11.5   (closest pair 0.3)
within-tile neighbour-pixel noise   7.8
ratio                               1.47x
all 18 tiles fully opaque, alpha 255 everywhere
```

**The sixteen mask tiles are the same picture.** Tile-to-tile variation barely clears the texture's
own grain, and the closest pair is identical to three decimal places. `wall-0` is an isolated pillar
and `wall-15` is fully enclosed; they render identically. `DungeonScenery.WallMask` computes a
correct mask and then selects between sixteen crops of one brick texture, so the autotiling has been
**decorative since it was written** — the tilemap cannot express a wall boundary at all.

This explains the author's report exactly. "The walls don't look like walls from slight angle but
just pattern tiles in different colours" is not an impression to be argued with; it is a literal
description of the files.

It also explains why §13 failed three times. Relief drawn in code lights a wall/floor boundary, and
there is no boundary anywhere in the set to light. A translucent quad over a uniform texture cannot
invent one.

### What this changes about the route

- **A single seamless texture does not fix it.** §12 concluded "one seamless fill, then derive the
  sixteen pieces by compositing quadrants". The first half is necessary and not sufficient: the
  pieces have to encode *which corners are wall and which are floor*. That is the deliverable, and
  it is the half that has never been attempted.
- **Opacity is the tell.** A wall tile that touches floor must have transparent pixels, or a drawn
  floor edge, on the side that faces it. Sixteen fully-opaque tiles is a set that has already lost.
- **The check is now automated.** `Tools/validate-tileset.py` gained an `encodes_shape` gate: it
  fails below 3x separation and reports 1.47x on the current set. Every other gate in that file
  reads one tile in isolation, which is why all sixteen passed individually while being identical —
  the same blind spot the file's own docstring warns about, in a new place.

### The cheapest way out

Buying a set that already encodes shape skips this entirely, and two candidates were verified by
downloading and looking at the previews rather than reading the blurb:

- **Szadi Rogue Fantasy Castle**, $3.20, 16x16, PSD included, licence explicitly public domain and
  repo-safe — plus a free companion Catacombs pack.
- **0x72 `dungeontileset-ii`**, CC0, whose file list carries `wall_outer_front`, `wall_outer_top_left`,
  `wall_edge_left/right` and `doors_leaf_open/closed` — named boundary pieces, which is the property
  this whole section is about.

Do not buy anything from **Seliel the Shaper**, whose art is the best in the survey. The Mana Seed
licence forbids use "in a project alongside 'AI' generated imagery, writing, code, or anything
else", and this project is both AI-generated art and agent-written code.
