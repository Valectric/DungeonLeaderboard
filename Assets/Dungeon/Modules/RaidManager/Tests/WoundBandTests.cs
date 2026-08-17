using System.Collections.Generic;
using System.Linq;
using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Measures whether the party ever reaches the health where the wound curve pays.
    /// </summary>
    /// <remarks>
    /// The diagnostic behind <c>GreedCurveTests</c>. That one found the game's decision to be real
    /// but its top end flat: the best policy banks 510 and the most timid banks 482, six percent
    /// apart, with nobody dying. CLAUDE.md says the opposite should be true — <i>"most of the money
    /// is in the last sliver of a health bar. That is the game."</i>
    /// <para>
    /// There are only two ways that can both be so. Either the multiplier is too shallow where the
    /// party actually lives, or the party never gets to where it is steep. This measures which, by
    /// counting member-time in health bands and weighting each by what
    /// <c>EnergyCurve.WoundMultiplier</c> pays there.
    /// </para>
    /// <para>
    /// It changes no balance. Which dial to turn — if any — is the author's, and the point of a
    /// diagnostic is to hand them the mechanism rather than a guess.
    /// </para>
    /// <para>
    /// <b>Measured 2026-08-17, at the policy that earns most, six seeds:</b>
    /// </para>
    /// <code>
    /// 100-80%   48.3% of time   pays 1.00x    32.1% of the earning
    ///  80-60%   19.4%           pays 1.02x    13.2%
    ///  60-40%   19.3%           pays 1.28x    16.4%
    ///  40-20%    7.7%           pays 2.51x    12.9%
    ///  20-5%     2.8%           pays 5.62x    10.5%
    ///    &lt;5%     2.5%           pays 8.93x    14.8%
    /// </code>
    /// <para>
    /// <b>The curve is not the problem.</b> A third of member-time sits below 60% health and produces
    /// <b>55% of the earning</b>; the bottom two bands are 5% of the time and a quarter of the money.
    /// CLAUDE.md's claim that the money lives in the last sliver of a health bar is true as measured.
    /// </para>
    /// <para>
    /// <b>So the flat top of the greed curve has a different cause, and this is it: the cease-fire
    /// controls SPAWNING, not damage.</b> A timid player stops adding monsters, but the ones already
    /// in the room keep swinging — so the party still falls through the paying bands and still
    /// collects the multiplier, while the deaths that come from over-committing are avoided
    /// entirely. Caution captures most of the upside because the upside is <i>already in flight</i>
    /// by the time the player decides to stop.
    /// </para>
    /// <para>
    /// That is the mechanism behind timidity banking 482 against a peak of 510 with nobody dying. If
    /// the author wants precision to pay, the lever is the marginal value of one more spawn against
    /// its death risk — not the steepness of the wound curve, which is already doing its job.
    /// </para>
    /// </remarks>
    public sealed class WoundBandTests
    {
        /// <summary>Health bands, from untouched to nearly dead.</summary>
        private static readonly (string Name, float Low, float High)[] Bands =
        {
            ("100-80%", 0.80f, 1.01f),
            (" 80-60%", 0.60f, 0.80f),
            (" 60-40%", 0.40f, 0.60f),
            (" 40-20%", 0.20f, 0.40f),
            (" 20-5%", 0.05f, 0.20f),
            ("  <5%", 0f, 0.05f)
        };

        /// <summary>
        /// Plays a raid at the policy that earns most, counting member-time by health band.
        /// </summary>
        /// <param name="seed">Seed for the party and combat rolls.</param>
        /// <returns>Member-ticks in each band, indexed as <see cref="Bands"/>.</returns>
        private static int[] Play(int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 3, extraSkeletonSpawners: 3, extraSlimeSpawners: 3);

            var raid = new Raid(layout, 0f, PartyComposition.Opening, seed);
            var tally = new int[Bands.Length];

            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                // The peak policy from GreedCurveTests, so this describes the health a player who is
                // playing WELL actually sees, not one who is being careless.
                bool press = raid.Party.WoundFraction > 0.5f;
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (press && raid.TotalEnergy > Raid.SpawnCost * 2f &&
                        raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) < 3)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);

                foreach (Adventurer member in raid.Party.Living)
                {
                    for (int i = 0; i < Bands.Length; i++)
                    {
                        if (member.HealthFraction >= Bands[i].Low &&
                            member.HealthFraction < Bands[i].High)
                        {
                            tally[i]++;
                            break;
                        }
                    }
                }
            }

            return tally;
        }

        /// <summary>
        /// Reports where the party's time is spent, and how much of the earning it produces.
        /// </summary>
        /// <remarks>
        /// The assertion is deliberately weak — some time below half health — because the figure the
        /// author needs is the distribution, not a pass mark. A party that never drops below half is
        /// one for whom the wound curve does not exist, however steep it is on paper.
        /// </remarks>
        [Test]
        public void TheWoundCurve_IsReachedInPractice()
        {
            var totals = new int[Bands.Length];
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
            {
                int[] one = Play(seed);
                for (int i = 0; i < totals.Length; i++)
                {
                    totals[i] += one[i];
                }
            }

            int all = totals.Sum();
            Assert.Greater(all, 0, "no member-time was recorded at all");

            // What each band pays, at the midpoint of its range, so the time can be weighted by it.
            var earned = new double[Bands.Length];
            for (int i = 0; i < Bands.Length; i++)
            {
                float middle = (Bands[i].Low + Mathf.Min(1f, Bands[i].High)) * 0.5f;
                earned[i] = totals[i] * EnergyCurve.WoundMultiplier(middle);
            }

            double allEarned = earned.Sum();

            for (int i = 0; i < Bands.Length; i++)
            {
                MooseRunnerFacade.Log(
                    $"{Bands[i].Name}: {totals[i] / (float)all,6:P1} of member-time, "
                    + $"pays {EnergyCurve.WoundMultiplier((Bands[i].Low + Mathf.Min(1f, Bands[i].High)) * 0.5f):F2}x, "
                    + $"{earned[i] / allEarned,6:P1} of the earning");
            }

            float belowHalf = (totals[2] + totals[3] + totals[4] + totals[5]) / (float)all;
            double earnedBelowHalf = (earned[2] + earned[3] + earned[4] + earned[5]) / allEarned;

            MooseRunnerFacade.Log(
                $"below 60% health: {belowHalf:P1} of member-time producing {earnedBelowHalf:P1} "
                + "of the earning");

            Assert.Greater(belowHalf, 0.05f,
                $"the party spends only {belowHalf:P1} of its time below 60% health, so the wound "
                + "curve is a multiplier on a state the game does not actually reach -- which is why "
                + "playing precisely pays almost nothing over playing safe");
        }
    }
}
