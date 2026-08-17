using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins the size of the tile menu the shop opens, on every screen the game ships to.
    /// </summary>
    /// <remarks>
    /// The author, playing on a phone: <i>"the UI that pops up when clicking on the ground is way too
    /// small on mobile, let's make it 3 times larger"</i>. That is a claim about <b>device pixels</b>,
    /// and the existing shop tests are about hit-testing and ordering, so nothing measured it.
    /// <para>
    /// Two things have to hold together and they pull against each other: a row has to be big enough
    /// for a thumb, and the whole menu has to stay between the header and the Ready button. The
    /// second is not decoration — <c>PopupRows</c> carries a note about a popup that once put its
    /// last row over Ready, so buying the bottom item started the raid instead and the player lost
    /// the purchase, the remaining shop time, and any idea of what they had done wrong.
    /// </para>
    /// </remarks>
    public sealed class PopupSizeTests
    {
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
        /// A phone gets a menu row about three times the height it used to.
        /// </summary>
        /// <remarks>
        /// Stated on the upright phones, which is what the author was holding. The old floor was 26
        /// device pixels a row; the request was three times that.
        /// </remarks>
        [Test]
        public void OnAPhone_TheMenuRowIsThreeTimesWhatItWas()
        {
            var rows = new List<string>();
            float worst = float.MaxValue;

            foreach (Vector2Int size in new[] { new Vector2Int(390, 844), new Vector2Int(360, 780) })
            {
                float scale = ScaleFor(size);
                Rect[] popup = ShopScreen.PopupRows(
                    new Vector2(size.x * 0.5f, size.y * 0.5f), scale, size.x, size.y);

                float rowHeight = popup[0].height;
                float total = popup[^1].yMax - popup[0].y;
                rows.Add($"{size.x}x{size.y}: row {rowHeight:F0}px, menu {total:F0}px, width {popup[0].width:F0}px");
                worst = Mathf.Min(worst, rowHeight);
            }

            MooseRunnerFacade.Log("tile menu on a phone -- " + string.Join("  |  ", rows));

            Assert.GreaterOrEqual(worst, 70f,
                $"a menu row is {worst:F0} device pixels on a phone, well short of three times the "
                + "26 it used to be -- which is the size the author asked for after playing on one");
        }

        /// <summary>
        /// The menu never overlaps the Ready button, at any size the game ships to.
        /// </summary>
        /// <remarks>
        /// The cost of making it bigger, and the reason the geometry shrinks to fit on a short
        /// screen rather than simply tripling everywhere. A row drawn under Ready is a row that
        /// starts the raid when the player meant to buy something.
        /// </remarks>
        [Test]
        public void TheMenu_NeverReachesTheReadyButton()
        {
            var rows = new List<string>();
            float worstGap = float.MaxValue;
            Vector2Int worstAt = Screens[0];

            foreach (Vector2Int size in Screens)
            {
                float scale = ScaleFor(size);
                Rect ready = ShopScreen.ReadyRect(scale, size.x, size.y);

                // Every anchor a tap can produce, not just the middle: the menu is placed relative
                // to where the tile was, and the bottom of the board is where it can run out of room.
                for (int step = 0; step <= 10; step++)
                {
                    float y = size.y * (step / 10f);
                    Rect[] popup = ShopScreen.PopupRows(
                        new Vector2(size.x * 0.5f, y), scale, size.x, size.y);

                    float gap = ready.y - popup[^1].yMax;
                    if (gap < worstGap)
                    {
                        worstGap = gap;
                        worstAt = size;
                    }
                }

                rows.Add($"{size.x}x{size.y}: row {ShopScreen.PopupRows(new Vector2(size.x * 0.5f, size.y * 0.5f), scale, size.x, size.y)[0].height:F0}px");
            }

            MooseRunnerFacade.Log("row height by screen -- " + string.Join("  |  ", rows));
            MooseRunnerFacade.Log(
                $"tightest gap between the menu and Ready: {worstGap:F1}px at {worstAt.x}x{worstAt.y}");

            Assert.GreaterOrEqual(worstGap, 0f,
                $"the menu's last row reaches {-worstGap:F0}px into the Ready button at "
                + $"{worstAt.x}x{worstAt.y}, so buying the bottom item starts the raid instead");
        }

        /// <summary>
        /// The frame drawn behind the menu contains every row of it, at every size.
        /// </summary>
        /// <remarks>
        /// The fault this catches was introduced by the enlargement itself and found by reading the
        /// drawing code rather than by any assertion. <c>DrawPopup</c> recomputed the title strip
        /// with its own copy of the old formula while <c>PopupRows</c> used the new one, so on a
        /// phone the purple frame no longer lined up with the rows inside it. Harmless only while
        /// two copies of a formula agree, which is the arrangement this file's own remarks warn
        /// about — a control drawn in one place and clicked in another.
        /// </remarks>
        [Test]
        public void TheFrame_ContainsEveryRow()
        {
            foreach (Vector2Int size in Screens)
            {
                float scale = ScaleFor(size);
                var anchor = new Vector2(size.x * 0.5f, size.y * 0.4f);
                Rect frame = ShopScreen.PopupFrame(anchor, scale, size.x, size.y);
                Rect[] rows = ShopScreen.PopupRows(anchor, scale, size.x, size.y);

                Assert.LessOrEqual(frame.y, rows[0].y + 0.01f,
                    $"{size.x}x{size.y}: the frame starts below its first row, so the title strip "
                    + "is drawn over the menu");

                Assert.GreaterOrEqual(frame.yMax, rows[^1].yMax - 0.01f,
                    $"{size.x}x{size.y}: the frame ends {rows[^1].yMax - frame.yMax:F0}px above its "
                    + "last row, so the bottom item is drawn outside the box it belongs to");
            }

            MooseRunnerFacade.Log("the menu frame contains its rows at every shipped size");
        }

        /// <summary>
        /// A desktop screen is left exactly as it was.
        /// </summary>
        /// <remarks>
        /// The request was "larger on mobile", and the enlargement is applied to floors that only
        /// bind on small screens for precisely that reason. At 1280x720 the scale is 1, so the
        /// scaled sizes win and nothing moves. This is the test that says so.
        /// </remarks>
        [Test]
        public void OnADesktop_NothingChanged()
        {
            var size = new Vector2Int(1280, 720);
            Rect[] popup = ShopScreen.PopupRows(
                new Vector2(640f, 300f), ScaleFor(size), size.x, size.y);

            MooseRunnerFacade.Log(
                $"1280x720: row {popup[0].height:F0}px, width {popup[0].width:F0}px");

            Assert.AreEqual(30f, popup[0].height, 0.5f,
                "a desktop row is no longer the 30 pixels RowHeight asks for, so the mobile "
                + "enlargement has leaked onto screens that never needed it");
        }
    }
}
