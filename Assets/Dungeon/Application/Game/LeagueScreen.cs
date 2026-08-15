using System.Globalization;
using Dungeon.LeagueManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws the league standings: the game's title screen, and the strip shown during a raid.
    /// </summary>
    /// <remarks>
    /// SPEC.md section 6: <b>the leaderboard is the title screen.</b> No menu, no logo. The game
    /// opens on the standings with the player highlighted and a red line under the bottom two,
    /// because that single screen is the 10-second hook -- a new player reads it and immediately
    /// understands "I am 14th, 16th is death, I need to climb".
    /// <para>
    /// Immediate-mode, like the raid HUD, so it needs no font asset or canvas and cannot fail in a
    /// WebGL build the way a missing dynamic font silently can.
    /// </para>
    /// </remarks>
    public static class LeagueScreen
    {
        private static readonly Color PlayerGreen = new(0.55f, 1f, 0.45f);
        private static readonly Color RelegationRed = new(0.95f, 0.28f, 0.28f);
        private static readonly Color Ink = new(0.78f, 0.76f, 0.85f);
        private static readonly Color Dim = new(0.45f, 0.43f, 0.52f);

        /// <summary>
        /// Draws the full standings screen.
        /// </summary>
        /// <param name="league">Table to show.</param>
        /// <param name="scale">UI scale, so the screen reads at any resolution.</param>
        /// <param name="shift">
        /// How far through the position-shift animation, 0 to 1. Rows slide from where they were to
        /// where they are, so a player sees themselves move rather than being told.
        /// </param>
        /// <param name="prompt">Line shown at the bottom.</param>
        /// <param name="nextParty">
        /// Who walks in next, announced above the prompt. The player has to be able to read the door
        /// before it opens -- composition is the game's main source of variety, and finding out who
        /// was in the party only after killing them is not a decision.
        /// </param>
        /// <summary>
        /// Where the "press any key" prompt lands, so a test can check it is on screen.
        /// </summary>
        /// <remarks>
        /// Shared with <see cref="Draw"/> rather than duplicated, so the two cannot drift. The line
        /// that tells a player how to start the game is the one line the screen cannot afford to
        /// lose, and it was lost once already when the next-party announcement was added.
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="hasNextParty">Whether the party announcement is shown.</param>
        /// <returns>The prompt's rectangle in GUI space.</returns>
        public static Rect PromptRect(float scale, bool hasNextParty)
        {
            return PromptRect(scale, hasNextParty, Screen.width, Screen.height);
        }

        /// <summary>
        /// Where the prompt lands on a canvas of a given size.
        /// </summary>
        /// <remarks>
        /// Takes the size explicitly so the layout can be checked at resolutions the editor is not
        /// running at. The itch.io embed is far smaller than the 960x600 canvas, and this screen has
        /// already lost its prompt off the bottom once — a failure that is invisible until somebody
        /// opens the published page.
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="hasNextParty">Whether the party announcement is shown.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>The prompt's rectangle in GUI space.</returns>
        public static Rect PromptRect(float scale, bool hasNextParty, float width, float height)
        {
            float rowHeight = 26f * scale;
            float top = Mathf.Max(8f * scale,
                (height - (rowHeight * (LeagueTable.Size + 7))) * 0.5f);
            float listTop = top + (rowHeight * 2.6f);
            float promptRow = LeagueTable.Size + 1.2f + (hasNextParty ? 1.9f : 0f);
            return new Rect(0f, listTop + (rowHeight * promptRow), width, rowHeight * 1.4f);
        }

        public static void Draw(LeagueTable league, float scale, float shift, string prompt,
            PartyManager.PartyComposition nextParty = null)
        {
            float width = Mathf.Min(Screen.width * 0.9f, 620f * scale);
            float left = (Screen.width - width) * 0.5f;
            float rowHeight = 26f * scale;

            // Twenty rows plus the title, the relegation warning, the next-party announcement and
            // the prompt. Budgeting for four spare rows instead of seven pushed the prompt clean off
            // the bottom of a 960x600 canvas the moment the announcement was added -- the standings
            // still looked fine, and the line telling the player how to start the game was gone.
            float top = Mathf.Max(8f * scale,
                (Screen.height - (rowHeight * (LeagueTable.Size + 7))) * 0.5f);

            // Darken the dungeon behind. It should still be visible -- the standings sit over the
            // player's own dungeon, which is the joke -- but at full brightness the torchlight and
            // masonry compete with twenty rows of small text and the board stops being readable.
            Color previous = GUI.color;
            GUI.color = new Color(0.06f, 0.05f, 0.09f, 0.82f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = new Color(0.85f, 0.7f, 1f);
            GUI.Label(new Rect(0f, top, Screen.width, rowHeight * 1.6f), "THE LEAGUE", title);

            var caption = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(13 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            // Nothing left to eliminate. Said plainly, because the alternative is what the winning
            // ending actually showed the first time anything rendered it: "1 DUNGEONS LEFT. THE
            // BOTTOM 1 ARE DESTROYED." -- a competition still running, in broken grammar, over the
            // one screen the whole run is played for.
            bool over = league.Entries.Count <= 1;

            caption.normal.textColor = Dim;
            GUI.Label(new Rect(0f, top + (rowHeight * 1.5f), Screen.width, rowHeight),
                over
                    ? "NOTHING IS LEFT TO BEAT."
                    : league.IsFinal
                        ? "THE FINAL.  ONE OF YOU LEAVES; THE OTHER WINS."
                        : $"{league.Entries.Count} DUNGEONS LEFT.  THE BOTTOM "
                          + $"{league.EliminationsThisRound} "
                          + (league.EliminationsThisRound == 1 ? "IS" : "ARE") + " DESTROYED.",
                caption);

            float listTop = top + (rowHeight * 2.6f);

            // Measured off the field as it stands, not off the twenty it started with. Dungeons
            // leave and are never replaced, so a line pinned to LeagueTable.Size would drift further
            // below the last row every round and stop marking anything -- and the drop zone is two
            // deep for nine rounds and one deep for the final, which the player has to be able to
            // see.
            int relegationFrom = league.Entries.Count - league.EliminationsThisRound;

            for (int i = 0; i < league.Entries.Count; i++)
            {
                LeagueEntry entry = league.Entries[i];

                // Slide from the old position to the new one. At shift 1 the row is home.
                float from = (entry.PreviousPosition - 1) * rowHeight;
                float to = i * rowHeight;
                float y = listTop + Mathf.Lerp(from, to, shift);

                DrawRow(entry, i + 1, new Rect(left, y, width, rowHeight), scale, i >= relegationFrom);
            }

            // The relegation line: the thing that makes the board a threat rather than a table.
            // Not drawn once the competition is over -- a red drop line above the winner's own row,
            // which is what the ending screen showed, reads as a threat to a player who has just
            // beaten everybody.
            if (!over)
            {
                float lineY = listTop + (relegationFrom * rowHeight) - (2f * scale);
                var line = new Rect(left, lineY, width, Mathf.Max(2f, 2f * scale));
                Color was = GUI.color;
                GUI.color = RelegationRed;
                GUI.DrawTexture(line, Texture2D.whiteTexture);
                GUI.color = was;
            }

            // Below the whole table, not beside the line. Every row is full width -- rank, name and
            // score -- so anything placed level with the line lands on top of a score, whichever
            // side of it you choose. Two attempts collided before this one.
            var warn = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(12 * scale),
                alignment = TextAnchor.MiddleCenter
            };
            warn.normal.textColor = RelegationRed;

            // Under the table as it stands rather than under the twenty rows it started with, and
            // gone entirely once there is nobody left to destroy. Pinned to the starting size it
            // floated in open space three rows below the last dungeon by the final, and on the
            // winning screen it announced "THE BOTTOM TWO ARE DESTROYED" beneath a table of one.
            if (!over)
            {
                GUI.Label(
                    new Rect(0f, listTop + (rowHeight * (league.Entries.Count + 0.4f)),
                        Screen.width, rowHeight),
                    league.IsFinal ? "LAST PLACE IS DESTROYED" : "THE BOTTOM TWO ARE DESTROYED",
                    warn);
            }

            float promptRow = LeagueTable.Size + 1.2f;

            if (nextParty != null)
            {
                var partyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(15 * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                partyStyle.normal.textColor = new Color(0.85f, 0.7f, 1f);
                GUI.Label(new Rect(0f, listTop + (rowHeight * promptRow), Screen.width, rowHeight),
                    "NEXT THROUGH THE DOOR:  " + nextParty.Name, partyStyle);

                var warnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(12 * scale),
                    alignment = TextAnchor.MiddleCenter
                };
                warnStyle.normal.textColor = Ink;
                GUI.Label(new Rect(0f, listTop + (rowHeight * (promptRow + 0.85f)),
                    Screen.width, rowHeight), nextParty.Warning, warnStyle);

            }

            var promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(16 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle.normal.textColor = PlayerGreen;
            GUI.Label(PromptRect(scale, nextParty != null), prompt, promptStyle);
        }

        /// <summary>Draws one standings row.</summary>
        private static void DrawRow(
            LeagueEntry entry, int position, Rect row, float scale, bool doomed)
        {
            if (entry.IsPlayer)
            {
                Color was = GUI.color;
                GUI.color = new Color(0.25f, 0.55f, 0.25f, 0.35f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = was;
            }

            Color ink = entry.IsPlayer ? PlayerGreen : doomed ? RelegationRed : Ink;

            var rank = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                alignment = TextAnchor.MiddleRight
            };
            rank.normal.textColor = doomed && !entry.IsPlayer ? RelegationRed : Dim;
            GUI.Label(new Rect(row.x, row.y, 34f * scale, row.height),
                position.ToString(CultureInfo.InvariantCulture), rank);

            var name = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                fontStyle = entry.IsPlayer ? FontStyle.Bold : FontStyle.Normal
            };
            name.normal.textColor = ink;
            GUI.Label(new Rect(row.x + (46f * scale), row.y, row.width * 0.6f, row.height),
                entry.Name, name);

            var score = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(15 * scale),
                alignment = TextAnchor.MiddleRight,
                fontStyle = entry.IsPlayer ? FontStyle.Bold : FontStyle.Normal
            };
            score.normal.textColor = ink;
            GUI.Label(new Rect(row.x, row.y, row.width - (12f * scale), row.height),
                entry.Score.ToString("N0", CultureInfo.InvariantCulture), score);
        }

        /// <summary>
        /// Draws the compact standings strip shown during a raid.
        /// </summary>
        /// <remarks>
        /// A window around the player rather than the whole table: during a raid the only question
        /// that matters is how close the relegation line is, and twenty rows would bury it.
        /// <para>
        /// Sized off the table's <b>current</b> length rather than off <c>LeagueTable.Size</c>, which
        /// is the length it <i>started</i> at. Rivals are eliminated as the run goes on, so the two
        /// diverge from the first relegation onward -- and the window was being clamped into rows
        /// that no longer existed. It threw <c>ArgumentOutOfRangeException</c> out of <c>OnGUI</c>,
        /// which takes the rest of the frame's interface down with it: the clock, the rate, the
        /// harvest. Late in a winning run, exactly when the standings are most worth reading.
        /// </para>
        /// </remarks>
        /// <param name="league">Table to show.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="liveScore">The player's score including the raid in progress.</param>
        public static void DrawStrip(LeagueTable league, float scale, float liveScore)
        {
            const int window = 2;
            int rows = league.Entries.Count;
            int centre = league.PlayerPosition - 1;
            int first = Mathf.Clamp(centre - window, 0, Mathf.Max(0, rows - ((window * 2) + 1)));
            float rowHeight = 18f * scale;
            float width = 220f * scale;
            float x = Screen.width - width - (16f * scale);
            float y = 96f * scale;

            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(11 * scale) };
            label.normal.textColor = Dim;
            GUI.Label(new Rect(x, y - (16f * scale), width, rowHeight), "STANDINGS", label);

            for (int i = first; i < first + (window * 2) + 1 && i < rows; i++)
            {
                LeagueEntry entry = league.Entries[i];
                bool doomed = i >= rows - league.EliminationsThisRound;
                var row = new Rect(x, y + ((i - first) * rowHeight), width, rowHeight);

                var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * scale) };
                style.normal.textColor = entry.IsPlayer ? PlayerGreen : doomed ? RelegationRed : Dim;

                float shown = entry.IsPlayer ? entry.Score + liveScore : entry.Score;
                GUI.Label(row, $"{i + 1,2}  {Trim(entry.Name, 16)}", style);

                var right = new GUIStyle(style) { alignment = TextAnchor.MiddleRight };
                GUI.Label(row, shown.ToString("N0", CultureInfo.InvariantCulture), right);
            }
        }

        /// <summary>Shortens a name so the strip cannot overflow its column.</summary>
        private static string Trim(string value, int length)
        {
            return value.Length <= length ? value : value[..(length - 2)] + "..";
        }
    }
}
