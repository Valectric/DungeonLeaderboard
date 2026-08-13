using System.Globalization;
using Dungeon.RaidManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Floats damage and healing numbers off the fight.
    /// </summary>
    /// <remarks>
    /// Red for damage, green for healing, so a glance at the room tells the player whether their
    /// monster is landing hits or the healer is undoing them. Without this the only evidence of
    /// combat was two bars slowly changing length, which reads as nothing happening.
    /// <para>
    /// Drawn in immediate mode and projected from world space, rather than as world-space text. That
    /// is the same reason the rest of the UI is IMGUI: it needs no font asset, so it cannot fail in a
    /// WebGL build the way a missing dynamic font silently can.
    /// </para>
    /// <para>
    /// These are <b>deltas, never totals</b>. SPEC.md's rule is that adventurer HP is never shown as
    /// a number, and it is kept: "12" tells the player what just happened, not how much is left, so
    /// the ambiguity between "nearly dead" and "dead in one hit" survives intact.
    /// </para>
    /// </remarks>
    public static class CombatNumbers
    {
        /// <summary>How far a number travels upward over its life, in world units.</summary>
        private const float Rise = 1.35f;

        /// <summary>How far a number fans sideways as it rises, on top of its lane offset.</summary>
        /// <remarks>
        /// A hint of separation, not a scatter. Combined with the lane offsets the total spread stays
        /// within about one number's width either side of whatever is bleeding, so a hit always
        /// clearly belongs to the thing it came off.
        /// </remarks>
        private const float Drift = 0.12f;

        /// <summary>How much larger a number is at the instant it appears.</summary>
        /// <remarks>
        /// The punch is what catches the eye. A number that simply fades in at its final size is easy
        /// to miss entirely in a busy room, which was the whole complaint.
        /// </remarks>
        private const float PopScale = 1.7f;

        /// <summary>Fraction of a number's life spent shrinking back from the pop.</summary>
        private const float PopFraction = 0.22f;

        /// <summary>
        /// An adventurer bleeding. Red, matching the party's own health bar as it empties.
        /// </summary>
        /// <remarks>
        /// The rule the player learns without being told: <b>a number is the colour of the bar it is
        /// draining.</b> Nothing has to be memorised, and there is never a moment of working out
        /// whose damage this was.
        /// </remarks>
        private static readonly Color AdventurerHurt = new(1f, 0.32f, 0.30f);

        /// <summary>A monster bleeding. Violet, exactly the colour of its own health bar.</summary>
        private static readonly Color MonsterHurt = new(0.78f, 0.42f, 0.95f);

        /// <summary>Healing, which only ever happens to adventurers.</summary>
        private static readonly Color Healing = new(0.45f, 1f, 0.45f);

        /// <summary>
        /// Draws every floating number.
        /// </summary>
        /// <param name="feed">Numbers to draw.</param>
        /// <param name="camera">Camera to project through.</param>
        /// <param name="scale">UI scale.</param>
        public static void Draw(CombatFeed feed, Camera camera, float scale)
        {
            if (feed == null || camera == null)
            {
                return;
            }

            foreach (CombatNumber number in feed.Numbers)
            {
                float life = Mathf.Clamp01(number.Age / CombatFeed.Lifetime);

                // Leaps, then hangs. An ease-out rise reads as something being knocked loose; a
                // linear drift reads as a label sliding, which is what this looked like before.
                float climb = 1f - ((1f - life) * (1f - life));

                // Fans outward as it goes, so a burst of hits opens into a spray instead of a stack.
                float sideways = number.Spread * (1f + (Drift * life));

                var world = new Vector3(
                    (number.Origin.x + sideways) * DungeonView.CellSize,
                    (number.Origin.y * DungeonView.CellSize) + 0.4f + (Rise * climb),
                    0f);

                Vector3 point = camera.WorldToScreenPoint(world);
                if (point.z < 0f)
                {
                    continue;
                }

                // A number scales with the camera, so it stays the same size on the dungeon whatever
                // the player has zoomed to. Fixed-size text would be a shout when zoomed in and
                // unreadable when zoomed out.
                // Sized against the sprites rather than the screen. At 0.42 the numbers were taller
                // than the adventurers they were coming off.
                float zoom = Screen.height / (camera.orthographicSize * 2f);
                float baseSize = zoom * 0.30f;

                // A big hit is a bigger number. Free information: the player learns which of their
                // verbs actually hurt without reading anything.
                baseSize *= 1f + Mathf.Clamp01(number.Amount / 60f) * 0.45f;

                // The pop: oversized for the first fifth of its life, settling to normal. This is
                // what makes a hit catch the eye in a busy room.
                float pop = life < PopFraction
                    ? Mathf.Lerp(PopScale, 1f, life / PopFraction)
                    : 1f;

                int size = Mathf.Clamp(Mathf.RoundToInt(baseSize * pop), 9, 44);

                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                // Fades only at the end, so the number is fully legible for most of its life. Fading
                // from the first frame makes fast, small hits almost invisible.
                Color ink = number.IsHeal ? Healing
                    : number.Target == CombatTarget.Monster ? MonsterHurt
                    : AdventurerHurt;
                ink.a = 1f - Mathf.Clamp01((life - 0.65f) / 0.35f);
                style.normal.textColor = ink;

                // Colour carries the meaning, but never colour alone: a monster's number is italic
                // so the two stay apart for a colour-blind player, and on a frame where a violet
                // number happens to sit over a violet crystal. Both read as plain minus figures --
                // brackets were tried and looked like an accounting statement rather than a wound.
                if (number.Target == CombatTarget.Monster && !number.IsHeal)
                {
                    style.fontStyle = FontStyle.BoldAndItalic;
                }

                string text = (number.IsHeal ? "+" : "-")
                              + number.Amount.ToString(CultureInfo.InvariantCulture);

                // GUI space measures from the top, input and projection from the bottom.
                var rect = new Rect(point.x - (60f * scale), Screen.height - point.y - (14f * scale),
                    120f * scale, 28f * scale);

                // A dark backing copy one pixel down, so a red number stays readable over a red
                // torch and a green one over the floor.
                Color shadow = new(0f, 0f, 0f, ink.a * 0.75f);
                var shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = shadow;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height),
                    text, shadowStyle);

                GUI.Label(rect, text, style);
            }
        }
    }
}
