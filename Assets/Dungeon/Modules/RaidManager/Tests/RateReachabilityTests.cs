using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Pins how much of the energy curve each roster can actually reach.
    /// </summary>
    /// <remarks>
    /// The project's verification doctrine names this exact gap: <i>"green unit tests hide a broken
    /// rate"</i>. <see cref="EnergyCurveTests"/> proves the curve has the right shape, and
    /// <see cref="ExploratoryTests"/> proves the rate never goes NaN or absurdly high — but nothing
    /// asserted that the steep end is <b>reachable</b>, which is precisely how D12 shipped a game
    /// whose rate never passed 4.1/s in a design built to reach 32.
    /// <para>
    /// These are characterisation tests. They pin what the game currently does, generously enough to
    /// survive tuning, so that a change which flattens the curve again fails here instead of being
    /// discovered by a player.
    /// </para>
    /// </remarks>
    public sealed class RateReachabilityTests
    {
        /// <summary>What one roster managed over a raid played with unlimited spawning.</summary>
        private struct Reach
        {
            /// <summary>Highest rate the raid ever showed.</summary>
            public float PeakRate;

            /// <summary>Health fraction of the worst-off survivor, at its lowest.</summary>
            public float DeepestWound;

            /// <summary>Fraction of ticks the party was in combat.</summary>
            public float EngagedShare;

            /// <summary>Monsters the player could afford across the whole raid.</summary>
            public int MobsAfforded;
        }

        /// <summary>
        /// Plays a roster with the spawn verb pressed at every spawner every tick.
        /// </summary>
        /// <remarks>
        /// Deliberately the most aggressive play possible, because the question is what the curve
        /// <i>can</i> reach rather than what a moderate player reaches. Spawning is still gated by
        /// energy, so this measures the ceiling the economy allows, not an infinite one.
        /// </remarks>
        /// <param name="composition">Roster to play.</param>
        /// <returns>What it reached.</returns>
        private static Reach Play(PartyComposition composition)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, composition, 909);

            var reach = new Reach { DeepestWound = 1f };
            int ticks = 0;
            int engaged = 0;

            while (raid.IsRunning && ticks++ < 5000)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    raid.SpawnMob(spawner);
                }

                raid.Tick(0.02f);

                reach.PeakRate = Mathf.Max(reach.PeakRate, raid.CurrentRate);
                if (raid.Party.LivingCount > 0)
                {
                    reach.DeepestWound = Mathf.Min(reach.DeepestWound, raid.Party.WoundFraction);
                }

                if (raid.Party.Goal == PartyGoal.Fighting)
                {
                    engaged++;
                }
            }

            reach.EngagedShare = engaged / (float)Mathf.Max(1, ticks);
            reach.MobsAfforded = raid.Mobs.Mobs.Count;
            return reach;
        }

        /// <summary>
        /// The steep end of the curve is reachable by at least one roster.
        /// </summary>
        /// <remarks>
        /// SPEC.md sizes the curve so a party at 5% health earns eight times what a healthy one does,
        /// and the HUD is built around a large pulsing number. If nothing ever reaches that end, the
        /// curve is decoration and the game is a flat trickle — which is exactly the state D12 found
        /// and fixed.
        /// </remarks>
        [Test]
        public void TheSteepEndOfTheCurve_IsReachable()
        {
            float best = 0f;
            string bestRoster = "nobody";

            foreach (PartyComposition composition in PartyComposition.All)
            {
                Reach reach = Play(composition);
                if (reach.PeakRate > best)
                {
                    best = reach.PeakRate;
                    bestRoster = composition.Name;
                }
            }

            MooseRunnerFacade.Log($"best reachable rate {best:F1}/s, by {bestRoster}");
            Assert.Greater(best, 20f,
                $"the best any roster managed was {best:F1}/s. The curve is built to reach the "
                + "high twenties and beyond; if nothing gets there it is decoration.");
        }

        /// <summary>
        /// Every roster earns more than an empty corridor, and the spread between them is bounded.
        /// </summary>
        /// <remarks>
        /// <b>This test documents a known problem rather than forbidding it.</b> Measured today, the
        /// two tankless rosters — THE GLASS CANNONS and THE SKIRMISHERS — peak at <b>4.1/s</b> while
        /// the rest reach 26 to 38. That is a ninefold spread decided by a roll the player does not
        /// make, on a third of all raids.
        /// <para>
        /// The mechanism is a feedback loop, not a constant. A fragile party kills monsters before
        /// they land enough blows to wound it, so its worst survivor stays around 76% health and the
        /// wound multiplier stays near 1. The rate stays near idle; spawning is gated only by energy,
        /// so the player can afford just <b>five monsters in a whole raid</b> against nine to eleven
        /// for the other rosters — and with no monsters in the room the party is scored as walking a
        /// corridor. Less income buys fewer monsters, which earns less income.
        /// </para>
        /// <para>
        /// Every way out of it is a balance decision the author owns: cheaper spawns, a floor on
        /// income, sturdier monsters against fragile parties, or accepting that a party which cannot
        /// be hurt is a poor customer. So this pins the current spread generously instead of
        /// asserting a target nobody has chosen.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryRoster_EarnsMoreThanAnEmptyCorridor()
        {
            float worst = float.MaxValue;
            float best = 0f;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                Reach reach = Play(composition);
                worst = Mathf.Min(worst, reach.PeakRate);
                best = Mathf.Max(best, reach.PeakRate);

                MooseRunnerFacade.Log(
                    $"{composition.Name}: peak {reach.PeakRate:F1}/s, deepest wound "
                    + $"{reach.DeepestWound:F2}, engaged {reach.EngagedShare * 100f:F0}%, "
                    + $"{reach.MobsAfforded} monsters afforded");

                // The ruler is the rate of a party that is ACTUALLY walking a corridor, summed the
                // way the game sums it. The old one -- BaseRate * IdleEngagement = 0.05/s -- was a
                // relic of the party-wide curve and roughly five times too weak: four members
                // standing still already earn 4 x 0.04 = 0.16/s, and four walking earn 0.24/s. A
                // roster could have been earning a third of what an untouched corridor pays and
                // still cleared it.
                float walkingCorridor =
                    4f * EnergyCurve.MemberRate(AdventurerAction.Walking, 1f);

                Assert.Greater(reach.PeakRate, walkingCorridor,
                    $"{composition.Name} peaked at {reach.PeakRate:F2}/s, which is no better than "
                    + $"the {walkingCorridor:F2}/s four members earn just walking a corridor");
            }

            MooseRunnerFacade.Log($"roster spread: worst {worst:F1}/s, best {best:F1}/s, "
                                  + $"ratio {best / Mathf.Max(0.01f, worst):F1}x");

            // Pinned at the measured 9x rather than at a target, so this catches the spread getting
            // worse without pretending the current figure is the intended one.
            Assert.Less(best / Mathf.Max(0.01f, worst), 14f,
                $"the gap between the best and worst roster grew to {best / worst:F1}x. Which "
                + "party walks in is not the player's choice, so a widening spread means more raids "
                + "decided before they start.");
        }

        /// <summary>
        /// A party that never gets hurt is the one that earns least, which is the design working.
        /// </summary>
        /// <remarks>
        /// The inverse check on the loop above, and the reason the spread is not simply a bug: the
        /// whole game pays for wounded survivors, so a roster that ends a raid at three-quarters
        /// health <i>should</i> be a poor customer. Asserting the correlation makes the tankless
        /// problem legible as a consequence of the design rather than an unexplained number.
        /// </remarks>
        [Test]
        public void TheLeastWoundedRoster_IsTheWorstPaying()
        {
            float leastWoundedDepth = 0f;
            float leastWoundedRate = 0f;
            float mostWoundedDepth = 1f;
            float mostWoundedRate = 0f;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                Reach reach = Play(composition);

                if (reach.DeepestWound > leastWoundedDepth)
                {
                    leastWoundedDepth = reach.DeepestWound;
                    leastWoundedRate = reach.PeakRate;
                }

                if (reach.DeepestWound < mostWoundedDepth)
                {
                    mostWoundedDepth = reach.DeepestWound;
                    mostWoundedRate = reach.PeakRate;
                }
            }

            MooseRunnerFacade.Log(
                $"least wounded roster bottomed at {leastWoundedDepth:F2} health and peaked at "
                + $"{leastWoundedRate:F1}/s; most wounded bottomed at {mostWoundedDepth:F2} and "
                + $"peaked at {mostWoundedRate:F1}/s");

            Assert.Greater(mostWoundedRate, leastWoundedRate,
                "the roster that was hurt worst did not earn most, which inverts the wound curve "
                + "the whole game is built on");
        }
    }
}
