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
    /// Measures how often the retreat valve fires as the party grows.
    /// </summary>
    /// <remarks>
    /// SPEC.md makes the retreat the player's <b>only</b> mercy — open a door behind a losing party
    /// and let them fall back — and the party's own AI reaches for it at
    /// <see cref="Party.RetreatThreshold"/>. That threshold reads <c>Party.WoundFraction</c>, which is
    /// the <b>worst single member</b>, not the party average.
    /// <para>
    /// Which means growing the party changes when it fires, and not in the obvious direction. Nine
    /// bodies are nine chances for somebody to drop under the line, so a bigger party can break off
    /// <i>more</i> readily than a small one even though it is collectively far healthier. A party
    /// that yo-yos between advancing and retreating spends the raid at the fleeing rate of 0.75
    /// instead of the fighting rate of 3.0 — which is the earning curve this game is made of.
    /// </para>
    /// <para>
    /// Nothing measured this. <c>AWoundedParty_Retreats</c> proves the valve works at four; whether
    /// it works the <i>same</i> at nine is a different question, and one the growth curve made real
    /// on 2026-08-17 (D47) after being unreachable for the whole project.
    /// </para>
    /// </remarks>
    public sealed class RetreatValveTests
    {
        /// <summary>
        /// Mobs held in one room at once by the pressure policy.
        /// </summary>
        /// <remarks>
        /// <b>Two was the first value and it made the measurement meaningless at nine.</b> Two mobs
        /// are a real threat to four adventurers and beneath the notice of nine, so the party size
        /// and the pressure were changing together and the result said only that the policy had not
        /// kept up. Raised to a number a player short of nothing would actually reach for.
        /// </remarks>
        private const int PressurePerRoom = 6;
        /// <summary>What one raid did with the valve.</summary>
        private struct ValveUse
        {
            /// <summary>How many separate times the party broke off.</summary>
            public int Episodes;

            /// <summary>Fraction of the raid spent running.</summary>
            public float FleeingShare;

            /// <summary>Lowest the worst member ever got.</summary>
            public float LowWater;

            /// <summary>How many of the party died.</summary>
            public int Deaths;

            /// <summary>Lowest the POOLED health ever got — what the retreat actually reads.</summary>
            public float LowPool;
        }

        /// <summary>
        /// Plays a raid under constant pressure and records what the valve did.
        /// </summary>
        /// <remarks>
        /// Pressure is applied the same way at every size — spawn whenever it is affordable — because
        /// holding the player constant is the only way the party size is the thing being measured.
        /// Deliberately a harder policy than a good player uses: the valve is what a party reaches
        /// for when it is losing, so it has to be losing.
        /// </remarks>
        /// <param name="size">Members to field.</param>
        /// <param name="seed">Seed for the party and the combat rolls.</param>
        /// <param name="perRoomCap">Most mobs to hold in one room at once.</param>
        /// <returns>What the valve did.</returns>
        private static ValveUse Play(int size, int seed, int perRoomCap)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 3, extraSkeletonSpawners: 3, extraSlimeSpawners: 3);

            PartyComposition party = PartyComposition.Opening.Grown(size);
            var raid = new Raid(layout, 0f, party, seed);

            int episodes = 0;
            int fleeingTicks = 0;
            int ticks = 0;
            float low = 1f;
            float lowPool = 1f;
            bool wasFleeing = false;
            int started = raid.Party.Living.Count();

            while (raid.IsRunning && ticks < 4000)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (raid.TotalEnergy > Raid.SpawnCost * 2f &&
                        raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) < perRoomCap)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);
                ticks++;

                bool fleeing = raid.Party.Goal == PartyGoal.Retreating;
                if (fleeing)
                {
                    fleeingTicks++;
                    if (!wasFleeing)
                    {
                        episodes++;
                    }
                }

                wasFleeing = fleeing;
                low = Mathf.Min(low, raid.Party.WoundFraction);
                lowPool = Mathf.Min(lowPool, raid.Party.HealthFraction);
            }

            return new ValveUse
            {
                Episodes = episodes,
                FleeingShare = ticks == 0 ? 0f : fleeingTicks / (float)ticks,
                LowWater = low,
                Deaths = started - raid.Party.Living.Count(),
                LowPool = lowPool
            };
        }

        /// <summary>
        /// The valve still fires for a nine-strong party, and does not fire constantly.
        /// </summary>
        /// <remarks>
        /// Two failures are possible and they point opposite ways, so both are asserted. A party that
        /// never breaks off has lost the safety valve the whole design rests on. A party that spends
        /// most of the raid running earns at 0.75 where it should be earning at 3.0, and the player's
        /// door becomes a lever attached to nothing.
        /// </remarks>
        [Test]
        public void TheValve_StillWorksAtEverySize()
        {
            var rows = new List<string>();
            var byNine = new List<ValveUse>();
            var byFour = new List<ValveUse>();

            foreach (int size in new[] { 4, 6, 9 })
            {
                var uses = new List<ValveUse>();
                for (int seed = 0; seed < 4; seed++)
                {
                    uses.Add(Play(size, 20260813 + (seed * 7919), PressurePerRoom));
                }

                float episodes = (float)uses.Average(u => u.Episodes);
                float share = uses.Average(u => u.FleeingShare);
                float low = uses.Average(u => u.LowWater);
                float deaths = (float)uses.Average(u => u.Deaths);
                float pool = uses.Average(u => u.LowPool);
                rows.Add(
                    $"{size}: {episodes:F1} episodes, {share:P0} fleeing, worst member {low:P0}, "
                    + $"pooled low {pool:P0}, {deaths:F1} dead");

                if (size == 9) { byNine = uses; }
                if (size == 4) { byFour = uses; }
            }

            MooseRunnerFacade.Log("retreat valve by party size -- " + string.Join("  |  ", rows));

            float nineEpisodes = (float)byNine.Average(u => u.Episodes);
            float nineShare = byNine.Average(u => u.FleeingShare);
            float fourEpisodes = (float)byFour.Average(u => u.Episodes);

            Assert.Greater(nineEpisodes, 0f,
                "a nine-strong party under constant pressure never broke off once, so the retreat "
                + "valve -- the player's only mercy, per SPEC -- does nothing at full party size");

            Assert.Less(nineShare, 0.5f,
                $"a nine-strong party spent {nineShare:P0} of the raid running, so it earns at the "
                + "fleeing rate instead of the fighting one for most of the clock");

            MooseRunnerFacade.Log(
                $"episodes four {fourEpisodes:F1} -> nine {nineEpisodes:F1}, "
                + $"{nineEpisodes / Mathf.Max(0.01f, fourEpisodes):F2}x");
        }
    }
}
