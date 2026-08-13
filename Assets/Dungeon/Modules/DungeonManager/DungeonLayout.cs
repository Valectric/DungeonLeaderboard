using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.DungeonManager
{
    /// <summary>
    /// A built dungeon: the grid plus the cells the game needs to refer to by name.
    /// </summary>
    /// <remarks>
    /// Milestone 1 is deliberately one corridor of rooms -- entrance on the left, boss room on the
    /// right -- because the gate it has to answer is whether stalling a party with doors is
    /// satisfying. More dungeon content cannot rescue a dull answer, so none is built until the
    /// question is settled.
    /// </remarks>
    public sealed class DungeonLayout
    {
        /// <summary>The grid itself.</summary>
        public DungeonGrid Grid { get; }

        /// <summary>Where the party enters.</summary>
        public Vector2Int EntranceCell { get; }

        /// <summary>Reaching this cell ends the raid and closes the earning window.</summary>
        public Vector2Int BossCell { get; }

        /// <summary>Centre cell of each room, left to right.</summary>
        public IReadOnlyList<Vector2Int> RoomCentres { get; }

        /// <summary>Cells holding a mob spawner the player can fire.</summary>
        public IReadOnlyList<Vector2Int> SpawnerCells { get; }

        /// <summary>Cells holding a trap the player can fire.</summary>
        public IReadOnlyList<Vector2Int> TrapCells { get; }

        /// <summary>Creates a layout. Use <see cref="BuildCorridor"/> rather than calling this.</summary>
        /// <param name="grid">The built grid.</param>
        /// <param name="entrance">Party entry cell.</param>
        /// <param name="boss">Raid-ending cell.</param>
        /// <param name="roomCentres">Centre of each room.</param>
        /// <param name="spawners">Spawner cells.</param>
        /// <param name="traps">Trap cells.</param>
        private DungeonLayout(DungeonGrid grid, Vector2Int entrance, Vector2Int boss,
            IReadOnlyList<Vector2Int> roomCentres, IReadOnlyList<Vector2Int> spawners,
            IReadOnlyList<Vector2Int> traps)
        {
            Grid = grid;
            EntranceCell = entrance;
            BossCell = boss;
            RoomCentres = roomCentres;
            SpawnerCells = spawners;
            TrapCells = traps;
        }

        /// <summary>
        /// Builds a horizontal run of rooms joined by doors.
        /// </summary>
        /// <param name="roomCount">How many rooms, at least two.</param>
        /// <param name="roomWidth">Interior width of each room in cells.</param>
        /// <param name="roomHeight">Interior height of each room in cells.</param>
        /// <param name="doorsStartOpen">
        /// Whether doors begin open. They do: a party that cannot move at all never demonstrates
        /// that stalling is a choice the player is making.
        /// </param>
        /// <returns>The built layout.</returns>
        public static DungeonLayout BuildCorridor(
            int roomCount = 3, int roomWidth = 5, int roomHeight = 5, bool doorsStartOpen = true)
        {
            roomCount = Mathf.Max(2, roomCount);
            roomWidth = Mathf.Max(2, roomWidth);
            roomHeight = Mathf.Max(2, roomHeight);

            // One cell of wall margin all round, so every room has a drawable border.
            const int margin = 1;
            int interiorWidth = (roomCount * roomWidth) + (roomCount - 1);
            var grid = new DungeonGrid(interiorWidth + (margin * 2), roomHeight + (margin * 2));

            var centres = new List<Vector2Int>();
            var spawners = new List<Vector2Int>();
            var traps = new List<Vector2Int>();
            int midY = margin + (roomHeight / 2);

            for (int room = 0; room < roomCount; room++)
            {
                int x0 = margin + (room * (roomWidth + 1));
                grid.CarveRoom(new RectInt(x0, margin, roomWidth, roomHeight), room);
                centres.Add(new Vector2Int(x0 + (roomWidth / 2), midY));

                if (room < roomCount - 1)
                {
                    grid.AddDoor(new Vector2Int(x0 + roomWidth, midY), room, room + 1, doorsStartOpen);
                }

                // Every room past the first earns its keep: somewhere to spawn from and something to
                // stand on. The first room is left clear so the party is never ambushed before the
                // player has had a moment to read the board.
                if (room > 0)
                {
                    spawners.Add(new Vector2Int(x0 + roomWidth - 1, margin));
                    traps.Add(new Vector2Int(x0 + 1, midY));
                }
            }

            var entrance = new Vector2Int(margin, midY);
            var boss = new Vector2Int(margin + interiorWidth - 1, midY);
            return new DungeonLayout(grid, entrance, boss, centres, spawners, traps);
        }
    }
}
