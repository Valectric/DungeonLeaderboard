# Generates the 64px dungeon terrain atlas with Sprite Studio.
#
# Verified with --print-prompt before first use. Three things in here are load-bearing and must not
# be "tidied up" -- each was caught by a dry run and each silently ruins the output:
#
#   1. No "NxN" token anywhere in the prompt text. Any such token is parsed as a canvas dimension
#      and overrides --width/--height; the harness then normalises the whole atlas down to it.
#      "sixty-four pixels square" is deliberate phrasing, not verbosity.
#   2. No the word "platform" -- it flips preset inference to `pixel platformer` (side view).
#   3. No --command. There is no `terrain` value for it; the router infers the terrain harness from
#      the prompt. Passing --command pack is what produced six loose prop files on 2026-08-12.
#
# Re-verify any edit with -WhatIf, which appends --print-prompt and launches nothing. The three
# lines to check are: `routed harness: terrain tileset`, `asset category: terrain`,
# `logical canvas: 512x512 pixels`.

param([switch]$WhatIf)

$SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/release/sprite-maker.exe"
if (-not (Test-Path $SPRITE)) {
    $SPRITE = "C:/Users/JohanHoltby/Documents/GitHub/sprite-maker/src-tauri/target/debug/sprite-maker.exe"
}
$STAGE = "C:/Users/JohanHoltby/Documents/sprite-studio-sandbox"
$REF   = "$STAGE/worktrees/dungeonassets/references"
$REPO  = "C:/Users/JohanHoltby/Documents/GitHub/DungeonLeaderboard/Assets/Art/referance"

# REQUIRED, and required in this same process: the codex lookup is cached for the process lifetime.
$env:Path = "$env:LOCALAPPDATA\Programs\OpenAI\Codex\bin;" + $env:Path

if (-not (Test-Path $SPRITE)) { throw "BLOCKED: sprite-maker binary missing at $SPRITE" }
if (-not (Get-Command codex -ErrorAction SilentlyContinue)) { throw "BLOCKED: codex not on PATH" }

$prompt = 'A complete top-down dungeon terrain tileset atlas for a 2D game. Deliver ONE single atlas image holding a coordinated tile family on a transparent background. Each base tile unit is sixty-four pixels square; do not use a thirty-two pixel grid. Include: a large repeatable dungeon floor area spanning at least four by three base tiles of dark flagstone; north, south, east and west wall edge strips; all four outer corners; all four inner concave corners; narrow single-tile horizontal and vertical corridor runs; and two restrained floor texture variants such as cracked flagstone and flagstone with rubble, both palette-compatible. This is a flat top-down dungeon interior, so no elevation and no cliffs. No labels, UI, grid lines, watermark, text, mockup or perspective view. Every shared boundary must use the same edge thickness, colour order and lighting so adjacent tiles meet with no seam, gap or doubled outline. The attached style-stone reference is finished art from this same game: match its stone colour, outline weight and pixel density exactly. Palette: violet-black #251B31, royal purple #50275E, magenta arcane glow #D75268, burnt orange #85432A candlelight, blood red #6D222F, blue-grey #504D63. Dark, eldritch, cute-but-grim. Top-down. Chunky readable pixels, dark outlines. NOT warm brown or tan stone.'

$args = @(
    'generate',
    '--workspace', $STAGE,
    '--prompt', $prompt,
    '--width', '512', '--height', '512',
    '--reference-category', 'palette',
    '--reference', "$REF/style-palette.png",
    '--reference-category', 'art_style',
    '--reference', "$REF/style-tiles.png",
    '--reference', "$REPO/style-stone.png"
)
if ($WhatIf) { $args += '--print-prompt' }

& $SPRITE @args
