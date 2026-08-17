using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins that the raid HUD stays readable, and stays out of its own way, on every screen.
    /// </summary>
    /// <remarks>
    /// The last screen in the interface sweep that followed the author's report of the tile menu
    /// being too small on a phone, and the third to be found with the same defect. Only the modifier
    /// line was floored: on a 360x780 phone the clock drew at ten pixels and both captions at four.
    /// <para>
    /// The rate is the one that matters. <c>GameController</c>'s own comment calls it "the game" —
    /// the biggest thing on screen, the number a player has to <i>see</i> costing them without
    /// reading a tutorial — and it was fifteen pixels tall on a phone, smaller than the desktop
    /// caption beneath it.
    /// </para>
    /// </remarks>
    public sealed class HudLegibilityTests
    {
        /// <summary>Measures HUD strings from inside a real IMGUI frame.</summary>
        private sealed class Measurer : MonoBehaviour
        {
            /// <summary>Strings to measure.</summary>
            public readonly List<string> Texts = new();

            /// <summary>Font size for each string.</summary>
            public readonly List<int> Sizes = new();

            /// <summary>Measured widths, once <see cref="Done"/> is set.</summary>
            public readonly List<float> Widths = new();

            /// <summary>Whether the measuring pass has run.</summary>
            public bool Done { get; private set; }

            /// <summary>Measures each string once.</summary>
            private void OnGUI()
            {
                if (Done)
                {
                    return;
                }

                for (int i = 0; i < Texts.Count; i++)
                {
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Sizes[i],
                        fontStyle = FontStyle.Bold
                    };

                    Widths.Add(style.CalcSize(new GUIContent(Texts[i])).x);
                }

                Done = true;
            }
        }



        /// <summary>The scale the HUD lays out at, floored.</summary>
        /// <param name="size">Screen size in pixels.</param>
        /// <returns>Layout scale.</returns>
        private static float HudScaleFor(Vector2Int size)
        {
            return Mathf.Max(Screens.ScaleFor(size), GameController.HudMinimumScale);
        }

        /// <summary>Nominal size of the HUD's captions, from the HUD block.</summary>
        private const float CaptionNominal = 15f;

        /// <summary>Nominal size of the rate, from the HUD block.</summary>
        private const float RateNominal = 52f;

        /// <summary>
        /// The HUD's smallest type stays at nine pixels or more.
        /// </summary>
        /// <remarks>
        /// Nine is the floor the rest of the interface already uses, so this is the standard the HUD
        /// was missing rather than a new opinion.
        /// </remarks>
        [Test]
        public void TheSmallestHudType_StaysReadable()
        {
            float worst = float.MaxValue;
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                float drawn = CaptionNominal * HudScaleFor(size);
                if (drawn < worst)
                {
                    worst = drawn;
                    worstAt = size;
                }
            }

            MooseRunnerFacade.Log(
                $"smallest HUD caption: {worst:F0}px at {worstAt.x}x{worstAt.y} "
                + $"(unfloored it would be {CaptionNominal * Screens.ScaleFor(worstAt):F0}px)");

            Assert.GreaterOrEqual(worst, 9f,
                $"the HUD draws its captions at {worst:F0}px on {worstAt.x}x{worstAt.y}");
        }

        /// <summary>
        /// The clock, the rate and the harvest figure never run into each other.
        /// </summary>
        /// <remarks>
        /// The cost of flooring the HUD, and the reason the floor is 0.6 rather than the review
        /// screen's 0.7. The three blocks are anchored left, centre and right of the same strip, so
        /// growing the type on the narrowest screen is exactly where they would collide — and the
        /// rate is centred, so it grows into both of its neighbours at once.
        /// <para>
        /// Measured against the worst strings the game can actually show: a full clock, a rate in
        /// the thirties, and a five-figure harvest.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask TheHudBlocks_NeverCollide(CancellationToken ct)
        {
            var host = new GameObject("hud-measurer");
            var measurer = host.AddComponent<Measurer>();

            foreach (Vector2Int size in Screens.All)
            {
                float hud = HudScaleFor(size);
                measurer.Texts.Add("1:00");
                measurer.Sizes.Add(Mathf.RoundToInt(34 * hud));
                measurer.Texts.Add("37.5/s");
                measurer.Sizes.Add(Mathf.RoundToInt(RateNominal * hud * 1.18f));
                measurer.Texts.Add("12345");
                measurer.Sizes.Add(Mathf.RoundToInt(28 * hud));
            }

            for (int frame = 0; frame < 30 && !measurer.Done; frame++)
            {
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(measurer.Done, "the measuring pass never ran");

            float worstGap = float.MaxValue;
            Vector2Int worstAt = Screens.All[0];

            for (int i = 0; i < Screens.All.Length; i++)
            {
                Vector2Int size = Screens.All[i];
                float hud = HudScaleFor(size);

                float clockRight = (24f * hud) + measurer.Widths[i * 3];
                float rateHalf = measurer.Widths[(i * 3) + 1] * 0.5f;
                float rateLeft = (size.x * 0.5f) - rateHalf;
                float rateRight = (size.x * 0.5f) + rateHalf;
                float harvestLeft = size.x - (24f * hud) - measurer.Widths[(i * 3) + 2];

                float gap = Mathf.Min(rateLeft - clockRight, harvestLeft - rateRight);
                if (gap < worstGap)
                {
                    worstGap = gap;
                    worstAt = size;
                }

                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: clock ends {clockRight:F0}, rate {rateLeft:F0}-{rateRight:F0}, "
                    + $"harvest starts {harvestLeft:F0}");
            }

            MooseRunnerFacade.Log(
                $"tightest HUD gap: {worstGap:F0}px at {worstAt.x}x{worstAt.y}");

            Object.DestroyImmediate(host);

            Assert.Greater(worstGap, 0f,
                $"the HUD's blocks overlap by {-worstGap:F0}px at {worstAt.x}x{worstAt.y}, so the "
                + "clock, the rate or the harvest figure is drawn over another of them");
        }
    }
}
