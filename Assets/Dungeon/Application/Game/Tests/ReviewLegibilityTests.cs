using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins that the review screen stays readable, and still fits, on every screen.
    /// </summary>
    /// <remarks>
    /// The review is where a player learns what the minute they just played was worth and what to do
    /// differently — <c>RaidReview</c>'s lesson line is the one that teaches the game. On a
    /// 360x780 phone it was drawing that line at <b>four pixels</b>, because every font and offset
    /// on the screen derives from the interface scale and this screen, alone among the three, had no
    /// floor on it.
    /// <para>
    /// Found while sweeping the interface for text that does not fit, after the author reported the
    /// tile menu being too small on mobile. The same complaint applied here and nobody had said so,
    /// which is the argument for sweeping rather than fixing what was reported.
    /// </para>
    /// </remarks>
    public sealed class ReviewLegibilityTests
    {


        /// <summary>Nominal size of the smallest type on the screen, from <c>ReviewScreen.Draw</c>.</summary>
        private const float SmallestNominal = 13f;

        /// <summary>Nominal offset of the lowest line, from <c>ReviewScreen.Draw</c>.</summary>
        private const float LowestLine = 292f + 28f;

        /// <summary>
        /// The smallest type on the review screen stays at nine pixels or more.
        /// </summary>
        /// <remarks>
        /// Nine is the floor the rest of the interface already uses — <c>LeagueScreen</c> and
        /// <c>ShopScreen</c> both clamp there — so it is the standard this screen was missing rather
        /// than a new opinion.
        /// </remarks>
        [Test]
        public void TheSmallestType_StaysReadable()
        {
            float worst = float.MaxValue;
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                float drawn = SmallestNominal * ReviewScreen.LayoutScale(Screens.ScaleFor(size));
                if (drawn < worst)
                {
                    worst = drawn;
                    worstAt = size;
                }
            }

            MooseRunnerFacade.Log(
                $"smallest review type: {worst:F0}px at {worstAt.x}x{worstAt.y} "
                + $"(unfloored it would be {SmallestNominal * Screens.ScaleFor(worstAt):F0}px)");

            Assert.GreaterOrEqual(worst, 9f,
                $"the review draws its smallest line at {worst:F0}px on {worstAt.x}x{worstAt.y}, "
                + "below the nine the rest of the interface floors at -- the screen that tells the "
                + "player what their raid was worth is unreadable there");
        }

        /// <summary>
        /// The floored layout still fits the shortest screen the game ships to.
        /// </summary>
        /// <remarks>
        /// The cost of the floor, and the reason it is 0.7 rather than 1. The screen starts a fifth
        /// of the way down and runs to its prompt; on the 523x293 itch embed there is not much room
        /// beneath that, so raising the floor further would push the prompt off the bottom — which
        /// is the same trade the tile menu makes against the Ready button.
        /// </remarks>
        [Test]
        public void TheFlooredLayout_StillFits()
        {
            float worstSpare = float.MaxValue;
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                float scale = ReviewScreen.LayoutScale(Screens.ScaleFor(size));
                float bottom = (size.y * 0.2f) + (LowestLine * scale);
                float spare = size.y - bottom;

                if (spare < worstSpare)
                {
                    worstSpare = spare;
                    worstAt = size;
                }

                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: layout scale {scale:F2}, lowest line at {bottom:F0} of {size.y}");
            }

            Assert.Greater(worstSpare, 0f,
                $"the review's lowest line falls {-worstSpare:F0}px off the bottom at "
                + $"{worstAt.x}x{worstAt.y}, so the prompt telling the player how to continue is "
                + "not on screen");
        }
    }
}
