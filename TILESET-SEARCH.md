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
