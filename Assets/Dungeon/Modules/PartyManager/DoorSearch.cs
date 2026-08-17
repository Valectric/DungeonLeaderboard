using System.Collections.Generic;
using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.PartyManager
{
    /// <summary>
    /// Finds the shut door standing between the party and where it wants to go.
    /// </summary>
    /// <remarks>
    /// Split out of <c>Party</c>, which was 1300 lines against a 400-line cap. These are pure
    /// questions about the grid — given a leader standing somewhere and a place they want to reach,
    /// which door is in the way and where do they stand to force it — so they carry none of the
    /// party's state and read far better away from the code that mutates it.
    /// <para>
    /// <b>Why this is not just pathfinding.</b> A closed door is not walkable, so the route the
    /// party would have taken does not exist as far as <c>FindPath</c> is concerned; asking it
    /// returns no path and no explanation. Every method here works around that by asking a
    /// reachability question first and then looking at doors directly.
    /// </para>
    /// </remarks>
    public sealed class DoorSearch
    {
        /// <summary>The dungeon being searched.</summary>
        private readonly DungeonGrid _grid;

        /// <summary>Where the party came in, and where it retreats to.</summary>
        private readonly Vector2Int _entranceCell;

        /// <summary>Where the party is trying to get to.</summary>
        private readonly Vector2Int _bossCell;

        /// <summary>
        /// Binds a search to one dungeon and its two landmarks.
        /// </summary>
        /// <param name="grid">The dungeon to search.</param>
        /// <param name="entranceCell">Where the party came in.</param>
        /// <param name="bossCell">Where the party is heading.</param>
        public DoorSearch(DungeonGrid grid, Vector2Int entranceCell, Vector2Int bossCell)
        {
            _grid = grid;
            _entranceCell = entranceCell;
            _bossCell = bossCell;
        }

        /// <summary>
        /// The shut door standing between the party and the boss room, if any.
        /// </summary>
        /// <remarks>
        /// Found by asking which door the party would path through if every door were open, then
        /// checking whether that one is actually shut. Pathfinding cannot answer this directly --
        /// a closed door is simply not walkable, so the route it would have been on does not exist.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>The door to deal with, or null when the way is clear.</returns>
        public Door TowardBoss(Adventurer leader)
        {
            return OnWayTo(_bossCell, leader);
        }

        /// <summary>
        /// The shut door standing between the party and the way out, if any.
        /// </summary>
        /// <remarks>
        /// A retreating party has a different obstacle from an advancing one, and asking the wrong
        /// question strands them: the door barring the route deeper is frequently not the door
        /// barring the route home. Shutting the door a party has just walked through is the player's
        /// most natural move, and it used to leave them standing against it for the rest of the raid.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>The door to deal with, or null when the way out is clear.</returns>
        public Door TowardExit(Adventurer leader)
        {
            return OnWayTo(_entranceCell, leader);
        }

        /// <summary>
        /// The shut door standing between the party and somewhere it wants to be.
        /// </summary>
        /// <remarks>
        /// Found by asking whether a route exists at all, then looking for a shut door on this
        /// room's threshold. Pathfinding cannot answer this directly -- a closed door is simply not
        /// walkable, so the route it would have been on does not exist.
        /// </remarks>
        /// <param name="destination">Where the party is trying to get to.</param>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>The door to deal with, or null when the way is clear.</returns>
        public Door OnWayTo(Vector2Int destination, Adventurer leader)
        {
            if (leader.Cell == destination || _grid.FindPath(leader.Cell, destination).Count > 0)
            {
                return null;
            }

            int room = _grid.RoomAt(leader.Cell);
            Door nearest = null;
            float best = float.MaxValue;

            foreach (Door door in _grid.Doors)
            {
                // Only a door on this room's threshold, and only one that still bars the way.
                if (door.IsOpen || (door.RoomA != room && door.RoomB != room))
                {
                    continue;
                }

                float distance = Vector2.Distance(leader.Position, door.Cell);
                if (distance < best)
                {
                    best = distance;
                    nearest = door;
                }
            }

            // Nothing shut on this room's threshold, yet the boss room is still unreachable: the
            // party has already opened its own way out and the next door along is somebody else's
            // threshold. Without this the objective fell through to a boss cell no path could reach,
            // MoveAlongPath had nowhere to go, and the party STOOD STILL FOR THE REST OF THE RAID.
            //
            // Measured with every door shut: all six rosters forced the first door at six or seven
            // seconds and then sat at cell (5,3) in room zero for the remaining fifty-three,
            // earning the idle floor. Two tests had encoded that as correct -- one asserting the
            // party is still in the first room after twenty seconds, one expecting the clock to run
            // out -- so the freeze was protected rather than caught.
            return nearest ?? NearestReachableShutDoor(leader);
        }

        /// <summary>
        /// The nearest shut door the party can actually walk to, wherever it is.
        /// </summary>
        /// <remarks>
        /// The fallback for a party that has opened its own room and now needs to cross another to
        /// reach the next obstacle. Reachability is the whole point: a door behind two more shut
        /// doors is no use as an objective, and pathing to it would strand the party exactly as
        /// before.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>A shut door with a walkable route to it, or null.</returns>
        private Door NearestReachableShutDoor(Adventurer leader)
        {
            Door nearest = null;
            int shortest = int.MaxValue;

            foreach (Door door in _grid.Doors)
            {
                if (door.IsOpen)
                {
                    continue;
                }

                Vector2Int approach = ApproachCell(door, leader);
                if (approach == leader.Cell)
                {
                    return door;
                }

                List<Vector2Int> route = _grid.FindPath(leader.Cell, approach);
                if (route.Count > 0 && route.Count < shortest)
                {
                    shortest = route.Count;
                    nearest = door;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The walkable square beside a door, on the party's side of it.
        /// </summary>
        /// <param name="door">Door to approach.</param>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>A cell to stand on, or the door's own cell if none is walkable.</returns>
        public Vector2Int ApproachCell(Door door, Adventurer leader)
        {
            Vector2Int best = door.Cell;
            float bestDistance = float.MaxValue;

            foreach (Vector2Int step in new[]
                     {
                         Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
                     })
            {
                Vector2Int candidate = door.Cell + step;
                if (!_grid.IsWalkable(candidate))
                {
                    continue;
                }

                float distance = Vector2.Distance(leader.Position, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
