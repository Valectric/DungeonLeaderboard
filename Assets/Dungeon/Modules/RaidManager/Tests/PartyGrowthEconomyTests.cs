using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.LeagueManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Measures what growing the party does to the money, which no existing instrument could see.
    /// </summary>
    /// <remarks>
    /// The energy rate sums <b>per member</b>, so party size scales the whole economy — and the
    /// league's rivals are priced against what the player can produce (<c>GoodRun</c>, D30). Growing to nine
    /// is therefore an economic change wearing the clothes of a difficulty change.
    /// <para>
    /// <b>Why this file had to exist.</b> <c>SeasonSweepTests</c> plays whole seasons and would have
    /// reported all-clear: it constructs every raid with <c>composition: null</c>, which is the
    /// four-strong opening roster, so it never once fields a grown party. Every season figure in the
    /// project is a measurement of a party size the game no longer only sends. That is the same shape
    /// as the D31 fault — a suite measuring something adjacent to the thing everyone believed it
    /// measured.
    /// </para>
    /// </remarks>
    public sealed class PartyGrowthEconomyTests
    {
        /// <summary>Plays one raid to its end and returns what it harvested.</summary>
        /// <param name="composition">The party to send in.</param>
        /// <param name="seed">Combat seed.</param>
        /// <returns>Energy harvested.</returns>
        private static float Harvest(PartyComposition composition, int seed)
        {
            var raid = new Raid(DungeonLayout.BuildCorridor(roomCount: 3), 0f, composition, seed);

            int guard = 0;
            while (raid.IsRunning && guard++ < 8000)
            {
                raid.Tick(0.05f);
            }

            Assert.AreNotEqual(RaidOutcome.InProgress, raid.Outcome,
                $"a {composition.Roles.Count}-strong {composition.Name} never resolved its raid");
            return raid.EnergyHarvested;
        }

        /// <summary>
        /// A party of nine still plays a raid to a finish, at every size on the way up.
        /// </summary>
        /// <remarks>
        /// The first question, before any balance one. Nine bodies spawn on a single entrance cell in
        /// a five-by-five room; if the AI deadlocks or the raid never terminates, the ramp is broken
        /// in a way no amount of retuning fixes.
        /// </remarks>
        [Test]
        public void EverySizeUpToNine_StillFinishesARaid()
        {
            foreach (PartyComposition roster in PartyComposition.All)
            {
                for (int size = 4; size <= PartyComposition.MaxSize; size++)
                {
                    float harvested = Harvest(roster.Grown(size), seed: 4242);

                    Assert.IsFalse(float.IsNaN(harvested), $"{roster.Name} at {size} harvested NaN");
                    Assert.GreaterOrEqual(harvested, 0f,
                        $"{roster.Name} at {size} harvested a negative amount");
                }
            }
        }

        /// <summary>
        /// Records what each party size is worth, so the league can be priced against it.
        /// </summary>
        /// <remarks>
        /// Asserts only the ordering — a bigger party must not be worth <i>less</i>, which would mean
        /// the ramp is punishing the player for a change they did not make. The magnitudes are logged
        /// rather than pinned, because what the right number is belongs to the author and pinning a
        /// figure here would quietly make this a balance decision made by a test.
        /// </remarks>
        [Test]
        public void WhatEachPartySizeIsWorth_IsRecorded()
        {
            var byRaid = new List<string>();
            float atFour = 0f;
            float atNine = 0f;

            for (int size = 4; size <= PartyComposition.MaxSize; size++)
            {
                float total = 0f;
                foreach (PartyComposition roster in PartyComposition.All)
                {
                    total += Harvest(roster.Grown(size), seed: 4242);
                }

                float mean = total / PartyComposition.All.Length;
                if (size == 4) { atFour = mean; }
                if (size == PartyComposition.MaxSize) { atNine = mean; }

                byRaid.Add($"{size}:{mean:F0}");
                MooseRunnerFacade.Log(
                    $"party of {size}: mean harvest {mean:F1} across "
                    + $"{PartyComposition.All.Length} rosters");
            }

            MooseRunnerFacade.Log(
                $"harvest by party size -- {string.Join("  ", byRaid)}  "
                + $"| nine is {atNine / Mathf.Max(0.01f, atFour):F2}x four");

            Assert.Greater(atNine, atFour,
                "a nine-strong party harvests no more than a four-strong one, so the ramp costs the "
                + "player difficulty and pays them nothing");
        }

        /// <summary>Plays a raid where the party is shut in and fought, and returns the harvest.</summary>
        /// <remarks>
        /// The walkthrough above is the floor, not the game. A raid the player actually works — doors
        /// shut, monsters fed in — is where the money is, and it is the case the rivals are priced
        /// against. Built the way <c>RaidRulesTests</c> builds its "stalled" raid so the two figures
        /// can be read against each other.
        /// </remarks>
        /// <param name="composition">The party to send in.</param>
        /// <param name="seed">Combat seed.</param>
        /// <returns>Energy harvested.</returns>
        private static float HarvestHeldAndFought(PartyComposition composition, int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 3);
            var raid = new Raid(layout, 0f, composition, seed);

            foreach (Door door in layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            raid.Mobs.Spawn(MobKind.Skeleton, layout.RoomCentres[0]);

            int guard = 0;
            while (raid.IsRunning && guard++ < 8000)
            {
                raid.Tick(0.05f);
            }

            return raid.EnergyHarvested;
        }

        /// <summary>
        /// Records what growth is worth on a raid the player actually works.
        /// </summary>
        /// <remarks>
        /// <see cref="LeagueTable.RivalCeiling"/> is the most a rival dungeon can earn in a round. If
        /// growth alone lifts a late-season raid near it, the player starts winning on the calendar
        /// rather than on play, which inverts the promise D20 and D25 rest on.
        /// <para>
        /// <b>Read from the constant, never restated.</b> This log line said "rival ceiling is 430"
        /// as a literal, and the figure moved twice on 2026-08-17 alone — to 560 and then 620 — so
        /// it was reporting a comparison against a number the league had stopped using. That is the
        /// same drift that made the growth curve unreachable in D47, in a log line instead of a
        /// constant. The test assembly now references LeagueManager purely so this cannot recur.
        /// </para>
        /// <para>
        /// <b>Logged, not asserted against <c>GoodRun</c>.</b> Retuning the rivals is the author's
        /// call, and a test that quietly enforced a ratio would be taking that decision for them.
        /// </para>
        /// <para>
        /// <b>The result is the opposite of the worry, and it is worth understanding.</b> Measured
        /// 2026-08-17: a worked raid earns <b>291 at four and 281 at nine — 0.97x</b>, while a raid
        /// nobody works rises 71 to 77. Headcount does not inflate a worked raid, because nine
        /// adventurers kill what they meet faster and a party that takes less damage per member sits
        /// lower on the wound curve, which is where the money is.
        /// </para>
        /// <para>
        /// Growth pays in <i>survival</i>, not in rate: a nine-strong party lives through raids that
        /// wipe a four, and so keeps earning for more of the clock. That is why the season sweep's
        /// best raid rose from 694 to 1120 when D47 fixed the curve while this figure did not move.
        /// The two measurements disagree only if the mechanism is assumed rather than asked about.
        /// </para>
        /// </remarks>
        [Test]
        public void WhatGrowthIsWorth_OnARaidThePlayerWorks()
        {
            float atFour = 0f;
            float atNine = 0f;

            // Rounds, not raids: 0 is the opening raid and 9 is the last of a ten-round season. This
            // read { 0, 8, 17 } and reported "raid 18", which no season reaches -- the same rejected
            // nineteen-round league that D47 found in the growth curve itself.
            foreach (int round in new[] { 0, 5, 9 })
            {
                float strolled = 0f;
                float fought = 0f;
                int size = PartyComposition.SizeForRound(round);

                for (int seed = 0; seed < 6; seed++)
                {
                    PartyComposition party = PartyComposition.ForRound(round, seed * 7919);
                    strolled += Harvest(party, seed: 4242 + seed);
                    fought += HarvestHeldAndFought(party, seed: 4242 + seed);
                }

                if (round == 0) { atFour = fought / 6f; }
                if (size >= PartyComposition.MaxSize) { atNine = fought / 6f; }

                MooseRunnerFacade.Log(
                    $"raid {round + 1}, party of {size}: strolled {strolled / 6f:F0}, "
                    + $"held and fought {fought / 6f:F0} "
                    + $"(rival ceiling is {LeagueTable.RivalCeiling:F0})");
            }

            MooseRunnerFacade.Log(
                $"worked raids: four {atFour:F0} -> nine {atNine:F0}, "
                + $"{atNine / Mathf.Max(0.01f, atFour):F2}x");

            Assert.Greater(atNine, 0f, "a late-season worked raid harvested nothing at all");
        }
    }
}
