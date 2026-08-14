using System.Globalization;
using Dungeon.RaidManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws the adventurers' review of the raid they just survived.
    /// </summary>
    /// <remarks>
    /// Shown between the raid and the standings, because this is where a new player learns the game.
    /// The standings tell them <i>how much</i> they scored; this tells them <b>why</b>, in the voice
    /// of the people it happened to, and a one-line instruction for doing better next time.
    /// </remarks>
    public static class ReviewScreen
    {
        private static readonly Color Ink = new(0.82f, 0.80f, 0.90f);
        private static readonly Color Dim = new(0.52f, 0.50f, 0.60f);
        private static readonly Color Gold = new(0.85f, 0.7f, 1f);

        /// <summary>
        /// Draws the review card.
        /// </summary>
        /// <param name="review">Review to show.</param>
        /// <param name="harvested">Energy taken, shown as the raid's score.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="age">Seconds the card has been up, used to animate the stars landing.</param>
        public static void Draw(RaidReview review, float harvested, float scale, float age)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.05f, 0.04f, 0.08f, 0.9f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            float top = Screen.height * 0.2f;

            var caption = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            caption.normal.textColor = Dim;
            GUI.Label(new Rect(0f, top, Screen.width, 24f * scale),
                "THE PARTY LEFT A REVIEW", caption);

            // Stars land one at a time. It is a small thing and it is the entire payoff of the
            // minute just played, so it is worth the third of a second it takes to watch.
            int landed = Mathf.Clamp(Mathf.FloorToInt(age / 0.22f), 0, 5);
            var shown = new RaidReview(
                Mathf.Min(review.Stars, landed), review.Headline, review.Quip, review.Lesson);

            var stars = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(46 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Five separate labels, all the SAME glyph, lit ones tinted and unlit ones dimmed.
            //
            // Drawn as one string it read as a rating that had lost its alignment: the filled star
            // was an asterisk, which sits up at cap height, and the empty ones were full stops
            // sitting on the baseline, so a one-star review showed a mark floating above a row of
            // dots. No font choice fixes that -- the two characters are meant to sit at different
            // heights. Using one glyph for both states makes the row level by construction, and
            // avoids betting the payoff screen on whether a proper star character survives the
            // WebGL font atlas.
            float starWidth = 34f * scale;
            float row = top + (26f * scale);
            float first = (Screen.width * 0.5f) - (starWidth * 2.5f);
            Color lit = review.Tint();
            var unlit = new Color(lit.r, lit.g, lit.b, 0.22f);

            for (int i = 0; i < 5; i++)
            {
                stars.normal.textColor = i < shown.Stars ? lit : unlit;
                GUI.Label(
                    new Rect(first + (i * starWidth), row, starWidth, 60f * scale), "*", stars);
            }

            var headline = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(26 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headline.normal.textColor = Ink;
            GUI.Label(new Rect(0f, top + (92f * scale), Screen.width, 40f * scale),
                review.Headline, headline);

            var quip = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            quip.normal.textColor = Dim;
            float quipWidth = Mathf.Min(Screen.width * 0.8f, 620f * scale);
            GUI.Label(new Rect((Screen.width - quipWidth) * 0.5f, top + (134f * scale),
                quipWidth, 60f * scale), review.Quip, quip);

            var harvest = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            harvest.normal.textColor = Gold;
            GUI.Label(new Rect(0f, top + (196f * scale), Screen.width, 40f * scale),
                harvested.ToString("N0", CultureInfo.InvariantCulture) + " HARVESTED", harvest);

            // The instruction. This is the line that teaches the game, so it is stated plainly rather
            // than left for the player to infer from a number moving.
            var lesson = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            lesson.normal.textColor = review.Tint();
            GUI.Label(new Rect((Screen.width - quipWidth) * 0.5f, top + (238f * scale),
                quipWidth, 50f * scale), review.Lesson, lesson);

            var prompt = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            prompt.normal.textColor = new Color(0.55f, 1f, 0.45f, 0.55f + (Mathf.Sin(age * 4f) * 0.35f));
            GUI.Label(new Rect(0f, top + (292f * scale), Screen.width, 28f * scale),
                "PRESS ANY KEY", prompt);
        }
    }
}
