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
    /// Measures what a raid is actually spent doing, member-second by member-second.
    /// </summary>
    /// <remarks>
    /// <c>EnergyCurve</c> prices fighting at 3.0 and walking at 0.06 — fifty times apart — so the
    /// <b>mix of actions is the economy</b>, not a detail of it. CLAUDE.md states the intent plainly:
    /// an unengaged party walking a corridor must earn almost nothing, and the player has to see dead
    /// time costing them.
    /// <para>
    /// Nothing measured the mix. Every other rate in the project has been checked — the wound curve,
    /// the room bonus, the harvest distribution, the retreat valve, the shop purse — and the one that
    /// decides what a minute of this game <i>feels</i> like was inferred from all of them and asserted
    /// by none. This project's own doctrine is that green tests hide broken rates: every Dweller test
    /// in the sister project passed while the encounter rate, the thing actually broken, was measured
    /// by nothing.
    /// </para>
    /// <para>
    /// <b>Measured 2026-08-17, and the shape is not what the headline rate suggests:</b>
    /// </para>
    /// <code>
    /// party of 4:  Fleeing 30%   Shooting 29%   Walking 23%   Fighting 18%
    /// party of 9:  Fleeing 33%   Shooting 27%   Walking 24%   Fighting 16%
    /// </code>
    /// <para>
    /// The party spends <b>twice as long fleeing as fighting</b>, and fleeing pays 0.75 against
    /// fighting's 3.0. That is a consequence of the retreat valve being repaired the same day (D48):
    /// before it, a party under pressure did not run, it died. Fleeing still pays twelve times what
    /// walking does, so a retreating party is earning rather than wasting the clock — but the number
    /// the player is chasing is the one they see least of.
    /// </para>
    /// <para>
    /// The mix depends on the policy as much as the game: this bot stops pressing at 45% health,
    /// which is cautious, and a greedier one would trade fleeing for fighting and corpses. That is
    /// the trade the design is about, so the figures are a description of one way to play rather
    /// than a property of the game.
    /// </para>
    /// </remarks>
    public sealed class ActionMixTests
    {
        /// <summary>Plays a raid with a fixed competent policy and counts member-ticks per action.</summary>
        /// <param name="size">Party size to field.</param>
        /// <param name="seed">Seed for the party and combat rolls.</param>
        /// <returns>Ticks spent in each action, summed over living members.</returns>
        private static Dictionary<AdventurerAction, int> Play(int size, int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 3, extraSkeletonSpawners: 2, extraSlimeSpawners: 2);

            PartyComposition party = PartyComposition.Opening.Grown(size);
            var raid = new Raid(layout, 0f, party, seed);
            var tally = new Dictionary<AdventurerAction, int>();

            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                // The one skill the design asks for: press while they can take it, stop when they
                // cannot. Holding the player constant is what makes the mix attributable to the game.
                bool safe = raid.Party.WoundFraction > 0.45f;
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (safe && raid.TotalEnergy > Raid.SpawnCost * 2f &&
                        raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) < 2)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);

                foreach (Adventurer member in raid.Party.Living)
                {
                    tally.TryGetValue(member.Action, out int had);
                    tally[member.Action] = had + 1;
                }
            }

            return tally;
        }

        /// <summary>
        /// A worked raid is mostly spent earning, not mostly spent walking.
        /// </summary>
        /// <remarks>
        /// The design's own framing: dead time is the thing the player is fighting against, and the
        /// rate is shown large and pulsing so they can watch it cost them. If a competently played
        /// raid is still mostly walking, the verbs are not doing enough and the minute is mostly
        /// spectating.
        /// <para>
        /// Asserted loosely — a third of member-time at the paying actions — because the interesting
        /// output is the printed mix. A tight bound here would be a balance opinion smuggled into a
        /// test.
        /// </para>
        /// </remarks>
        [Test]
        public void AWorkedRaid_IsMostlySpentEarning()
        {
            var rows = new List<string>();
            float worstPaying = 1f;
            int worstSize = 0;

            foreach (int size in new[] { 4, 9 })
            {
                var totals = new Dictionary<AdventurerAction, int>();
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    foreach (KeyValuePair<AdventurerAction, int> entry in Play(size, seed))
                    {
                        totals.TryGetValue(entry.Key, out int had);
                        totals[entry.Key] = had + entry.Value;
                    }
                }

                int all = totals.Values.Sum();
                Assert.Greater(all, 0, "the raid recorded no member-ticks at all");

                // Anything above walking and idling is the party being made to pay for its time.
                int paying = totals
                    .Where(e => e.Key is not (AdventurerAction.Walking or AdventurerAction.Idle))
                    .Sum(e => e.Value);

                float share = paying / (float)all;
                if (share < worstPaying)
                {
                    worstPaying = share;
                    worstSize = size;
                }

                string mix = string.Join("  ", totals
                    .OrderByDescending(e => e.Value)
                    .Select(e => $"{e.Key} {e.Value / (float)all:P0}"));

                rows.Add($"party of {size}: {mix}");
            }

            foreach (string row in rows)
            {
                MooseRunnerFacade.Log(row);
            }

            MooseRunnerFacade.Log(
                $"least time spent earning: {worstPaying:P0} at a party of {worstSize}");

            Assert.Greater(worstPaying, 0.33f,
                $"a competently worked raid spends only {worstPaying:P0} of its member-time doing "
                + "anything that pays, at a party of " + worstSize + " -- the rest is walking and "
                + "standing, which is the dead time the whole design is built to price");
        }
    }
}
