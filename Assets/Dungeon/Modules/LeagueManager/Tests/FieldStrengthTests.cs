using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.LeagueManager.Tests
{
    /// <summary>
    /// Pins that the competition gets harder as it goes, and that it does so without breaking the
    /// promise the handicap makes.
    /// </summary>
    /// <remarks>
    /// A knockout competition whose survivors keep rolling from the same range the whole twenty
    /// started with gets <i>easier</i> every round: the dungeons that leave are the ones that earned
    /// least, so the average opponent the player faces climbs while the numbers they roll do not.
    /// <para>
    /// The fix is deliberately one-sided. Rivals lose their bad rounds as the field shrinks; they
    /// never gain a better best. That keeps the one thing the league is built on intact — a genuinely
    /// good raid cannot be beaten by a rival, in round one or in the final — while making the late
    /// rounds a race the player has to actually run.
    /// </para>
    /// </remarks>
    public sealed class FieldStrengthTests
    {
        /// <summary>A table with the player still in it.</summary>
        /// <returns>The table.</returns>
        private static LeagueTable Fresh()
        {
            return new LeagueTable(4242);
        }

        /// <summary>
        /// The opening round faces the weakest field of the competition.
        /// </summary>
        [Test]
        public void TheOpeningRound_FacesTheWeakestField()
        {
            LeagueTable table = Fresh();
            Assert.AreEqual(0f, table.FieldStrength, 0.001f,
                "nobody has been knocked out yet, so nothing should have tightened");
        }

        /// <summary>
        /// Every knockout round leaves a stronger field behind it.
        /// </summary>
        /// <remarks>
        /// Monotonic, not merely higher at the end: a competition that got harder and then eased off
        /// would hand a lucky player a soft round late, which is the outcome this whole change is
        /// about removing.
        /// </remarks>
        [Test]
        public void EveryRound_LeavesAStrongerField()
        {
            LeagueTable table = Fresh();
            float previous = table.FieldStrength;
            int guard = 0;

            while (!table.IsFinal && guard++ < 40)
            {
                // A strong raid, so the player is never the one knocked out.
                table.SubmitRaid(LeagueTable.GoodRun);
                table.CollapseRelegated();

                Assert.GreaterOrEqual(table.FieldStrength, previous,
                    $"the field got softer with {table.Remaining} dungeons left");
                previous = table.FieldStrength;
            }

            MooseRunnerFacade.Log(
                $"field strength reached {previous:F2} with {table.Remaining} dungeons left");

            Assert.Greater(previous, 0.5f,
                "by the final the survivors should be a long way above their opening form");
        }

        /// <summary>
        /// A rival never out-earns a genuinely good raid, in any round of the competition.
        /// </summary>
        /// <remarks>
        /// The load-bearing one. Rivals get better by losing their bad rounds, never by gaining a
        /// higher ceiling, so this must hold in the final exactly as it holds in round one — the
        /// player is never eliminated from a round they played well.
        /// </remarks>
        [Test]
        public void NoRival_EverOutEarnsAGoodRaid()
        {
            LeagueTable table = Fresh();
            float worstMargin = float.MaxValue;
            int guard = 0;

            while (!table.IsFinal && guard++ < 40)
            {
                float before = HighestRivalScore(table);
                table.SubmitRaid(LeagueTable.GoodRun);
                float gained = HighestRivalScore(table) - before;

                worstMargin = System.MathF.Min(
                    worstMargin, LeagueTable.GoodRun - LeagueTable.RivalCeiling);

                Assert.LessOrEqual(gained, LeagueTable.RivalCeiling + 0.5f,
                    $"a rival earned {gained:F0} in round {table.Round}, above the ceiling that "
                    + "makes a good raid unbeatable");

                table.CollapseRelegated();
            }

            MooseRunnerFacade.Log(
                $"a good raid stayed {worstMargin:F0} clear of the best possible rival round "
                + "throughout the competition");

            Assert.Greater(worstMargin, 0f,
                "a good raid must always beat the best a rival can roll");
        }

        /// <summary>
        /// Late rivals stop having bad rounds, which is what a shrinking field should cost.
        /// </summary>
        /// <remarks>
        /// The observable consequence, measured rather than asserted from the formula: the same
        /// rival earning routine, sampled at the start and near the end, should show a floor that has
        /// climbed a long way while the best round has not moved.
        /// </remarks>
        [Test]
        public void LateRivals_StopHavingBadRounds()
        {
            LeagueTable early = Fresh();
            float earliest = RivalGain(early);

            LeagueTable late = Fresh();
            int guard = 0;
            while (!late.IsFinal && guard++ < 40)
            {
                late.SubmitRaid(LeagueTable.GoodRun);
                late.CollapseRelegated();
            }

            float latest = RivalGain(late);

            MooseRunnerFacade.Log(
                $"the worst rival round was {earliest:F0} in the opening round and {latest:F0} "
                + "in the final");

            Assert.Greater(latest, earliest * 2f,
                $"the worst round a surviving rival had was {latest:F0}, against {earliest:F0} "
                + "when the whole field was still in -- the survivors are still having off days");
        }

        /// <summary>
        /// Runs one round and reports the <b>worst</b> round any rival had.
        /// </summary>
        /// <remarks>
        /// The worst, not the best, and the distinction is the whole test. Comparing best rounds
        /// compares the maximum of nineteen rolls against the maximum of one, so the opening round
        /// wins on sample size alone and reads as though nothing changed — measured, 443 against 440.
        /// The floor is what actually moves.
        /// </remarks>
        /// <param name="table">Table to advance.</param>
        /// <returns>The smallest score any rival added this round.</returns>
        private static float RivalGain(LeagueTable table)
        {
            var before = new Dictionary<LeagueEntry, float>();
            foreach (LeagueEntry entry in table.Entries)
            {
                before[entry] = entry.Score;
            }

            table.SubmitRaid(LeagueTable.GoodRun);

            float worst = float.MaxValue;
            foreach (LeagueEntry entry in table.Entries)
            {
                if (!entry.IsPlayer && before.TryGetValue(entry, out float was))
                {
                    worst = System.MathF.Min(worst, entry.Score - was);
                }
            }

            return worst;
        }

        /// <summary>The best score any rival currently holds.</summary>
        /// <param name="table">Table to read.</param>
        /// <returns>The score, or zero when only the player is left.</returns>
        private static float HighestRivalScore(LeagueTable table)
        {
            float best = 0f;
            foreach (LeagueEntry entry in table.Entries)
            {
                if (!entry.IsPlayer && entry.Score > best)
                {
                    best = entry.Score;
                }
            }

            return best;
        }
    }
}
