using System.Globalization;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// The winning ending: a celebration card with the player's final total.
    /// </summary>
    /// <remarks>
    /// The ending the game is played for, and until now it was the standings with a different line
    /// of text under them — the same screen a losing player sees, saying something else. The author
    /// asked for the win to actually land: <i>"a big congratulations, you are the last dungeon
    /// standing with a total of blah blah how many points"</i>, over a picture worth looking at, held
    /// for a few seconds before any key starts a new run.
    /// <para>
    /// Held rather than skippable for the same reason the opening card is: a player who has just
    /// won and is still pressing to dismiss a review would blow straight through the one screen the
    /// whole season was for. <see cref="Seconds"/> is the floor, not the duration — the card stays
    /// up until a key is pressed after it.
    /// </para>
    /// </remarks>
    public static class VictoryScreen
    {
        /// <summary>Seconds the card is held before a key will dismiss it.</summary>
        /// <remarks>
        /// The author asked for "five seconds or so". Long enough to read the total and look at the
        /// art, short enough that a player who wants to go again is not held hostage.
        /// </remarks>
        public const float Seconds = 5f;

        /// <summary>Where the celebration art lives.</summary>
        private const string Art = "scenes/victory";

        /// <summary>Green the game uses for the player everywhere else.</summary>
        private static readonly Color PlayerGreen = new(0.55f, 1f, 0.45f);

        /// <summary>Violet used for the harvest figure on the review card.</summary>
        private static readonly Color Violet = new(0.85f, 0.7f, 1f);

        /// <summary>
        /// Draws the winning card.
        /// </summary>
        /// <remarks>
        /// Must be called from <c>OnGUI</c>. The art is drawn first and everything else sits on top
        /// of it, so a missing sprite costs the picture and not the words.
        /// </remarks>
        /// <param name="total">The player's final banked score.</param>
        /// <param name="scale">Interface scale.</param>
        /// <param name="age">Seconds this screen has been up.</param>
        public static void Draw(float total, float scale, float age)
        {
            // Floored like every other screen in the game. On a 360-pixel phone the raw scale is
            // 0.28, which would draw the headline at fourteen pixels -- see the review screen's note
            // for why the floor goes on the scale rather than on each font.
            float ui = Mathf.Max(scale, 0.6f);

            var backdrop = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.color = new Color(0.09f, 0.07f, 0.12f);
            GUI.DrawTexture(backdrop, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // COVER, not stretch: the art is 512x288 and the screen can be any shape, so it is
            // scaled by whichever axis needs more and centred. Stretching it to the window would
            // make a phone's throne hall a tall thin smear.
            Sprite art = Resources.Load<Sprite>(Art);
            if (art != null)
            {
                float cover = Mathf.Max(
                    Screen.width / art.rect.width, Screen.height / art.rect.height);
                float w = art.rect.width * cover;
                float h = art.rect.height * cover;
                GUI.DrawTexture(
                    new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h),
                    art.texture);

                // Darkened, because the art is busy and the words are the point. The picture reads
                // as celebration at a glance; the total has to be legible at a glance too.
                GUI.color = new Color(0.05f, 0.04f, 0.08f, 0.55f);
                GUI.DrawTexture(backdrop, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            float y = Screen.height * 0.16f;

            var headline = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(52 * ui),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            headline.normal.textColor = PlayerGreen;
            GUI.Label(new Rect(0f, y, Screen.width, 80f * ui), "CONGRATULATIONS", headline);

            var line = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22 * ui),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            line.normal.textColor = new Color(0.9f, 0.88f, 0.95f);
            GUI.Label(new Rect(0f, y + (66f * ui), Screen.width, 40f * ui),
                "YOURS IS THE LAST DUNGEON STANDING", line);

            var figure = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(64 * ui),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            figure.normal.textColor = Violet;
            GUI.Label(new Rect(0f, y + (118f * ui), Screen.width, 90f * ui),
                total.ToString("N0", CultureInfo.InvariantCulture), figure);

            var caption = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(9, Mathf.RoundToInt(17 * ui)),
                alignment = TextAnchor.UpperCenter
            };
            caption.normal.textColor = new Color(0.72f, 0.7f, 0.8f);
            GUI.Label(new Rect(0f, y + (196f * ui), Screen.width, 40f * ui),
                "POINTS HARVESTED ACROSS THE SEASON", caption);

            // Only once the hold is over, so the prompt never invites a press that does nothing.
            if (age < Seconds)
            {
                return;
            }

            var prompt = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(20 * ui)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };

            // Breathing, so it reads as newly available rather than as something that was always
            // there and ignored.
            float pulse = 0.65f + (0.35f * Mathf.Abs(Mathf.Sin((age - Seconds) * 2.2f)));
            prompt.normal.textColor = new Color(0.55f, 1f, 0.45f, pulse);
            GUI.Label(new Rect(0f, Screen.height * 0.82f, Screen.width, 40f * ui),
                "PRESS ANY KEY  -  BUILD ANOTHER DUNGEON", prompt);
        }
    }
}
