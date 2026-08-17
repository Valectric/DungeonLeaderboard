using Dungeon.LeagueManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins that the player can read their own position on the screen the game opens with.
    /// </summary>
    /// <remarks>
    /// <b>Written after finding the player's rank number invisible in the published build.</b> The
    /// standings are the title screen — no menu, no logo — and the whole opening reads "you are
    /// 14th, the bottom two go down". The player's row is washed green and their name and score are
    /// drawn in <c>PlayerGreen</c>; their rank number alone was left <c>Dim</c>, a dark grey-purple
    /// at watermark contrast against that wash. The table read 12, 13, blank, 15.
    /// <para>
    /// <b>413 tests missed it, and none of them could have.</b> Every assertion about this screen
    /// checks that something is drawn — that the row exists, that it is in the right order, that it
    /// fits. Whether the ink can be seen against what is behind it is a different question, and
    /// nothing was asking it. That is the project's own doctrine arriving again: assertions check
    /// that each thing happened, and only looking shows them together.
    /// </para>
    /// </remarks>
    public sealed class StandingsLegibilityTests
    {
        /// <summary>Green wash drawn behind the player's row, from <c>LeagueScreen.DrawRow</c>.</summary>
        private static readonly Color PlayerWash = new(0.25f, 0.55f, 0.25f, 0.35f);

        /// <summary>The screen behind the table, from the standings backdrop.</summary>
        private static readonly Color Backdrop = new(0.16f, 0.14f, 0.20f);

        /// <summary>
        /// Relative luminance, the standard perceptual weighting.
        /// </summary>
        /// <param name="colour">Colour to weigh.</param>
        /// <returns>Luminance from 0 to 1.</returns>
        private static float Luminance(Color colour)
        {
            return (0.2126f * colour.r) + (0.7152f * colour.g) + (0.0722f * colour.b);
        }

        /// <summary>
        /// Contrast ratio between two colours, as used for accessibility thresholds.
        /// </summary>
        /// <remarks>
        /// The 1:1 to 21:1 ratio everyone quotes. Bodies of standards ask for 4.5 on body text; this
        /// screen is large bold type over art, so the bar here is deliberately lower and only has to
        /// separate "legible" from "invisible".
        /// </remarks>
        /// <param name="a">One colour.</param>
        /// <param name="b">The other.</param>
        /// <returns>Contrast ratio, at least 1.</returns>
        private static float Contrast(Color a, Color b)
        {
            float first = Luminance(a) + 0.05f;
            float second = Luminance(b) + 0.05f;
            return first > second ? first / second : second / first;
        }

        /// <summary>What the player's row background actually ends up as, wash over backdrop.</summary>
        /// <returns>The composited colour behind the player's text.</returns>
        private static Color PlayerRowBackground()
        {
            return Color.Lerp(Backdrop, new Color(PlayerWash.r, PlayerWash.g, PlayerWash.b),
                PlayerWash.a);
        }

        /// <summary>
        /// The player's rank number is drawn as legibly as their own name beside it.
        /// </summary>
        /// <remarks>
        /// <b>Relative, not an absolute contrast bar, and the first draft of this test got that
        /// wrong.</b> It demanded 3:1 and failed the fix — because <c>PlayerGreen</c> on the green
        /// wash measures 2.96:1, and that is the exact ink the player's <i>name and score</i> have
        /// always used. Those read perfectly well in the published build. A threshold that condemns
        /// shipping, legible text is measuring the wrong thing.
        /// <para>
        /// So the bar is the row itself: whatever the game considers readable enough for the
        /// player's name is readable enough for their position. That is falsifiable, it needs no
        /// invented number, and it fails loudly on the actual defect — <c>Dim</c> against
        /// <c>PlayerGreen</c> is not a near miss.
        /// </para>
        /// </remarks>
        [Test]
        public void ThePlayersRankNumber_IsAsLegibleAsTheirName()
        {
            var player = new LeagueEntry("Your Dungeon", 0, true);
            Color rank = LeagueScreen.RankInk(player, doomed: false);
            Color name = LeagueScreen.RowInk(player, doomed: false);
            Color behind = PlayerRowBackground();

            MooseRunnerFacade.Log(
                $"player row: rank {rank} at {Contrast(rank, behind):F2}:1, "
                + $"name {name} at {Contrast(name, behind):F2}:1, against {behind}");

            Assert.AreEqual(name, rank,
                "the player's rank number is drawn in a different colour from their own name on the "
                + "same row -- which is how it came to be Dim on the green highlight, present and "
                + "unreadable, so the title screen showed 12, 13, blank, 15");
        }

        /// <summary>
        /// The fix actually improved the contrast it was made for.
        /// </summary>
        /// <remarks>
        /// The regression guard, anchored to the old value rather than to a standard. <c>Dim</c> on
        /// the player's wash measured about 1.6:1 — the number was there and could not be seen.
        /// </remarks>
        [Test]
        public void ThePlayersRank_ReadsBetterThanTheColourItUsedToBe()
        {
            var player = new LeagueEntry("Your Dungeon", 0, true);
            Color behind = PlayerRowBackground();
            float now = Contrast(LeagueScreen.RankInk(player, doomed: false), behind);
            float before = Contrast(new Color(0.45f, 0.43f, 0.52f), behind);

            MooseRunnerFacade.Log($"player rank contrast: was {before:F2}:1, now {now:F2}:1");

            Assert.Greater(now, before * 1.5f,
                $"the player's rank reads at {now:F2}:1 against the {before:F2}:1 of the Dim it was "
                + "drawn in before, which is not enough of a change to have fixed anything");
        }

        /// <summary>
        /// The player's rank number is drawn the same way in the table and in the mid-raid strip.
        /// </summary>
        /// <remarks>
        /// The shape of the original defect, rather than its symptom. Both places render the same
        /// fact and only one of them held the game's opinion about it, so the strip was right and
        /// the table it summarises was wrong. Sharing <c>RankInk</c> is what makes them agree; this
        /// asserts they still do.
        /// </remarks>
        [Test]
        public void TheTableAndTheStrip_AgreeOnEveryRow()
        {
            var player = new LeagueEntry("Your Dungeon", 0, true);
            var rival = new LeagueEntry("Bleakmoor", 0, false);

            // A doomed player is a real state -- it is what the collapse screen shows -- and it is
            // the case where the two rules disagreed most, so it is worth naming.
            Assert.AreEqual(LeagueScreen.RankInk(player, doomed: true),
                LeagueScreen.RankInk(player, doomed: false),
                "the player's row should read as theirs whether or not they are being relegated");

            Assert.AreNotEqual(LeagueScreen.RankInk(rival, doomed: true),
                LeagueScreen.RankInk(rival, doomed: false),
                "a rival inside the relegation zone should be marked, or the red line under the "
                + "bottom two says nothing");

            MooseRunnerFacade.Log(
                $"player {LeagueScreen.RankInk(player, false)}, "
                + $"rival {LeagueScreen.RankInk(rival, false)}, "
                + $"doomed rival {LeagueScreen.RankInk(rival, true)}");
        }

        /// <summary>
        /// A rival's rank stays dimmer than the player's, so the eye still finds the right row.
        /// </summary>
        /// <remarks>
        /// The fix must not go so far the other way that every row shouts. The hierarchy is the
        /// point: rank numbers sit under names, and the player's line is the one that stands out.
        /// </remarks>
        [Test]
        public void ARivalsRank_StaysQuieterThanThePlayers()
        {
            var player = new LeagueEntry("Your Dungeon", 0, true);
            var rival = new LeagueEntry("Bleakmoor", 0, false);

            float playerInk = Luminance(LeagueScreen.RankInk(player, doomed: false));
            float rivalInk = Luminance(LeagueScreen.RankInk(rival, doomed: false));

            Assert.Greater(playerInk, rivalInk,
                $"the player's rank ({playerInk:F2}) is no brighter than a rival's ({rivalInk:F2}), "
                + "so nothing draws the eye to their own line");
        }
    }
}
