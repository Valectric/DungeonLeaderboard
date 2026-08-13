using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Stress-tests the shop's money, looking for ways the purse can be cheated or stranded.
    /// </summary>
    /// <remarks>
    /// Purchases are permanent for a whole season, so anything that lets energy be spent twice or
    /// conjured from nothing compounds every round rather than costing one raid. These are the
    /// assertions that a purse cannot ratchet.
    /// </remarks>
    public sealed class ShopEconomySweepTests
    {
        /// <summary>Every item in a fixed order.</summary>
        private static readonly ShopItem[] Items =
        {
            ShopItem.Skeleton, ShopItem.Slime, ShopItem.SpikeTrap,
            ShopItem.PoisonDart, ShopItem.Door, ShopItem.Chest
        };

        /// <summary>
        /// Energy is conserved: what leaves the purse equals what arrives in the loadout.
        /// </summary>
        [Test]
        public void EnergyIsConserved_AcrossAnyBuyingSpree()
        {
            for (int purse = 0; purse <= 2000; purse += 137)
            {
                var shop = new Shop(purse);
                float spent = 0f;

                foreach (ShopItem item in Items)
                {
                    while (shop.CanAfford(item))
                    {
                        float price = shop.Price(item);
                        Assert.IsTrue(shop.Buy(item), "an affordable item refused to sell");
                        spent += price;
                    }
                }

                Assert.AreEqual(purse - spent, shop.Purse, 0.001f,
                    $"starting with {purse}, the books do not balance");
                Assert.GreaterOrEqual(shop.Purse, 0f, "the purse went negative");
            }
        }

        /// <summary>
        /// A shop cannot be milked by pressing Ready more than once.
        /// </summary>
        /// <remarks>
        /// The bonus scales with time remaining and closes the shop. If a second press paid again,
        /// or if the clock could be re-read after closing, a player could mint energy from a button.
        /// </remarks>
        [Test]
        public void ReadyCannotBeMilked()
        {
            var shop = new Shop(500f);
            float first = shop.Ready();
            float total = first;

            for (int i = 0; i < 20; i++)
            {
                total += shop.Ready();
            }

            MooseRunnerFacade.Log($"first Ready paid {first}, twenty more paid {total - first}");
            Assert.Greater(first, 0f, "the first press should pay");
            Assert.AreEqual(first, total, 0.001f, "pressing Ready again minted energy");
        }

        /// <summary>Ticking a closed shop cannot revive it or move its clock.</summary>
        [Test]
        public void AClosedShop_StaysClosed()
        {
            var shop = new Shop(1000f);
            shop.Ready();

            for (int i = 0; i < 100; i++)
            {
                shop.Tick(1f);
            }

            Assert.IsFalse(shop.IsOpen, "a closed shop reopened");
            Assert.AreEqual(0f, shop.TimeRemaining, 0.001f, "the clock moved after closing");
            Assert.IsFalse(shop.Buy(ShopItem.Chest), "a closed shop sold something");
            Assert.AreEqual(0f, shop.PendingBonus, 0.001f, "a closed shop still offered a bonus");
        }

        /// <summary>
        /// Waiting in the shop is never worth more than starting early.
        /// </summary>
        /// <remarks>
        /// The Ready bonus falls as the clock runs down, so the bonus alone always favours leaving.
        /// What must not happen is the bonus <i>rising</i> with time spent, which would make the
        /// decision one-sided in the other direction and delete the phase's whole tension.
        /// </remarks>
        [Test]
        public void TheReadyBonus_OnlyEverFalls()
        {
            var shop = new Shop(100f);
            float previous = shop.PendingBonus;

            for (float t = 0f; t < Shop.ShopSeconds; t += 1f)
            {
                shop.Tick(1f);
                Assert.LessOrEqual(shop.PendingBonus, previous + 0.001f,
                    $"the bonus rose at t={t}");
                previous = shop.PendingBonus;
            }

            Assert.AreEqual(0f, previous, 0.001f, "the bonus should be nothing at the bell");
        }

        /// <summary>A negative or absurd purse cannot buy anything or crash the shop.</summary>
        [Test]
        public void HostilePurses_AreHandled()
        {
            foreach (float purse in new[] { -1000f, -1f, 0f, 0.0001f, float.MaxValue })
            {
                var shop = new Shop(purse);
                foreach (ShopItem item in Items)
                {
                    bool affordable = shop.CanAfford(item);
                    bool bought = shop.Buy(item);
                    Assert.AreEqual(affordable, bought,
                        $"purse {purse}: CanAfford and Buy disagreed about {item}");
                }

                Assert.IsFalse(float.IsNaN(shop.Purse), $"purse {purse} produced NaN");
            }
        }

        /// <summary>
        /// Every item is reachable from a plausible raid's takings.
        /// </summary>
        /// <remarks>
        /// An item nobody can ever afford is dead content occupying one of the spec's six slots. A
        /// competent raid leaves a few hundred spendable, so everything should be within reach of one
        /// or two good rounds.
        /// </remarks>
        [Test]
        public void EveryItem_IsAffordableFromARealRaid()
        {
            var shop = new Shop(300f);
            var unaffordable = new List<ShopItem>();

            foreach (ShopItem item in Items)
            {
                if (!shop.CanAfford(item))
                {
                    unaffordable.Add(item);
                }
            }

            MooseRunnerFacade.Log(
                $"with 300 to spend, cannot afford: "
                + (unaffordable.Count == 0 ? "nothing" : string.Join(", ", unaffordable)));
            Assert.IsEmpty(unaffordable, "some item is out of reach of a good raid's leftovers");
        }

        /// <summary>The loadout counts exactly what was bought, and nothing else.</summary>
        [Test]
        public void TheLoadout_CountsExactlyWhatWasBought()
        {
            var shop = new Shop(10000f);
            var expected = new Dictionary<ShopItem, int>();

            for (int i = 0; i < 40; i++)
            {
                ShopItem item = Items[i % Items.Length];
                if (shop.Buy(item))
                {
                    expected[item] = expected.GetValueOrDefault(item, 0) + 1;
                }
            }

            int total = 0;
            foreach (KeyValuePair<ShopItem, int> pair in expected)
            {
                Assert.AreEqual(pair.Value, shop.Loadout.Count(pair.Key),
                    $"{pair.Key} count is wrong");
                total += pair.Value;
            }

            Assert.AreEqual(total, shop.Loadout.Total, "the loadout total does not match its parts");
        }
    }
}
