using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.DungeonManager
{
    /// <summary>What occupies a single dungeon cell.</summary>
    public enum CellKind
    {
        /// <summary>Solid. Never walkable.</summary>
        Wall = 0,

        /// <summary>Open floor. Always walkable.</summary>
        Floor = 1,

        /// <summary>A doorway. Walkable only while its door is open.</summary>
        Doorway = 2
    }

    /// <summary>
    /// A door between two rooms. The player's primary verb, and the only safety valve in the game.
    /// </summary>
    /// <remarks>
    /// Toggling a door both stalls a party and rescues one: mobs will not pursue past a room
    /// threshold, so opening a door behind a losing party lets it break off and heal. That is the
    /// central regret in the design and it costs no extra verb.
    /// </remarks>
    public sealed class Door
    {
        /// <summary>Cell this door occupies.</summary>
        public Vector2Int Cell { get; }

        /// <summary>Room on the low side of the threshold.</summary>
        public int RoomA { get; }

        /// <summary>Room on the high side of the threshold.</summary>
        public int RoomB { get; }

        /// <summary>Whether the door currently lets anything through.</summary>
        public bool IsOpen { get; set; }

        /// <summary>Creates a door joining two rooms at a cell.</summary>
        /// <param name="cell">Grid cell the door occupies.</param>
        /// <param name="roomA">Room on one side.</param>
        /// <param name="roomB">Room on the other side.</param>
        /// <param name="isOpen">Whether it starts open.</param>
        public Door(Vector2Int cell, int roomA, int roomB, bool isOpen)
        {
            Cell = cell;
            RoomA = roomA;
            RoomB = roomB;
            IsOpen = isOpen;
        }
    }

    /// <summary>
    /// The dungeon as a grid of cells grouped into rooms, with doors between them.
    /// </summary>
    /// <remarks>
    /// Rooms are a first-class lookup rather than a geometric question, because two rules depend on
    /// answering "which room is this?" cheaply and constantly: mob pursuit stops at a room boundary,
    /// and the party retreats through doors. Storing a room index per cell makes both an array read.
    /// <para>
    /// Plain C# with no Unity lifecycle -- only <see cref="Vector2Int"/> for convenience -- so the
    /// whole dungeon can be built and asserted in a test without a scene.
    /// </para>
    /// </remarks>
    public sealed class DungeonGrid
    {
        /// <summary>Room index meaning "no room owns this cell".</summary>
        public const int NoRoom = -1;

        private readonly CellKind[] _cells;
        private readonly int[] _roomOf;
        private readonly List<Door> _doors = new();

        /// <summary>Grid width in cells.</summary>
        public int Width { get; }

        /// <summary>Grid height in cells.</summary>
        public int Height { get; }

        /// <summary>Every door in the dungeon, in creation order.</summary>
        public IReadOnlyList<Door> Doors => _doors;

        /// <summary>Creates an all-wall grid of the given size.</summary>
        /// <param name="width">Width in cells; must be positive.</param>
        /// <param name="height">Height in cells; must be positive.</param>
        public DungeonGrid(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _cells = new CellKind[Width * Height];
            _roomOf = new int[Width * Height];
            for (int i = 0; i < _roomOf.Length; i++)
            {
                _roomOf[i] = NoRoom;
            }
        }

        /// <summary>Whether a cell lies inside the grid.</summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True when within bounds.</returns>
        public bool InBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        /// <summary>Reads what occupies a cell. Out-of-bounds reads as <see cref="CellKind.Wall"/>.</summary>
        /// <param name="cell">Cell to read.</param>
        /// <returns>The cell's kind.</returns>
        public CellKind KindAt(Vector2Int cell)
        {
            return InBounds(cell) ? _cells[Index(cell)] : CellKind.Wall;
        }

        /// <summary>Reads which room owns a cell, or <see cref="NoRoom"/>.</summary>
        /// <param name="cell">Cell to read.</param>
        /// <returns>The owning room index.</returns>
        public int RoomAt(Vector2Int cell)
        {
            return InBounds(cell) ? _roomOf[Index(cell)] : NoRoom;
        }

        /// <summary>Paints a rectangle of floor and assigns it to a room.</summary>
        /// <param name="bounds">Rectangle in cells.</param>
        /// <param name="room">Room index to assign.</param>
        public void CarveRoom(RectInt bounds, int room)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!InBounds(cell))
                    {
                        continue;
                    }

                    _cells[Index(cell)] = CellKind.Floor;
                    _roomOf[Index(cell)] = room;
                }
            }
        }

        /// <summary>Places a door cell joining two rooms.</summary>
        /// <param name="cell">Cell the door occupies.</param>
        /// <param name="roomA">Room on one side.</param>
        /// <param name="roomB">Room on the other side.</param>
        /// <param name="isOpen">Whether it starts open.</param>
        /// <returns>The created door.</returns>
        public Door AddDoor(Vector2Int cell, int roomA, int roomB, bool isOpen)
        {
            _cells[Index(cell)] = CellKind.Doorway;

            // A doorway belongs to neither room. Mob pursuit stops when the room index changes, so
            // giving the threshold its own identity is what stops a mob following through an open
            // door -- the retreat valve depends on this, so it is load-bearing, not tidiness.
            _roomOf[Index(cell)] = NoRoom;

            var door = new Door(cell, roomA, roomB, isOpen);
            _doors.Add(door);
            return door;
        }

        /// <summary>Finds the door occupying a cell, if any.</summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>The door, or null.</returns>
        public Door DoorAt(Vector2Int cell)
        {
            foreach (Door door in _doors)
            {
                if (door.Cell == cell)
                {
                    return door;
                }
            }

            return null;
        }

        /// <summary>Whether anything can currently stand on or cross a cell.</summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True for floor, and for doorways whose door is open.</returns>
        public bool IsWalkable(Vector2Int cell)
        {
            CellKind kind = KindAt(cell);
            if (kind == CellKind.Floor)
            {
                return true;
            }

            if (kind != CellKind.Doorway)
            {
                return false;
            }

            Door door = DoorAt(cell);
            return door != null && door.IsOpen;
        }

        /// <summary>
        /// Breadth-first path between two cells, respecting walls and closed doors.
        /// </summary>
        /// <param name="from">Starting cell.</param>
        /// <param name="to">Target cell.</param>
        /// <returns>
        /// Cells to walk, excluding <paramref name="from"/> and ending at <paramref name="to"/>.
        /// Empty when no route exists -- which is the normal result of the player closing a door,
        /// not an error.
        /// </returns>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            return FindPath(from, to, null);
        }

        /// <summary>
        /// Breadth-first path that also steers around a set of cells.
        /// </summary>
        /// <param name="from">Starting cell.</param>
        /// <param name="to">Target cell.</param>
        /// <param name="avoid">
        /// Cells to route around if at all possible -- armed traps, typically. When no route exists
        /// that avoids them the search is retried without the restriction, because an adventurer
        /// walking through a trap is far better behaviour than one standing still forever.
        /// </param>
        /// <returns>Cells to walk, excluding <paramref name="from"/>. Empty when no route exists.</returns>
        public List<Vector2Int> FindPath(
            Vector2Int from, Vector2Int to, IReadOnlyCollection<Vector2Int> avoid)
        {
            // Hashed once here rather than scanned per candidate cell: the search tests every
            // neighbour it visits, so a linear lookup would be inside the hot loop.
            HashSet<Vector2Int> blocked =
                avoid == null || avoid.Count == 0 ? null : new HashSet<Vector2Int>(avoid);

            List<Vector2Int> route = Search(from, to, blocked);
            return route.Count > 0 || blocked == null ? route : Search(from, to, null);
        }

        /// <summary>Runs one breadth-first search, optionally treating some cells as blocked.</summary>
        private List<Vector2Int> Search(
            Vector2Int from, Vector2Int to, HashSet<Vector2Int> avoid)
        {
            var result = new List<Vector2Int>();
            if (!IsWalkable(to) || !InBounds(from) || from == to)
            {
                return result;
            }

            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int> { from };
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (current == to)
                {
                    return Reconstruct(cameFrom, from, to, result);
                }

                foreach (Vector2Int step in Neighbours(current))
                {
                    if (seen.Contains(step) || !IsWalkable(step))
                    {
                        continue;
                    }

                    // The destination is always reachable even if it is itself avoided, otherwise
                    // "walk to the trap and disarm it" style goals become impossible to express.
                    if (avoid != null && step != to && avoid.Contains(step))
                    {
                        continue;
                    }

                    seen.Add(step);
                    cameFrom[step] = current;
                    queue.Enqueue(step);
                }
            }

            return result;
        }

        /// <summary>
        /// Whether an unobstructed straight line runs between two points.
        /// </summary>
        /// <remarks>
        /// Walks the line in small steps and fails on the first cell that cannot be stood in, so a
        /// closed door blocks sight exactly as it blocks movement. That matters: the player shutting
        /// a door should hide the party from whatever was about to charge it.
        /// </remarks>
        /// <param name="from">Viewer position, in grid units.</param>
        /// <param name="to">Target position, in grid units.</param>
        /// <returns>True when nothing solid lies between them.</returns>
        public bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance * 4f));
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                var cell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
                if (!IsWalkable(cell))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Four-way neighbours of a cell. Diagonals are excluded so paths hug the grid.</summary>
        private static IEnumerable<Vector2Int> Neighbours(Vector2Int cell)
        {
            yield return new Vector2Int(cell.x + 1, cell.y);
            yield return new Vector2Int(cell.x - 1, cell.y);
            yield return new Vector2Int(cell.x, cell.y + 1);
            yield return new Vector2Int(cell.x, cell.y - 1);
        }

        /// <summary>Walks the came-from chain back into a forward-ordered path.</summary>
        private static List<Vector2Int> Reconstruct(
            IReadOnlyDictionary<Vector2Int, Vector2Int> cameFrom,
            Vector2Int from,
            Vector2Int to,
            List<Vector2Int> into)
        {
            Vector2Int cursor = to;
            while (cursor != from)
            {
                into.Add(cursor);
                cursor = cameFrom[cursor];
            }

            into.Reverse();
            return into;
        }

        /// <summary>Flattens a cell to its array index.</summary>
        private int Index(Vector2Int cell)
        {
            return (cell.y * Width) + cell.x;
        }
    }
}
