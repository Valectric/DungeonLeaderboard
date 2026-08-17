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
            var rows = new List<Rect>();
            var items = new List<ShopItem>();
            var where = new List<Vector2Int>();
            var scales = new List<float>();
            var overflow = new List<float>();
            var fonts = new List<float>();

            foreach (Vector2Int size in Screens.All)
            {
                float scale = Screens.ScaleFor(size);
                Rect[] popup = ShopLayout.PopupRows(
                    new Vector2(size.x * 0.5f, size.y * 0.4f), scale, size.x, size.y);

                foreach (ShopItem item in ShopScreen.Items)
                {
                    rows.Add(popup[0]);
                    scales.Add(scale);
                    items.Add(item);
                    where.Add(size);
                }
            }

            await GuiPass.Run(() =>
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    // Production's own answer for the size it will draw at, then the width that
                    // produces -- rather than this test keeping a copy of either.
                    float font = ShopScreen.NameFontSize(rows[i], scales[i], items[i]);
                    fonts.Add(font);

                    float pad = Mathf.Max(10f * scales[i], rows[i].height * 0.13f);
                    float box = (rows[i].width * 0.62f) - pad;
                    overflow.Add(
                        GuiPass.Width(ShopScreen.NameOf(items[i]), Mathf.RoundToInt(font)) - box);
                }
            }, ct);

            float worst = float.MinValue;
            string worstText = string.Empty;
            Vector2Int worstAt = Screens.All[0];
            float worstFont = 0f;

            for (int i = 0; i < overflow.Count; i++)
            {
                if (overflow[i] > worst)
                {
                    worst = overflow[i];
                    worstText = ShopScreen.NameOf(items[i]);
                    worstAt = where[i];
                    worstFont = fonts[i];
                }
            }

            MooseRunnerFacade.Log(
                $"tightest label: \"{worstText}\" at {worstAt.x}x{worstAt.y}, drawn at "
                + $"{worstFont:F0}px -- {(worst > 0f ? "OVERFLOWS by" : "spare")} "
                + $"{Mathf.Abs(worst):F0}px");


            Assert.LessOrEqual(worst, 0.5f,
                $"\"{worstText}\" is drawn {worst:F0}px wider than its box at "
                + $"{worstAt.x}x{worstAt.y}, so it is clipped on the screen where the player "
                + "chooses what to build");
        }
    }
}
