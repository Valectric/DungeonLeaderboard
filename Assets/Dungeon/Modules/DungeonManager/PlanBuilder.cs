using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.DungeonManager
{
    /// <summary>
    /// Turns a <see cref="RoomPlan"/> into carved cells, walls and doors.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="DungeonLayout"/> so the geometry has one home and the layout keeps its
    /// public surface. Everything here is arithmetic on the lattice: a room at lattice
    /// <c>(gx, gy)</c> occupies a fixed rectangle, and two rooms that are lattice neighbours share a
    /// wall with a door in the middle of it.
    /// </remarks>
    public static class PlanBuilder
    {
        /// <summary>Cells of wall kept around the whole dungeon, so every room has a border.</summary>
        public const int Margin = 1;

        /// <summary>
        /// Carves every room in a plan and joins the neighbours with doors.
        /// </summary>
        /// <param name="plan">Shape of the dungeon.</param>
        /// <param name="roomWidth">Interior width of a room, in cells.</param>
        /// <param name="roomHeight">Interior height of a room, in cells.</param>
        /// <param name="doorsStartOpen">Whether the doors begin open.</param>
        /// <param name="centres">Receives the centre cell of each room, indexed by room.</param>
        /// <returns>The carved grid.</returns>
        public static DungeonGrid Build(
            RoomPlan plan, int roomWidth, int roomHeight, bool doorsStartOpen,
            out List<Vector2Int> centres)
        {
            plan.Extent(out Vector2Int min, out Vector2Int max);
            Vector2Int span = max - min + Vector2Int.one;

            int width = (span.x * roomWidth) + (span.x - 1) + (Margin * 2);
            int height = (span.y * roomHeight) + (span.y - 1) + (Margin * 2);
            var grid = new DungeonGrid(width, height);

            centres = new List<Vector2Int>();
            for (int room = 0; room < plan.Count; room++)
            {
                Vector2Int origin = OriginOf(plan.Rooms[room], min, roomWidth, roomHeight);
                grid.CarveRoom(new RectInt(origin.x, origin.y, roomWidth, roomHeight), room);
                centres.Add(new Vector2Int(
                    origin.x + (roomWidth / 2), origin.y + (roomHeight / 2)));
            }

            // One door per shared wall, and only once per pair -- walking the rooms in order and
            // looking only right and up cannot produce a duplicate the way looking all four ways
            // would.
            for (int room = 0; room < plan.Count; room++)
            {
                Vector2Int lattice = plan.Rooms[room];
                Vector2Int origin = OriginOf(lattice, min, roomWidth, roomHeight);

                int east = plan.IndexOf(lattice + Vector2Int.right);
                if (east >= 0)
                {
                    grid.AddDoor(
                        new Vector2Int(origin.x + roomWidth, origin.y + (roomHeight / 2)),
                        room, east, doorsStartOpen);
                }

                int north = plan.IndexOf(lattice + Vector2Int.up);
                if (north >= 0)
                {
                    grid.AddDoor(
                        new Vector2Int(origin.x + (roomWidth / 2), origin.y + roomHeight),
                        room, north, doorsStartOpen);
                }
            }

            return grid;
        }

        /// <summary>Bottom-left interior cell of the room at a lattice coordinate.</summary>
        /// <param name="lattice">Lattice coordinate of the room.</param>
        /// <param name="min">Lowest lattice coordinate in the plan, which anchors the grid.</param>
        /// <param name="roomWidth">Interior width of a room.</param>
        /// <param name="roomHeight">Interior height of a room.</param>
        /// <returns>The room's origin cell.</returns>
        public static Vector2Int OriginOf(
            Vector2Int lattice, Vector2Int min, int roomWidth, int roomHeight)
        {
            return new Vector2Int(
                Margin + ((lattice.x - min.x) * (roomWidth + 1)),
                Margin + ((lattice.y - min.y) * (roomHeight + 1)));
        }
    }
}
