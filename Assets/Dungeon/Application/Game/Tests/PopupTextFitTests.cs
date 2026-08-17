using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dungeon.ShopManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Checks that the tile menu's labels fit the boxes they are drawn in.
    /// </summary>
    /// <remarks>
    /// The risk the mobile enlargement created. Tripling the rows for a phone and then sizing the
    /// type from the row takes the font from 11 pixels to about 33 — but the name is drawn into
    /// <c>row.width * 0.62</c>, and the row only got wider by the width cap, not by three. Bigger
    /// text in a box that did not grow as fast is how a label gets clipped, and "TREASURE CHEST" is
    /// fourteen characters.
    /// <para>
    /// Measured with <c>GUIStyle.CalcSize</c> against the real style rather than by counting
    /// characters, because the font is the game's own and a guess at average glyph width is not
    /// evidence. This is the same lesson as the phone defects of 2026-08-16, where the tests that
    /// missed them carried their own copy of the font size instead of reading production's.
    /// </para>
    /// </remarks>
    public sealed class PopupTextFitTests
    {
        /// <summary>
        /// Measures text the only way Unity allows: from inside an <c>OnGUI</c> pass.
        /// </summary>
        /// <remarks>
        /// <c>GUI.skin</c> throws "You can only call GUI functions from inside OnGUI" anywhere else,
        /// so a test that wants the real width of a real label has to get itself into a real IMGUI
        /// frame. This exists for that and nothing else.
        /// </remarks>
        private sealed class Measurer : MonoBehaviour
        {
            /// <summary>Rows to check, one per request.</summary>
            public readonly List<Rect> Rows = new();

            /// <summary>Interface scale for each request.</summary>
            public readonly List<float> Scales = new();

            /// <summary>Item whose name is being measured, per request.</summary>
            public readonly List<ShopItem> Items = new();

            /// <summary>How much wider the drawn name is than its box; negative means it fits.</summary>
            public readonly List<float> Overflow = new();

            /// <summary>Font size production chose, per request.</summary>
            public readonly List<float> Fonts = new();

            /// <summary>Whether the measuring pass has run.</summary>
            public bool Done { get; private set; }

            /// <summary>Asks the game for its font size and measures the result.</summary>
            private void OnGUI()
            {
                if (Done)
                {
                    return;
                }

                for (int i = 0; i < Rows.Count; i++)
                {
                    Rect row = Rows[i];
                    float scale = Scales[i];

                    // Production's own answer, not a restatement of it.
                    float font = ShopScreen.NameFontSize(row, scale, Items[i]);
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = Mathf.RoundToInt(font),
                        fontStyle = FontStyle.Bold
                    };

                    float pad = Mathf.Max(10f * scale, row.height * 0.13f);
                    float box = (row.width * 0.62f) - pad;
                    float drawn = style.CalcSize(new GUIContent(ShopScreen.NameOf(Items[i]))).x;

                    Fonts.Add(font);
                    Overflow.Add(drawn - box);
                }

                Done = true;
            }
        }

        /// <summary>Screens the game is expected to run on, matching the resolution sweep.</summary>
        private static readonly Vector2Int[] Screens =
        {
            new(1920, 1080), new(1280, 720), new(1024, 768), new(800, 480),
            new(768, 1024), new(390, 844), new(360, 780), new(523, 293)
        };

        /// <summary>The interface scale the game uses at a given size.</summary>
        /// <param name="size">Screen size in pixels.</param>
        /// <returns>Scale factor.</returns>
        private static float ScaleFor(Vector2Int size)
        {
            return Mathf.Min(size.x / 1280f, size.y / 720f);
        }

        /// <summary>
        /// Every item name fits the box it is drawn in, on every screen the game ships to.
        /// </summary>
        /// <remarks>
        /// A clipped name is worse than a small one: the player is choosing between things whose
        /// names they cannot finish reading, on the screen where the choice is made.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask EveryItemName_FitsItsBox(CancellationToken ct)
        {
            var host = new GameObject("measurer");
            var measurer = host.AddComponent<Measurer>();
            var where = new List<Vector2Int>();

            foreach (Vector2Int size in Screens)
            {
                float scale = ScaleFor(size);
                Rect[] rows = ShopLayout.PopupRows(
                    new Vector2(size.x * 0.5f, size.y * 0.4f), scale, size.x, size.y);

                foreach (ShopItem item in ShopScreen.Items)
                {
                    measurer.Rows.Add(rows[0]);
                    measurer.Scales.Add(scale);
                    measurer.Items.Add(item);
                    where.Add(size);
                }
            }

            for (int frame = 0; frame < 30 && !measurer.Done; frame++)
            {
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(measurer.Done, "the measuring pass never ran");

            float worst = float.MinValue;
            string worstText = string.Empty;
            Vector2Int worstAt = Screens[0];
            float worstFont = 0f;

            for (int i = 0; i < measurer.Overflow.Count; i++)
            {
                if (measurer.Overflow[i] > worst)
                {
                    worst = measurer.Overflow[i];
                    worstText = ShopScreen.NameOf(measurer.Items[i]);
                    worstAt = where[i];
                    worstFont = measurer.Fonts[i];
                }
            }

            MooseRunnerFacade.Log(
                $"tightest label: \"{worstText}\" at {worstAt.x}x{worstAt.y}, drawn at "
                + $"{worstFont:F0}px -- {(worst > 0f ? "OVERFLOWS by" : "spare")} "
                + $"{Mathf.Abs(worst):F0}px");

            Object.DestroyImmediate(host);

            Assert.LessOrEqual(worst, 0.5f,
                $"\"{worstText}\" is drawn {worst:F0}px wider than its box at "
                + $"{worstAt.x}x{worstAt.y}, so it is clipped on the screen where the player "
                + "chooses what to build");
        }
    }
}
