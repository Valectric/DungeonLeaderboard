using UnityEngine;

namespace Dungeon.RaidManager
{
    /// <summary>
    /// What the adventurers thought of the dungeon, out of five stars.
    /// </summary>
    /// <remarks>
    /// This is the game's teaching moment, and the reason it works is that the adventurers' taste and
    /// the player's interest point the same way. A party wants a <i>thrilling</i> dungeon -- a near
    /// death, a desperate retreat, a story to tell -- and that is precisely the party that has been
    /// alive, engaged and badly wounded for a full sixty seconds, which is the maximum payout. So
    /// five stars and a big number always arrive together.
    /// <para>
    /// It also delivers the lesson the design most needs a new player to learn, in the voice of the
    /// people it happened to: <b>kill the party and the review is one star</b>, because corpses do
    /// not recommend anything. SPEC.md's central inversion, explained by the victims.
    /// </para>
    /// </remarks>
    public sealed class RaidReview
    {
        /// <summary>Stars awarded, 1 to 5.</summary>
        public int Stars { get; }

        /// <summary>The review headline.</summary>
        public string Headline { get; }

        /// <summary>The body of the review, in an adventurer's voice.</summary>
        public string Quip { get; }

        /// <summary>What the player should take from it, in the game's own voice.</summary>
        public string Lesson { get; }

        /// <summary>Creates a review.</summary>
        /// <param name="stars">Stars awarded.</param>
        /// <param name="headline">Headline.</param>
        /// <param name="quip">Review body.</param>
        /// <param name="lesson">Advice for the player.</param>
        public RaidReview(int stars, string headline, string quip, string lesson)
        {
            Stars = stars;
            Headline = headline;
            Quip = quip;
            Lesson = lesson;
        }

        /// <summary>
        /// Judges a finished raid.
        /// </summary>
        /// <param name="outcome">How the raid ended.</param>
        /// <param name="harvested">Energy taken from the party. This is the score.</param>
        /// <param name="survivors">How many adventurers walked out.</param>
        /// <param name="partySize">How many walked in, so a wipe can be counted correctly.</param>
        /// <returns>The review.</returns>
        public static RaidReview For(
            RaidOutcome outcome, float harvested, int survivors, int partySize = 4)
        {
            // A wipe is the worst outcome in the design and it is always the player's fault, so it
            // overrides the takings entirely. A dungeon can have a magnificent minute and still ruin
            // it in the last two seconds by killing the party, and the review has to say so.
            if (outcome == RaidOutcome.PartyWiped || survivors == 0)
            {
                // Counted, not assumed. This line read "four death notices" until 2026-08-16,
                // which was true for every party the game could send until the league started
                // growing them -- from raid six a wipe is five notices, and from raid eighteen it
                // is nine. A screen that miscounts the bodies it is reporting on is worse than one
                // that does not count them at all.
                return new RaidReview(1,
                    "NOBODY CAME BACK",
                    $"\"...\"  -- the guild, filing {Spell(partySize)} death notices",
                    "DEAD ADVENTURERS PAY NOTHING.  WOUND THEM, DO NOT KILL THEM.");
            }

            if (outcome == RaidOutcome.PartyEscaped)
            {
                return harvested < 40f
                    ? new RaidReview(2,
                        "WALKED STRAIGHT THROUGH",
                        "\"Found the boss room in under a minute. Barely scratched. Bit of a letdown.\"",
                        "THEY LEFT EARLY AND STOPPED PAYING.  SHUT A DOOR IN FRONT OF THEM.")
                    : new RaidReview(3,
                        "GOOD, BUT SHORT",
                        "\"Solid fight in the second room, then we found the exit. Wanted more.\"",
                        "A PARTY THAT REACHES THE BOSS ROOM STOPS EARNING.  STALL THEM LONGER.");
            }

            // Survived the full minute. Now the takings decide, because the takings are the whole
            // measure of how well they were kept in danger without being killed.
            if (harvested >= 320f)
            {
                return new RaidReview(5,
                    "THE BEST NIGHT OF OUR LIVES",
                    "\"I have never been so frightened. Twice I was certain we were finished. "
                    + "We will absolutely be back.\"",
                    "PERFECT.  ALIVE, IN COMBAT, AND NEARLY DEAD IS WHERE THE MONEY IS.");
            }

            if (harvested >= 160f)
            {
                return new RaidReview(4,
                    "GENUINELY TERRIFYING",
                    "\"Lost a lot of blood in there. Cannot stop thinking about it. "
                    + "Highly recommended.\"",
                    "STRONG.  HOLD THEM IN THE FIGHT LONGER AND LET THEM GET WORSE.");
            }

            if (harvested >= 70f)
            {
                return new RaidReview(3,
                    "A DECENT AFTERNOON",
                    "\"Reasonable monsters, one nasty trap. Nothing we could not handle.\"",
                    "COMFORTABLE IS CHEAP.  THE LAST SLIVER OF A HEALTH BAR PAYS THE MOST.");
            }

            return new RaidReview(2,
                "QUIET, FRANKLY",
                "\"We wandered around for a minute. Somebody opened a door. That was it.\"",
                "AN UNENGAGED PARTY EARNS ALMOST NOTHING.  AMBUSH THEM.");
        }

        /// <summary>The stars as text, for a UI with no star glyph in its font.</summary>
        /// <remarks>
        /// Asterisks rather than a star character on purpose. The built-in font renders many
        /// non-ASCII glyphs as invisible gaps in a WebGL build -- em-dashes and middots already did
        /// exactly that on this project's standings screen.
        /// </remarks>
        /// <returns>Five slots, filled and unfilled.</returns>
        public string StarBar()
        {
            var bar = new System.Text.StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                bar.Append(i < Stars ? "*" : ".");
                if (i < 4)
                {
                    bar.Append(' ');
                }
            }

            return bar.ToString();
        }

        /// <summary>Colour for the star row, warming as the rating climbs.</summary>
        /// <returns>The colour.</returns>
        public Color Tint()
        {
            return Stars >= 5 ? new Color(1f, 0.85f, 0.35f)
                : Stars >= 4 ? new Color(0.95f, 0.75f, 0.4f)
                : Stars >= 3 ? new Color(0.8f, 0.75f, 0.85f)
                : new Color(0.9f, 0.4f, 0.4f);
        }

        /// <summary>Spells a party size, because "filing 9 death notices" reads as a receipt.</summary>
        /// <param name="count">How many bodies.</param>
        /// <returns>The word, or the numeral for anything outside the party sizes the game sends.</returns>
        private static string Spell(int count)
        {
            return count switch
            {
                1 => "one",
                2 => "two",
                3 => "three",
                4 => "four",
                5 => "five",
                6 => "six",
                7 => "seven",
                8 => "eight",
                9 => "nine",
                _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

    }
}
