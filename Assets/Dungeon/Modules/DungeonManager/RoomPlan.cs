using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.DungeonManager
{
    /// <summary>
    /// Which lattice cells hold rooms, before any of it is carved into a grid.
    /// </summary>
    /// <remarks>
    /// The dungeon was a strictly horizontal run: room <c>n</c> sat at
    /// <c>x = margin + n * (width + 1)</c> and every door joined <c>n</c> to <c>n + 1</c>. That made
    /// "buy another hall" a single button, the party's route a straight line, and the boss room
    /// whatever was furthest right.
    /// <para>
    /// A plan is a set of lattice coordinates instead, so a room can be added in any of the four
    /// directions from any room that is not already boxed in. The plan knows nothing about cells,
    /// walls or doors — it is the shape of the dungeon, and <see cref="DungeonLayout"/> turns it into
    /// geometry. Keeping those apart is what lets the shop offer a direction without knowing how wide
    /// a room is.
    /// </para>
    /// </remarks>
    public sealed class RoomPlan
    {
        private readonly List<Vector2Int> _rooms = new();

        /// <summary>The four directions a dungeon can grow in.</summary>
        public static readonly Vector2Int[] Directions =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        /// <summary>Rooms in the order they were added, which is their room index.</summary>
        public IReadOnlyList<Vector2Int> Rooms => _rooms;

        /// <summary>How many rooms the plan holds.</summary>
        public int Count => _rooms.Count;

        /// <summary>Creates a plan with a single room at the origin.</summary>
        public RoomPlan()
        {
            _rooms.Add(Vector2Int.zero);
        }

        /// <summary>Whether a lattice cell already holds a room.</summary>
        /// <param name="lattice">Cell to test.</param>
        /// <returns>True when taken.</returns>
        public bool Holds(Vector2Int lattice) => _rooms.Contains(lattice);

        /// <summary>The index of the room at a lattice cell, or -1.</summary>
        /// <param name="lattice">Cell to look up.</param>
        /// <returns>Room index, or -1 when empty.</returns>
        public int IndexOf(Vector2Int lattice) => _rooms.IndexOf(lattice);

        /// <summary>
        /// Whether a room could be added at a lattice cell.
        /// </summary>
        /// <remarks>
        /// It must be empty and touch an existing room. Rooms that only meet at a corner do not
        /// count: a door joins two rooms through a shared wall, and diagonal neighbours share none.
        /// </remarks>
        /// <param name="lattice">Cell to test.</param>
        /// <returns>True when a room may be built there.</returns>
        public bool CanAdd(Vector2Int lattice)
        {
            if (Holds(lattice))
            {
                return false;
            }

            foreach (Vector2Int direction in Directions)
            {
                if (Holds(lattice + direction))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Adds a room, if the cell allows it.</summary>
        /// <param name="lattice">Cell to build on.</param>
        /// <returns>True when the room was added.</returns>
        public bool Add(Vector2Int lattice)
        {
            if (!CanAdd(lattice))
            {
                return false;
            }

            _rooms.Add(lattice);
            return true;
        }

        /// <summary>
        /// Every lattice cell a new room could be added to, in a stable order.
        /// </summary>
        /// <remarks>
        /// What the shop offers. Ordered by room then by direction rather than by set iteration, so
        /// the markers appear in the same places every frame and a seeded run stays reproducible.
        /// </remarks>
        /// <returns>The buildable cells.</returns>
        public List<Vector2Int> Expansions()
        {
            var found = new List<Vector2Int>();
            foreach (Vector2Int room in _rooms)
            {
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int candidate = room + direction;
                    if (!Holds(candidate) && !found.Contains(candidate))
                    {
                        found.Add(candidate);
                    }
                }
            }

            return found;
        }

        /// <summary>Builds a plan that is a straight run away from the entrance.</summary>
        /// <remarks>
        /// <b>Vertical, at the author's request 2026-08-16</b> — the entrance is at the bottom and the
        /// dungeon runs upward, so the party charges *forward* into the screen rather than sideways
        /// across it. This read as <c>(i, 0)</c> until then and the dungeon ran left to right.
        /// <para>
        /// Nothing else in the builder needed to change for it: <see cref="PlanBuilder.OriginOf"/> is
        /// symmetric in the two axes, and <c>PlanBuilder</c> already walks each room looking right and
        /// up, so a stack of rooms gets its doors on the shared north walls without being asked.
        /// </para>
        /// </remarks>
        /// <param name="roomCount">How many rooms, at least one.</param>
        /// <returns>The plan.</returns>
        public static RoomPlan Corridor(int roomCount)
        {
            var plan = new RoomPlan();
            for (int i = 1; i < Mathf.Max(1, roomCount); i++)
            {
                plan.Add(new Vector2Int(0, i));
            }

            return plan;
        }

        /// <summary>The lattice bounds the plan spans, for sizing a grid.</summary>
        /// <param name="min">Receives the lowest lattice coordinate used.</param>
        /// <param name="max">Receives the highest.</param>
        public void Extent(out Vector2Int min, out Vector2Int max)
        {
            min = _rooms[0];
            max = _rooms[0];
            foreach (Vector2Int room in _rooms)
            {
                min = Vector2Int.Min(min, room);
                max = Vector2Int.Max(max, room);
            }
        }
    }
}
