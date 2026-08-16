using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Verifies the shop: prices, the countdown, the Ready bonus, and that a purchase actually
    /// changes the dungeon the next party walks into.
    /// </summary>
    /// <remarks>
    /// The last group matters most. A shop whose purchases only increment a counter would pass every
    /// obvious assertion about money while leaving the game unchanged, which is exactly the class of
    /// bug this project's doctrine warns about: green tests hiding a thing that does nothing.
    /// </remarks>
    public sealed class ShopRulesTests
    {
        /// <summary>A shop rich enough to buy anything.</summary>
        /// <returns>The shop.</returns>
        private static Shop RichShop() => new(2000f);

        /// <summary>A new shop opens with the full thirty seconds and is open for business.</summary>
        [Test]
        public void NewShop_OpensWithFullClock()
        {
            var shop = RichShop();

            Assert.IsTrue(shop.IsOpen, "a new shop should be open");
            Assert.AreEqual(Shop.ShopSeconds, shop.TimeRemaining, 0.001f, "clock should be full");
            Assert.AreEqual(0, shop.Loadout.Total, "nothing owned yet");
        }

        /// <summary>Every one of the six items has a price, and none is free.</summary>
        [Test]
        public void EverySixItem_HasAPrice()
        {
            var shop = RichShop();
            var seen = new HashSet<float>();

            foreach (ShopItem item in System.Enum.GetValues(typeof(ShopItem)))
            {
                float price = shop.Price(item);
                Assert.Greater(price, 0f, $"{item} must cost something");
                seen.Add(price);
            }

            Assert.AreEqual(6, System.Enum.GetValues(typeof(ShopItem)).Length,
                "the spec is explicit that the shop holds exactly six items");
            Assert.Greater(seen.Count, 1, "six identical prices would make the choice meaningless");
        }

        /// <summary>Buying takes the money and hands over the goods.</summary>
        [Test]
        public void Buy_SpendsAndDelivers()
        {
            var shop = new Shop(1000f);
            float price = shop.Price(ShopItem.Skeleton);

            Assert.IsTrue(shop.Buy(ShopItem.Skeleton), "an affordable item should sell");
            Assert.AreEqual(1000f - price, shop.Purse, 0.001f, "purse should be charged");
            Assert.AreEqual(1, shop.Loadout.Count(ShopItem.Skeleton), "should own one");
        }

        /// <summary>A purse that cannot cover the price buys nothing and is not charged.</summary>
        [Test]
        public void Buy_RefusesWhenTooPoor()
        {
            var shop = new Shop(10f);

            Assert.IsFalse(shop.CanAfford(ShopItem.Chest), "10 energy buys nothing");
            Assert.IsFalse(shop.Buy(ShopItem.Chest), "the purchase should be refused");
            Assert.AreEqual(10f, shop.Purse, 0.001f, "a refused purchase must not charge");
            Assert.AreEqual(0, shop.Loadout.Total, "and must not deliver");
        }

        /// <summary>The clock runs down and closes the shop exactly once.</summary>
        [Test]
        public void Tick_ClosesTheShopWhenTheClockRunsOut()
        {
            var shop = RichShop();

            shop.Tick(Shop.ShopSeconds - 1f);
            Assert.IsTrue(shop.IsOpen, "one second left is still open");

            shop.Tick(1f);
            Assert.IsFalse(shop.IsOpen, "the party has arrived");
            Assert.AreEqual(0f, shop.TimeRemaining, 0.001f, "clock should not go negative");
        }

        /// <summary>A closed shop sells nothing, whatever the purse holds.</summary>
        [Test]
        public void ClosedShop_SellsNothing()
        {
            var shop = RichShop();
            shop.Tick(Shop.ShopSeconds);

            Assert.IsFalse(shop.Buy(ShopItem.Slime), "the doors are open, the shop is not");
            Assert.AreEqual(0, shop.Loadout.Total, "nothing should have been delivered");
        }

        /// <summary>
        /// Ready pays for the seconds skipped, so pressing it early is worth more than pressing it
        /// late. This is the whole reason the phase is a decision rather than a menu.
        /// </summary>
        [Test]
        public void Ready_PaysMoreTheEarlierItIsPressed()
        {
            var early = RichShop();
            float earlyBonus = early.Ready();

            var late = RichShop();
            late.Tick(25f);
            float lateBonus = late.Ready();

            Assert.Greater(earlyBonus, lateBonus,
                "skipping more of the shop must pay more, or nobody would ever press it early");
            Assert.AreEqual(Shop.ShopSeconds * Shop.BonusPerSecond, earlyBonus, 0.5f,
                "a full skip pays the full thirty seconds");
            MooseRunnerFacade.Log($"early={earlyBonus} late={lateBonus}");
        }

        /// <summary>Ready closes the shop, and pressing it twice pays only once.</summary>
        [Test]
        public void Ready_ClosesTheShopAndPaysOnce()
        {
            var shop = RichShop();

            float first = shop.Ready();
            float second = shop.Ready();

            Assert.Greater(first, 0f, "the first press should pay");
            Assert.AreEqual(0f, second, 0.001f, "the second must not");
            Assert.IsFalse(shop.IsOpen, "Ready means the doors open now");
        }

        /// <summary>
        /// The Ready bonus is worth roughly one cheap item, so skipping is a real alternative to
        /// buying rather than a rounding error nobody would take.
        /// </summary>
        [Test]
        public void ReadyBonus_IsWorthAboutOneItem()
        {
            var shop = RichShop();
            float bonus = Shop.ShopSeconds * Shop.BonusPerSecond;
            float cheapest = float.MaxValue;

            foreach (ShopItem item in System.Enum.GetValues(typeof(ShopItem)))
            {
                cheapest = Mathf.Min(cheapest, shop.Price(item));
            }

            Assert.Greater(bonus, cheapest * 0.5f, "a bonus nobody would take is not a choice");
            Assert.Less(bonus, cheapest * 3f, "a bonus this good would make the shop pointless");
        }

        /// <summary>Buying a spawner puts a real spawner into the next dungeon.</summary>
        [Test]
        public void BoughtSpawner_AppearsInTheNextDungeon()
        {
            DungeonLayout plain = DungeonLayout.BuildCorridor();
            DungeonLayout bought = DungeonLayout.BuildCorridor(extraSkeletonSpawners: 2);

            Assert.AreEqual(plain.SpawnerCells.Count + 2, bought.SpawnerCells.Count,
                "two bought spawners should be two more spawners");
        }

        /// <summary>Buying a slime pit produces slimes, not the tough default monster.</summary>
        [Test]
        public void SlimePit_SpawnsSlimes()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(extraSlimeSpawners: 1);
            var raid = new Raid(layout);

            // The bought spawner is the last one added.
            Vector2Int bought = layout.SpawnerCells[layout.SpawnerCells.Count - 1];
            Assert.AreEqual(0, layout.SpawnerTierAt(bought), "a slime pit is the light tier");

            Assert.IsTrue(raid.SpawnMob(bought), "the spawner should fire");
            Assert.AreEqual(MobManager.MobKind.Slime, raid.Mobs.Mobs[0].Kind,
                "a slime pit that spawns skeletons is a slime pit in name only");
        }

        /// <summary>Buying a hall makes the corridor longer, so the party has further to walk.</summary>
        [Test]
        public void BoughtHall_MakesTheCorridorLonger()
        {
            DungeonLayout plain = DungeonLayout.BuildCorridor(roomCount: 3);
            DungeonLayout longer = DungeonLayout.BuildCorridor(roomCount: 4);

            // BossCell.y, not .x: the dungeon runs bottom to top since 2026-08-16, so "further
            // away" is further UP. The claim is unchanged -- a bought hall must put the boss room
            // further from the entrance -- only the axis it is measured on moved.
            Assert.Greater(longer.BossCell.y, plain.BossCell.y,
                "another hall must put the boss room further away, or it bought nothing");
            Assert.AreEqual(plain.RoomCentres.Count + 1, longer.RoomCentres.Count,
                "one more room");
        }

        /// <summary>Bought traps and chests land on cells inside the dungeon, not in the walls.</summary>
        [Test]
        public void BoughtFittings_LandOnWalkableFloor()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(extraTraps: 3, chests: 2);

            Assert.AreEqual(2, layout.ChestCells.Count, "two chests bought, two chests placed");

            foreach (Vector2Int cell in layout.TrapCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                    $"trap at {cell} is not in any room");
            }

            foreach (Vector2Int cell in layout.ChestCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                    $"chest at {cell} is not in any room");
            }
        }

        /// <summary>Nothing bought ever lands on top of something else already placed.</summary>
        [Test]
        public void BoughtFittings_NeverStack()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                extraSlimeSpawners: 4, extraSkeletonSpawners: 4, extraTraps: 6, chests: 4);

            AssertNoDuplicates(layout.SpawnerCells, "spawners");
            AssertNoDuplicates(layout.TrapCells, "traps");
            AssertNoDuplicates(layout.ChestCells, "chests");
            Assert.AreEqual(layout.SpawnerCells.Count, layout.SpawnerTiers.Count,
                "every spawner needs a tier or the wrong monster comes out");
        }

        /// <summary>A bought dungeon is still crossable, so the party can still escape.</summary>
        /// <remarks>
        /// The losing ending has to stay reachable. If enough purchases could seal the corridor the
        /// player would be guaranteed a full sixty seconds every raid, and the design's central
        /// tension -- they might leave early -- would quietly stop existing.
        /// </remarks>
        [Test]
        public void HeavilyBoughtDungeon_IsStillCrossable()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 5, extraSlimeSpawners: 4, extraSkeletonSpawners: 4,
                extraTraps: 6, chests: 4);

            List<Vector2Int> path = layout.Grid.FindPath(layout.EntranceCell, layout.BossCell);
            Assert.Greater(path.Count, 0, "the party must still be able to reach the boss room");
        }

        /// <summary>The shop's Ready bonus reaches the next raid as spendable energy.</summary>
        [Test]
        public void ReadyBonus_ArrivesAsStartingEnergy()
        {
            var shop = RichShop();
            float bonus = shop.Ready();

            var plain = new Raid(DungeonLayout.BuildCorridor());
            var boosted = new Raid(DungeonLayout.BuildCorridor(), bonus);

            Assert.AreEqual(plain.TotalEnergy + bonus, boosted.TotalEnergy, 0.001f,
                "the bonus should be spendable in the raid it was bought for");
            Assert.AreEqual(0f, boosted.EnergyHarvested, 0.001f,
                "but it must not count as score -- the league ranks harvest, not savings");
        }

        /// <summary>
        /// A chest genuinely delays the party, rather than merely existing.
        /// </summary>
        /// <remarks>
        /// This is the assertion the whole item stands on. Every other chest test would still pass if
        /// the party walked straight past one: the cell would be placed, the sprite drawn, the money
        /// taken, and nothing about the raid would change. Seconds are the only currency in this
        /// game, so the question is how many seconds a chest buys.
        /// </remarks>
        [Test]
        public void Chest_CostsThePartyRealSeconds()
        {
            float plain = SecondsToReachBoss(DungeonLayout.BuildCorridor());
            float looted = SecondsToReachBoss(DungeonLayout.BuildCorridor(chests: 1));

            MooseRunnerFacade.Log($"crossing: plain={plain:F1}s with one chest={looted:F1}s");
            Assert.Greater(looted, plain + Party.LootSeconds,
                "a chest should cost the party the loot timer plus the walk to reach it");

            // The upper bound is the one that matters. A chest the party cannot quite reach stays
            // its objective forever and deadlocks the raid -- which is precisely what happened at a
            // tighter LootReach, and it looked from the outside like a very effective chest.
            Assert.Less(looted, Raid.RaidSeconds - 1f,
                "the party must still get out; a chest that stalls them forever is a deadlock");
        }

        /// <summary>A chest is emptied once and then ignored, so the party cannot stall forever.</summary>
        [Test]
        public void Chest_IsLootedOnlyOnce()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(chests: 1);
            var raid = new Raid(layout);

            for (int step = 0; step < 3000 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            Assert.AreEqual(1, raid.Party.LootedChests.Count, "the one chest should be emptied once");
            Assert.IsTrue(raid.Party.HasLooted(layout.ChestCells[0]), "and it should be that chest");
        }

        /// <summary>Walks an unopposed party to the boss room and reports how long it took.</summary>
        /// <param name="layout">Dungeon to cross.</param>
        /// <returns>Seconds taken, or the full raid length if they never got there.</returns>
        private static float SecondsToReachBoss(DungeonLayout layout)
        {
            var raid = new Raid(layout);
            float elapsed = 0f;

            // No verbs are used, so nothing but the layout differs between the two runs.
            while (raid.IsRunning && elapsed < Raid.RaidSeconds)
            {
                raid.Tick(0.02f);
                elapsed += 0.02f;
            }

            return elapsed;
        }

        /// <summary>Fails the test when a cell list contains the same cell twice.</summary>
        /// <param name="cells">Cells to check.</param>
        /// <param name="what">Name used in the failure message.</param>
        private static void AssertNoDuplicates(IReadOnlyList<Vector2Int> cells, string what)
        {
            var seen = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in cells)
            {
                Assert.IsTrue(seen.Add(cell), $"two {what} on {cell}");
            }
        }
    }
}
