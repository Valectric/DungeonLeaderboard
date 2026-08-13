# Generates the dungeon tile art with Sprite Studio, via the TERRAIN harness.
#
# WHY TERRAIN AND NOT PACK -- this is the whole point of the file.
#
# `--command pack` never forwards `--reference` images to ImageGen. Grep the harness docs:
#
#   grep -rn "referenced_image_paths" .../skills/sprite-director/references/
#
# Only terrain-tileset-harness.md and effect-harness.md contain the instruction "Supply every
# attached visual reference through `referenced_image_paths`". The pack harness never mentions it.
# So a pack run shows the reference to the AGENT, which describes it back to you in convincing
# prose, while the model actually drawing the pixels sees text only. Six pack runs were refined
# against a reference the generator had never seen; stating the instruction in the prompt did not
# fix it either.
#
# The terrain harness forwards references by contract. Its cost is that it returns ONE atlas of
# assembled macro-regions rather than discrete tiles, so the output has to be cut afterwards --
# Tools/extract-wall.py already finds a masonry period by autocorrelation and can do that.
#
# Other traps that still apply: no "NxN" token in the prompt (parsed as a canvas dimension and
# silently overrides --width/--height), and never the word "platform" (flips the preset to a
# side-view platformer). Confirm with -WhatIf that the routed harness really is `terrain tileset`.

param([switch]$WhatIf)

$SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/release/sprite-maker.exe"
if (-not (Test-Path $SPRITE)) {
    $SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/debug/sprite-maker.exe"
}
$STAGE = "C:/Users/JohanHoltby/Documents/sprite-studio-sandbox"
$REF   = "$STAGE/worktrees/dungeonassets/references"
$REPO  = "C:/Users/JohanHoltby/Documents/GitHub/DungeonLeaderboard/Assets/Art/referance"

$env:Path = "$env:LOCALAPPDATA\Programs\OpenAI\Codex\bin;" + $env:Path

if (-not (Test-Path $SPRITE)) { throw "BLOCKED: sprite-maker binary missing at $SPRITE" }
if (-not (Get-Command codex -ErrorAction SilentlyContinue)) { throw "BLOCKED: codex not on PATH" }

$prompt = 'A complete top-down dungeon terrain tileset atlas for a 2D game, covering both wall and floor.

THE ATTACHED style-wall IMAGE IS THE STANDARD. It is a cutout of real wall from this exact game, with no props and no torchlight on it. Pass it to the image generator through referenced_image_paths and instruct the generator to copy it. The new masonry must match that reference block for block: rounded rectangular stone blocks that carry mottled surface texture, a pale worn rim catching light along the top edge of each block, deep near-black gaps between blocks, and rounded corners. The blocks are NOT flat rectangles of a single colour, and the wall is NOT a fine mesh of small bricks.

Cover: a broad wall surface built from those blocks; a mossy variant; a cracked variant; a dark flagstone floor; a cracked flagstone floor; and a floor with an iron drain grate.

These are hard requirements, given as numbers because earlier attempts missed them badly:

BRIGHTNESS. The reference wall averages luminance 30 out of 255, darkest tenth near 5, brightest tenth near 51. The walls must land there. Earlier attempts came back at 83, nearly three times too bright, and were rejected. Floors must be darker still, around 26, so walls read against them.

FEATURE SIZE. Every mark is a chunky block four pixels across. Nothing finer. No single-pixel speckle, no dithering, no smooth gradients.

BLOCK SCALE. A masonry block is about sixty-four pixels across, so roughly one block per floor tile, with only a thin mortar joint between blocks.

RELIEF. Each block is lit from directly above: pale rim along the top edge, mottled mid-tone body, dark shadow along the bottom edge. That light-to-dark ramp is what makes stone look thick rather than flat.

Strictly orthographic top-down, no perspective, no labels, no grid lines, no text, no UI. Palette: violet-black #251B31, blue-grey #504D63 stone, royal purple #50275E shadow, with magenta #D75268 and burnt orange #85432A as very rare accents. Dark, eldritch, cute-but-grim. NOT warm brown or tan stone, and NOT light grey.'

$argv = @(
    'generate',
    '--workspace', $STAGE,
    '--prompt', $prompt,
    '--width', '512', '--height', '512',
    '--reference-category', 'palette',
    '--reference', "$REF/style-palette.png",
    '--reference-category', 'art_style',
    '--reference', "$REPO/style-wall.png"
)
if ($WhatIf) { $argv += '--print-prompt' }

& $SPRITE @argv
