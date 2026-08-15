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
