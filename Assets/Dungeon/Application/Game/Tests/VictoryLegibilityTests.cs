using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins that the winning card fits, and reads, on every screen the game ships to.
    /// </summary>
    /// <remarks>
    /// <b>Written the same night as the screen it checks, and before anyone has seen it on a
    /// phone.</b> Every other screen in the game earned a floor and a resolution sweep during the
    /// mobile pass — the tile menu, the title line, the review, the HUD, the shop header — and each
    /// of those was found broken by measuring rather than by looking. This one was written to a
    /// desktop window with a 52-pixel headline and three lines beneath it, which is exactly the
    /// shape that came out four pixels tall last time.
    /// <para>
    /// The widest line is the risk rather than the smallest. <i>YOURS IS THE LAST DUNGEON
    /// STANDING</i> is thirty-four characters drawn centred with no wrapping, and the title screen's
    /// announcement — the same shape — was found spilling eleven pixels off a 360-wide phone.
    /// </para>
    /// </remarks>
    public sealed class VictoryLegibilityTests
    {
        /// <summary>The lines the card draws, with the nominal size each is asked for at.</summary>
        /// <remarks>
        /// Restated from <c>VictoryScreen.Draw</c>, which is the one thing here that is a copy —
        /// the sizes are literals inside a method that cannot be asked. The scale rule itself is
        /// asked, not copied, which is the half that actually drifts.
        /// </remarks>
        private static readonly (string Text, float Nominal)[] Lines =
        {
            ("CONGRATULATIONS", 52f),
            ("YOURS IS THE LAST DUNGEON STANDING", 22f),
            ("45,382", 64f),
            ("POINTS HARVESTED ACROSS THE SEASON", 17f),
            ("PRESS ANY KEY  -  BUILD ANOTHER DUNGEON", 20f)
        };

        /// <summary>
        /// Every line on the card is at least nine pixels tall.
        /// </summary>
        /// <remarks>
        /// Nine is the floor the rest of the interface already uses, so this is the standard the
        /// screen has to meet rather than a new opinion.
        /// </remarks>
        [Test]
        public void EveryLine_StaysReadable()
        {
            float worst = float.MaxValue;
            string worstLine = string.Empty;
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                float ui = VictoryScreen.LayoutScale(Screens.ScaleFor(size));
                foreach ((string text, float nominal) in Lines)
                {
                    float drawn = nominal * ui;
                    if (drawn < worst)
                    {
                        worst = drawn;
                        worstLine = text;
                        worstAt = size;
                    }
                }
            }

            MooseRunnerFacade.Log(
                $"smallest type on the winning card: {worst:F0}px "
                + $"(\"{worstLine}\") at {worstAt.x}x{worstAt.y}");

            Assert.GreaterOrEqual(worst, 9f,
                $"the winning card draws \"{worstLine}\" at {worst:F0}px on {worstAt.x}x{worstAt.y}, "
                + "below the nine the rest of the interface floors at");
        }

        /// <summary>
        /// No line runs off the side of the screen.
        /// </summary>
        /// <remarks>
        /// Measured with the real font inside a GUI pass, because a character count is not a width
        /// and the difference is what the title line's eleven-pixel spill was made of. Every line is
        /// centred across the full window, so fitting means the whole string fits the width.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask NoLine_RunsOffTheScreen(CancellationToken ct)
        {
            var widths = new List<float>();

            await GuiPass.Run(() =>
            {
                foreach (Vector2Int size in Screens.All)
                {
                    float ui = VictoryScreen.LayoutScale(Screens.ScaleFor(size));
                    foreach ((string text, float nominal) in Lines)
                    {
                        widths.Add(GuiPass.Width(text, Mathf.RoundToInt(nominal * ui), true));
                    }
                }
            }, ct);

            float worstSpare = float.MaxValue;
            string worstLine = string.Empty;
            Vector2Int worstAt = Screens.All[0];
            int i = 0;

            foreach (Vector2Int size in Screens.All)
            {
                foreach ((string text, float _) in Lines)
                {
                    float spare = size.x - widths[i++];
                    if (spare < worstSpare)
                    {
                        worstSpare = spare;
                        worstLine = text;
                        worstAt = size;
                    }
                }
            }

            MooseRunnerFacade.Log(
                $"tightest fit on the winning card: {worstSpare:F0}px spare "
                + $"(\"{worstLine}\") at {worstAt.x}x{worstAt.y}");

            Assert.Greater(worstSpare, 0f,
                $"\"{worstLine}\" is {-worstSpare:F0}px wider than a {worstAt.x}x{worstAt.y} screen, "
                + "so the sentence telling the player they won is cut off at both ends");
        }

        /// <summary>
        /// The lowest thing on the card is still on the screen.
        /// </summary>
        /// <remarks>
        /// The cost of flooring the scale, and the check the review screen needed for the same
        /// reason. The block starts a sixth of the way down and the caption sits 196 scaled pixels
        /// below that, so a short screen — the 523x293 itch embed is the shortest — is where a
        /// floored layout runs out of room.
        /// </remarks>
        [Test]
        public void TheLowestLine_StaysOnScreen()
        {
            float worstSpare = float.MaxValue;
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                float ui = VictoryScreen.LayoutScale(Screens.ScaleFor(size));

                // The caption's offset from Draw, plus the height of its own box.
                float bottom = (size.y * 0.16f) + (196f * ui) + (40f * ui);
                float spare = size.y - bottom;

                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: card scale {ui:F2}, lowest line ends at {bottom:F0} "
                    + $"of {size.y}");

                if (spare < worstSpare)
                {
                    worstSpare = spare;
                    worstAt = size;
                }
            }

            Assert.Greater(worstSpare, 0f,
                $"the winning card's lowest line falls {-worstSpare:F0}px off the bottom at "
                + $"{worstAt.x}x{worstAt.y}, so the player never sees what they scored");
        }
    }
}
