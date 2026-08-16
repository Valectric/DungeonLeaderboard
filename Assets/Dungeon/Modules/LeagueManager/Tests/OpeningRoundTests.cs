using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.LeagueManager.Tests
{
    /// <summary>
    /// Measures what round one actually demands, which D20 has only ever described.
    /// </summary>
    /// <remarks>
    /// "Round one is sudden death" has been open since D20 as a worry rather than a number: everyone
    /// starts on zero, so one weak opening raid can put the player last with no banked score to
    /// absorb it. The levers offered were seeding a small opening score, exempting the first round, or
    /// leaving it as a sharp lesson — and choosing between them needs the threshold, which nobody had
    /// measured.
    /// <para>
    /// Observed in the shipped build first: doing nothing for sixty seconds harvests 51 and finishes
    /// 19th of 20, which ends the run. This finds where the line actually sits, across seeds, so the
    /// choice can be made against a figure instead of an impression.
    /// </para>
    /// </remarks>
    public sealed class OpeningRoundTests
    {
        /// <summary>Seeds swept, so one lucky table cannot answer for the rest.</summary>
        private static readonly int[] Seeds = { 12345, 777, 20260815, 4242, 99 };

        /// <summary>
        /// Whether banking this much in round one survives the first cut, on one seed.
        /// </summary>
        /// <param name="seed">Seed for the table.</param>
        /// <param name="harvested">What the player banks.</param>
        /// <returns>True when the player is clear of the drop.</returns>
        private static bool Survives(int seed, float harvested)
        {
            var table = new LeagueTable(seed);
            table.SubmitRaid(harvested);
            return !table.PlayerRelegated;
        }

        /// <summary>
        /// The smallest opening harvest that survives, found per seed and reported.
        /// </summary>
        /// <remarks>
        /// The assertion is deliberately loose — the interesting output is the printed threshold, not
        /// a bound. What it does pin is the shape the design depends on: <b>the opening board is worth
        /// far more than the line</b>, so a player who touches one spawner is never in danger, and
        /// only a player who does nothing at all goes out. If that stops being true the game has
        /// become a coin toss on its first screen, and this is what would say so.
        /// </remarks>
        [Test]
        public void TheOpeningRound_DemandsFarLessThanTheBoardIsWorth()
        {
            var thresholds = new List<int>();

            foreach (int seed in Seeds)
            {
                int threshold = -1;
                for (int harvest = 0; harvest <= 600; harvest += 5)
                {
                    if (Survives(seed, harvest))
                    {
                        threshold = harvest;
                        break;
                    }
                }

                thresholds.Add(threshold);
                MooseRunnerFacade.Log($"seed {seed}: survives round one from {threshold}");
            }

            int worst = 0;
            foreach (int t in thresholds)
            {
                worst = t > worst ? t : worst;
            }

            // Measured elsewhere and quoted here because the comparison is the whole point:
            // RoomsPayTests plays the shipped one-room opening at 342, and an untouched raid earns
            // about 51. The threshold sitting between those two is what makes the first round a
            // lesson rather than a lottery.
            MooseRunnerFacade.Log(
                $"round-one survival threshold: {string.Join(", ", thresholds)} "
                + $"(worst {worst}) against 51 for doing nothing and 342 for using the board");

            Assert.Less(worst, 342,
                $"round one demands {worst}, which is more than the opening board is worth (342), so "
                + "the first raid cannot be survived by playing it well");
        }

        /// <summary>
        /// An untouched dungeon is at real risk in round one, and a played one never is.
        /// </summary>
        /// <remarks>
        /// <b>Written the other way round first, and the measurement said no.</b> Having watched the
        /// shipped build bank 51 and finish 19th, this asserted that doing nothing never survives.
        /// It survives on <b>three of five seeds</b> — so that was one observation generalised into a
        /// rule, which is the mistake this project spent a day on.
        /// <para>
        /// What is true is narrower and still worth pinning: the idle rate is a gamble and the board
        /// is not. D20's "round one is sudden death" is therefore half right — it is not death for
        /// anyone who touches a spawner, because the threshold tops out at 75 against a board worth
        /// 342, and it is not reliably a lesson either, because doing nothing gets away with it more
        /// often than not.
        /// </para>
        /// </remarks>
        [Test]
        public void AnUntouchedDungeon_IsAGambleAndAPlayedOneIsNot()
        {
            var idleSurvived = new List<int>();
            var playedSurvived = new List<int>();

            foreach (int seed in Seeds)
            {
                if (Survives(seed, 51f))
                {
                    idleSurvived.Add(seed);
                }

                // What RoomsPayTests measures the shipped one-room opening at, played competently.
                if (Survives(seed, 342f))
                {
                    playedSurvived.Add(seed);
                }
            }

            MooseRunnerFacade.Log(
                $"round one: idle (51) survives {idleSurvived.Count} of {Seeds.Length} seeds, "
                + $"played (342) survives {playedSurvived.Count} of {Seeds.Length}");

            Assert.AreEqual(Seeds.Length, playedSurvived.Count,
                "using the opening board did not survive round one on every seed, so the first raid "
                + "is a lottery rather than a lesson");

            Assert.Less(idleSurvived.Count, Seeds.Length,
                "doing nothing for sixty seconds survived round one on every seed, so the opening "
                + "round no longer teaches that an untouched dungeon earns almost nothing");
        }
    }
}
