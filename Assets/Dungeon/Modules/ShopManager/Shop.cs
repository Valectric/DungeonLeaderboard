using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.ShopManager
{
    /// <summary>The six things a dungeon core can buy between raids.</summary>
    /// <remarks>
    /// Six exactly, per SPEC.md section 5: two mob types, two trap types, a door and a chest. The
    /// list is deliberately short -- the demo is proving three verbs, and a shop that sprawls turns
    /// a thirty-second decision into an inventory screen.
    /// </remarks>
    public enum ShopItem
    {
        /// <summary>A tougher mob: holds the party longer.</summary>
        Skeleton = 0,

        /// <summary>A cheap mob: dies faster but costs less.</summary>
        Slime = 1,

        /// <summary>A trap that wounds whoever stands on it.</summary>
        SpikeTrap = 2,

        /// <summary>A second trap type, so a room can hold two.</summary>
        PoisonDart = 3,

        /// <summary>Another door: another place to stall, and another escape route.</summary>
        Door = 4,

        /// <summary>A chest: something for the party to detour to.</summary>
        Chest = 5
    }

    /// <summary>What the player owns going into the next raid.</summary>
    public sealed class Loadout
    {
        private readonly Dictionary<ShopItem, int> _owned = new();

        /// <summary>How many of an item the player has.</summary>
        /// <param name="item">Item to count.</param>
        /// <returns>The count, zero if none.</returns>
        public int Count(ShopItem item) => _owned.GetValueOrDefault(item, 0);

        /// <summary>Adds one of an item.</summary>
        /// <param name="item">Item bought.</param>
        public void Add(ShopItem item) => _owned[item] = Count(item) + 1;

        /// <summary>Total items owned, for display.</summary>
        public int Total
        {
            get
            {
                int total = 0;
                foreach (int count in _owned.Values)
                {
                    total += count;
                }

                return total;
            }
        }
    }

    /// <summary>
    /// The thirty seconds between raids: spend energy, or take a bonus for starting early.
    /// </summary>
    /// <remarks>
    /// This is a Module.
    /// <para>
    /// The Ready button is the whole design of this phase. Starting early pays a bonus scaled to the
    /// seconds skipped, so the greedy player takes the money and goes in under-equipped, and the
    /// careful one spends the full thirty seconds and forfeits it. Both are defensible, which is
    /// what makes it a decision rather than a menu.
    /// </para>
    /// </remarks>
    public sealed class Shop
    {
        /// <summary>How long the shop stays open.</summary>
        public const float ShopSeconds = 30f;

        /// <summary>Energy granted per second skipped by pressing Ready.</summary>
        /// <remarks>
        /// Worth roughly a cheap item if pressed immediately, so skipping is a real alternative to
        /// buying rather than a rounding error nobody would take.
        /// </remarks>
        public const float BonusPerSecond = 4f;

        private readonly Dictionary<ShopItem, float> _prices = new()
        {
            [ShopItem.Skeleton] = 125f,
            [ShopItem.Slime] = 100f,
            [ShopItem.SpikeTrap] = 100f,
            [ShopItem.PoisonDart] = 100f,
            [ShopItem.Door] = 75f,
            [ShopItem.Chest] = 75f
        };

        /// <summary>Energy the player has to spend.</summary>
        public float Purse { get; private set; }

        /// <summary>Seconds left before the next party enters.</summary>
        public float TimeRemaining { get; private set; } = ShopSeconds;

        /// <summary>What the player owns for the next raid.</summary>
        public Loadout Loadout { get; } = new();

        /// <summary>Whether the shop is still open.</summary>
        public bool IsOpen { get; private set; } = true;

        /// <summary>Bonus earned by pressing Ready, once the shop has closed.</summary>
        public float EarlyBonus { get; private set; }

        /// <summary>Opens a shop with the energy the player has accumulated.</summary>
        /// <param name="purse">Energy available to spend.</param>
        public Shop(float purse)
        {
            Purse = purse;
        }

        /// <summary>What an item costs.</summary>
        /// <param name="item">Item to price.</param>
        /// <returns>Its price in energy.</returns>
        public float Price(ShopItem item) => _prices[item];

        /// <summary>Whether the player can afford an item right now.</summary>
        /// <param name="item">Item to test.</param>
        /// <returns>True when it is affordable and the shop is open.</returns>
        public bool CanAfford(ShopItem item) => IsOpen && Purse >= Price(item);

        /// <summary>
        /// Buys one of an item.
        /// </summary>
        /// <param name="item">Item to buy.</param>
        /// <returns>True when the purchase went through.</returns>
        public bool Buy(ShopItem item)
        {
            if (!CanAfford(item))
            {
                return false;
            }

            Purse -= Price(item);
            Loadout.Add(item);
            return true;
        }

        /// <summary>Counts the shop down, closing it when the next party arrives.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(float deltaTime)
        {
            if (!IsOpen)
            {
                return;
            }

            TimeRemaining = Mathf.Max(0f, TimeRemaining - deltaTime);
            if (TimeRemaining <= 0f)
            {
                IsOpen = false;
            }
        }

        /// <summary>
        /// Closes the shop early and banks a bonus for the time skipped.
        /// </summary>
        /// <returns>The bonus granted.</returns>
        public float Ready()
        {
            if (!IsOpen)
            {
                return 0f;
            }

            EarlyBonus = MathF.Round(TimeRemaining * BonusPerSecond);
            Purse += EarlyBonus;
            TimeRemaining = 0f;
            IsOpen = false;
            return EarlyBonus;
        }

        /// <summary>The bonus pressing Ready would pay right now, for the button's label.</summary>
        public float PendingBonus => MathF.Round(TimeRemaining * BonusPerSecond);
    }
}
