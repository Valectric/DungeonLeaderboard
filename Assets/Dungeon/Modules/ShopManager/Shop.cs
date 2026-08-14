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

    /// <summary>One bought item and the cell the player put it on.</summary>
    /// <remarks>
    /// The shop used to sell counts and let the dungeon decide where things went, which meant the
    /// player bought a spawner and then found out afterwards where it had landed. Buying is a
    /// placement decision now, so the purchase and the position are one fact rather than two.
    /// </remarks>
    public readonly struct Placement
    {
        /// <summary>What was bought.</summary>
        public ShopItem Item { get; }

        /// <summary>Where it goes.</summary>
        public Vector2Int Cell { get; }

        /// <summary>Records a placed purchase.</summary>
        /// <param name="item">Item bought.</param>
        /// <param name="cell">Cell it was placed on.</param>
        public Placement(ShopItem item, Vector2Int cell)
        {
            Item = item;
            Cell = cell;
        }
    }

    /// <summary>What the player owns going into the next raid.</summary>
    public sealed class Loadout
    {
        private readonly Dictionary<ShopItem, int> _owned = new();
        private readonly List<Placement> _placements = new();

        /// <summary>How many of an item the player has.</summary>
        /// <param name="item">Item to count.</param>
        /// <returns>The count, zero if none.</returns>
        public int Count(ShopItem item) => _owned.GetValueOrDefault(item, 0);

        /// <summary>
        /// Every purchase the player put on a specific cell, in the order they were bought.
        /// </summary>
        /// <remarks>
        /// A <see cref="ShopItem.Door"/> is never in here: a hall is a section of the dungeon rather
        /// than something standing on a tile, so it is bought from the marker at the end of the
        /// corridor and shows up as a room count instead of a placement.
        /// </remarks>
        public IReadOnlyList<Placement> Placements => _placements;

        /// <summary>Adds one of an item without saying where it goes.</summary>
        /// <param name="item">Item bought.</param>
        public void Add(ShopItem item) => _owned[item] = Count(item) + 1;

        /// <summary>Adds one of an item, on a cell the player chose.</summary>
        /// <param name="item">Item bought.</param>
        /// <param name="cell">Cell the player put it on.</param>
        public void Add(ShopItem item, Vector2Int cell)
        {
            Add(item);
            _placements.Add(new Placement(item, cell));
        }

        /// <summary>Whether anything has already been placed on a cell.</summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True when the cell is taken.</returns>
        public bool Occupies(Vector2Int cell)
        {
            foreach (Placement placement in _placements)
            {
                if (placement.Cell == cell)
                {
                    return true;
                }
            }

            return false;
        }

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

        /// <summary>
        /// Buys one of an item and puts it on a cell.
        /// </summary>
        /// <remarks>
        /// Refuses a cell that already holds something the player bought. Two spawners on one tile
        /// would draw over each other and be firable twice from a single tap, and the player would
        /// have paid twice for one usable thing.
        /// </remarks>
        /// <param name="item">Item to buy.</param>
        /// <param name="cell">Cell to place it on.</param>
        /// <returns>True when the purchase went through.</returns>
        public bool BuyAt(ShopItem item, Vector2Int cell)
        {
            if (!CanAfford(item) || Loadout.Occupies(cell))
            {
                return false;
            }

            Purse -= Price(item);
            Loadout.Add(item, cell);
            return true;
        }

        /// <summary>The five items that stand on a tile, as opposed to buying a whole hall.</summary>
        /// <remarks>
        /// Ordered cheapest first so the popup reads as a price list and the affordable end is
        /// nearest the tap.
        /// </remarks>
        public static readonly ShopItem[] Placeable =
        {
            ShopItem.Chest, ShopItem.Slime, ShopItem.SpikeTrap,
            ShopItem.PoisonDart, ShopItem.Skeleton
        };

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
