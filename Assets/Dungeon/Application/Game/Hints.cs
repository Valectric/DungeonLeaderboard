using Dungeon.DungeonManager;
using Dungeon.RaidManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Coaching text drawn over the dungeon during the first raid of a run.
    /// </summary>
    /// <remarks>
    /// The game has no tutorial and should not grow one — but it does have a rule that is the exact
    /// opposite of what a dungeon game trains a player to expect: <b>killing the adventurers is
    /// losing</b>. Nothing on the HUD says so. A new player spawns everything they can afford, wipes
    /// the party in twenty seconds, watches the rate collapse and concludes the game is broken.
    /// <para>
    /// So the first raid, and only the first raid, is labelled: one headline over the room the party
    /// walks into, and a small tag on each thing that can be tapped. From the second raid on the
    /// board is bare again, because a label that never goes away stops being read and starts being
    /// clutter.
    /// </para>
    /// <para>
    /// Drawn in world space rather than as a HUD panel, so each instruction sits on the thing it is
    /// talking about. A legend in the corner would make the player match names to sprites, which is
    /// the work the label is supposed to save them.
    /// </para>
    /// </remarks>
    public static class Hints
    {
        /// <summary>How long the headline stays up, in seconds of raid time.</summary>
        /// <remarks>
        /// Long enough to be read twice while the party is still walking in, short enough that the
        /// end of the first raid -- the wounded, expensive part -- is not played through a caption.
        /// </remarks>
        public const float HeadlineSeconds = 18f;

        /// <summary>
        /// Whether the instruction should still be on screen.
        /// </summary>
        /// <remarks>
        /// Two conditions, and the author asked for the second. It clears when the party **loots the
        /// chest**, because that is the moment the opening lesson is over — they have crossed the
        /// room, met the slime pit and found the thing worth stopping for, and the caption is now
        /// covering the game rather than teaching it. The timer stays as a floor for a party that
        /// never reaches the chest at all.
        /// </remarks>
        /// <param name="raid">The raid in progress.</param>
        /// <returns>True while the instruction is still earning its place.</returns>
        public static bool HeadlineWanted(Raid raid)
        {
            if (raid?.Party == null)
            {
                return false;
            }

            return raid.Party.LootedCount == 0
                && (Raid.RaidSeconds - raid.TimeRemaining) < HeadlineSeconds;
        }

        /// <summary>Seconds the headline spends fading out at the end of its life.</summary>
        private const float FadeSeconds = 2.5f;

        /// <summary>Whether the hints should be drawn at all.</summary>
        /// <remarks>
        /// The first raid of a run is the one before anything has been banked, which is exactly
        /// <c>Round == 0</c>. Read from the league rather than from a flag of its own so that
        /// starting a new run after a collapse brings the hints back -- a player who lost in the
        /// first minute is the player who most needs them.
        /// </remarks>
        /// <param name="round">League rounds completed so far.</param>
        /// <returns>True during the first raid of a run.</returns>
        public static bool ShouldShow(int round) => round == 0;

        /// <summary>
        /// Draws the first-raid hints over the dungeon.
        /// </summary>
        /// <param name="raid">The raid in progress.</param>
        /// <param name="camera">Camera the dungeon is drawn with, for placing labels on cells.</param>
        /// <param name="scale">UI scale, so this reads at the itch embed's 0.4.</param>
        /// <param name="round">League rounds completed so far.</param>
        public static void Draw(Raid raid, Camera camera, float scale, int round)
        {
            if (raid == null || camera == null || !ShouldShow(round))
            {
                return;
            }

            DungeonLayout layout = raid.Layout;

            // A tag under the instruction block is suppressed while the block is up, and returns
            // when it fades. Photographed: the chest's gold "THEY STOP TO LOOT" landed exactly on the
            // third instruction line and interleaved with it, leaving both unreadable -- the chest in
            // the starter room sits at the room centre plus two rows, which is where that line goes.
            //
            // Suppressed rather than moved, because the block is transient and the tags are not: a
            // tag nudged aside would stay nudged for the whole raid to dodge something that is gone
            // in a few seconds.
            bool headlineUp = HeadlineWanted(raid);
            Rect block = headlineUp ? HeadlineBlock(camera, scale, layout) : new Rect();

            foreach (Vector2Int cell in layout.SpawnerCells)
            {
                Tag(camera, scale, cell,
                    layout.SpawnerTierAt(cell) == 0 ? "SLIME PIT - TAP TO SPAWN" : "TAP TO SPAWN",
                    new Color(0.6f, 0.95f, 0.55f), block);
            }

            foreach (Vector2Int cell in layout.ChestCells)
            {
                Tag(camera, scale, cell, "THEY STOP TO LOOT", new Color(0.95f, 0.82f, 0.4f), block);
            }

            foreach (Vector2Int cell in layout.TrapCells)
            {
                Tag(camera, scale, cell, "TAP TO WOUND", new Color(0.95f, 0.55f, 0.45f), block);
            }

            foreach (Door door in layout.Grid.Doors)
            {
                Tag(camera, scale, door.Cell, door.IsOpen ? "TAP TO SHUT" : "TAP TO OPEN",
                    new Color(0.7f, 0.75f, 1f), block);
            }

            DrawHeadline(raid, camera, scale, layout);
        }

        /// <summary>
        /// How wide the headline block is allowed to be, in pixels.
        /// </summary>
        /// <remarks>
        /// Scaled like everything else <b>until the scale gets small</b>, and then held at a floor.
        /// The itch embed runs at 523x293, where the UI scale is 0.41 and a straight 560-times-scale
        /// gives a 228-pixel box — while the longest line, <i>TAP THE SLIME PIT TO HOLD THEM - TOO
        /// MANY AND THEY DIE</i>, is about 265 pixels at the 9-pixel minimum font. It would have
        /// clipped on the page most jam voters will actually play the game on, and nowhere else.
        /// <para>
        /// The floor is capped by the screen, so a narrow phone gets almost the full width rather
        /// than a box wider than the display. Large screens are untouched: at scale 1 this returns
        /// the same 560 it always did, so the block still sits over the room rather than spreading
        /// across the whole window.
        /// </para>
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <param name="screenWidth">Canvas width in pixels.</param>
        /// <returns>The block width.</returns>
        public static float BlockWidth(float scale, float screenWidth)
        {
            float room = Mathf.Max(32f, screenWidth - 16f);
            return Mathf.Clamp(760f * scale, Mathf.Min(500f, room), room);
        }

        /// <summary>
        /// Where the three-line instruction block sits, so a tag can avoid landing on it.
        /// </summary>
        /// <remarks>
        /// Shared with <c>DrawHeadline</c> rather than recomputed, because two copies of this
        /// arithmetic drifting apart is exactly how a label ends up half a line off the thing it is
        /// meant to be avoiding.
        /// </remarks>
        /// <param name="camera">Camera the dungeon is drawn with.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="layout">The dungeon being raided.</param>
        /// <returns>The block's rectangle in GUI space.</returns>
        /// <summary>
        /// How much of the bottom of the screen the verb bar occupies, in pixels.
        /// </summary>
        /// <remarks>
        /// Mirrors the rect <c>GameController</c> draws that bar into — <c>Screen.height - 44 * scale</c>
        /// with a 30-high label. Duplicated deliberately rather than plumbed through: the alternative
        /// is Hints holding a reference to the controller to ask where one label went, and the number
        /// has not moved in the project's life. If it ever does, this is the other end of it.
        /// </remarks>
        private static float VerbBarHeight => 44f * Mathf.Min(
            Screen.width / 1280f, Screen.height / 720f);

        public static Rect HeadlineBlock(Camera camera, float scale, DungeonLayout layout)
        {
            Vector2Int anchor = layout.RoomCentres.Count > 0
                ? layout.RoomCentres[0]
                : layout.EntranceCell;

            Vector2 point = GuiPointOf(camera, anchor);

            float width = BlockWidth(scale, Screen.width);
            float lineHeight = Mathf.Max(22f, 42f * scale);
            float blockHeight = lineHeight * 3f;

            // CLEARANCE MEASURED IN WORLD CELLS, NOT IN UI PIXELS. This used to sit 86 * scale above
            // the room centre, and that is the wrong unit: the UI scale follows the canvas size while
            // the room and the party are sized by the CAMERA's zoom. The two agree at 1280x720, where
            // every editor capture was taken, and disagree at the viewport the build actually runs
            // in -- where the party walked straight through the third line, an archer and a health
            // bar drawn across "TOO MANY AND THEY DIE". See D33.
            //
            // Three cells up clears the top wall of a five-tall room with a cell to spare, and it
            // clears it at any zoom because the camera does the projecting.
            Vector2 aboveRoom = GuiPointOf(camera, anchor + new Vector2Int(0, 3));

            float top = aboveRoom.y - blockHeight - (8f * scale);
            if (top < Screen.height * 0.2f)
            {
                // Below the room instead, measured the same way, so a zoomed-in camera does not put
                // the block back on top of the party it was moved to avoid.
                //
                // Clamped above the VERB BAR rather than above the screen edge. GameController draws
                // that bar at Screen.height - 44 * scale, and clamping to the edge let the block run
                // straight through it: at the itch embed's 523x293 the bottom was allowed to reach
                // 290 against a bar starting at 275, and "TAP THE SLIME PIT TO HOLD THEM" was drawn
                // over "TAP A DOOR TO STALL". Photographed at that size, which is the only way this
                // shows -- the fault is one drawn thing landing on another and no rect check sees it.
                Vector2 belowRoom = GuiPointOf(camera, anchor - new Vector2Int(0, 3));
                float aboveVerbBar = Screen.height - VerbBarHeight - blockHeight - (6f * scale);
                top = Mathf.Min(belowRoom.y + (8f * scale), aboveVerbBar);
            }

            float left = Mathf.Clamp(
                point.x - (width * 0.5f), 8f * scale, Mathf.Max(8f * scale, Screen.width - width));

            return new Rect(left, top, width, blockHeight);
        }

        /// <summary>Draws the one big instruction, over the room the party walks into.</summary>
        /// <param name="raid">The raid in progress.</param>
        /// <param name="camera">Camera the dungeon is drawn with.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="layout">The dungeon being raided.</param>
        private static void DrawHeadline(
            Raid raid, Camera camera, float scale, DungeonLayout layout)
        {
            float age = Raid.RaidSeconds - raid.TimeRemaining;
            if (!HeadlineWanted(raid))
            {
                return;
            }

            float alpha = Mathf.Clamp01((HeadlineSeconds - age) / FadeSeconds);

            // Over the first room, high enough to clear the party walking through it -- and kept
            // wholly on screen. Anchored naively it ran off the right-hand edge and sat across the
            // party's health bars, because the opening dungeon is one small room and the camera puts
            // it wherever the world allows. The three lines are laid out as one block so they cannot
            // drift apart from each other while being pushed back on screen.
            Rect block = HeadlineBlock(camera, scale, layout);
            float width = block.width;
            float lineHeight = block.height / 3f;
            float top = block.y;
            float left = block.x;

            var headline = new GUIStyle(GUI.skin.label)
            {
                // Floored with a minimum: the itch embed runs at 0.4 scale, which is where an
                // unfloored size once landed at eight-and-a-bit pixels and became unreadable.
                fontSize = Mathf.Max(17, Mathf.RoundToInt(34 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            var sub = new GUIStyle(headline)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(20 * scale))
            };

            Write(new Rect(left, top, width, lineHeight),
                "DON'T KILL THE CHARGING TEAM", headline,
                new Color(1f, 0.55f, 0.55f, alpha), alpha, scale);

            Write(new Rect(left, top + lineHeight, width, lineHeight),
                "HURT, ALIVE AND STILL INSIDE PAYS BEST", sub,
                new Color(0.88f, 0.86f, 0.95f, alpha), alpha, scale);

            // The restraint is part of the instruction, not a footnote to it. Measured: a player who
            // simply mashes the opening slime pit spawns twenty-five slimes and wipes the party, in
            // the one raid whose own caption says not to -- and the starting dungeon is a single
            // room, so it has no threshold, no door, and therefore none of the retreat valve SPEC
            // calls the player's only mercy. "Tap the spawner to keep them busy" invited exactly
            // that, and the review's verdict afterwards is a one-star NOBODY CAME BACK.
            Write(new Rect(left, top + (lineHeight * 2f), width, lineHeight),
                "TAP THE SLIME PIT TO HOLD THEM  -  TOO MANY AND THEY DIE", sub,
                new Color(0.72f, 0.7f, 0.82f, alpha), alpha, scale);
        }

        /// <summary>Draws a small label above a dungeon cell, unless the instruction is there.</summary>
        /// <param name="camera">Camera the dungeon is drawn with.</param>
        /// <param name="scale">UI scale.</param>
        /// <param name="cell">Cell to label.</param>
        /// <param name="text">What to say.</param>
        /// <param name="colour">Colour to say it in.</param>
        /// <param name="avoid">Rectangle not to draw into; pass an empty rect to draw regardless.</param>
        private static void Tag(
            Camera camera, float scale, Vector2Int cell, string text, Color colour, Rect avoid)
        {
            Vector2 point = GuiPointOf(camera, cell);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(9, Mathf.RoundToInt(12 * scale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Kept on screen, for the same reason the headline is: a spawner near the edge of the
            // frame would otherwise have half its label cut off, and half a label is worse than
            // none -- the player reads it as the game clipping.
            float width = Mathf.Min(220f * scale, Screen.width);
            float left = Mathf.Clamp(point.x - (width * 0.5f), 0f, Mathf.Max(0f, Screen.width - width));

            // Above the thing it names, except in the top third of the screen, where the headline
            // lives -- the opening chest sits on the room's top row and its label landed straight
            // across "DON'T KILL THE CHARGING TEAM".
            float lift = point.y < Screen.height * 0.36f ? -30f * scale : 34f * scale;
            float top = Mathf.Clamp(point.y - lift, 0f, Screen.height - (20f * scale));

            var rect = new Rect(left, top, width, 20f * scale);

            // And that flip was not enough on its own: it moved the chest's label off the first
            // instruction line and onto the THIRD, where the two interleaved character by character
            // and left both unreadable. Photographed, not reasoned about. So the block is tested
            // rather than dodged by arithmetic, and a label that would land on it simply waits the
            // few seconds until it fades.
            if (avoid.width > 0f && rect.Overlaps(avoid))
            {
                return;
            }

            Write(rect, text, style, colour, 1f, scale);
        }

        /// <summary>
        /// Draws text with a dark copy behind it.
        /// </summary>
        /// <remarks>
        /// The dungeon floor is violet-black in some places and lit stone in others, and a single
        /// colour is unreadable over one or the other. A one-pixel shadow costs a second draw call
        /// and works over both, which a panel behind the text would not -- a panel would hide the
        /// thing the label is pointing at.
        /// </remarks>
        /// <param name="rect">Where to draw.</param>
        /// <param name="text">What to draw.</param>
        /// <param name="style">Style to draw it in.</param>
        /// <param name="colour">Colour of the text itself.</param>
        /// <param name="alpha">Opacity, applied to the shadow as well.</param>
        /// <param name="scale">UI scale, so the offset holds at every size.</param>
        private static void Write(
            Rect rect, string text, GUIStyle style, Color colour, float alpha, float scale)
        {
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0.03f, 0.02f, 0.05f, alpha * 0.85f);
            GUI.Label(
                new Rect(rect.x + Mathf.Max(1f, 2f * scale), rect.y + Mathf.Max(1f, 2f * scale),
                    rect.width, rect.height),
                text, shadow);

            var front = new GUIStyle(style);
            front.normal.textColor = colour;
            GUI.Label(rect, text, front);
        }

        /// <summary>Where a dungeon cell sits on screen, in GUI space.</summary>
        /// <param name="camera">Camera the dungeon is drawn with.</param>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>The point in GUI space.</returns>
        private static Vector2 GuiPointOf(Camera camera, Vector2Int cell)
        {
            Vector3 screen = camera.WorldToScreenPoint(DungeonView.CellToWorld(cell));
            return new Vector2(screen.x, Screen.height - screen.y);
        }
    }
}
