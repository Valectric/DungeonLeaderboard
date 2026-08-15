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
        public static float HeaderBottom(float scale) => Mathf.Max(24f, 106f * scale);

        public static Rect ReadyRect(float scale, float width, float height)
        {
            float buttonWidth = Mathf.Min(width * 0.9f, 620f * scale);
            return new Rect(
                (width - buttonWidth) * 0.5f,
                height - (66f * scale),
                buttonWidth,
                50f * scale);
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
            // Floored in absolute pixels, not just scaled. The itch.io embed is 523x293, which puts
            // the scale at 0.4 and would give 12-pixel rows -- on screen, drawn correctly, and far
            // too small for a thumb or for the text to be read. A control that cannot be hit is not
            // a control.
            float popupWidth = Mathf.Min(width * 0.88f, Mathf.Max(180f, PopupWidth * scale));
            float rowHeight = Mathf.Max(26f, RowHeight * scale);
            float titleHeight = Mathf.Max(18f, 24f * scale);
            float popupHeight = titleHeight + (rowHeight * Items.Length);

            float left = Mathf.Clamp(anchor.x - (popupWidth * 0.5f), 4f, width - popupWidth - 4f);
            float floor = Mathf.Min(
                height - popupHeight - 4f,
                ReadyRect(scale, width, height).y - popupHeight - (6f * scale));
            // Under the header for the same reason the hall markers are: a tile near the top of the
            // board would otherwise open its menu across the purse and the countdown.
            float ceiling = HeaderBottom(scale);
            float top = Mathf.Clamp(anchor.y + (14f * scale), ceiling, Mathf.Max(ceiling, floor));

            var rows = new Rect[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                rows[i] = new Rect(left, top + titleHeight + (i * rowHeight), popupWidth, rowHeight);
            }

            return rows;
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

        /// <summary>Draws the title, the countdown, the purse and the one-line instruction.</summary>
        private static void DrawHeader(Shop shop, float scale)
        {
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
            float titleHeight = Mathf.Max(18f, 24f * scale);
            var frame = new Rect(rows[0].x, rows[0].y - titleHeight, rows[0].width,
                titleHeight + (rows.Length * rows[0].height));

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
                fontSize = Mathf.RoundToInt(Mathf.Max(10f, 12f * scale)),
                fontStyle = FontStyle.Bold
            };
            heading.normal.textColor = Dim;
            GUI.Label(new Rect(frame.x + (8f * scale), frame.y + (4f * scale),
                frame.width, titleHeight), "BUILD HERE", heading);

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
        private static void DrawPopupRow(Shop shop, ShopItem item, Rect row, float scale)
        {
            bool affordable = shop.CanAfford(item);

            var name = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Max(11f, 14f * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            name.normal.textColor = affordable ? Ink : new Color(0.34f, 0.32f, 0.40f);
            GUI.Label(new Rect(row.x + (10f * scale), row.y, row.width * 0.62f, row.height),
                NameOf(item), name);

            var price = new GUIStyle(name) { alignment = TextAnchor.MiddleRight };
            price.normal.textColor = affordable ? Gold : new Color(0.42f, 0.24f, 0.28f);
            GUI.Label(new Rect(row.x, row.y, row.width - (10f * scale), row.height),
                shop.Price(item).ToString("0", CultureInfo.InvariantCulture), price);
        }

        /// <summary>Draws the Ready button and the bonus it currently pays.</summary>
        private static void DrawReady(Shop shop, Rect ready, float scale)
        {
            Color was = GUI.color;
            GUI.color = new Color(0.18f, 0.42f, 0.20f, 0.95f);
            GUI.DrawTexture(ready, Texture2D.whiteTexture);
            GUI.color = was;

            var label = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(19 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            label.normal.textColor = Green;
            GUI.Label(ready, "READY  -  OPEN THE DOORS NOW FOR +"
                             + shop.PendingBonus.ToString("0", CultureInfo.InvariantCulture)
                             + " STARTING ENERGY", label);
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
