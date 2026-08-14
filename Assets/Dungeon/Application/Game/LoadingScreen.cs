using Dungeon.PartyManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// The two seconds before the standings appear: key art, with a party marching across it.
    /// </summary>
    /// <remarks>
    /// The author asked for generated key art on the loading screen but was explicit that the motion
    /// stay <i>"made with Unity"</i> — so the backdrop is a picture and everything moving on top of
    /// it is drawn live from the same walk frames the game uses during a raid. Nothing here is baked
    /// into the image.
    /// <para>
    /// That constraint is worth more than it looks. The party is animated from
    /// <c>Resources/party/walk/*</c>, so if a walk cycle is ever regenerated the loading screen picks
    /// it up for free, and it cannot drift out of step with the game the way a rendered video would.
    /// It also means this screen is a small standing check that the walk frames load at all — a
    /// thing that has silently broken here before.
    /// </para>
    /// <para>
    /// The art was drawn with its lower third deliberately near-empty (measured average luminance
    /// 8.4 against 11.8 for the whole image), which is the band the marchers occupy.
    /// </para>
    /// </remarks>
    public static class LoadingScreen
    {
        /// <summary>How long the screen is shown before the standings take over.</summary>
        public const float Seconds = 2f;

        /// <summary>Frames in a walk cycle, matching the generated art.</summary>
        private const int WalkFrames = 6;

        /// <summary>Where the frame numbering starts on disk.</summary>
        /// <remarks>
        /// The generated frames are named <c>tank-walk-1</c> through <c>-6</c>, not zero-based.
        /// Checked against the filesystem rather than assumed: an off-by-one here loads nothing,
        /// and a null texture draws nothing, so the party would simply be absent with no error.
        /// </remarks>
        private const int FirstFrame = 1;

        /// <summary>Frames per second the cycle plays at, matching the raid view.</summary>
        private const float WalkFps = 12f;

        /// <summary>Roles that march across, in marching order.</summary>
        private static readonly AdventurerRole[] Marchers =
        {
            AdventurerRole.Tank, AdventurerRole.Healer,
            AdventurerRole.Ranged, AdventurerRole.Mage
        };

        /// <summary>Cached backdrop, so this does not hit Resources every frame.</summary>
        private static Texture2D _art;

        /// <summary>Key art behind the marchers, or null when it could not be loaded.</summary>
        /// <remarks>
        /// Null is supported. The screen degrades to a dark field with a marching party, which is
        /// still a loading screen; a missing texture must never be the thing that stops the game
        /// starting.
        /// </remarks>
        private static Texture2D Art =>
            _art != null ? _art : _art = Resources.Load<Texture2D>("scenes/loading-screen");

        /// <summary>
        /// Draws the loading screen.
        /// </summary>
        /// <param name="age">Seconds this screen has been up.</param>
        /// <param name="scale">UI scale, so this reads at the itch embed's 0.4.</param>
        public static void Draw(float age, float scale)
        {
            Color previous = GUI.color;

            if (Art != null)
            {
                // ScaleAndCrop rather than StretchToFill: the art is 16:9 and the embed is not
                // always, and stretching a stone floor is immediately obvious.
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height), Art, ScaleMode.ScaleAndCrop);
            }
            else
            {
                GUI.color = new Color(0.05f, 0.04f, 0.08f, 1f);
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = previous;
            }

            DrawMarchers(age, scale);
            DrawCaption(age, scale);

            GUI.color = previous;
        }

        /// <summary>Walks the party across the empty lower third of the art.</summary>
        /// <param name="age">Seconds this screen has been up.</param>
        /// <param name="scale">UI scale.</param>
        private static void DrawMarchers(float age, float scale)
        {
            int frame = Mathf.FloorToInt(age * WalkFps) % WalkFrames;

            // Sized off screen height so the party keeps its footing in the art at any aspect.
            float size = Mathf.Max(24f, Screen.height * 0.11f);
            float spacing = size * 0.78f;
            float baseline = Screen.height * 0.74f;

            // They enter from the left and are still walking when the screen hands over -- a party
            // that arrives and stops would read as finished rather than as loading.
            float travelled = (age / Seconds) * (Screen.width * 0.62f);
            float lead = (Screen.width * 0.16f) + travelled;

            for (int i = 0; i < Marchers.Length; i++)
            {
                Texture2D sprite = FrameFor(Marchers[i], frame + i);
                if (sprite == null)
                {
                    continue;
                }

                // A gentle bob per member, offset so they are not in lockstep.
                float bob = Mathf.Sin((age * WalkFps * 0.5f) + i) * size * 0.03f;
                var rect = new Rect(
                    lead - (i * spacing), baseline + bob - size, size, size);

                GUI.DrawTexture(rect, sprite, ScaleMode.ScaleToFit);
            }
        }

        /// <summary>Loads one walk frame for a role.</summary>
        /// <param name="role">Whose frame to load.</param>
        /// <param name="frame">Frame index, wrapped into the cycle.</param>
        /// <returns>The texture, or null when that art is missing.</returns>
        private static Texture2D FrameFor(AdventurerRole role, int frame)
        {
            string stem = role switch
            {
                AdventurerRole.Tank => "tank",
                AdventurerRole.Healer => "healer",
                AdventurerRole.Ranged => "ranged",
                _ => "mage"
            };

            int index = ((frame % WalkFrames) + WalkFrames) % WalkFrames;
            return Resources.Load<Texture2D>($"party/walk/{stem}-walk-{index + FirstFrame}");
        }

        /// <summary>Draws the one line of text, fading in.</summary>
        /// <param name="age">Seconds this screen has been up.</param>
        /// <param name="scale">UI scale.</param>
        private static void DrawCaption(float age, float scale)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(9, Mathf.RoundToInt(22 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Floored to whole pixels and given a minimum size, because the itch embed runs at 0.4
            // scale and an unfloored 22 lands at eight-and-a-bit pixels, which is where a menu row
            // once came out twelve pixels tall and unreadable.
            float alpha = Mathf.Clamp01(age / 0.5f);
            style.normal.textColor = new Color(0.85f, 0.82f, 0.9f, alpha);

            var rect = new Rect(
                0f, Mathf.Floor(Screen.height * 0.16f), Screen.width,
                Mathf.Floor(40f * scale));

            GUI.Label(rect, "THEY ARE COMING", style);
        }
    }
}
