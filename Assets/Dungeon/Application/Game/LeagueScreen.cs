using System.Globalization;
using Dungeon.PartyManager;
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
        /// <summary>
        /// The line that names the party about to enter, exactly as it is drawn.
        /// </summary>
        /// <remarks>
        /// Public so a test can measure the real string rather than rebuild it. The two halves of
        /// this — the roster's name and the size clause — both grew on 2026-08-17, and the line is
        /// drawn centred across the whole screen with no wrapping, so on a narrow phone it is the
        /// most likely thing on the title screen to run off the edge.
        /// </remarks>
        /// <param name="party">The party about to raid.</param>
        /// <returns>The announcement, or an empty string when there is no party.</returns>
        public static string Announcement(PartyComposition party)
        {
            if (party == null)
            {
                return string.Empty;
            }

            int strength = party.Roles.Count;
            return strength > PartyComposition.BaseSize
                ? $"NEXT THROUGH THE DOOR:  {party.Name},  "
                  + $"{PartyComposition.SpellSize(strength).ToUpperInvariant()} STRONG"
                : "NEXT THROUGH THE DOOR:  " + party.Name;
        }

        /// <summary>Font size the announcement would like to be drawn at.</summary>
        /// <param name="scale">UI scale.</param>
        /// <returns>Font size in pixels, before it is fitted to the screen.</returns>
        public static int AnnouncementFontSize(float scale)
        {
            return Mathf.Max(10, Mathf.RoundToInt(15 * scale));
        }

        /// <summary>
        /// Font size the announcement is actually drawn at, shrunk until the line fits the screen.
        /// </summary>
        /// <remarks>
        /// <b>Must be called from inside <c>OnGUI</c></b> — it measures the real font.
        /// <para>
        /// Measured 2026-08-17: <i>"NEXT THROUGH THE DOOR:  THE BALANCED PARTY,  NINE STRONG"</i> is
        /// 11 pixels wider than a 360-pixel phone at the nominal size. The line is <b>centred, not
        /// clipped</b>, so an overrun spills off both edges at once and the player loses the ends of
        /// it — on the standings, which SPEC.md makes the title screen and the ten-second hook.
        /// </para>
        /// <para>
        /// It only became possible on 2026-08-17, when the growth curve was fixed and rosters
        /// started reaching nine: the size clause appears only above the base four, so until then
        /// the longest line the game could draw was two words shorter. Nine is floored rather than
        /// ten because the warning line beneath already draws at nine, so it is a size this screen
        /// is known to be readable at.
        /// </para>
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="line">The announcement, from <see cref="Announcement"/>.</param>
        /// <param name="width">Canvas width in pixels.</param>
        /// <returns>Font size in pixels.</returns>
        public static int FittedAnnouncementFontSize(float scale, string line, float width)
        {
            int wanted = AnnouncementFontSize(scale);
            if (string.IsNullOrEmpty(line))
            {
                return wanted;
            }

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = wanted,
                fontStyle = FontStyle.Bold
            };

            float drawn = style.CalcSize(new GUIContent(line)).x;
            if (drawn <= width || drawn <= 0.01f)
            {
                return wanted;
            }

            return Mathf.Max(9, Mathf.FloorToInt(wanted * (width / drawn)));
        }

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
        /// <summary>
        /// Height of one standings row, with a floor.
        /// </summary>
        /// <remarks>
        /// The UI scale is the smaller of the two axes, so a phone held upright reports about 0.3 —
        /// and 26 times that is an eight-pixel row carrying five-pixel text. The standings are this
        /// game's <b>title screen</b>: the first thing anybody sees, and the screen SPEC calls the
        /// ten-second hook. It cannot be the one drawn too small to read.
        /// <para>
        /// Portrait has vertical room to spare — twenty floored rows and the furniture come to about
        /// 360 pixels of an 844-pixel phone — so the floor costs nothing where it does not bite. It
        /// is here rather than inlined because <see cref="PromptRect"/> and <see cref="Draw"/> must
        /// agree to the pixel, and they have drifted before.
        /// </para>
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="height">Canvas height in pixels.</param>
        /// <returns>The row height in pixels.</returns>
        public static float RowHeight(float scale, float height)
        {
            // Never taller than the canvas can hold. Twenty legible rows want 405 pixels, and the
            // itch embed is 293 tall -- flooring without this ceiling pushed the table off the
            // bottom of the page the game actually ships on, which is the failure this screen has
            // already had once. Where there is room, the floor applies; where there is not, the
            // rows fit and the page settings are the fix rather than the layout.
            // 7.6 rather than the 7 the layout budgets: the prompt's own row ends at about 27.1
            // rows, so dividing by exactly 27 fills the canvas to the pixel and pushes the last
            // line -- the one telling the player how to start -- six pixels off the bottom.
            return Mathf.Min(Mathf.Max(15f, 26f * scale), height / (LeagueTable.Size + 7.6f));
        }

        public static Rect PromptRect(float scale, bool hasNextParty, float width, float height)
        {
            float rowHeight = RowHeight(scale, height);
            float top = Mathf.Max(8f * scale,
                (height - (rowHeight * (LeagueTable.Size + 7))) * 0.5f);
            float listTop = top + (rowHeight * 2.6f);
            float promptRow = LeagueTable.Size + 1.2f + (hasNextParty ? 1.9f : 0f);
            return new Rect(0f, listTop + (rowHeight * promptRow), width, rowHeight * 1.4f);
        }

        /// <param name="promptColour">
        /// Colour for the closing line, or null for the player's green. Passed in rather than
        /// inferred, because only the caller knows whether this screen is announcing a win, a
        /// collapse, or an ordinary round about to start.
        /// </param>
        public static void Draw(LeagueTable league, float scale, float shift, string prompt,
            PartyManager.PartyComposition nextParty = null, Color? promptColour = null)
        {
            float width = Mathf.Min(Screen.width * 0.9f, 620f * scale);
            float left = (Screen.width - width) * 0.5f;
            float rowHeight = RowHeight(scale, Screen.height);

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
                fontSize = Mathf.Max(15, Mathf.RoundToInt(30 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = new Color(0.85f, 0.7f, 1f);
            GUI.Label(new Rect(0f, top, Screen.width, rowHeight * 1.6f), "THE LEAGUE", title);

            var caption = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(9, Mathf.RoundToInt(13 * scale)),
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
                fontSize = Mathf.Max(9, Mathf.RoundToInt(12 * scale)),
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
                string announcement = Announcement(nextParty);
                var partyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = FittedAnnouncementFontSize(scale, announcement, Screen.width),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                partyStyle.normal.textColor = new Color(0.85f, 0.7f, 1f);
                // The SIZE is announced once the league starts growing parties, and it is not a
                // decoration. GameController's own note on NextParty says variety the player cannot
                // see before they have to act on it is just noise -- and size is the largest single
                // factor in how a raid goes: a worked raid is worth 240 at four and 433 at nine.
                // The player was being told WHICH party was coming and not HOW MANY.
                //
                // Only above the base four, so the opening raids read exactly as they always have
                // and the first "FIVE STRONG" is itself the signal that something has changed.
                int strength = nextParty.Roles.Count;

                GUI.Label(new Rect(0f, listTop + (rowHeight * promptRow), Screen.width, rowHeight),
                    announcement, partyStyle);

                var warnStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(12 * scale)),
                    alignment = TextAnchor.MiddleCenter
                };
                warnStyle.normal.textColor = Ink;
                GUI.Label(new Rect(0f, listTop + (rowHeight * (promptRow + 0.85f)),
                    Screen.width, rowHeight), nextParty.Warning, warnStyle);

            }

            var promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(11, Mathf.RoundToInt(16 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Green is the player's colour on this screen -- their row, their score, the line that
            // says they won. Drawing "YOUR DUNGEON COLLAPSED IN 20th" in it too made the one line
            // announcing the run is over read as congratulation, which is what the photograph of the
            // collapse screen showed.
            promptStyle.normal.textColor = promptColour ?? PlayerGreen;
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
                fontSize = Mathf.Max(10, Mathf.RoundToInt(15 * scale)),
                alignment = TextAnchor.MiddleRight
            };
            rank.normal.textColor = doomed && !entry.IsPlayer ? RelegationRed : Dim;
            GUI.Label(new Rect(row.x, row.y, 34f * scale, row.height),
                position.ToString(CultureInfo.InvariantCulture), rank);

            var name = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(15 * scale)),
                fontStyle = entry.IsPlayer ? FontStyle.Bold : FontStyle.Normal
            };
            name.normal.textColor = ink;
            GUI.Label(new Rect(row.x + (46f * scale), row.y, row.width * 0.6f, row.height),
                entry.Name, name);

            var score = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(10, Mathf.RoundToInt(15 * scale)),
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

            var label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(9, Mathf.RoundToInt(11 * scale)) };
            label.normal.textColor = Dim;
            GUI.Label(new Rect(x, y - (16f * scale), width, rowHeight), "STANDINGS", label);

            for (int i = first; i < first + (window * 2) + 1 && i < rows; i++)
            {
                LeagueEntry entry = league.Entries[i];
                bool doomed = i >= rows - league.EliminationsThisRound;
                var row = new Rect(x, y + ((i - first) * rowHeight), width, rowHeight);

                var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(9, Mathf.RoundToInt(12 * scale)) };
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
