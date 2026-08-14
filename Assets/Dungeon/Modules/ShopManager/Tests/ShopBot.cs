using Dungeon.DungeonManager;
using Dungeon.RaidManager;
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

        /// <summary>
        /// Plays one raid the way a competent player would, and reports how long it took.
        /// </summary>
        /// <remarks>
        /// Spawners do not fire themselves — spawning is one of the game's three verbs, so a raid
        /// nobody plays meets no monsters at all. A sweep that ticks a raid without pressing anything
        /// measures an empty corridor and reports a harvest of one, whatever was bought. That is
        /// exactly what the first version of the placement sweep did, and it made twelve spawners
        /// look worthless.
        /// <para>
        /// Competent, not a masher: spawning at every spawner every tick drains the purse to nothing
        /// and leaves the shop unreachable. One mob per empty room, traps fired under the party's
        /// feet, and always a reserve kept back.
        /// </para>
        /// </remarks>
        /// <param name="raid">Raid to play.</param>
        /// <param name="layout">Dungeon being raided.</param>
        /// <param name="aggressive">
        /// Whether to use every spawner in a room rather than keeping one monster alive at a time.
        /// <para>
        /// Off by default, and the default is load-bearing: D13's rival earnings were calibrated
        /// against the conservative policy, so changing it silently re-tunes the whole league. Turn
        /// it on for tests about what a player can do to themselves, not for tests about balance.
        /// </para>
        /// <para>
        /// It exists because the conservative policy makes buying spawners look pointless: it only
        /// spawns into an <i>empty</i> room, so twelve bone piles crammed into one room behave
        /// exactly like one. Measured, densities of 1 and 12 harvested an identical 378 — the sweep
        /// that was supposed to prove stacking cannot break the game was comparing a dungeon against
        /// itself six times.
        /// </para>
        /// </param>
        /// <returns>Seconds of simulated time, or -1 if the raid never ended.</returns>
        public static float Play(Raid raid, DungeonLayout layout, bool aggressive = false)
        {
            int ticks = 0;
            while (raid.IsRunning && ticks++ < 5000)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    int room = layout.Grid.RoomAt(spawner);
                    int allowed = aggressive ? SpawnersIn(layout, room) : 1;
                    if (raid.Mobs.CountInRoom(room) < allowed &&
                        raid.TotalEnergy > Raid.SpawnCost * 2f)
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

            return ticks >= 5000 ? -1f : ticks * 0.02f;
        }

        /// <summary>How many spawners a room holds, which caps how hard it can be played.</summary>
        /// <param name="layout">Dungeon to count in.</param>
        /// <param name="room">Room index.</param>
        /// <returns>The spawner count, at least one.</returns>
        private static int SpawnersIn(DungeonLayout layout, int room)
        {
            int count = 0;
            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                if (layout.Grid.RoomAt(spawner) == room)
                {
                    count++;
                }
            }

            return Mathf.Max(1, count);
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
