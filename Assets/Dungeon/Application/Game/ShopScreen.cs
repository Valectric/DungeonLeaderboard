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

            DrawReady(shop, ShopLayout.ReadyRect(scale, Screen.width, Screen.height), scale);
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
            scale = ShopLayout.HeaderScale(scale);

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
            Rect rect = ShopLayout.HallMarkerRect(anchor, scale, Screen.width, Screen.height);
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
            Rect[] rows = ShopLayout.PopupRows(anchor, scale, Screen.width, Screen.height);
            Rect frame = ShopLayout.PopupFrame(anchor, scale, Screen.width, Screen.height);
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



    }
}
