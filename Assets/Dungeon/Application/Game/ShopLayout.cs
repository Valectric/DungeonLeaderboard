using Dungeon.ShopManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Where every control on the shop screen sits, and what a tap on one of them hit.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="ShopScreen"/> on 2026-08-17, which had reached 710 lines against this
    /// project's 400-line cap — and most of the growth was mine, from the mobile sizing work. The
    /// seam is the one the file was already organised around: this class answers <b>where</b>, and
    /// <c>ShopScreen</c> answers <b>what it looks like</b>.
    /// <para>
    /// Keeping layout and hit-testing together is deliberate and is the reason this is one class
    /// rather than two. <c>ShopScreen</c>'s own remark states the rule: layout and hit-testing come
    /// from the same call in every case, so a control can never be drawn in one place and clicked in
    /// another. Splitting them would be the one refactor that could reintroduce that fault.
    /// </para>
    /// </remarks>
    public static class ShopLayout
    {
        /// <summary>Height of one row in the tile popup, before scaling.</summary>
        private const float RowHeight = 30f;
        /// <summary>Width of the tile popup, before scaling.</summary>
        private const float PopupWidth = 250f;
        /// <summary>
        /// How much bigger the tile menu is on a small screen than it used to be.
        /// </summary>
        /// <remarks>
        /// The author, playing on a phone: <i>"the UI that pops up when clicking on the ground is
        /// way too small on mobile, let's make it 3 times larger"</i>.
        /// <para>
        /// <b>Applied to the screens the old floors were already rescuing, and to no others.</b>
        /// The obvious reading — multiply the three floors below by three — does not do that, and
        /// was tried: 26 times three is 78, which is above the 30 a 1280x720 desktop computes from
        /// its own scale, so the floor starts binding where it never bound and the menu grows
        /// everywhere. Measured that way it took a desktop row from 30 pixels to 78 and the whole
        /// menu to 72% of the window, which is not what "too small on mobile" asks for.
        /// </para>
        /// <para>
        /// So <c>PopupRows</c> asks whether the old floor was doing the work: if the scaled size
        /// falls under it this is a small screen and the enlargement applies, and if not the scaled
        /// size stands. Measured after: 78 pixels a row on both phones, <b>30 unchanged at
        /// 1280x720</b>, 45 unchanged at 1920x1080, and short screens squeezed back to fit rather
        /// than overflowing onto the Ready button.
        /// </para>
        /// </remarks>
        private const float MobileEnlargement = 3f;
        /// <summary>The row height a small screen used before the author asked for three times.</summary>
        private const float OldMinimumRowHeight = 26f;
        /// <summary>The title height a small screen used before the author asked for three times.</summary>
        private const float OldMinimumTitleHeight = 18f;
        /// <summary>The width a small screen used before the author asked for three times.</summary>
        private const float OldMinimumPopupWidth = 180f;
        /// <summary>
        /// The Ready button's rectangle, in GUI space.
        /// </summary>
        /// <param name="scale">UI scale.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>Where Ready is drawn and clicked.</returns>
        /// <summary>
        /// The first row below the shop's own header, in GUI space.
        /// </summary>
        /// <remarks>
        /// The header is the purse, the countdown and the one-line instruction, drawn from 8 to
        /// about 102 scaled pixels. Anything anchored to the dungeon has to stay under it: the
        /// dungeon moves with the camera and the header does not, so without a floor here a control
        /// lands on the number telling the player how much they have to spend.
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <returns>The lowest safe top edge for anything drawn over the board.</returns>
        public static float HeaderBottom(float scale) => 106f * HeaderScale(scale);
        /// <summary>
        /// Smallest scale the shop's header lays itself out at.
        /// </summary>
        /// <remarks>
        /// The header is the purse, the countdown and the one-line instruction, and two of the three
        /// had no floor: on a 360x780 phone the countdown drew at <b>eleven pixels</b> and the
        /// instruction at <b>four</b>. The comment on the countdown calls it "the pressure, and it
        /// turns red at the end, because a shop that quietly closes is a shop the player will swear
        /// they were never given" — which it cannot be at eleven pixels.
        /// <para>
        /// Floored on the scale for the same reason as the HUD and the review screen: the three
        /// lines are stacked at offsets derived from it, so flooring the type alone would pile them
        /// on top of each other. <see cref="HeaderBottom"/> is derived from the same value, so the
        /// tile menu and the hall markers move down with the header they must keep clear of, rather
        /// than being left overlapping it.
        /// </para>
        /// </remarks>
        /// <para>
        /// 0.7 rather than the HUD's 0.6, because the header's smallest line is a 13-pixel nominal
        /// and 0.6 leaves it at eight — a pixel under the nine the rest of the interface floors at,
        /// and the whole point of this sweep was to stop screens each inventing their own minimum.
        /// The header sits above the board rather than over it, so the extra ten pixels cost a strip
        /// of background rather than a strip of dungeon.
        /// </para>
        /// <param name="scale">UI scale.</param>
        /// <returns>The scale the header block uses.</returns>
        public static float HeaderScale(float scale) => Mathf.Max(scale, 0.7f);
        /// <summary>
        /// The Ready button's rectangle, in GUI space.
        /// </summary>
        /// <remarks>
        /// <b>Floored in absolute pixels, like the tile menu it sits under.</b> Unfloored, a 360x780
        /// phone drew the button that starts the raid at <b>174 by 14 pixels</b> with a five-pixel
        /// label on it — the primary action of the whole shop, smaller than a fingertip and
        /// unreadable. Every other control on this screen had been given a floor at some point; this
        /// one had not, and nothing measured it because the tests here ask where it is rather than
        /// how big.
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>Where Ready is drawn and clicked.</returns>
        public static Rect ReadyRect(float scale, float width, float height)
        {
            float buttonWidth = Mathf.Min(width * 0.9f, Mathf.Max(320f, 620f * scale));
            float buttonHeight = Mathf.Max(34f, 50f * scale);
            return new Rect(
                (width - buttonWidth) * 0.5f,
                height - buttonHeight - Mathf.Max(10f, 16f * scale),
                buttonWidth,
                buttonHeight);
        }
        /// <summary>
        /// The rows of the popup opened on a tile, in GUI space.
        /// </summary>
        /// <remarks>
        /// Anchored to the tapped point but clamped inside the canvas, because a tile near the right
        /// edge would otherwise open a menu half off screen — and the itch.io embed is far narrower
        /// than the editor, so "it fits here" proves nothing about where it ships.
        /// <para>
        /// It is also kept clear of the Ready button, which is checked first by the hit test. A menu
        /// opened on a low tile put its last row straight over Ready, so buying the bottom item
        /// started the raid instead — the player loses the purchase, the remaining shop time, and any
        /// idea of what they did wrong. Found by a test tapping every row rather than by reading the
        /// arithmetic.
        /// </para>
        /// </remarks>
        /// <param name="anchor">Where on screen the tile was tapped, in GUI space.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>One rectangle per entry of <see cref="ShopScreen.Items"/>.</returns>
        public static Rect[] PopupRows(Vector2 anchor, float scale, float width, float height)
        {
            Geometry(anchor, scale, width, height, out Rect[] laidOut, out _);
            return laidOut;
        }
        /// <summary>
        /// The purple frame drawn behind the tile menu, title strip included.
        /// </summary>
        /// <remarks>
        /// <b>Derived from the same call as the rows, and that is the point.</b> The drawing code
        /// used to recompute the title strip with its own copy of the formula, which was harmless
        /// only while the two copies agreed. Enlarging the menu for mobile changed one of them, and
        /// the frame stopped lining up with the rows it contains -- the same class of fault this
        /// file's own remarks warn about, where a control is drawn in one place and clicked in
        /// another.
        /// </remarks>
        /// <param name="anchor">Where on screen the tile was tapped, in GUI space.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>The frame enclosing the title strip and every row.</returns>
        public static Rect PopupFrame(Vector2 anchor, float scale, float width, float height)
        {
            Geometry(anchor, scale, width, height, out Rect[] laidOut, out float title);
            return new Rect(
                laidOut[0].x,
                laidOut[0].y - title,
                laidOut[0].width,
                title + (laidOut.Length * laidOut[0].height));
        }
        /// <summary>Lays the menu out once, for both the rows and the frame around them.</summary>
        /// <param name="anchor">Where on screen the tile was tapped, in GUI space.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <param name="rows">One rectangle per entry of <see cref="ShopScreen.Items"/>.</param>
        /// <param name="titleStrip">Height of the strip above the first row.</param>
        private static void Geometry(
            Vector2 anchor, float scale, float width, float height,
            out Rect[] rows, out float titleStrip)
        {
            // Floored in absolute pixels, not just scaled. The itch.io embed is 523x293, which puts
            // the scale at 0.4 and would give 12-pixel rows -- on screen, drawn correctly, and far
            // too small for a thumb or for the text to be read. A control that cannot be hit is not
            // a control.
            // Enlarged on exactly the screens that needed a floor in the first place, and nowhere
            // else. Multiplying the floors directly does NOT do that: three times 26 is 78, which is
            // above the 30 a 1280x720 desktop computes from its own scale, so the floor starts
            // binding on screens it never bound on and the menu grows everywhere. Measured that way
            // it took a desktop row from 30 pixels to 78 and the whole menu to 72% of the window.
            //
            // So the test is whether the OLD floor was doing the work. If it was, this is a small
            // screen and the author's three times applies; if it was not, the scaled size stands.
            // ONE decision for the whole menu, not three. Asking the question per dimension lets
            // them disagree: at 1024x768 the row height fell under its floor and tripled while the
            // width did not, so 33-pixel type landed in a 114-pixel name box and "TREASURE CHEST"
            // overflowed by 219 pixels. A menu is one object and grows as one.
            bool smallScreen = RowHeight * scale < OldMinimumRowHeight;

            float popupWidth = smallScreen
                ? OldMinimumPopupWidth * MobileEnlargement
                : PopupWidth * scale;
            popupWidth = Mathf.Min(width * 0.88f, popupWidth);

            float rowHeight = smallScreen
                ? OldMinimumRowHeight * MobileEnlargement
                : RowHeight * scale;

            float titleHeight = smallScreen
                ? OldMinimumTitleHeight * MobileEnlargement
                : 24f * scale;

            float popupHeight = titleHeight + (rowHeight * ShopScreen.Items.Length);

            // Shrink to fit rather than overflow. Tripling the floors is what makes the menu usable
            // on a phone, and on a SHORT screen -- the 523x293 itch embed, or a phone held
            // sideways -- three times 26 pixels a row does not fit between the header and Ready.
            // Overflowing there would push rows under the Ready button, which is the exact fault
            // the note above records: buying the bottom item would start the raid instead.
            float band = ReadyRect(scale, width, height).y - (6f * scale) - HeaderBottom(scale);
            if (popupHeight > band && band > 0f)
            {
                float squeeze = band / popupHeight;
                rowHeight = Mathf.Max(OldMinimumRowHeight, rowHeight * squeeze);
                titleHeight = Mathf.Max(OldMinimumTitleHeight, titleHeight * squeeze);
                popupHeight = titleHeight + (rowHeight * ShopScreen.Items.Length);
            }

            // WHOLE PIXELS, and it is not only tidiness. Rows are laid out exactly adjacent -- each
            // one starts where the last ended -- and with a squeezed height like 55.1005 the
            // accumulated top of one row lands an ulp past the bottom of the one before it, so
            // Rect.Overlaps reports them as overlapping. ResolutionSweepTests caught precisely that
            // at 800x480, and it matters: overlapping rectangles mean a tap lands on whichever row
            // the hit test checks first, which the player experiences as buying the wrong thing.
            rowHeight = Mathf.Floor(rowHeight);
            titleHeight = Mathf.Floor(titleHeight);
            popupHeight = titleHeight + (rowHeight * ShopScreen.Items.Length);

            float left = Mathf.Clamp(anchor.x - (popupWidth * 0.5f), 4f, width - popupWidth - 4f);
            float floor = Mathf.Min(
                height - popupHeight - 4f,
                ReadyRect(scale, width, height).y - popupHeight - (6f * scale));
            // Under the header for the same reason the hall markers are: a tile near the top of the
            // board would otherwise open its menu across the purse and the countdown.
            //
            // CEILED, because the rows below are pinned to whole pixels. The frame's top is derived
            // back from the first row -- Floor(top + titleHeight) - titleHeight -- so against a
            // fractional ceiling that rounding lands the frame a fraction ABOVE the line it was
            // clamped to. Measured at 1024x768, where the header bottom is 84.8: the menu was drawn
            // one pixel into it. Ceiling the clamp costs at most a pixel of space and cannot round
            // the wrong way.
            float ceiling = Mathf.Ceil(HeaderBottom(scale));
            float top = Mathf.Clamp(anchor.y + (14f * scale), ceiling, Mathf.Max(ceiling, floor));

            float first = Mathf.Floor(top + titleHeight);
            rows = new Rect[ShopScreen.Items.Length];
            for (int i = 0; i < ShopScreen.Items.Length; i++)
            {
                rows[i] = new Rect(left, first + (i * rowHeight), popupWidth, rowHeight);
            }

            titleStrip = titleHeight;
        }
        /// <summary>
        /// The marker that buys the next hall, in GUI space.
        /// </summary>
        /// <param name="anchor">Screen point just past the end of the corridor, in GUI space.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>Where the marker is drawn and clicked.</returns>
        public static Rect HallMarkerRect(Vector2 anchor, float scale, float width, float height)
        {
            float markerWidth = Mathf.Max(96f, 132f * scale);
            float markerHeight = Mathf.Max(44f, 60f * scale);

            // Held clear of Ready for the same reason the menu is: Ready is hit-tested first, so
            // anything overlapping it is not merely ugly, it is unreachable.
            float floor = Mathf.Min(
                height - markerHeight - 2f,
                ReadyRect(scale, width, height).y - markerHeight - (6f * scale));

            // And held clear of the header, which was not a problem while the dungeon was three
            // rooms wide: a marker offered ABOVE the dungeon clamped to the top of the canvas and
            // sat across the purse and the countdown. A single room has a free side in all four
            // directions, so the upward one is now offered every time a run starts.
            float ceiling = HeaderBottom(scale);

            return new Rect(
                Mathf.Clamp(anchor.x - (markerWidth * 0.5f), 8f, width - markerWidth - 8f),
                Mathf.Clamp(anchor.y - (markerHeight * 0.5f), ceiling,
                    Mathf.Max(ceiling, floor)),
                markerWidth, markerHeight);
        }
        /// <summary>
        /// Works out which popup row a tap landed on.
        /// </summary>
        /// <param name="screenPosition">
        /// Tap position in input space, whose origin is the <b>bottom</b> left. GUI rectangles are
        /// measured from the top, so it is flipped here rather than at every call site.
        /// </param>
        /// <param name="anchor">Where the popup is anchored, in GUI space.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="item">Receives the item tapped, when one was.</param>
        /// <returns>True for an item row, false otherwise.</returns>
        public static bool TryHitPopup(
            Vector2 screenPosition, Vector2 anchor, float scale, out ShopItem item)
        {
            item = default;
            var point = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Rect[] rows = PopupRows(anchor, scale, Screen.width, Screen.height);

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Contains(point))
                {
                    item = ShopScreen.Items[i];
                    return true;
                }
            }

            return false;
        }
        /// <summary>Whether a tap landed on the marker that buys another hall.</summary>
        /// <param name="screenPosition">Tap position in input space.</param>
        /// <param name="anchor">Where the marker is anchored, in GUI space.</param>
        /// <param name="scale">UI scale.</param>
        /// <returns>True when the hall marker was pressed.</returns>
        public static bool HitHallMarker(Vector2 screenPosition, Vector2 anchor, float scale)
        {
            return HallMarkerRect(anchor, scale, Screen.width, Screen.height)
                .Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }
        /// <summary>Whether a tap landed on the Ready button.</summary>
        /// <param name="screenPosition">Tap position in input space.</param>
        /// <param name="scale">UI scale.</param>
        /// <returns>True when Ready was pressed.</returns>
        public static bool HitReady(Vector2 screenPosition, float scale)
        {
            return ReadyRect(scale, Screen.width, Screen.height)
                .Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }
    }
}
