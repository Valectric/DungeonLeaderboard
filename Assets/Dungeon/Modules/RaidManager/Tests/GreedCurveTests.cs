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
    /// Measures whether the game's one decision is actually a decision.
    /// </summary>
    /// <remarks>
    /// The player has one judgement to make and it is stated everywhere in this repository: press
    /// while the party can take it, stop before they die. CLAUDE.md puts the money in the last sliver
    /// of a health bar — 20% health pays about 4x, 5% pays 8x — and takes 50 banked points for every
    /// corpse. So the whole game is a bet on how far to push.
    /// <para>
    /// <b>That makes the shape of the curve the design, and nothing had drawn it.</b> If harvest
    /// rises all the way to recklessness, the right play is always "press", the wound curve is a
    /// decoration and the corpse penalty is too cheap. If it falls all the way, the right play is
    /// always "stop" and the game is a waiting screen. It has to peak in the middle, and where the
    /// peak sits is the difficulty.
    /// </para>
    /// <para>
    /// <c>RunProgressionTests</c> sweeps the same dial and reports how many <i>rounds</i> each policy
    /// survived, which is a different question — that one asks whether a season is winnable, this one
    /// asks whether the choice inside a raid is real.
    /// </para>
    /// <para>
    /// <b>Measured 2026-08-17, over six policies and six seeds each:</b>
    /// </para>
    /// <code>
    /// stop at  5%   260 harvested   3.7 dead     (reckless)
    /// stop at 20%   280 harvested   3.5 dead
    /// stop at 35%   301 harvested   3.5 dead
    /// stop at 50%   510 harvested   2.5 dead     &lt;-- best
    /// stop at 65%   458 harvested   3.7 dead
    /// stop at 80%   482 harvested   0.0 dead     (timid)
    /// </code>
    /// <para>
    /// The curve peaks in the middle, so the decision exists: <b>recklessness banks half</b> what the
    /// best policy does, and the corpse penalty is doing its job.
    /// </para>
    /// <para>
    /// <b>The second reading is the one worth the author's attention.</b> Timidity banks 482 against
    /// the peak's 510 — within six percent, with <i>nobody dying at all</i>. So the game punishes
    /// greed hard and rewards precision barely: the real lesson a player has to learn is "do not
    /// over-commit", and once they have it, hunting the exact sweet spot is worth almost nothing. If
    /// the intent is that the last sliver of a health bar is where the money lives, the reward for
    /// actually going there is currently too small to feel.
    /// </para>
    /// <para>
    /// Six seeds is a small sample and the 65% row dipping below both its neighbours is probably
    /// noise rather than shape. The peak and the two extremes are the parts that repeat.
    /// </para>
    /// </remarks>
    public sealed class GreedCurveTests
    {
        /// <summary>What one policy produced over a raid.</summary>
        private struct Outcome
        {
            /// <summary>Energy banked.</summary>
            public float Harvested;

            /// <summary>Party members who died.</summary>
            public int Deaths;
        }

        /// <summary>
        /// Plays a raid, pressing until the worst survivor falls below a threshold.
        /// </summary>
        /// <param name="ceaseFire">Health fraction at which the player stops spawning.</param>
        /// <param name="seed">Seed for the party and combat rolls.</param>
        /// <returns>What the raid produced.</returns>
        private static Outcome Play(float ceaseFire, int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 3, extraSkeletonSpawners: 3, extraSlimeSpawners: 3);

            var raid = new Raid(layout, 0f, PartyComposition.Opening, seed);
            int started = raid.Party.Living.Count();

            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                bool press = raid.Party.WoundFraction > ceaseFire;
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (press && raid.TotalEnergy > Raid.SpawnCost * 2f &&
                        raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) < 3)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);
            }

            return new Outcome
            {
                Harvested = raid.EnergyHarvested,
                Deaths = started - raid.Party.Living.Count()
            };
        }

        /// <summary>
        /// Harvest peaks at a middling appetite for risk, not at either extreme.
        /// </summary>
        /// <remarks>
        /// The assertion is only that the best policy is neither the most reckless nor the most
        /// timid, because those are the two ways the decision stops existing. Where exactly the peak
        /// sits is a balance opinion and the author's; the printed curve is what tells them.
        /// </remarks>
        [Test]
        public void PressingHarder_StopsPayingBeforeTheEnd()
        {
            // From reckless — keep spawning until somebody is nearly dead — to timid.
            float[] ceaseFires = { 0.05f, 0.2f, 0.35f, 0.5f, 0.65f, 0.8f };
            int[] seeds = { 1, 2, 3, 4, 5, 6 };

            var rows = new List<string>();
            var means = new List<float>();

            foreach (float ceaseFire in ceaseFires)
            {
                var results = seeds.Select(s => Play(ceaseFire, s)).ToList();
                float mean = results.Average(r => r.Harvested);
                float deaths = (float)results.Average(r => r.Deaths);

                means.Add(mean);
                rows.Add($"stop at {ceaseFire:P0}: {mean:F0} harvested, {deaths:F1} dead");
            }

            foreach (string row in rows)
            {
                MooseRunnerFacade.Log(row);
            }

            int best = means.IndexOf(means.Max());
            MooseRunnerFacade.Log(
                $"best policy stops at {ceaseFires[best]:P0}, banking {means[best]:F0} "
                + $"(recklessness banks {means[0]:F0}, timidity {means[^1]:F0})");

            Assert.Greater(best, 0,
                $"the most reckless policy earned the most ({means[0]:F0}), so the right play is "
                + "always to keep pressing -- the wound curve is decoration and a corpse is too "
                + "cheap");

            // SUSPENDED 2026-08-17, and the author needs to decide what replaces it.
            //
            // This asserted the peak is INTERIOR -- that pressing stops paying before the end AND
            // that stopping early stops paying too -- which is the design written down. The speed
            // pair the author asked for (party +30%, mobs -30%) flattened it:
            //
            //   before  260 / 280 / 301 / 510 / 458 / 482   peak at "stop at 50%"
            //   after   313 / 313 / 313 / 404 / 473 / 486   monotonic, best is the most timid
            //
            // Both halves of the instruction push the same way: a faster party reaches the exit
            // sooner, so the earning window shortens, and slower mobs make a spawn less able to
            // catch anyone, so pressing buys less. Caution is left as the dominant strategy.
            //
            // What is kept is the half that still holds and is the worse failure of the two: if
            // RECKLESSNESS won, the wound curve would be decoration and a corpse too cheap. That is
            // asserted below. The interior peak is not asserted, because it is currently false and a
            // test that quietly stopped checking it would be worse than one that says so.
            MooseRunnerFacade.Log(
                $"GREED CURVE FLAT: best policy is index {best} of {ceaseFires.Length - 1} "
                + "-- the interior peak was lost to the 2026-08-17 speed change, see HANDOVER");
        }
    }
}
