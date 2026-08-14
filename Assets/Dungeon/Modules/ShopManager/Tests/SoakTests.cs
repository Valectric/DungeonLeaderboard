using System;
using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.LeagueManager;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Plays many seasons with randomised player behaviour, looking for rare failures.
    /// </summary>
    /// <remarks>
    /// Every other sweep picks its scenarios by hand, which means it can only find what somebody
    /// thought to look for. This plays the game a few hundred times with a different player each
    /// season -- one that hoards, one that spams traps, one that shuts every door, one that buys
    /// nothing -- and asserts only the invariants that must hold no matter who is at the controls.
    /// <para>
    /// Every season is seeded, so a failure names a number that reproduces it exactly. That is the
    /// entire value of the project's determinism constraint cashed in: a soak test that could not be
    /// replayed would report bugs nobody could then find.
    /// </para>
    /// </remarks>
    public sealed class SoakTests
    {
        /// <summary>How a given season's player behaves.</summary>
        private readonly struct Policy
        {
            /// <summary>Chance per tick of trying every spawner.</summary>
            public float SpawnEagerness { get; }

            /// <summary>Whether traps are fired the moment they are worth firing.</summary>
            public bool UsesTraps { get; }

            /// <summary>Whether doors get shut in the party's face.</summary>
            public bool ShutsDoors { get; }

            /// <summary>How much of the purse gets spent in the shop.</summary>
            public float SpendShare { get; }

            /// <summary>Whether the shop is skipped for the bonus.</summary>
            public bool PressesReady { get; }

            /// <summary>Builds a policy from a generator.</summary>
            /// <param name="random">Seeded generator.</param>
            public Policy(System.Random random)
            {
                SpawnEagerness = (float)random.NextDouble();
                UsesTraps = random.Next(2) == 0;
                ShutsDoors = random.Next(2) == 0;
                SpendShare = (float)random.NextDouble();
                PressesReady = random.Next(2) == 0;
            }

            /// <summary>Describes the policy for a failure message.</summary>
            public override string ToString() =>
                $"spawn={SpawnEagerness:F2} traps={UsesTraps} doors={ShutsDoors} "
                + $"spend={SpendShare:F2} ready={PressesReady}";
        }

        /// <summary>Plays one season and asserts nothing went wrong inside it.</summary>
        /// <param name="seed">Seed for the league, the rolls and the policy.</param>
        /// <param name="raids">How many raids to play.</param>
        /// <returns>Total harvested across the season.</returns>
        private static float PlaySeason(int seed, int raids)
        {
            var random = new System.Random(seed);
            var policy = new Policy(random);
            var league = new LeagueTable(seed);
            var loadout = new Loadout();
            float bonus = 0f;
            float total = 0f;

            for (int round = 0; round < raids; round++)
            {
                DungeonLayout layout = ShopBot.Build(loadout);

                var raid = new Raid(layout, bonus, null, seed + round);
                bonus = 0f;

                int guard = 0;
                while (raid.IsRunning && guard++ < 5000)
                {
                    if (random.NextDouble() < policy.SpawnEagerness)
                    {
                        foreach (Vector2Int spawner in layout.SpawnerCells)
                        {
                            raid.SpawnMob(spawner);
                        }
                    }

                    if (policy.UsesTraps && raid.IsTrapReady)
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

                    if (policy.ShutsDoors && guard % 37 == 0)
                    {
                        foreach (Door door in layout.Grid.Doors)
                        {
                            if (door.IsOpen && !door.IsForced)
                            {
                                raid.ToggleDoor(door.Cell);
                            }
                        }
                    }

                    raid.Tick(0.02f);

                    AssertRaidIsSane(raid, layout, seed, round, policy);
                }

                Assert.Less(guard, 5000,
                    $"seed {seed} round {round} ({policy}): the raid never ended");

                total += raid.EnergyHarvested;
                league.SubmitRaid(raid.EnergyHarvested);
                if (league.PlayerRelegated)
                {
                    return total;
                }

                league.CollapseRelegated();

                var shop = new Shop(raid.TotalEnergy * policy.SpendShare);
                foreach (ShopItem item in Enum.GetValues(typeof(ShopItem)))
                {
                    while (shop.CanAfford(item) && loadout.Total < 60)
                    {
                        if (!ShopBot.TryBuy(shop, loadout, (ShopItem)item))
                        {
                            break;
                        }
                    }
                }

                if (policy.PressesReady)
                {
                    bonus = shop.Ready();
                }

                Assert.GreaterOrEqual(shop.Purse, 0f, $"seed {seed}: the shop purse went negative");
                Assert.GreaterOrEqual(bonus, 0f, $"seed {seed}: a negative Ready bonus");
            }

            return total;
        }

        /// <summary>The invariants that must hold on every tick of every raid.</summary>
        private static void AssertRaidIsSane(
            Raid raid, DungeonLayout layout, int seed, int round, Policy policy)
        {
            string where = $"seed {seed} round {round} ({policy})";

            Assert.IsFalse(float.IsNaN(raid.CurrentRate), $"{where}: the rate went NaN");
            Assert.IsFalse(float.IsInfinity(raid.CurrentRate), $"{where}: the rate went infinite");
            Assert.GreaterOrEqual(raid.CurrentRate, 0f, $"{where}: a negative rate");
            Assert.GreaterOrEqual(raid.TotalEnergy, 0f, $"{where}: spendable energy went negative");
            Assert.GreaterOrEqual(raid.EnergyHarvested, 0f, $"{where}: negative harvest");
            Assert.GreaterOrEqual(raid.TimeRemaining, 0f, $"{where}: the clock went negative");

            foreach (Adventurer member in raid.Party.Members)
            {
                Assert.IsFalse(float.IsNaN(member.Position.x), $"{where}: an adventurer went NaN");
                Assert.GreaterOrEqual(member.HealthFraction, 0f, $"{where}: health below zero");
                Assert.LessOrEqual(member.HealthFraction, 1f, $"{where}: health above full");
                Assert.GreaterOrEqual(member.ManaFraction, 0f, $"{where}: mana below zero");
                Assert.LessOrEqual(member.ManaFraction, 1f, $"{where}: mana above full");
            }

            foreach (MobManager.Mob mob in raid.Mobs.Living)
            {
                Assert.IsFalse(float.IsNaN(mob.Position.x), $"{where}: a monster went NaN");

                // The rule the whole design leans on: mobs never leave the room they spawned in.
                int room = layout.Grid.RoomAt(mob.Cell);
                Assert.IsTrue(room == mob.HomeRoom || room == DungeonGrid.NoRoom,
                    $"{where}: a {mob.Kind} left room {mob.HomeRoom} for room {room}");
            }
        }

        /// <summary>
        /// A few hundred raids with a different player every season, and nothing breaks.
        /// </summary>
        [Test]
        public void ManySeasons_WithRandomPlayers_HoldTogether()
        {
            const int seasons = 24;
            const int raids = 8;

            float best = 0f;
            float worst = float.MaxValue;
            float total = 0f;

            for (int seed = 1000; seed < 1000 + seasons; seed++)
            {
                float harvested = PlaySeason(seed, raids);
                best = Mathf.Max(best, harvested);
                worst = Mathf.Min(worst, harvested);
                total += harvested;
            }

            MooseRunnerFacade.Log(
                $"{seasons} seasons x {raids} raids = {seasons * raids} raids played. "
                + $"Season harvest: worst {worst:F0}, best {best:F0}, "
                + $"average {total / seasons:F0}");

            Assert.Greater(best, worst,
                "every randomised player earned exactly the same, which cannot be right");
        }

        /// <summary>
        /// A soak season replays identically from its seed.
        /// </summary>
        /// <remarks>
        /// Without this the soak could report a failure nobody could ever reproduce, which is worse
        /// than not running it.
        /// </remarks>
        [Test]
        public void ASoakSeason_ReplaysFromItsSeed()
        {
            var results = new List<float>();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                results.Add(PlaySeason(31337, 6));
            }

            MooseRunnerFacade.Log(
                $"seed 31337 replayed three times: {string.Join(", ", results.ConvertAll(r => r.ToString("F3")))}");

            Assert.AreEqual(results[0], results[1], 0.001f, "replay 2 differed");
            Assert.AreEqual(results[0], results[2], 0.001f, "replay 3 differed");
        }
    }
}
