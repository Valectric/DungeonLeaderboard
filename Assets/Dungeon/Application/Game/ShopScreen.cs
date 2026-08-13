using System.Globalization;
using Dungeon.ShopManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws the thirty-second shop, and answers where the player just tapped.
    /// </summary>
    /// <remarks>
    /// Layout and hit-testing come from the same <see cref="Cards"/> call, so a card can never be
    /// drawn in one place and clicked in another.
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
        /// <summary>The six items, in the order they are shown.</summary>
        public static readonly ShopItem[] Items =
        {
            ShopItem.Slime, ShopItem.Skeleton, ShopItem.SpikeTrap,
            ShopItem.PoisonDart, ShopItem.Door, ShopItem.Chest
        };

        private static readonly Color Ink = new(0.82f, 0.80f, 0.90f);
        private static readonly Color Dim = new(0.45f, 0.43f, 0.52f);
        private static readonly Color Gold = new(0.85f, 0.7f, 1f);
        private static readonly Color Green = new(0.55f, 1f, 0.45f);

        /// <summary>Human-readable name of an item.</summary>
        /// <param name="item">Item to name.</param>
        /// <returns>The name shown on its card.</returns>
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
        /// <returns>The description shown on its card.</returns>
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
        /// Computes the card rectangles, in GUI space.
        /// </summary>
        /// <param name="scale">UI scale.</param>
        /// <param name="ready">Receives the Ready button's rectangle.</param>
        /// <returns>One rectangle per entry of <see cref="Items"/>.</returns>
        public static Rect[] Cards(float scale, out Rect ready)
        {
            float panelWidth = Mathf.Min(Screen.width * 0.94f, 760f * scale);
            float left = (Screen.width - panelWidth) * 0.5f;
            float gap = 10f * scale;
            float cardWidth = (panelWidth - (gap * 2f)) / 3f;
            float cardHeight = 96f * scale;
            float top = Mathf.Max(96f * scale, (Screen.height - (cardHeight * 2.9f)) * 0.5f);

            var rects = new Rect[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                int column = i % 3;
                int row = i / 3;
                rects[i] = new Rect(
                    left + (column * (cardWidth + gap)),
                    top + (row * (cardHeight + gap)),
                    cardWidth, cardHeight);
            }

            ready = new Rect(left, top + ((cardHeight + gap) * 2f), panelWidth, 54f * scale);
            return rects;
        }

        /// <summary>Draws the whole shop over the dungeon.</summary>
        /// <param name="shop">Shop being shown.</param>
        /// <param name="scale">UI scale.</param>
        public static void Draw(Shop shop, float scale)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.06f, 0.05f, 0.09f, 0.86f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = Gold;
            GUI.Label(new Rect(0f, 16f * scale, Screen.width, 40f * scale), "SPEND IT", title);

            // The countdown is the pressure. It sits under the title at a size that cannot be missed,
            // and turns red at the end, because a shop that quietly closes is a shop the player will
            // swear they were never given.
            var clock = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(40 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            clock.normal.textColor = shop.TimeRemaining <= 8f
                ? new Color(0.95f, 0.35f, 0.35f)
                : Ink;
            GUI.Label(new Rect(0f, 46f * scale, Screen.width, 52f * scale),
                Mathf.CeilToInt(shop.TimeRemaining).ToString(CultureInfo.InvariantCulture), clock);

            Rect[] cards = Cards(scale, out Rect ready);
            for (int i = 0; i < cards.Length; i++)
            {
                DrawCard(shop, Items[i], cards[i], scale);
            }

            DrawReady(shop, ready, scale);

            var purse = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            purse.normal.textColor = Gold;
            GUI.Label(new Rect(0f, ready.yMax + (10f * scale), Screen.width, 30f * scale),
                shop.Purse.ToString("0", CultureInfo.InvariantCulture) + " ENERGY TO SPEND", purse);

            var footer = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            footer.normal.textColor = Dim;
            GUI.Label(new Rect(0f, ready.yMax + (36f * scale), Screen.width, 26f * scale),
                "WHAT YOU DO NOT SPEND IS LOST WHEN THE PARTY ARRIVES", footer);
        }

        /// <summary>Draws one item card.</summary>
        /// <remarks>
        /// The colours here were set by looking at a WebGL build, not by picking values that read
        /// well in a swatch. The first pass drew the description in the same grey the standings use
        /// and it was <b>completely invisible</b> on every affordable card: those cards rendered far
        /// lighter than the numbers suggested, so grey-on-dark became grey-on-grey. The cards are
        /// near-black now and the description is bright, which is legible whichever way the
        /// compositing goes.
        /// </remarks>
        private static void DrawCard(Shop shop, ShopItem item, Rect card, float scale)
        {
            bool affordable = shop.CanAfford(item);
            int owned = shop.Loadout.Count(item);

            Color was = GUI.color;

            // An outline, drawn as a slightly larger rectangle behind the fill. Affordable cards get
            // a violet edge so they read as pressable rather than as coloured panels.
            GUI.color = affordable
                ? new Color(0.42f, 0.24f, 0.55f, 1f)
                : new Color(0.10f, 0.09f, 0.13f, 1f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);

            float inset = Mathf.Max(1f, 2f * scale);
            GUI.color = affordable
                ? new Color(0.045f, 0.035f, 0.085f, 1f)
                : new Color(0.03f, 0.028f, 0.04f, 1f);
            GUI.DrawTexture(
                new Rect(card.x + inset, card.y + inset,
                    card.width - (inset * 2f), card.height - (inset * 2f)),
                Texture2D.whiteTexture);
            GUI.color = was;

            var name = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16 * scale),
                fontStyle = FontStyle.Bold
            };
            name.normal.textColor = affordable ? Ink : new Color(0.34f, 0.32f, 0.40f);
            GUI.Label(new Rect(card.x + (10f * scale), card.y + (8f * scale),
                card.width - (20f * scale), 24f * scale), NameOf(item), name);

            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(11 * scale),
                wordWrap = true
            };
            body.normal.textColor = affordable
                ? new Color(0.68f, 0.64f, 0.78f)
                : new Color(0.28f, 0.27f, 0.33f);
            GUI.Label(new Rect(card.x + (10f * scale), card.y + (30f * scale),
                card.width - (20f * scale), 36f * scale), DescriptionOf(item), body);

            var price = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(17 * scale),
                fontStyle = FontStyle.Bold
            };
            price.normal.textColor = affordable ? Gold : new Color(0.42f, 0.24f, 0.28f);
            GUI.Label(new Rect(card.x + (10f * scale), card.yMax - (28f * scale),
                    card.width - (20f * scale), 24f * scale),
                shop.Price(item).ToString("0", CultureInfo.InvariantCulture), price);

            if (owned > 0)
            {
                var count = new GUIStyle(price) { alignment = TextAnchor.MiddleRight };
                count.normal.textColor = Green;
                GUI.Label(new Rect(card.x, card.yMax - (28f * scale),
                    card.width - (10f * scale), 24f * scale), "x" + owned, count);
            }
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
                fontSize = Mathf.RoundToInt(20 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            label.normal.textColor = Green;
            GUI.Label(ready, "READY  -  OPEN THE DOORS NOW FOR +"
                             + shop.PendingBonus.ToString("0", CultureInfo.InvariantCulture)
                             + " STARTING ENERGY", label);
        }

        /// <summary>
        /// Works out what a tap landed on.
        /// </summary>
        /// <param name="screenPosition">
        /// Tap position in input space, whose origin is the <b>bottom</b> left. GUI rectangles are
        /// measured from the top, so it is flipped here rather than at every call site.
        /// </param>
        /// <param name="scale">UI scale.</param>
        /// <param name="item">Receives the item tapped, when one was.</param>
        /// <returns>True for an item, false otherwise; check <paramref name="item"/> only when true.</returns>
        public static bool TryHitItem(Vector2 screenPosition, float scale, out ShopItem item)
        {
            item = default;
            var point = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Rect[] cards = Cards(scale, out _);

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].Contains(point))
                {
                    item = Items[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a tap landed on the Ready button.</summary>
        /// <param name="screenPosition">Tap position in input space.</param>
        /// <param name="scale">UI scale.</param>
        /// <returns>True when Ready was pressed.</returns>
        public static bool HitReady(Vector2 screenPosition, float scale)
        {
            Cards(scale, out Rect ready);
            return ready.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }
    }
}
