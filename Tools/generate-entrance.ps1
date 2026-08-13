# Generates the dungeon ENTRANCE scene: forest, path, and the mouth of the dungeon.
#
# This is the one piece the moodboard does not contain, so it is the one piece worth generating.
# Everything else -- walls, floors, doors -- is cut from the moodboard directly, because six
# attempts at generating wall tiles produced art that did not match it.
#
# Routed through the TERRAIN harness deliberately. Per CLAUDE.md, only terrain and effect forward
# `--reference` images to ImageGen; `--command pack` shows them to the agent and never to the model
# actually drawing. That single fact accounts for every failed tile run.
#
# Traps that still apply: no "NxN" token in the prompt (read as a canvas dimension, silently
# overrides --width/--height), and never the word "platform".

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

$prompt = 'A top-down terrain scene showing the approach to a dungeon entrance, read left to right, as one wide image.

LAYOUT, left to right, and THE ORIENTATION IS THE MOST IMPORTANT PART: a small stretch of dark green forest with a handful of small conifer trees and undergrowth on the LEFT; a worn dirt path winding out of the trees and running from LEFT to RIGHT; the path opening onto a flagstone forecourt; and at the RIGHT-HAND EDGE, the mouth of the dungeon.

The dungeon wall must be a VERTICAL wall running from the top of the image to the bottom, standing at the right-hand edge like the left-hand face of a building. The archway is set into that vertical wall and ITS OPENING FACES LEFT, WEST, back toward the forest and the path. A party walking left-to-right along the path must be able to walk straight in through it.

Do NOT draw the wall as a horizontal band across the top with the arch opening downward -- that is the wrong orientation for this game and a previous attempt was rejected for it. The wall is vertical, on the right, and the arch faces left.

PASS EVERY ATTACHED REFERENCE IMAGE TO THE IMAGE GENERATOR through referenced_image_paths, and instruct it to copy their look. The style-wall reference is real masonry from this exact game and the archway and wall must match it: rounded rectangular stone blocks with mottled surface texture, a pale worn rim catching the light along the top of each block, and deep near-black gaps between blocks.

Strictly orthographic top-down, no perspective, no horizon, no sky, no labels, no text, no UI, no grid lines.

Brightness: the dungeon stone averages luminance 30 out of 255, darkest tenth near 5, brightest near 51. The forest is darker still and heavily desaturated -- muted green-black, never bright or saturated green. This is a grim eldritch world, not a cheerful one.

Feature size: every mark is a chunky block four pixels across. No single-pixel speckle, no dithering, no smooth gradients.

Palette: violet-black #251B31, blue-grey #504D63 stone, royal purple #50275E shadow, muted green-black foliage, burnt orange #85432A only as rare torchlight near the arch. Dark, eldritch, cute-but-grim. NOT warm brown or tan stone, and NOT light grey.'

$argv = @(
    'generate',
    '--workspace', $STAGE,
    '--prompt', $prompt,
    '--width', '512', '--height', '256',
    '--reference-category', 'palette',
    '--reference', "$REF/style-palette.png",
    '--reference-category', 'art_style',
    '--reference', "$REPO/style-wall.png",
    '--reference', "$REF/style-tiles.png"
)
if ($WhatIf) { $argv += '--print-prompt' }

& $SPRITE @argv
