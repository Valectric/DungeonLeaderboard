using System.Collections.Generic;
using System.Globalization;
using Dungeon.ShopManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws the thirty-second shop, and answers where the player just tapped.
    /// </summary>
    /// <remarks>
    /// The shop is spatial. The player looks at the dungeon they are about to send a party into and
    /// buys onto it: a marker past the last hall extends the corridor, and tapping any empty tile
    /// opens a small menu of things that can stand there. The old six-card grid sold counts and let
    /// the dungeon decide where they landed, so the player chose <i>what</i> and never <i>where</i> —
    /// which is the more interesting half of the decision and the half that makes the layout theirs.
    /// <para>
    /// Layout and hit-testing come from the same call in every case, so a control can never be drawn
    /// in one place and clicked in another.
    /// </para>
    /// <para>
    /// It deliberately does not use <c>GUI.Button</c>. Every verb in this game is read through
    /// <c>Mouse.current</c> and <c>Touchscreen.current</c>, because the project runs the Input System
    /// package and an earlier build shipped with all three verbs silently dead. The shop uses that
    /// same proven path rather than introducing a second input mechanism whose failure would look
    /// identical.
    /// </para>
    /// </remarks>
    public static class ShopScreen
    {
        /// <summary>The items that stand on a tile, in the order the popup lists them.</summary>
        public static readonly ShopItem[] Items = Shop.Placeable;

        private static readonly Color Ink = new(0.82f, 0.80f, 0.90f);
        private static readonly Color Dim = new(0.45f, 0.43f, 0.52f);
        private static readonly Color Gold = new(0.85f, 0.7f, 1f);
        private static readonly Color Green = new(0.55f, 1f, 0.45f);

        /// <summary>Human-readable name of an item.</summary>
        /// <param name="item">Item to name.</param>
        /// <returns>The name shown on its row.</returns>
        public static string NameOf(ShopItem item) => item switch
        {
            ShopItem.Slime => "SLIME PIT",
            ShopItem.Skeleton => "BONE PILE",
            ShopItem.SpikeTrap => "SPIKE TRAP",
            ShopItem.PoisonDart => "DART TRAP",
            ShopItem.Door => "DEEPER HALL",
            _ => "TREASURE CHEST"
        };

        /// <summary>One line saying what an item does to the next raid.</summary>
        /// <param name="item">Item to describe.</param>
        /// <returns>The description shown beside its name.</returns>
        public static string DescriptionOf(ShopItem item) => item switch
        {
            ShopItem.Slime => "a spawner. weak, cheap, buys seconds",
            ShopItem.Skeleton => "a spawner. tough. holds a party still",
            ShopItem.SpikeTrap => "one more plate to wound them on",
            ShopItem.PoisonDart => "another plate. traps are the wound curve",
            ShopItem.Door => "one more room, and one more door to shut",
            _ => "they detour to loot it. seconds are money"
        };

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
        /// <returns>One rectangle per entry of <see cref="Items"/>.</returns>
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
        /// <param name="rows">One rectangle per entry of <see cref="Items"/>.</param>
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

            float popupHeight = titleHeight + (rowHeight * Items.Length);

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
                popupHeight = titleHeight + (rowHeight * Items.Length);
            }

            // WHOLE PIXELS, and it is not only tidiness. Rows are laid out exactly adjacent -- each
            // one starts where the last ended -- and with a squeezed height like 55.1005 the
            // accumulated top of one row lands an ulp past the bottom of the one before it, so
            // Rect.Overlaps reports them as overlapping. ResolutionSweepTests caught precisely that
            // at 800x480, and it matters: overlapping rectangles mean a tap lands on whichever row
            // the hit test checks first, which the player experiences as buying the wrong thing.
            rowHeight = Mathf.Floor(rowHeight);
            titleHeight = Mathf.Floor(titleHeight);
            popupHeight = titleHeight + (rowHeight * Items.Length);

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
            rows = new Rect[Items.Length];
            for (int i = 0; i < Items.Length; i++)
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
        /// Draws the shop over the dungeon it is spending money on.
        /// </summary>
        /// <param name="shop">Shop being shown.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="hallAnchors">Every place a new hall could go, in GUI space.</param>
        /// <param name="hallPrice">What that hall costs.</param>
        /// <param name="popupAnchor">Where a tile popup is open, in GUI space, or null for none.</param>
        public static void Draw(Shop shop, float scale, IReadOnlyList<Vector2> hallAnchors,
            float hallPrice, Vector2? popupAnchor)
        {
            // Barely a tint. The dungeon underneath is the thing being shopped for, and the old
            // 86%-opaque panel hid it completely -- which was fine when the shop was a list of cards
            // and is the whole problem now.
            Color previous = GUI.color;
            GUI.color = new Color(0.06f, 0.05f, 0.09f, 0.34f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            DrawHeader(shop, scale);

            // Hall markers are hidden while a tile menu is open, because while it is open they
            // cannot be pressed: GameController.TapShop gives the menu the tap first and treats
            // anything outside it as a dismissal, deliberately, so no mis-tap costs energy.
            //
            // Drawing them anyway showed a control that does nothing, and the menu clipped the one
            // below it into reading "HALL 75" with its "+" and its left border underneath the panel.
            // A player cannot tell a disabled control from a half-drawn one, and the input model here
            // was already right -- it was only the picture that disagreed with it.
            if (hallAnchors != null && !popupAnchor.HasValue)
            {
                foreach (Vector2 anchor in hallAnchors)
                {
                    DrawHallMarker(shop, anchor, hallPrice, scale);
                }
            }

            if (popupAnchor.HasValue)
            {
                DrawPopup(shop, popupAnchor.Value, scale);
            }

            DrawReady(shop, ReadyRect(scale, Screen.width, Screen.height), scale);
        }

        /// <summary>
        /// Font size the Ready caption is drawn at: floored, then fitted to the button.
        /// </summary>
        /// <remarks>
        /// Must be called from inside <c>OnGUI</c> — it measures the real font. Public so
        /// <c>ShopLegibilityTests</c> can ask the game what it will draw rather than keeping a copy
        /// of the arithmetic, which is the arrangement the tile menu and the title line now use.
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="caption">The caption about to be drawn.</param>
        /// <param name="buttonWidth">Width of the button it goes in.</param>
        /// <returns>Font size in pixels.</returns>
        public static int ReadyFontSize(float scale, string caption, float buttonWidth)
        {
            int size = Mathf.Max(12, Mathf.RoundToInt(19 * scale));
            if (string.IsNullOrEmpty(caption))
            {
                return size;
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = FontStyle.Bold
            };

            float room = buttonWidth * 0.94f;
            float drawn = style.CalcSize(new GUIContent(caption)).x;
            return drawn <= room || drawn <= 0.01f
                ? size
                : Mathf.Max(9, Mathf.FloorToInt(size * (room / drawn)));
        }

        /// <summary>Draws the title, the countdown, the purse and the one-line instruction.</summary>
        private static void DrawHeader(Shop shop, float scale)
        {
            // See HeaderScale: the whole block scales together, so the three stacked lines cannot
            // collide with each other however small the screen gets.
            scale = HeaderScale(scale);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(24 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = Gold;
            GUI.Label(new Rect(0f, 8f * scale, Screen.width, 32f * scale),
                shop.Purse.ToString("0", CultureInfo.InvariantCulture) + " ENERGY TO SPEND", title);

            // The countdown is the pressure, and it turns red at the end, because a shop that quietly
            // closes is a shop the player will swear they were never given.
            var clock = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(38 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            clock.normal.textColor = shop.TimeRemaining <= 8f
                ? new Color(0.95f, 0.35f, 0.35f)
                : Ink;
            GUI.Label(new Rect(0f, 34f * scale, Screen.width, 48f * scale),
                Mathf.CeilToInt(shop.TimeRemaining).ToString(CultureInfo.InvariantCulture), clock);

            var hint = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            hint.normal.textColor = Dim;
            GUI.Label(new Rect(0f, 80f * scale, Screen.width, 22f * scale),
                // Zoom is worth a mention here and not during a raid. In the itch embed a five-room
                // corridor puts a tile at about sixteen pixels across, and the shop asks the player
                // to aim at one -- so the mitigation has to be discoverable, not merely present.
                "TAP ANY EMPTY TILE TO BUILD ON IT  /  SCROLL OR PINCH TO ZOOM IN", hint);
        }

        /// <summary>Draws the marker that buys another hall onto the end of the corridor.</summary>
        /// <remarks>
        /// Drawn where the hall would actually appear rather than in a menu, so the purchase reads as
        /// extending this dungeon rather than as incrementing a number.
        /// </remarks>
        private static void DrawHallMarker(Shop shop, Vector2 anchor, float price, float scale)
        {
            Rect rect = HallMarkerRect(anchor, scale, Screen.width, Screen.height);
            bool affordable = shop.IsOpen && shop.Purse >= price;

            Color was = GUI.color;
            GUI.color = affordable
                ? new Color(0.42f, 0.24f, 0.55f, 0.95f)
                : new Color(0.14f, 0.12f, 0.17f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float inset = Mathf.Max(1f, 2f * scale);
            GUI.color = new Color(0.045f, 0.035f, 0.085f, 0.96f);
            GUI.DrawTexture(
                new Rect(rect.x + inset, rect.y + inset,
                    rect.width - (inset * 2f), rect.height - (inset * 2f)),
                Texture2D.whiteTexture);
            GUI.color = was;

            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Max(11f, 15f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            label.normal.textColor = affordable ? Ink : new Color(0.36f, 0.34f, 0.42f);
            GUI.Label(new Rect(rect.x, rect.y + (7f * scale), rect.width, 22f * scale),
                "+ HALL", label);

            var cost = new GUIStyle(label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Max(13f, 18f * scale)),
                alignment = TextAnchor.LowerCenter
            };
            cost.normal.textColor = affordable ? Gold : new Color(0.42f, 0.24f, 0.28f);
            GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height - (8f * scale)),
                price.ToString("0", CultureInfo.InvariantCulture), cost);
        }

        /// <summary>Draws the menu of things that can stand on the tapped tile.</summary>
        private static void DrawPopup(Shop shop, Vector2 anchor, float scale)
        {
            Rect[] rows = PopupRows(anchor, scale, Screen.width, Screen.height);
            Rect frame = PopupFrame(anchor, scale, Screen.width, Screen.height);
            float titleHeight = rows[0].y - frame.y;

            Color was = GUI.color;
            GUI.color = new Color(0.42f, 0.24f, 0.55f, 0.98f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            float inset = Mathf.Max(1f, 2f * scale);
            GUI.color = new Color(0.035f, 0.028f, 0.06f, 0.99f);
            GUI.DrawTexture(new Rect(frame.x + inset, frame.y + inset,
                frame.width - (inset * 2f), frame.height - (inset * 2f)), Texture2D.whiteTexture);
            GUI.color = was;

            var heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Max(12f * scale, titleHeight * 0.46f)),
                fontStyle = FontStyle.Bold
            };
            heading.normal.textColor = Dim;
            float headingPad = Mathf.Max(8f * scale, titleHeight * 0.22f);
            GUI.Label(new Rect(frame.x + headingPad, frame.y, frame.width, titleHeight),
                "BUILD HERE", heading);

            for (int i = 0; i < rows.Length; i++)
            {
                DrawPopupRow(shop, Items[i], rows[i], scale);
            }
        }

        /// <summary>Draws one row of the tile popup.</summary>
        /// <remarks>
        /// Unaffordable rows are dimmed rather than hidden. A menu that changes length as the purse
        /// empties moves every other row out from under the player's finger.
        /// </remarks>
        /// <summary>
        /// The font size an item's name is drawn at, shrunk until it fits its half of the row.
        /// </summary>
        /// <remarks>
        /// <b>Must be called from inside <c>OnGUI</c></b> — it measures the real font, which is the
        /// point. Sizing the type from the row height alone assumes the row grew wider in the same
        /// proportion it grew taller, and it does not: the width is capped by the screen. Measured
        /// before this existed, a phone-sized menu drew "TREASURE CHEST" 219 pixels wider than the
        /// box it was given.
        /// <para>
        /// Public so <c>PopupTextFitTests</c> can ask the game what size it will use, rather than
        /// keeping its own copy of the arithmetic. A test carrying its own copy of a font size is
        /// exactly how the phone defects of 2026-08-16 got past the sweep meant to catch them.
        /// </para>
        /// </remarks>
        /// <param name="row">The row the name is drawn in.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="item">The item whose name is being drawn.</param>
        /// <returns>Font size in pixels.</returns>
        public static float NameFontSize(Rect row, float scale, ShopItem item)
        {
            float type = Mathf.Max(14f * scale, row.height * 0.42f);
            float pad = Mathf.Max(10f * scale, row.height * 0.13f);
            float nameBox = (row.width * 0.62f) - pad;

            var measure = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(type),
                fontStyle = FontStyle.Bold
            };

            // FLOORED, not rounded. The style draws at RoundToInt of whatever comes back, so a
            // shrink that lands on 30.6 is drawn at 31 and overflows the box it was shrunk to fit --
            // measured at exactly one pixel on "SPIKE TRAP" at 390x844, which is the kind of margin
            // that is invisible until it clips a letter.
            float wanted = measure.CalcSize(new GUIContent(NameOf(item))).x;
            return wanted > nameBox && wanted > 0.01f
                ? Mathf.Max(11f, Mathf.Floor(type * (nameBox / wanted)))
                : type;
        }

        private static void DrawPopupRow(Shop shop, ShopItem item, Rect row, float scale)
        {
            bool affordable = shop.CanAfford(item);

            // Sized from the ROW, not from the interface scale. Tripling the rows for mobile while
            // leaving the type on 14 * scale would have put 11-pixel text in a 78-pixel box -- a
            // bigger target with the same unreadable label, which is not what was asked for. The
            // scaled size still wins wherever it is larger, so a desktop is untouched.
            float type = Mathf.Max(14f * scale, row.height * 0.42f);
            float pad = Mathf.Max(10f * scale, row.height * 0.13f);

            // SHRINK TO FIT, measured. Sizing the type from the row height alone assumes the row got
            // wider in the same proportion it got taller, and it does not -- the width is capped by
            // the screen. Rather than tune a ratio until the longest name happens to fit, ask the
            // font: this is inside OnGUI, so CalcSize is available and exact, and it stays right if
            // an item is ever renamed to something longer.
            type = NameFontSize(row, scale, item);

            var name = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(type),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            name.normal.textColor = affordable ? Ink : new Color(0.34f, 0.32f, 0.40f);
            GUI.Label(new Rect(row.x + pad, row.y, row.width * 0.62f, row.height),
                NameOf(item), name);

            var price = new GUIStyle(name) { alignment = TextAnchor.MiddleRight };
            price.normal.textColor = affordable ? Gold : new Color(0.42f, 0.24f, 0.28f);
            GUI.Label(new Rect(row.x, row.y, row.width - pad, row.height),
                shop.Price(item).ToString("0", CultureInfo.InvariantCulture), price);
        }

        /// <summary>Draws the Ready button and the bonus it currently pays.</summary>
        private static void DrawReady(Shop shop, Rect ready, float scale)
        {
            Color was = GUI.color;
            GUI.color = new Color(0.18f, 0.42f, 0.20f, 0.95f);
            GUI.DrawTexture(ready, Texture2D.whiteTexture);
            GUI.color = was;

            string caption = "READY  -  OPEN THE DOORS NOW FOR +"
                             + shop.PendingBonus.ToString("0", CultureInfo.InvariantCulture)
                             + " STARTING ENERGY";

            // Floored so it is readable, then fitted so it stays inside the button. The caption is
            // fifty characters and the button is capped by the screen, so on a phone a floor alone
            // would push the words out past both ends of the thing they label.
            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = ReadyFontSize(scale, caption, ready.width),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            label.normal.textColor = Green;
            GUI.Label(ready, caption, label);
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
                    item = Items[i];
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
