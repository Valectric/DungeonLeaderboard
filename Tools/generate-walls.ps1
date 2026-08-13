# Regenerates ONLY the wall tiles. The floor tiles from the previous pack are good; this leaves
# them alone.
#
# What was wrong with the first walls, measured against the moodboard's own room panels:
#   * blocks about three across a tile where the moodboard has roughly one and a half -- too fine,
#     so the wall read as brick texture rather than chunky masonry
#   * almost no contrast: mortar and block face were both dark violet within a few luminance steps
#   * speckled noise across each face instead of flat colour, which is what made it look mushy
#   * no top-edge highlight, so nothing conveyed that the wall is lit from above
#
# The prompt below states all four explicitly. Prompt rules from CLAUDE.md still apply: no "NxN"
# token (parsed as a canvas size), no the word "platform" (flips the preset to a side-view
# platformer), and say full-bleed or tiles arrive with a transparent margin.
#
# -WhatIf appends --print-prompt: composes and prints, launches nothing.

param([switch]$WhatIf)

$SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/release/sprite-maker.exe"
if (-not (Test-Path $SPRITE)) {
    $SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/debug/sprite-maker.exe"
}
$STAGE = "C:/Users/JohanHoltby/Documents/sprite-studio-sandbox"
$REF   = "$STAGE/worktrees/dungeonassets/references"

$env:Path = "$env:LOCALAPPDATA\Programs\OpenAI\Codex\bin;" + $env:Path

if (-not (Test-Path $SPRITE)) { throw "BLOCKED: sprite-maker binary missing at $SPRITE" }
if (-not (Get-Command codex -ErrorAction SilentlyContinue)) { throw "BLOCKED: codex not on PATH" }

$prompt = 'A coordinated pack of three seamless top-down dungeon WALL tiles for a tilemap. Every item is one square tile, full-bleed to every canvas edge with no transparent margin and no outline around the tile boundary, and opposite edges must match exactly so a grid of copies shows no seam. The three items are: a plain masonry wall; the same wall with pale green moss creeping over a few blocks; and the same wall with one cracked and broken block. Match the attached style-tiles reference, which shows the exact dungeon rooms these walls must build. Copy its masonry language exactly. CRITICAL DETAILS: build the wall from LARGE rectangular stone blocks, only about one and a half to two blocks across the width of the tile, in offset courses like brickwork -- NOT a fine mesh of small bricks. Give every block a BRIGHT PALE top edge one or two pixels tall where the light catches it, a FLAT mid-tone blue-grey face, and a HARD NEAR-BLACK mortar gap below and between blocks. The contrast between the near-black mortar and the much lighter block faces must be strong and obvious, not subtle. Keep each block face FLAT and CLEAN with no speckled noise, no dithering and no scattered stray pixels; the texture comes from the block shapes and their lighting, not from grain. The wall is lit from directly above so every block reads as thick raised stone. Strictly orthographic top-down, no perspective, no labels, no grid lines. Palette: near-black mortar around violet-black #251B31, block faces in blue-grey #504D63 with highlights lighter still, and royal purple #50275E in shadow. Dark, eldritch, cute-but-grim. Chunky readable pixels. NOT warm brown or tan stone.'

$argv = @(
    'generate',
    '--workspace', $STAGE,
    '--prompt', $prompt,
    '--command', 'pack',
    '--width', '64', '--height', '64',
    '--reference-category', 'palette',
    '--reference', "$REF/style-palette.png",
    '--reference-category', 'art_style',
    '--reference', "$REF/style-tiles.png"
)
if ($WhatIf) { $argv += '--print-prompt' }

& $SPRITE @argv
