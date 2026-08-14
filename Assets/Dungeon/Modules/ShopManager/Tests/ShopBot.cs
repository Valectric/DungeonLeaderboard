using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Buys and builds the way the shipped game does, for the sweeps that measure balance.
    /// </summary>
    /// <remarks>
    /// The season and soak sweeps are the authority on whether the economy works — D13's rival
    /// earnings were set from what they measured. That only holds while they play the game that
    /// ships. When the shop became spatial they went on buying counts and scattering them by the old
    /// formula, so they were measuring a dungeon nobody would ever be handed: same number of
    /// spawners, different rooms, different distances, different fight lengths.
    /// <para>
    /// This is the bot's placement policy — fill from the entrance outwards — and the translation
    /// from purchases to furniture, matching <c>GameController</c>. It is deliberately one shared
    /// copy; two sweeps with two policies would disagree about the same season.
    /// </para>
    /// </remarks>
    public static class ShopBot
    {
        /// <summary>Deepest the corridor is allowed to get, matching the controller's cap.</summary>
        public const int MaxRooms = 5;

        /// <summary>Builds the dungeon a loadout describes, placements and all.</summary>
        /// <param name="loadout">What the bot has bought so far.</param>
        /// <returns>The layout for the next raid.</returns>
        public static DungeonLayout Build(Loadout loadout)
        {
            return DungeonLayout.BuildCorridor(
                roomCount: Mathf.Min(MaxRooms, 3 + loadout.Count(ShopItem.Door)),
                placed: Furniture(loadout));
        }

        /// <summary>Turns purchases into the cells the dungeon should be furnished with.</summary>
        /// <param name="loadout">What the bot has bought so far.</param>
        /// <returns>Furniture positioned where it was bought.</returns>
        public static Furnishings Furniture(Loadout loadout)
        {
            var furniture = new Furnishings();
            foreach (Placement placement in loadout.Placements)
            {
                switch (placement.Item)
                {
                    case ShopItem.Slime:
                        furniture.SlimeSpawners.Add(placement.Cell);
                        break;
                    case ShopItem.Skeleton:
                        furniture.SkeletonSpawners.Add(placement.Cell);
                        break;
                    case ShopItem.SpikeTrap:
                    case ShopItem.PoisonDart:
                        furniture.Traps.Add(placement.Cell);
                        break;
                    case ShopItem.Chest:
                        furniture.Chests.Add(placement.Cell);
                        break;
                }
            }

            return furniture;
        }

        /// <summary>
        /// Buys one item the way a player would: onto a tile, or as a whole new hall.
        /// </summary>
        /// <param name="shop">Shop to buy from.</param>
        /// <param name="loadout">Persistent loadout to record the purchase in.</param>
        /// <param name="item">Item to buy.</param>
        /// <returns>True when the purchase went through.</returns>
        public static bool TryBuy(Shop shop, Loadout loadout, ShopItem item)
        {
            if (item == ShopItem.Door)
            {
                // A hall is bought from the marker at the end of the corridor, and refused once the
                // corridor has reached the cap -- otherwise the bot spends on halls that are never
                // built and the season reads as poorer than it is.
                if (loadout.Count(ShopItem.Door) >= MaxRooms - 3 || !shop.Buy(item))
                {
                    return false;
                }

                loadout.Add(item);
                return true;
            }

            Vector2Int cell = FirstFreeCell(Build(loadout));
            if (cell.x < 0 || !shop.BuyAt(item, cell))
            {
                return false;
            }

            loadout.Add(item, cell);
            return true;
        }

        /// <summary>Finds the first tile the player could build on, scanning from the entrance.</summary>
        /// <param name="layout">Dungeon to search.</param>
        /// <returns>A buildable cell, or a negative cell when the dungeon is full.</returns>
        private static Vector2Int FirstFreeCell(DungeonLayout layout)
        {
            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.CanBuildOn(cell))
                    {
                        return cell;
                    }
                }
            }

            return new Vector2Int(-1, -1);
        }
    }
}
