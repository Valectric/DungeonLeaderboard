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
            new(2560, 1080)     // ultrawide
        };

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
                float scale = size.y / 720f;

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
                float scale = size.y / 720f;
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
                float scale = size.y / 720f;
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
                float scale = size.y / 720f;
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
                float scale = size.y / 720f;
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
    }
}
