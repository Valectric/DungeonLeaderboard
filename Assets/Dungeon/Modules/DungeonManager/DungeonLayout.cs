using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.DungeonManager
{
    /// <summary>
    /// One placed trap, which the party's rogue can defuse given long enough.
    /// </summary>
    /// <remarks>
    /// Traps being disarmable is what stops them from being free damage the player fires whenever
    /// the cooldown allows. A trap the party is standing next to is on a clock, so the player has to
    /// decide whether to spend it now or lose it -- and a rogue crouched over a trap is four seconds
    /// the party is not walking toward the boss room, which is itself worth energy.
    /// </remarks>
    public sealed class Trap
    {
        /// <summary>Where the trap sits.</summary>
        public Vector2Int Cell { get; }

        /// <summary>Seconds of uninterrupted work needed to defuse it.</summary>
        public float DisarmSeconds { get; }

        /// <summary>Work done so far, in seconds.</summary>
        public float DisarmProgress { get; private set; }

        /// <summary>Whether the trap can still be fired.</summary>
        public bool IsArmed { get; private set; } = true;

        /// <summary>How far along disarming is, from 0 to 1.</summary>
        public float DisarmFraction =>
            DisarmSeconds <= 0f ? 1f : Mathf.Clamp01(DisarmProgress / DisarmSeconds);

        /// <summary>Creates an armed trap.</summary>
        /// <param name="cell">Where it sits.</param>
        /// <param name="disarmSeconds">How long it takes to defuse.</param>
        public Trap(Vector2Int cell, float disarmSeconds)
        {
            Cell = cell;
            DisarmSeconds = Mathf.Max(0.5f, disarmSeconds);
        }

        /// <summary>Advances disarming, disarming the trap once the work is complete.</summary>
        /// <param name="seconds">Seconds of work done this tick.</param>
        /// <returns>True when this tick finished the job.</returns>
        public bool Disarm(float seconds)
        {
            if (!IsArmed || seconds <= 0f)
            {
                return false;
            }

            DisarmProgress += seconds;
            if (DisarmProgress < DisarmSeconds)
            {
                return false;
            }

            IsArmed = false;
            return true;
        }

        /// <summary>Spends the trap, so a fired trap cannot be fired again.</summary>
        public void Fire()
        {
            IsArmed = false;
        }
    }

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

        /// <summary>The placed traps, with their disarm state.</summary>
        public IReadOnlyList<Trap> Traps { get; private set; }

        /// <summary>Cells of traps that are still armed, for the party to route around.</summary>
        /// <returns>The armed trap cells.</returns>
        public IReadOnlyCollection<Vector2Int> ArmedTrapCells()
        {
            var armed = new List<Vector2Int>();
            foreach (Trap trap in Traps)
            {
                if (trap.IsArmed)
                {
                    armed.Add(trap.Cell);
                }
            }

            return armed;
        }

        /// <summary>Finds the trap occupying a cell, if any.</summary>
        /// <param name="cell">Cell to test.</param>
        /// <returns>The trap, or null.</returns>
        public Trap TrapAt(Vector2Int cell)
        {
            foreach (Trap trap in Traps)
            {
                if (trap.Cell == cell)
                {
                    return trap;
                }
            }

            return null;
        }

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

            // Disarm times vary per trap so the rogue's detour is a different gamble each time --
            // a four-second trap is worth walking to, a ten-second one may not be.
            var placed = new List<Trap>();
            for (int i = 0; i < traps.Count; i++)
            {
                placed.Add(new Trap(traps[i], 4f + (i * 2.5f)));
            }

            Traps = placed;
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
