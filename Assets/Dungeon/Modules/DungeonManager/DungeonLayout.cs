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

        /// <summary>
        /// How heavy a monster each spawner produces: 0 for the cheap kind, 1 for the tough one.
        /// </summary>
        /// <remarks>
        /// An integer rather than a <c>MobKind</c> on purpose. MobManager depends on DungeonManager,
        /// so the dependency cannot run the other way without a cycle -- the dungeon describes what
        /// sort of spawner sits where, and the raid, which knows both modules, turns that into an
        /// actual monster.
        /// </remarks>
        public IReadOnlyList<int> SpawnerTiers { get; }

        /// <summary>The tier of the spawner on a cell.</summary>
        /// <param name="cell">Cell to look up.</param>
        /// <returns>The tier, or 1 when the cell holds no spawner.</returns>
        public int SpawnerTierAt(Vector2Int cell)
        {
            for (int i = 0; i < SpawnerCells.Count; i++)
            {
                if (SpawnerCells[i] == cell)
                {
                    return SpawnerTiers[i];
                }
            }

            return 1;
        }

        /// <summary>Cells holding a trap the player can fire.</summary>
        public IReadOnlyList<Vector2Int> TrapCells { get; }

        /// <summary>The placed traps, with their disarm state.</summary>
        public IReadOnlyList<Trap> Traps { get; private set; }

        /// <summary>
        /// Cells holding a chest.
        /// </summary>
        /// <remarks>
        /// SPEC.md section 5 calls chests "useful stalling infrastructure": they give the party a
        /// reason to leave the shortest path to the boss room, and every second spent detouring is a
        /// second still earning.
        /// </remarks>
        public IReadOnlyList<Vector2Int> ChestCells { get; }

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

        /// <summary>
        /// Where another hall would be centred if one were bought.
        /// </summary>
        /// <remarks>
        /// The shop draws its "buy this section" marker here, in the dungeon, rather than putting it
        /// in a menu — so extending the corridor reads as extending <i>this</i> dungeon rather than
        /// incrementing a number. The spacing is measured off the rooms that exist instead of being
        /// restated as a constant, so it cannot drift from the geometry it is describing.
        /// </remarks>
        public Vector2Int NextHallCentre
        {
            get
            {
                Vector2Int last = RoomCentres[RoomCentres.Count - 1];
                int spacing = RoomCentres.Count > 1
                    ? RoomCentres[1].x - RoomCentres[0].x
                    : 6;
                return new Vector2Int(last.x + spacing, last.y);
            }
        }

        /// <summary>
        /// Whether the player may put something new on a cell.
        /// </summary>
        /// <remarks>
        /// The rule the spatial shop is hit-tested against, and the reason it lives here rather than
        /// in the UI: the shop, the preview it draws and the dungeon the raid finally builds must all
        /// agree about which tiles are available, or the player buys a spawner onto a tile that
        /// silently discards it.
        /// <para>
        /// Doorways are excluded because a doorway belongs to no room, and the entrance and boss cell
        /// because furnishing either one changes what the party does before it has taken a step.
        /// </para>
        /// </remarks>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True when the cell is empty floor inside a room.</returns>
        public bool CanBuildOn(Vector2Int cell)
        {
            if (Grid.RoomAt(cell) == DungeonGrid.NoRoom)
            {
                return false;
            }

            if (cell == EntranceCell || cell == BossCell)
            {
                return false;
            }

            return !Holds(SpawnerCells, cell)
                   && !Holds(TrapCells, cell)
                   && !Holds(ChestCells, cell);
        }

        /// <summary>Whether a list of cells contains one.</summary>
        /// <param name="cells">Cells to search.</param>
        /// <param name="cell">Cell to look for.</param>
        /// <returns>True when present.</returns>
        private static bool Holds(IReadOnlyList<Vector2Int> cells, Vector2Int cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Creates a layout. Use <see cref="BuildCorridor"/> rather than calling this.</summary>
        /// <param name="grid">The built grid.</param>
        /// <param name="entrance">Party entry cell.</param>
        /// <param name="boss">Raid-ending cell.</param>
        /// <param name="roomCentres">Centre of each room.</param>
        /// <param name="spawners">Spawner cells.</param>
        /// <param name="traps">Trap cells.</param>
        /// <param name="chests">Chest cells.</param>
        /// <param name="spawnerTiers">Tier of each spawner, parallel to <paramref name="spawners"/>.</param>
        private DungeonLayout(DungeonGrid grid, Vector2Int entrance, Vector2Int boss,
            IReadOnlyList<Vector2Int> roomCentres, IReadOnlyList<Vector2Int> spawners,
            IReadOnlyList<Vector2Int> traps, IReadOnlyList<Vector2Int> chests,
            IReadOnlyList<int> spawnerTiers)
        {
            ChestCells = chests;
            SpawnerTiers = spawnerTiers;
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
        /// <param name="extraSlimeSpawners">Slime spawners bought in the shop.</param>
        /// <param name="extraSkeletonSpawners">Skeleton spawners bought in the shop.</param>
        /// <param name="extraTraps">Extra traps bought in the shop.</param>
        /// <param name="chests">Chests bought in the shop.</param>
        /// <param name="placed">
        /// Exactly where the player put each purchase. When supplied it replaces the four count
        /// parameters above entirely — the counts scatter things by a formula, which is the wrong
        /// answer once the player has pointed at a tile.
        /// </param>
        /// <returns>The built layout.</returns>
        public static DungeonLayout BuildCorridor(
            int roomCount = 3, int roomWidth = 5, int roomHeight = 5, bool doorsStartOpen = true,
            int extraSlimeSpawners = 0, int extraSkeletonSpawners = 0, int extraTraps = 0,
            int chests = 0, Furnishings placed = null)
        {
            roomCount = Mathf.Max(2, roomCount);
            return Build(RoomPlan.Corridor(roomCount), roomWidth, roomHeight, doorsStartOpen,
                extraSlimeSpawners, extraSkeletonSpawners, extraTraps, chests, placed);
        }

        /// <summary>
        /// Builds any shape of dungeon from a room plan.
        /// </summary>
        /// <remarks>
        /// The general form. <see cref="BuildCorridor"/> is now a plan of rooms in a line, so the
        /// dungeon the game shipped with is one case of this rather than the only one it can make.
        /// </remarks>
        /// <param name="plan">Which lattice cells hold rooms.</param>
        /// <param name="roomWidth">Interior width of each room in cells.</param>
        /// <param name="roomHeight">Interior height of each room in cells.</param>
        /// <param name="doorsStartOpen">Whether doors begin open.</param>
        /// <param name="extraSlimeSpawners">Slime spawners bought in the shop.</param>
        /// <param name="extraSkeletonSpawners">Skeleton spawners bought in the shop.</param>
        /// <param name="extraTraps">Extra traps bought in the shop.</param>
        /// <param name="chests">Chests bought in the shop.</param>
        /// <param name="placed">Exactly where the player put each purchase.</param>
        /// <returns>The built layout.</returns>
        public static DungeonLayout Build(
            RoomPlan plan, int roomWidth = 5, int roomHeight = 5, bool doorsStartOpen = true,
            int extraSlimeSpawners = 0, int extraSkeletonSpawners = 0, int extraTraps = 0,
            int chests = 0, Furnishings placed = null)
        {
            roomWidth = Mathf.Max(2, roomWidth);
            roomHeight = Mathf.Max(2, roomHeight);
            int roomCount = plan.Count;

            DungeonGrid grid = PlanBuilder.Build(
                plan, roomWidth, roomHeight, doorsStartOpen, out List<Vector2Int> centres);

            plan.Extent(out Vector2Int latticeMin, out _);
            const int margin = PlanBuilder.Margin;

            var spawners = new List<Vector2Int>();
            var spawnerTiers = new List<int>();
            var traps = new List<Vector2Int>();

            // Every room past the first earns its keep: somewhere to spawn from and something to
            // stand on. The first room is left clear so the party is never ambushed before the
            // player has had a moment to read the board.
            for (int room = 1; room < roomCount; room++)
            {
                Vector2Int origin = PlanBuilder.OriginOf(
                    plan.Rooms[room], latticeMin, roomWidth, roomHeight);
                spawners.Add(new Vector2Int(origin.x + roomWidth - 1, origin.y));
                spawnerTiers.Add(1);
                traps.Add(new Vector2Int(origin.x + 1, origin.y + (roomHeight / 2)));
            }

            Vector2Int firstOrigin = PlanBuilder.OriginOf(
                plan.Rooms[0], latticeMin, roomWidth, roomHeight);
            int midY = firstOrigin.y + (roomHeight / 2);
            int interiorWidth = grid.Width - (margin * 2);

            var chestCells = new List<Vector2Int>();

            // The player pointed at these tiles, so they go exactly there and the scattering formula
            // below is skipped entirely. The room-bound check still applies: a hall the player later
            // stopped paying for would otherwise leave its furniture floating in the rock.
            if (placed != null)
            {
                placed.ApplyTo(grid, spawners, spawnerTiers, traps, chestCells);
                return new DungeonLayout(
                    grid, new Vector2Int(margin, midY),
                    new Vector2Int(margin + interiorWidth - 1, midY),
                    centres, spawners, traps, chestCells, spawnerTiers);
            }

            // Bought equipment goes into the rooms the party walks through. Placed on the rows above
            // and below the party's route rather than on it, so a purchase never blocks the corridor
            // the whole game depends on being walkable.
            // Two traps on one cell would draw over each other and be firable twice from a single
            // tap, so every placement below refuses a cell that is already taken.
            int extraSpawners = extraSlimeSpawners + extraSkeletonSpawners;
            for (int i = 0; i < extraSpawners; i++)
            {
                int room = 1 + (i % Mathf.Max(1, roomCount - 1));
                int x0 = margin + (room * (roomWidth + 1));
                var cell = new Vector2Int(
                    x0 + 1 + (i % Mathf.Max(1, roomWidth - 2)), margin + roomHeight - 1);
                if (!spawners.Contains(cell))
                {
                    spawners.Add(cell);
                    spawnerTiers.Add(i < extraSlimeSpawners ? 0 : 1);
                }
            }

            for (int i = 0; i < extraTraps; i++)
            {
                int room = 1 + (i % Mathf.Max(1, roomCount - 1));
                int x0 = margin + (room * (roomWidth + 1));
                var cell = new Vector2Int(x0 + 2 + (i % Mathf.Max(1, roomWidth - 3)), midY);
                if (!traps.Contains(cell))
                {
                    traps.Add(cell);
                }
            }

            for (int i = 0; i < chests; i++)
            {
                int room = 1 + (i % Mathf.Max(1, roomCount - 1));
                int x0 = margin + (room * (roomWidth + 1));
                var cell = new Vector2Int(x0 + 1 + (i % 2), margin);
                if (!chestCells.Contains(cell) && !spawners.Contains(cell))
                {
                    chestCells.Add(cell);
                }
            }

            var entrance = new Vector2Int(margin, midY);
            var boss = new Vector2Int(margin + interiorWidth - 1, midY);
            return new DungeonLayout(
                grid, entrance, boss, centres, spawners, traps, chestCells, spawnerTiers);
        }
    }
}
