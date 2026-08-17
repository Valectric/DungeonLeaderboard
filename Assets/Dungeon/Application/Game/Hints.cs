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

            // raid.Elapsed, NOT RaidSeconds - TimeRemaining: the room bonus pushes TimeRemaining
            // above RaidSeconds, so the old expression ran backwards and the caption lingered by
            // however many seconds the party had earned walking into rooms.
            return raid.Party.LootedCount == 0 && raid.Elapsed < HeadlineSeconds;
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
                // Gone once the party has taken it. The tag describes something that is about to
                // happen -- once it has happened the chest is an empty box and the label is telling
                // the player about a thing they already watched, while still covering the room.
                if (raid.Party.HasLooted(cell))
                {
                    continue;
                }

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

        /// <summary>The longest world tag the game ever draws.</summary>
        /// <remarks>
        /// Kept beside the width and font-size below so a resolution test can ask the real question
        /// -- does the longest label fit the box it is given -- instead of a proxy for it.
        /// </remarks>
        public const string LongestTag = "SLIME PIT - TAP TO SPAWN";

        /// <summary>The longest line the three-line opening instruction ever draws.</summary>
        public const string LongestHintLine =
            "TAP THE SLIME PIT TO HOLD THEM  -  TOO MANY AND THEY DIE";

        /// <summary>Font size of the instruction's smaller lines, in pixels.</summary>
        /// <remarks>
        /// Exposed for the same reason <see cref="TagFontSize"/> is. The resolution sweep used to
        /// carry its own copy of this number, and when the sizes were raised on 2026-08-16 the test
        /// kept computing with the old 14 -- so it was measuring a font the game had stopped using
        /// and could have passed while the real line overflowed by nearly half again.
        /// </remarks>
        /// <param name="scale">UI scale.</param>
        /// <returns>The font size.</returns>
        public static int HintSubFontSize(float scale)
        {
            // The floor is 11 and not 13, and the two pixels are load-bearing on a phone held
            // upright. Raising this line from 14 to 20 on 2026-08-16 took the floor from 9 to 13,
            // and at 360x780 the block is 344px against a longest line then wanting 400 -- so the
            // instruction the whole tutorial rests on overflowed on exactly the device most likely
            // to meet it. The stale copy of "14" in the resolution sweep hid it for the day.
            //
            // 11 is the largest floor the narrowest shipped size fits, measured rather than picked:
            // 55 characters at 0.55 of the font size is 333px into 344px. Everything at 800x480 and
            // above is far away from the floor and keeps the full 20 * scale the author asked for.
            return Mathf.Max(11, Mathf.RoundToInt(20f * scale));
        }

        /// <summary>Font size of the instruction's headline, in pixels.</summary>
        /// <param name="scale">UI scale.</param>
        /// <returns>The font size.</returns>
        public static int HintHeadlineFontSize(float scale)
        {
            return Mathf.Max(17, Mathf.RoundToInt(34f * scale));
        }

        /// <summary>Width of a world tag's box, in pixels.</summary>
        /// <param name="scale">UI scale.</param>
        /// <param name="screenWidth">Canvas width in pixels.</param>
        /// <returns>The box width.</returns>
        public static float TagWidth(float scale, float screenWidth)
        {
            // A FLOOR as well as a ceiling, for the same reason BlockWidth has one, and it is not
            // decoration: the font stops shrinking at 18px while a purely scaled box keeps going, so
            // on a narrow canvas the box outruns the text it has to hold.
            //
            // Measured across the six sizes the game ships at: a portrait phone gave a 124-134px box
            // for a label needing about 238px, so roughly HALF of "SLIME PIT - TAP TO SPAWN" was cut
            // off -- the label telling a new player what to tap first, on the form factor most likely
            // to meet it through the itch embed.
            //
            // Pre-existing rather than new. Doubling the tags on 2026-08-16 doubled the box with the
            // font and left the overflow ratio at 1.78x either way; what was new is that anything
            // measured it at all.
            float room = Mathf.Max(32f, screenWidth - 16f);
            return Mathf.Clamp(440f * scale, Mathf.Min(300f, room), room);
        }

        /// <summary>Font size a world tag is drawn at, in pixels.</summary>
        /// <param name="scale">UI scale.</param>
        /// <returns>The font size.</returns>
        public static int TagFontSize(float scale)
        {
            return Mathf.Max(18, Mathf.RoundToInt(24f * scale));
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
                float aboveVerbBar = Screen.height - GameController.VerbBarHeight - blockHeight - (6f * scale);
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
            float age = raid.Elapsed;
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
                fontSize = HintHeadlineFontSize(scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            var sub = new GUIStyle(headline)
            {
                fontSize = HintSubFontSize(scale)
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
                // Twice the old 12, at the author's request -- they were legible on a monitor and
                // not on a phone, which is where this game is played. The floor doubles with it,
                // because the itch embed runs at 0.4 scale and that is what the floor is for.
                fontSize = TagFontSize(scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            // Kept on screen, for the same reason the headline is: a spawner near the edge of the
            // frame would otherwise have half its label cut off, and half a label is worse than
            // none -- the player reads it as the game clipping.
            //
            // The box doubles with the text. Doubling the font alone would have kept the old 220
            // and quietly clipped "SLIME PIT - TAP TO SPAWN" instead, which is the same defect the
            // clamp above exists to prevent.
            float width = TagWidth(scale, Screen.width);
            float left = Mathf.Clamp(point.x - (width * 0.5f), 0f, Mathf.Max(0f, Screen.width - width));

            // Above the thing it names, except in the top third of the screen, where the headline
            // lives -- the opening chest sits on the room's top row and its label landed straight
            // across "DON'T KILL THE CHARGING TEAM". The lift grows with the box so a taller label
            // still clears the sprite it belongs to rather than sitting on it.
            float lift = point.y < Screen.height * 0.36f ? -44f * scale : 48f * scale;
            float top = Mathf.Clamp(point.y - lift, 0f, Screen.height - (40f * scale));

            var rect = new Rect(left, top, width, 40f * scale);

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
