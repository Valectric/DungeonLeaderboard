using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.LeagueManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Plays whole seasons end to end, looking for the failures a single raid cannot show.
    /// </summary>
    /// <remarks>
    /// A raid is sixty seconds; a run is a dozen of them plus a shop between each. Anything that
    /// accumulates -- a loadout that grows without bound, a corridor that outgrows the clock, a purse
    /// that ratchets, a league that stops moving -- is invisible in one raid and obvious over a
    /// season. This is the sweep for drift.
    /// </remarks>
    public sealed class SeasonSweepTests
    {
        /// <summary>What one simulated season produced.</summary>
        private sealed class Season
        {
            /// <summary>Harvest from each raid, in order.</summary>
            public List<float> Harvests { get; } = new();

            /// <summary>Total items bought over the run.</summary>
            public int Bought { get; set; }

            /// <summary>Rooms in the final dungeon.</summary>
            public int FinalRooms { get; set; }

            /// <summary>Where the player finished.</summary>
            public int FinalPosition { get; set; }

            /// <summary>Whether the player was relegated.</summary>
            public bool Relegated { get; set; }
        }

        /// <summary>
        /// Plays a season: raid, bank, shop, repeat.
        /// </summary>
        /// <remarks>
        /// Mirrors GameController's flow rather than driving it, so this can run headless and fast.
        /// The player policy is deliberately crude -- ambush at every spawner, buy whatever is
        /// affordable -- because a crude policy is the floor, and the floor is what must not break.
        /// </remarks>
        /// <param name="seed">Seed for the league and the combat rolls.</param>
        /// <param name="raids">How many raids to play.</param>
        /// <returns>What happened.</returns>
        private static Season PlaySeason(int seed, int raids)
        {
            var league = new LeagueTable(seed);
            var loadout = new Loadout();
            var result = new Season();
            float carried = 0f;
            float bonus = 0f;

            for (int round = 0; round < raids; round++)
            {
                int rooms = Mathf.Min(5, 3 + loadout.Count(ShopItem.Door));
                DungeonLayout layout = DungeonLayout.BuildCorridor(
                    roomCount: rooms,
                    extraSlimeSpawners: loadout.Count(ShopItem.Slime),
                    extraSkeletonSpawners: loadout.Count(ShopItem.Skeleton),
                    extraTraps: loadout.Count(ShopItem.SpikeTrap) + loadout.Count(ShopItem.PoisonDart),
                    chests: loadout.Count(ShopItem.Chest));

                var raid = new Raid(layout, bonus, null, seed + round);
                bonus = 0f;

                int guard = 0;
                while (raid.IsRunning && guard++ < 5000)
                {
                    // A competent player, not a button-masher. Spawning at every spawner on every
                    // tick drains the purse to zero and leaves nothing for the shop -- which is
                    // exactly what an earlier version of this bot did, buying zero items across
                    // twenty seasons and hiding whether the shop was reachable at all.
                    foreach (Vector2Int spawner in layout.SpawnerCells)
                    {
                        int room = layout.Grid.RoomAt(spawner);
                        if (raid.Mobs.CountInRoom(room) == 0 && raid.TotalEnergy > Raid.SpawnCost * 2f)
                        {
                            raid.SpawnMob(spawner);
                        }
                    }

                    // Traps are the wound curve, and the wound curve is where the money is.
                    if (raid.IsTrapReady && raid.TotalEnergy > Raid.TrapCost * 2f)
                    {
                        foreach (Trap trap in layout.Traps)
                        {
                            if (trap.IsArmed && trap.Cell == raid.Party.Cell)
                            {
                                raid.FireTrap(trap.Cell);
                                break;
                            }
                        }
                    }

                    raid.Tick(0.02f);
                }

                Assert.Less(guard, 5000, $"raid {round} never ended");
                result.Harvests.Add(raid.EnergyHarvested);
                result.FinalRooms = rooms;

                league.SubmitRaid(raid.EnergyHarvested);
                if (league.PlayerRelegated)
                {
                    result.Relegated = true;
                    result.FinalPosition = league.PlayerPosition;
                    return result;
                }

                league.CollapseRelegated();

                // The shop: spend whatever the raid left over, then start early for the bonus.
                var shop = new Shop(raid.TotalEnergy);
                foreach (ShopItem item in ShopScreen_Items)
                {
                    while (shop.CanAfford(item) && loadout.Total < 40)
                    {
                        if (!shop.Buy(item))
                        {
                            break;
                        }

                        loadout.Add(item);
                        result.Bought++;
                    }
                }

                bonus = shop.Ready();
                carried = shop.Purse;
            }

            result.FinalPosition = league.PlayerPosition;
            _ = carried;
            return result;
        }

        /// <summary>The six items, in a fixed order so a season is reproducible.</summary>
        private static readonly ShopItem[] ShopScreen_Items =
        {
            ShopItem.Skeleton, ShopItem.Slime, ShopItem.SpikeTrap,
            ShopItem.PoisonDart, ShopItem.Door, ShopItem.Chest
        };

        /// <summary>A full season runs to the end without hanging or exploding.</summary>
        [Test]
        public void AFullSeason_RunsToTheEnd()
        {
            for (int seed = 1; seed <= 5; seed++)
            {
                Season season = PlaySeason(seed, 12);

                float best = 0f;
                float total = 0f;
                foreach (float harvest in season.Harvests)
                {
                    best = Mathf.Max(best, harvest);
                    total += harvest;
                }

                MooseRunnerFacade.Log(
                    $"seed {seed}: {season.Harvests.Count} raids, bought {season.Bought}, "
                    + $"rooms {season.FinalRooms}, finished {season.FinalPosition}, "
                    + $"relegated={season.Relegated}, best raid {best:F0}, "
                    + $"avg {total / Mathf.Max(1, season.Harvests.Count):F0}");

                foreach (float harvest in season.Harvests)
                {
                    Assert.GreaterOrEqual(harvest, 0f, "a raid harvested a negative amount");
                    Assert.Less(harvest, 50000f, "a raid harvested an absurd amount");
                }
            }
        }

        /// <summary>
        /// The dungeon never outgrows the clock.
        /// </summary>
        /// <remarks>
        /// Buying halls lengthens the corridor. If it could grow past what a party can cross in sixty
        /// seconds, the party could never reach the boss room and the design's losing ending would
        /// quietly stop existing -- a guaranteed full minute every raid, forever.
        /// </remarks>
        [Test]
        public void TheCorridor_NeverOutgrowsTheClock()
        {
            Season season = PlaySeason(9, 15);
            Assert.LessOrEqual(season.FinalRooms, 5, "the corridor grew past its cap");

            DungeonLayout biggest = DungeonLayout.BuildCorridor(roomCount: 5);
            var raid = new Raid(biggest);
            float elapsed = 0f;

            while (raid.IsRunning && elapsed < Raid.RaidSeconds)
            {
                raid.Tick(0.02f);
                elapsed += 0.02f;
            }

            MooseRunnerFacade.Log(
                $"largest corridor: {raid.Outcome} after {elapsed:F1}s");
            Assert.AreEqual(RaidOutcome.PartyEscaped, raid.Outcome,
                "an unopposed party cannot cross the largest dungeon the shop can build");
        }

        /// <summary>
        /// A dungeon stuffed with a whole season's purchases is still a playable dungeon.
        /// </summary>
        /// <remarks>
        /// Purchases are permanent, so they compound every round -- a competent player buys about
        /// thirty-five things across a season. The question is not how many, it is whether the result
        /// still works: the party must still be able to cross it, or the design's losing ending
        /// quietly stops existing.
        /// </remarks>
        [Test]
        public void ASeasonOfPurchases_StillMakesAPlayableDungeon()
        {
            Season season = PlaySeason(3, 20);
            MooseRunnerFacade.Log($"twenty raids bought {season.Bought} items");

            DungeonLayout stuffed = DungeonLayout.BuildCorridor(
                roomCount: 5, extraSlimeSpawners: 10, extraSkeletonSpawners: 10,
                extraTraps: 12, chests: 8);

            List<Vector2Int> path = stuffed.Grid.FindPath(stuffed.EntranceCell, stuffed.BossCell);
            Assert.Greater(path.Count, 0, "a fully-bought dungeon cannot be crossed at all");

            var raid = new Raid(stuffed);
            float elapsed = 0f;
            while (raid.IsRunning && elapsed < Raid.RaidSeconds)
            {
                raid.Tick(0.02f);
                elapsed += 0.02f;
            }

            MooseRunnerFacade.Log($"fully-bought dungeon: {raid.Outcome} after {elapsed:F1}s");
            Assert.IsFalse(raid.IsRunning, "a fully-bought dungeon hung the raid");
        }

        /// <summary>
        /// How well the player raids decides where they finish.
        /// </summary>
        /// <remarks>
        /// Varies the <i>play</i> rather than the seed. An earlier version ran ten seeds through the
        /// same bot and demanded different outcomes, which was a poor test: a deterministic policy
        /// harvesting 291-296 every time should land in the same place every time, and it failing
        /// said nothing about the league. What matters is that the table answers performance.
        /// </remarks>
        [Test]
        public void WhereYouFinish_DependsOnHowYouPlay()
        {
            var byQuality = new Dictionary<float, int>();

            foreach (float perRaid in new[] { 20f, 150f, 300f, 450f, 700f })
            {
                var league = new LeagueTable(31337);
                for (int round = 0; round < 10 && !league.PlayerRelegated; round++)
                {
                    league.SubmitRaid(perRaid);
                    if (!league.PlayerRelegated)
                    {
                        league.CollapseRelegated();
                    }
                }

                byQuality[perRaid] = league.PlayerPosition;
                MooseRunnerFacade.Log($"harvesting {perRaid:F0} a raid finishes {league.PlayerPosition}");
            }

            Assert.Greater(byQuality.Values.Count, 1, "the league produced no range of outcomes");
            Assert.Less(byQuality[700f], byQuality[20f],
                "harvesting 700 a raid finished no better than harvesting 20");
            Assert.Less(byQuality[450f], byQuality[150f],
                "the table is not responding to how well the player raids");
        }

        /// <summary>
        /// Playing well climbs the table, and playing badly sinks toward relegation.
        /// </summary>
        /// <remarks>
        /// The assertion that should have existed from the first day of M2, and did not. Rivals
        /// earned 380-1280 a round against a well-played 292, so the player shed ground every round
        /// whatever they did: ten seasons across ten seeds all finished in <b>exactly 18th</b>. The
        /// standings were a backdrop, and SPEC.md's ten-second hook -- "I am 14th, 16th is death, I
        /// need to climb" -- was unwinnable by construction.
        /// </remarks>
        [Test]
        public void GoodPlayClimbs_AndBadPlaySinks()
        {
            const int rounds = 10;

            var climbing = new LeagueTable(31337);
            var sinking = new LeagueTable(31337);
            int start = climbing.PlayerPosition;

            for (int round = 0; round < rounds; round++)
            {
                climbing.SubmitRaid(380f);   // a good raid, measured from actual play
                sinking.SubmitRaid(20f);     // barely engaging the party at all

                if (!climbing.PlayerRelegated)
                {
                    climbing.CollapseRelegated();
                }

                if (!sinking.PlayerRelegated)
                {
                    sinking.CollapseRelegated();
                }
            }

            MooseRunnerFacade.Log(
                $"from {start}: good play finished {climbing.PlayerPosition}, "
                + $"bad play finished {sinking.PlayerPosition} "
                + $"(relegated={sinking.PlayerRelegated})");

            Assert.Less(climbing.PlayerPosition, start,
                "a season of good raids did not climb the table at all");
            Assert.Greater(sinking.PlayerPosition, climbing.PlayerPosition,
                "playing badly finished no worse than playing well");
        }

        /// <summary>
        /// A season is reproducible from its seed.
        /// </summary>
        /// <remarks>
        /// The project's hard constraint, asserted at the level that matters: a whole run, not one
        /// roll.
        /// </remarks>
        [Test]
        public void ASeason_IsReproducibleFromItsSeed()
        {
            Season first = PlaySeason(4242, 8);
            Season again = PlaySeason(4242, 8);

            Assert.AreEqual(first.Harvests.Count, again.Harvests.Count, "different raid counts");
            for (int i = 0; i < first.Harvests.Count; i++)
            {
                Assert.AreEqual(first.Harvests[i], again.Harvests[i], 0.01f,
                    $"raid {i} differed between two runs of the same seed");
            }

            Assert.AreEqual(first.FinalPosition, again.FinalPosition, "different final position");
            MooseRunnerFacade.Log(
                $"seed 4242 reproduced: {first.Harvests.Count} raids, finished {first.FinalPosition}");
        }
    }
}
