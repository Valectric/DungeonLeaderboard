using Dungeon.ShopManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Checks every screen still fits at the sizes the game is actually played at.
    /// </summary>
    /// <remarks>
    /// The canvas is 960x600, but <b>the itch.io embed is 523x293</b> — a little over half as wide
    /// and less than half as tall. A layout that fits in the editor and overflows there is invisible
    /// until somebody opens the published page, which is exactly how the standings lost their "press
    /// any key" prompt off the bottom edge.
    /// <para>
    /// The scale factor is <c>height / 720</c>, so a short canvas shrinks everything — which usually
    /// helps — while a wide, short one is the awkward case, because row heights scale down but the
    /// row <i>count</i> does not.
    /// </para>
    /// </remarks>
    public sealed class ResolutionSweepTests
    {
        /// <summary>Sizes worth checking, including the one the game is embedded at.</summary>
        private static readonly Vector2Int[] Sizes =
        {
            new(523, 293),      // the itch.io embed, as configured today
            new(960, 600),      // the canvas the build declares
            new(1280, 720),
            new(1920, 1080),
            new(800, 480),      // a small landscape phone
            new(1024, 768),     // 4:3
            new(2560, 1080),    // ultrawide
            new(390, 844),      // a phone held upright, which is how people pick one up
            new(360, 780),      // a smaller one
            new(768, 1024)      // a tablet in portrait
        };

        /// <summary>
        /// The scale the shipped game draws at, for a given screen.
        /// </summary>
        /// <remarks>
        /// This has to be <c>GameController.UiScale</c>'s arithmetic and not an approximation of it.
        /// Every case here used <c>height / 720</c>, which agrees with the game in landscape and is
        /// wildly wrong in portrait: a phone held upright reports 1.17 by that formula and 0.30 by
        /// the game's, so the whole sweep was checking a layout four times larger than the one on
        /// the glass. Portrait text coming out squished was reported from a real phone and could
        /// not have failed here, because here it was never portrait.
        /// </remarks>
        /// <param name="size">Screen size to scale for.</param>
        /// <returns>The UI scale.</returns>
        private static float UiScaleAt(Vector2Int size)
        {
            return Mathf.Min(size.x / 1280f, size.y / 720f);
        }

        /// <summary>The standings prompt is on screen at every size.</summary>
        /// <remarks>
        /// The one line that tells a new player how to start. Losing it strands them on a screen that
        /// appears to have stopped responding.
        /// </remarks>
        [Test]
        public void TheStandingsPrompt_FitsAtEverySize()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = UiScaleAt(size);

                foreach (bool announcing in new[] { false, true })
                {
                    Rect prompt = LeagueScreen.PromptRect(scale, announcing, size.x, size.y);

                    MooseRunnerFacade.Log(
                        $"{size.x}x{size.y} announcing={announcing}: "
                        + $"prompt spans {prompt.y:F0}..{prompt.yMax:F0} of {size.y}");

                    Assert.GreaterOrEqual(prompt.y, 0f,
                        $"{size.x}x{size.y}: the prompt is off the top");
                    Assert.LessOrEqual(prompt.yMax, size.y,
                        $"{size.x}x{size.y}: the prompt runs {prompt.yMax - size.y:F0}px "
                        + "off the bottom");
                }
            }
        }

        /// <summary>
        /// The Ready button and a tile menu are fully on screen at every size.
        /// </summary>
        /// <remarks>
        /// A control half off the edge is worse than a missing one: the player can see part of it,
        /// taps it, and the hit test lands somewhere else entirely. The menu is the harder case
        /// because it is anchored to whatever tile was tapped, so it has to survive being opened on a
        /// tile in the very corner of a narrow embed.
        /// </remarks>
        [Test]
        public void TheShopFits_AtEverySize()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = UiScaleAt(size);
                Rect ready = ShopScreen.ReadyRect(scale, size.x, size.y);

                Assert.LessOrEqual(ready.yMax, size.y,
                    $"{size.x}x{size.y}: the Ready button runs {ready.yMax - size.y:F0}px "
                    + "off the bottom, so the player cannot start early");
                Assert.GreaterOrEqual(ready.x, 0f, $"{size.x}x{size.y}: Ready is off the left edge");
                Assert.LessOrEqual(ready.xMax, size.x, $"{size.x}x{size.y}: Ready runs off the right");

                // Opened hard against each corner, which is where clamping either works or does not.
                foreach (Vector2 corner in new[]
                         {
                             new Vector2(0f, 0f), new Vector2(size.x, 0f),
                             new Vector2(0f, size.y), new Vector2(size.x, size.y),
                             new Vector2(size.x * 0.5f, size.y * 0.5f)
                         })
                {
                    Rect[] rows = ShopScreen.PopupRows(corner, scale, size.x, size.y);
                    foreach (Rect row in rows)
                    {
                        Assert.GreaterOrEqual(row.x, 0f,
                            $"{size.x}x{size.y}: a menu row opened at {corner} is off the left");
                        Assert.LessOrEqual(row.xMax, size.x,
                            $"{size.x}x{size.y}: a menu row opened at {corner} runs off the right");
                        Assert.GreaterOrEqual(row.y, 0f,
                            $"{size.x}x{size.y}: a menu row opened at {corner} is off the top");
                        Assert.LessOrEqual(row.yMax, size.y,
                            $"{size.x}x{size.y}: a menu row opened at {corner} runs "
                            + $"{row.yMax - size.y:F0}px off the bottom");
                    }
                }

                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: Ready ends at {ready.yMax:F0} of {size.y}");
            }
        }

        /// <summary>
        /// Nothing in the shop is ever drawn over the Ready button.
        /// </summary>
        /// <remarks>
        /// Ready is hit-tested before anything else, so a control overlapping it is not merely untidy
        /// — it is unreachable, and pressing it starts the raid instead. A menu opened on a low tile
        /// did exactly that: the bottom row sat over Ready, so buying the last item threw away the
        /// purchase, the rest of the shop clock, and any chance of understanding why.
        /// <para>
        /// Swept over every anchor the dungeon can put a control at, because which tiles are low on
        /// screen depends on zoom, pan and canvas shape, and the failing one was three quarters of
        /// the way down.
        /// </para>
        /// </remarks>
        [Test]
        public void NothingIsDrawnOverReady()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = UiScaleAt(size);
                Rect ready = ShopScreen.ReadyRect(scale, size.x, size.y);

                for (int step = 0; step <= 10; step++)
                {
                    var anchor = new Vector2(size.x * (step / 10f), size.y * (step / 10f));

                    foreach (Rect row in ShopScreen.PopupRows(anchor, scale, size.x, size.y))
                    {
                        Assert.IsFalse(row.Overlaps(ready),
                            $"{size.x}x{size.y}: a menu row opened at {anchor} covers Ready, so "
                            + "buying it would start the raid instead");
                    }

                    Assert.IsFalse(
                        ShopScreen.HallMarkerRect(anchor, scale, size.x, size.y).Overlaps(ready),
                        $"{size.x}x{size.y}: the hall marker at {anchor} covers Ready");
                }
            }
        }

        /// <summary>Menu rows never overlap each other.</summary>
        /// <remarks>
        /// Overlapping rectangles mean a tap lands on whichever the hit test happens to check first,
        /// which is a bug the player experiences as buying the wrong thing.
        /// </remarks>
        [Test]
        public void MenuRows_NeverOverlap()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = UiScaleAt(size);
                Rect[] rows = ShopScreen.PopupRows(
                    new Vector2(size.x * 0.5f, size.y * 0.4f), scale, size.x, size.y);

                for (int i = 0; i < rows.Length; i++)
                {
                    for (int j = i + 1; j < rows.Length; j++)
                    {
                        Assert.IsFalse(rows[i].Overlaps(rows[j]),
                            $"{size.x}x{size.y}: {ShopScreen.Items[i]} overlaps "
                            + $"{ShopScreen.Items[j]}");
                    }
                }
            }
        }

        /// <summary>Every menu row and the hall marker are big enough to hit with a thumb.</summary>
        /// <remarks>
        /// The game ships to phones. A row only a few pixels tall is technically on screen and
        /// practically untappable.
        /// </remarks>
        [Test]
        public void EveryControl_IsBigEnoughToTap()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = UiScaleAt(size);
                Rect[] rows = ShopScreen.PopupRows(
                    new Vector2(size.x * 0.5f, size.y * 0.4f), scale, size.x, size.y);

                foreach (Rect row in rows)
                {
                    Assert.Greater(row.width, 24f,
                        $"{size.x}x{size.y}: a menu row is only {row.width:F0}px wide");
                    Assert.Greater(row.height, 18f,
                        $"{size.x}x{size.y}: a menu row is only {row.height:F0}px tall");
                }

                Rect marker = ShopScreen.HallMarkerRect(
                    new Vector2(size.x * 0.8f, size.y * 0.5f), scale, size.x, size.y);
                Assert.Greater(marker.width, 24f,
                    $"{size.x}x{size.y}: the hall marker is only {marker.width:F0}px wide");
                Assert.Greater(marker.height, 18f,
                    $"{size.x}x{size.y}: the hall marker is only {marker.height:F0}px tall");
                Assert.GreaterOrEqual(marker.x, 0f, $"{size.x}x{size.y}: the marker is off the left");
                Assert.LessOrEqual(marker.xMax, size.x,
                    $"{size.x}x{size.y}: the marker runs off the right");
            }
        }

        /// <summary>
        /// The first raid's coaching text has room to be read at every size.
        /// </summary>
        /// <remarks>
        /// The hints are the only tutorial the game has, and they are shown to the player least
        /// likely to forgive a broken screen — the one who has never played it. The longest line is
        /// about fifty-three characters, which at the nine-pixel floor the styles clamp to needs
        /// roughly 265 pixels; a straight 560-times-scale gives 228 of them in the itch embed.
        /// <para>
        /// Checked in characters rather than by rendering, because IMGUI cannot measure text outside
        /// a repaint. Approximate on purpose, and the approximation errs generous: at these sizes
        /// bold Arial averages nearer half the point size per character than the 0.55 assumed here.
        /// </para>
        /// </remarks>
        [Test]
        public void TheOpeningHints_FitAtEverySize()
        {
            const string longest = "TAP THE SLIME PIT TO HOLD THEM  -  TOO MANY AND THEY DIE";

            foreach (Vector2Int size in Sizes)
            {
                float scale = UiScaleAt(size);
                float width = Hints.BlockWidth(scale, size.x);
                float fontSize = Mathf.Max(9, Mathf.RoundToInt(14 * scale));
                float needed = longest.Length * fontSize * 0.55f;

                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: hint block {width:F0}px wide, longest line needs "
                    + $"about {needed:F0}px at {fontSize:F0}px");

                Assert.LessOrEqual(width, size.x,
                    $"{size.x}x{size.y}: the hint block is wider than the screen");
                Assert.Greater(width, needed,
                    $"{size.x}x{size.y}: the longest hint needs about {needed:F0}px and has "
                    + $"{width:F0}px, so the first thing a new player is told is cut off");
            }
        }

        /// <summary>
        /// A dungeon tile is big enough to tap at every size the game ships at.
        /// </summary>
        /// <remarks>
        /// New with the spatial shop, and the one thing about it that the itch.io embed genuinely
        /// threatens. Buying is now aimed at a <i>tile</i>, so the tile is a button — and at 523x293
        /// the whole three-room corridor is fitted into 523 pixels of width. If a cell lands at eight
        /// pixels, the shop is unusable on the page most jam voters will play it on, while looking
        /// perfect in the editor. Exactly the class of bug the project's doctrine says only a
        /// measurement catches.
        /// <para>
        /// The camera fit is reproduced here rather than called, because <c>FrameCamera</c> is
        /// private to the controller and depends on a live scene. The arithmetic is small and stated
        /// in one place; if the framing changes and this drifts, the numbers logged below stop
        /// matching what the game does and the next person has a thread to pull.
        /// </para>
        /// </remarks>
        [Test]
        public void ADungeonTile_IsBigEnoughToTap()
        {
            // Swept over both ends of what the shop can build. Checking only the opening dungeon
            // would have reported a comfortable 26px and missed the case that matters: a player who
            // buys halls all season ends up aiming at a corridor two thirds again as wide, fitted
            // into the same 523 pixels.
            const int roomWidth = 5;
            const int roomHeight = 5;
            const int gridHeight = roomHeight + 2;

            float smallest = float.MaxValue;
            string smallestAt = "nowhere";
            float smallestUpright = float.MaxValue;
            string uprightAt = "nowhere";

            foreach (int roomCount in new[] { 3, 5 })
            {
                int gridWidth = (roomCount * roomWidth) + (roomCount - 1) + 2;

                foreach (Vector2Int size in Sizes)
                {
                    float aspect = size.x / (float)size.y;

                    // Matches GameController.FrameCamera: fit the dungeon by whichever axis binds.
                    float halfHeight = (gridHeight * 0.5f) + 1.6f;
                    float halfWidth = (gridWidth * 0.5f) + 0.5f;
                    float orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);

                    // One world unit is one cell, and the view is two orthographic sizes tall.
                    float pixelsPerCell = size.y / (orthographicSize * 2f);

                    MooseRunnerFacade.Log(
                        $"{roomCount} rooms at {size.x}x{size.y}: one tile is "
                        + $"{pixelsPerCell:F1}px across");

                    bool upright = size.y > size.x;
                    if (upright && pixelsPerCell < smallestUpright)
                    {
                        smallestUpright = pixelsPerCell;
                        uprightAt = $"{roomCount} rooms at {size.x}x{size.y}";
                    }

                    if (!upright && pixelsPerCell < smallest)
                    {
                        smallest = pixelsPerCell;
                        smallestAt = $"{roomCount} rooms at {size.x}x{size.y}";
                    }
                }
            }

            // 15px rather than a thumb-sized 44, because the shop lets the player zoom and pan and
            // the hint now says so. The tightest case measured is a five-room corridor in the itch
            // embed at 16.4px: fiddly with a mouse and genuinely awkward on a phone without zooming
            // first. If it drops below this the default framing has stopped being a usable board and
            // the shop needs to open closer in rather than fitted to the whole dungeon.
            Assert.Greater(smallest, 15f,
                $"at {smallestAt} a dungeon tile is only {smallest:F1}px across, so aiming a "
                + "purchase at one is guesswork on the page the game actually ships on");

            // A phone held upright is a separate requirement, and a weaker one, because it is
            // arithmetic rather than a choice: a five-room corridor is thirty-one cells wide, and
            // thirty-one cells across 360 pixels is eleven pixels each however the camera is
            // configured. Fitting by height instead would make the tiles four times bigger and take
            // most of the dungeon off screen, which is worse during a raid -- the whole board has to
            // be readable while the party crosses it.
            //
            // So aiming a purchase in portrait means pinching in first, which is exactly the gesture
            // that did not work until D32: the first finger was read as a tap, so a player on a
            // phone could neither see the tile nor zoom to it. This asserts only that the board is
            // still legible at the opening framing; the pinch is what makes it usable.
            MooseRunnerFacade.Log(
                $"upright phones: tightest tile is {smallestUpright:F1}px at {uprightAt} "
                + $"(landscape tightest {smallest:F1}px at {smallestAt})");

            Assert.Greater(smallestUpright, 8f,
                $"at {uprightAt} a dungeon tile is only {smallestUpright:F1}px across -- below this "
                + "the board is not readable at all on a phone held upright, and no amount of "
                + "zooming makes an unreadable overview usable");
        }
    }
}
