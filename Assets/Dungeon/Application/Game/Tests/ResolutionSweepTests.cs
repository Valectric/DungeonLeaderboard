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

        /// <summary>Every shop card and the Ready button are fully on screen at every size.</summary>
        /// <remarks>
        /// A card that is half off the edge is worse than one that is missing: the player can see
        /// part of it, taps it, and the hit test lands somewhere else entirely.
        /// </remarks>
        [Test]
        public void TheShopFits_AtEverySize()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = size.y / 720f;
                Rect[] cards = ShopScreen.Cards(scale, size.x, size.y, out Rect ready);

                for (int i = 0; i < cards.Length; i++)
                {
                    Rect card = cards[i];
                    Assert.GreaterOrEqual(card.x, 0f,
                        $"{size.x}x{size.y}: {ShopScreen.Items[i]} is off the left edge");
                    Assert.LessOrEqual(card.xMax, size.x,
                        $"{size.x}x{size.y}: {ShopScreen.Items[i]} runs off the right edge");
                    Assert.GreaterOrEqual(card.y, 0f,
                        $"{size.x}x{size.y}: {ShopScreen.Items[i]} is off the top");
                    Assert.LessOrEqual(card.yMax, size.y,
                        $"{size.x}x{size.y}: {ShopScreen.Items[i]} runs "
                        + $"{card.yMax - size.y:F0}px off the bottom");
                }

                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: cards span {cards[0].y:F0}..{cards[^1].yMax:F0}, "
                    + $"Ready ends at {ready.yMax:F0} of {size.y}");

                Assert.LessOrEqual(ready.yMax, size.y,
                    $"{size.x}x{size.y}: the Ready button runs {ready.yMax - size.y:F0}px "
                    + "off the bottom, so the player cannot start early");
                Assert.GreaterOrEqual(ready.x, 0f, $"{size.x}x{size.y}: Ready is off the left edge");
                Assert.LessOrEqual(ready.xMax, size.x, $"{size.x}x{size.y}: Ready runs off the right");
            }
        }

        /// <summary>Shop cards never overlap each other or the Ready button.</summary>
        /// <remarks>
        /// Overlapping rectangles mean a tap lands on whichever the hit test happens to check first,
        /// which is a bug the player experiences as buying the wrong thing.
        /// </remarks>
        [Test]
        public void ShopCards_NeverOverlap()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = size.y / 720f;
                Rect[] cards = ShopScreen.Cards(scale, size.x, size.y, out Rect ready);

                for (int i = 0; i < cards.Length; i++)
                {
                    Assert.IsFalse(cards[i].Overlaps(ready),
                        $"{size.x}x{size.y}: {ShopScreen.Items[i]} overlaps the Ready button");

                    for (int j = i + 1; j < cards.Length; j++)
                    {
                        Assert.IsFalse(cards[i].Overlaps(cards[j]),
                            $"{size.x}x{size.y}: {ShopScreen.Items[i]} overlaps "
                            + $"{ShopScreen.Items[j]}");
                    }
                }
            }
        }

        /// <summary>Every card is big enough to hit with a thumb.</summary>
        /// <remarks>
        /// The game ships to phones. A card only a few pixels tall is technically on screen and
        /// practically untappable.
        /// </remarks>
        [Test]
        public void EveryCard_IsBigEnoughToTap()
        {
            foreach (Vector2Int size in Sizes)
            {
                float scale = size.y / 720f;
                Rect[] cards = ShopScreen.Cards(scale, size.x, size.y, out _);

                foreach (Rect card in cards)
                {
                    Assert.Greater(card.width, 24f,
                        $"{size.x}x{size.y}: a card is only {card.width:F0}px wide");
                    Assert.Greater(card.height, 18f,
                        $"{size.x}x{size.y}: a card is only {card.height:F0}px tall");
                }
            }
        }
    }
}
