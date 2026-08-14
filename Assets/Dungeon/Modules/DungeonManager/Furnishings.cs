using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.DungeonManager
{
    /// <summary>
    /// Exactly where the player put each thing they bought.
    /// </summary>
    /// <remarks>
    /// The dungeon used to place purchases itself, scattering them across the rooms past the first by
    /// a formula. That was fine while the shop sold counts, and wrong the moment the player could
    /// point at a tile: a spawner bought on a specific square has to appear on that square.
    /// <para>
    /// Stated in plain cells rather than shop items so this module stays ignorant of the shop. The
    /// application layer owns both and does the translation, which keeps the dependency running one
    /// way — the dungeon knows what a spawner is, and nothing here knows what one costs.
    /// </para>
    /// </remarks>
    public sealed class Furnishings
    {
        /// <summary>Cells the player put a slime spawner on.</summary>
        public List<Vector2Int> SlimeSpawners { get; } = new();

        /// <summary>Cells the player put a skeleton spawner on.</summary>
        public List<Vector2Int> SkeletonSpawners { get; } = new();

        /// <summary>Cells the player put a trap on.</summary>
        public List<Vector2Int> Traps { get; } = new();

        /// <summary>Cells the player put a chest on.</summary>
        public List<Vector2Int> Chests { get; } = new();

        /// <summary>Whether the player placed nothing at all.</summary>
        public bool IsEmpty =>
            SlimeSpawners.Count == 0 && SkeletonSpawners.Count == 0
                                     && Traps.Count == 0 && Chests.Count == 0;

        /// <summary>
        /// Lands every placement in a grid being built, dropping any that no longer fits.
        /// </summary>
        /// <remarks>
        /// The room check is not defensive padding. A player can furnish a hall and then have the
        /// corridor come out shorter than the placement assumed — the room count is capped, and a
        /// purchase made against a five-room preview must not leave a spawner floating in the rock
        /// where nothing can ever reach it. Dropping it is the honest outcome; the alternative is a
        /// dungeon whose contents disagree with its walls.
        /// </remarks>
        /// <param name="grid">Grid being built, for the room lookup.</param>
        /// <param name="spawners">Spawner cells to append to.</param>
        /// <param name="spawnerTiers">Tiers parallel to <paramref name="spawners"/>.</param>
        /// <param name="traps">Trap cells to append to.</param>
        /// <param name="chests">Chest cells to append to.</param>
        public void ApplyTo(DungeonGrid grid, List<Vector2Int> spawners, List<int> spawnerTiers,
            List<Vector2Int> traps, List<Vector2Int> chests)
        {
            AddSpawners(grid, SlimeSpawners, 0, spawners, spawnerTiers);
            AddSpawners(grid, SkeletonSpawners, 1, spawners, spawnerTiers);

            foreach (Vector2Int cell in Traps)
            {
                if (grid.RoomAt(cell) != DungeonGrid.NoRoom && !traps.Contains(cell))
                {
                    traps.Add(cell);
                }
            }

            foreach (Vector2Int cell in Chests)
            {
                if (grid.RoomAt(cell) != DungeonGrid.NoRoom
                    && !chests.Contains(cell) && !spawners.Contains(cell))
                {
                    chests.Add(cell);
                }
            }
        }

        /// <summary>Appends spawner cells of one tier, skipping cells outside a room or taken.</summary>
        /// <param name="grid">Grid being built.</param>
        /// <param name="cells">Cells the player placed.</param>
        /// <param name="tier">Tier to record for each.</param>
        /// <param name="spawners">Spawner cells to append to.</param>
        /// <param name="spawnerTiers">Tiers parallel to <paramref name="spawners"/>.</param>
        private static void AddSpawners(DungeonGrid grid, List<Vector2Int> cells, int tier,
            List<Vector2Int> spawners, List<int> spawnerTiers)
        {
            foreach (Vector2Int cell in cells)
            {
                if (grid.RoomAt(cell) != DungeonGrid.NoRoom && !spawners.Contains(cell))
                {
                    spawners.Add(cell);
                    spawnerTiers.Add(tier);
                }
            }
        }
    }
}
