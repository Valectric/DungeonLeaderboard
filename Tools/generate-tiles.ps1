# Generates six seamless 64px dungeon tiles with Sprite Studio.
#
# Why a pack and not the terrain harness: the terrain harness deliberately composes ONE atlas of
# large assembled macro-regions for a human to read, at whatever internal pitch ImageGen felt like
# (measured: ~27px, irregular). Slicing arbitrary squares out of that produced tiles that cut
# through stones mid-block and whose mortar lines did not meet -- the first attempt at this, and it
# looked exactly as bad as that description sounds.
#
# A pack gives one item per tile, each drawn on its own 64px canvas, in a single run that shares
# context. The harness guarantees "the same projection, pixel density, outline treatment, lighting
# direction, palette logic, and logical canvas" across items, which is precisely the property a tile
# set needs and six separate runs would not have.
#
# Load-bearing prompt rules, each learned from a dry run (see CLAUDE.md):
#   * no "NxN" token anywhere -- it is parsed as a canvas dimension and overrides --width/--height
#   * no the word "platform" -- it flips preset inference to a side-view pixel platformer
#   * say "full-bleed, reaches every canvas edge, no transparent margin" or tiles arrive with a
#     margin and leave visible grid lines across the dungeon floor
#
# -WhatIf appends --print-prompt: composes and prints, launches nothing.

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

$prompt = 'A coordinated pack of six seamless top-down dungeon floor and wall tiles for a tilemap. Every item is one square tile that must tile seamlessly against copies of itself on all four edges: the art is full-bleed and reaches every canvas edge, with no transparent margin, no border framing and no outline drawn around the tile edge. Opposite edges must match exactly so a grid of copies shows no seam. The six items are: dark flagstone floor; cracked flagstone floor; flagstone floor with scattered rubble; a masonry wall of stone blocks viewed from directly overhead; the same wall with pale green moss creeping over the blocks; and a dark flagstone floor with an iron drain grate. IMPORTANT, match the attached style-tiles reference closely: it shows the exact dungeon rooms these tiles must build. Copy its relief and its value structure. The WALLS are LIGHT blue-grey stone blocks that stand out brightly against the floor, each block carved with a pale highlight along its top edge and a hard near-black shadow beneath it, so the wall reads as chunky raised stone. The FLOOR is much DARKER than the wall, a deep violet-black flagstone, so walls and floor never read as the same value. Use big chunky blocks, roughly three stone blocks across the width of the tile, not a fine brick mesh. Give every tile a full tonal ramp of about ten to fourteen colours: near-black shadow in the mortar gaps, a mid stone body, and a bright highlight edge. Do NOT make the tiles flat or low contrast, and do not use only two or three colours. Strictly orthographic top-down with no perspective and no drop shadow leaving the tile. No labels, no grid lines, no text. Palette: violet-black #251B31 for floor and shadow, blue-grey #504D63 for stone bodies with lighter highlights above it, royal purple #50275E, magenta arcane glow #D75268 and burnt orange #85432A only as rare accents. Dark, eldritch, cute-but-grim. Chunky readable pixels. NOT warm brown or tan stone.'

$argv = @(
    'generate',
    '--workspace', $STAGE,
    '--prompt', $prompt,
    '--command', 'pack',
    '--width', '64', '--height', '64',
    '--reference-category', 'palette',
    '--reference', "$REF/style-palette.png",
    '--reference-category', 'art_style',
    # style-tiles is the moodboard's own TILE / ROOM EXAMPLES strip -- the actual rooms these tiles
    # have to build. Omitting it the first time is why the first pack came back flat: the only style
    # references given were a palette grid and a strip of *objects*, so nothing in the input showed
    # what the walls and floors were supposed to look like. It is the most important reference here.
    '--reference', "$REF/style-tiles.png",
    '--reference', "$REPO/style-stone.png"
)
if ($WhatIf) { $argv += '--print-prompt' }

& $SPRITE @argv
