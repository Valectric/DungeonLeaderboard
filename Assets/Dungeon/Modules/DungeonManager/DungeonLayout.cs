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

        /// <summary>
        /// The shape this dungeon was built from, so the shop can offer somewhere to grow.
        /// </summary>
        /// <remarks>
        /// Null for layouts built the old way, by room count alone.
        /// </remarks>
        public RoomPlan Plan { get; private set; }

        /// <summary>
        /// Lowest lattice coordinate the plan uses, which anchors every cell in the grid.
        /// </summary>
        /// <remarks>
        /// Load-bearing for the shop. Growing the dungeon <i>left or down</i> moves this, and every
        /// carved cell moves with it — so a spawner the player placed at an absolute cell would
        /// silently end up in a different room. Anything holding absolute cells has to translate
        /// them by the change in this value.
        /// </remarks>
        public Vector2Int LatticeAnchor { get; private set; }

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
        /// Wraps an already-carved grid as a layout, for tooling that builds geometry by hand.
        /// </summary>
        /// <remarks>
        /// Every other entry point here starts from a <see cref="RoomPlan"/>, which places rooms on
        /// a lattice — and a lattice cannot produce a wall stub, an isolated pillar, or an outer
        /// corner jutting into open floor. Those are exactly the wall shapes the art has never been
        /// asked to draw, because nothing could build a dungeon containing one.
        /// <para>
        /// This exists so a sampler grid can be drawn by the normal view code and photographed. It
        /// is not for the game: the layout it returns has no plan, no furnishing and no lattice
        /// anchor, so the shop cannot extend it and the party's exploration rules would find no
        /// room centres to visit.
        /// </para>
        /// </remarks>
        /// <param name="grid">A grid whose cells are already carved.</param>
        /// <param name="entrance">Where a party would enter.</param>
        /// <param name="boss">The deepest cell.</param>
        /// <returns>A layout drawing that grid and nothing else.</returns>
        public static DungeonLayout FromGrid(
            DungeonGrid grid, Vector2Int entrance, Vector2Int boss)
        {
            return new DungeonLayout(
                grid, entrance, boss,
                new List<Vector2Int> { boss },
                new List<Vector2Int>(), new List<Vector2Int>(), new List<Vector2Int>(),
                new List<int>());
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
        /// <param name="furnishedRooms">
        /// How many of the plan's rooms arrive with a spawner and a trap already in them. Defaults to
        /// all of them, which is what tests and the opening corridor want; the game passes the size
        /// of the starting corridor so that halls bought later arrive empty for the player to fit
        /// out themselves.
        /// </param>
        /// <returns>The built layout.</returns>
        public static DungeonLayout BuildCorridor(
            int roomCount = 3, int roomWidth = 5, int roomHeight = 5, bool doorsStartOpen = true,
            int extraSlimeSpawners = 0, int extraSkeletonSpawners = 0, int extraTraps = 0,
            int chests = 0, Furnishings placed = null, int furnishedRooms = int.MaxValue)
        {
            roomCount = Mathf.Max(2, roomCount);
            return Build(RoomPlan.Corridor(roomCount), roomWidth, roomHeight, doorsStartOpen,
                extraSlimeSpawners, extraSkeletonSpawners, extraTraps, chests, placed,
                furnishedRooms);
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
        /// <param name="furnishedRooms">
        /// How many of the plan's rooms arrive with a spawner and a trap already in them. Defaults to
        /// all of them, which is what tests and the opening corridor want; the game passes the size
        /// of the starting corridor so that halls bought later arrive empty for the player to fit
        /// out themselves.
        /// </param>
        /// <returns>The built layout.</returns>
        public static DungeonLayout Build(
            RoomPlan plan, int roomWidth = 5, int roomHeight = 5, bool doorsStartOpen = true,
            int extraSlimeSpawners = 0, int extraSkeletonSpawners = 0, int extraTraps = 0,
            int chests = 0, Furnishings placed = null, int furnishedRooms = int.MaxValue)
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

            // Only the rooms the dungeon STARTS with come furnished. A hall bought in the shop
            // arrives empty and the player fits it out by clicking its floor, because a bought hall
            // that already contained a spawner and a trap silently bundled two fittings the player
            // did not choose and could not place -- and a room they had furnished deliberately
            // looked no different from one the builder had stocked.
            //
            // The opening corridor is still stocked. A dungeon with nothing in it has no verbs to
            // press and earns nothing, so an unfurnished round one would be a game over screen with
            // extra steps. Room 0 is left clear regardless, so the party is never ambushed before
            // the player has had a moment to read the board.
            for (int room = 1; room < Mathf.Min(roomCount, furnishedRooms); room++)
            {
                Vector2Int origin = PlanBuilder.OriginOf(
                    plan.Rooms[room], latticeMin, roomWidth, roomHeight);
                // The trap goes ON the party's route and the spawner beside it. That is the whole
                // point of the pair: the party walks onto the trap, and the monster comes at them
                // from off to one side.
                //
                // The route is the CENTRE COLUMN since the dungeon was turned to run bottom to top
                // on 2026-08-16. These read (origin.x + 1, origin.y + roomHeight/2) -- the middle
                // ROW -- and left alone they quietly moved every trap OFF the path, because the
                // middle row is now something the party crosses in one stride rather than walks
                // along. Nothing would have failed loudly; the dungeon would simply have stopped
                // wounding anybody.
                // TRANSPOSED, not re-guessed. The horizontal layout put the trap at (1, height/2)
                // from the room origin and the spawner at (width-1, 0); swapping the axes gives
                // (width/2, 1) and (0, height-1), which preserves both relationships exactly: the
                // trap sits on the party's route two cells inside the door, and the spawner sits
                // off the route in the far corner.
                //
                // Worth doing arithmetically rather than by eye. A first attempt moved the trap
                // correctly and the spawner by guess, which took the suite from two failures to
                // seven -- the trap was the fix and the spawner was noise on top of it.
                spawners.Add(new Vector2Int(origin.x, origin.y + roomHeight - 1));
                spawnerTiers.Add(1);
                traps.Add(new Vector2Int(origin.x + (roomWidth / 2), origin.y + 1));
            }

            Vector2Int firstOrigin = PlanBuilder.OriginOf(
                plan.Rooms[0], latticeMin, roomWidth, roomHeight);

            // VERTICAL since 2026-08-16, at the author's request: the party enters at the BOTTOM and
            // charges up the screen. midX is the column running through room zero, and the entrance
            // and boss sit on the bottom and top edges of that column rather than the left and right
            // edges of a row. This was midY and interiorWidth until then.
            int midX = firstOrigin.x + (roomWidth / 2);
            int interiorHeight = grid.Height - (margin * 2);

            var chestCells = new List<Vector2Int>();

            // The way in is an OPENING carved through the SOUTH wall below the entrance cell, so the
            // first room reads as a place a party walks into rather than a sealed box with figures
            // already inside it. Author's request. (West wall, until the dungeon was turned to run
            // bottom to top on 2026-08-16.)
            //
            // Carved as a Doorway but given NO Door, deliberately. A Door here would be tappable, and
            // shutting the way in before the party arrives would strand them outside for the whole
            // minute on the idle rate -- a way to lose the raid by pressing the thing the tutorial
            // tells the player to press.
            //
            // ABOVE BOTH RETURNS, which is the point of its position. This method returns early
            // whenever `placed` is non-null, and that is EVERY raid in the real game because
            // furniture comes from the loadout. Carved after that branch it applied only to layouts
            // built without placed furniture -- so it worked in the tests and did nothing in the
            // game, and photographing the opening frame is what caught it.
            grid.CarveOpening(new Vector2Int(midX, margin - 1));

            // The player pointed at these tiles, so they go exactly there and the scattering formula
            // below is skipped entirely. The room-bound check still applies: a hall the player later
            // stopped paying for would otherwise leave its furniture floating in the rock.
            if (placed != null)
            {
                placed.ApplyTo(grid, spawners, spawnerTiers, traps, chestCells);
                return Tagged(plan, latticeMin, new DungeonLayout(
                    grid, new Vector2Int(midX, margin),
                    new Vector2Int(midX, margin + interiorHeight - 1),
                    centres, spawners, traps, chestCells, spawnerTiers));
            }

            // Bought equipment goes into the rooms the party walks through. Rooms are indexed UP the
            // Y axis since 2026-08-16, so y0 walks the stack; each placement below is the old
            // horizontal formula with its axes transposed, which keeps every piece the same distance
            // from the party's route as it was.
            //
            // Two traps on one cell would draw over each other and be firable twice from a single
            // tap, so every placement below refuses a cell that is already taken.
            int extraSpawners = extraSlimeSpawners + extraSkeletonSpawners;
            for (int i = 0; i < extraSpawners; i++)
            {
                int room = 1 + (i % Mathf.Max(1, roomCount - 1));
                int y0 = margin + (room * (roomHeight + 1));
                var cell = new Vector2Int(
                    margin + roomWidth - 1, y0 + 1 + (i % Mathf.Max(1, roomHeight - 2)));
                if (!spawners.Contains(cell))
                {
                    spawners.Add(cell);
                    spawnerTiers.Add(i < extraSlimeSpawners ? 0 : 1);
                }
            }

            for (int i = 0; i < extraTraps; i++)
            {
                int room = 1 + (i % Mathf.Max(1, roomCount - 1));
                int y0 = margin + (room * (roomHeight + 1));
                var cell = new Vector2Int(midX, y0 + 2 + (i % Mathf.Max(1, roomHeight - 3)));
                if (!traps.Contains(cell))
                {
                    traps.Add(cell);
                }
            }

            for (int i = 0; i < chests; i++)
            {
                int room = 1 + (i % Mathf.Max(1, roomCount - 1));
                int y0 = margin + (room * (roomHeight + 1));
                var cell = new Vector2Int(margin, y0 + 1 + (i % 2));
                if (!chestCells.Contains(cell) && !spawners.Contains(cell))
                {
                    chestCells.Add(cell);
                }
            }

            var entrance = new Vector2Int(midX, margin);
            var boss = new Vector2Int(midX, margin + interiorHeight - 1);

            return Tagged(plan, latticeMin, new DungeonLayout(
                grid, entrance, boss, centres, spawners, traps, chestCells, spawnerTiers));
        }

        /// <summary>Records which plan a layout came from, and where its lattice is anchored.</summary>
        /// <param name="plan">Shape the layout was built from.</param>
        /// <param name="anchor">Lowest lattice coordinate the plan uses.</param>
        /// <param name="layout">Layout to tag.</param>
        /// <returns>The same layout.</returns>
        private static DungeonLayout Tagged(RoomPlan plan, Vector2Int anchor, DungeonLayout layout)
        {
            layout.Plan = plan;
            layout.LatticeAnchor = anchor;
            return layout;
        }

        /// <summary>
        /// Where a room would be centred if one were built at a lattice coordinate.
        /// </summary>
        /// <remarks>
        /// What the shop draws its expansion markers on. Computed from this layout's own anchor and
        /// room size, so a marker cannot drift from the geometry it is offering to extend.
        /// </remarks>
        /// <param name="lattice">Lattice coordinate to locate.</param>
        /// <param name="roomWidth">Interior width of a room.</param>
        /// <param name="roomHeight">Interior height of a room.</param>
        /// <returns>The centre cell that room would have.</returns>
        public Vector2Int CentreOfLattice(Vector2Int lattice, int roomWidth = 5, int roomHeight = 5)
        {
            Vector2Int origin = PlanBuilder.OriginOf(
                lattice, LatticeAnchor, roomWidth, roomHeight);
            return new Vector2Int(origin.x + (roomWidth / 2), origin.y + (roomHeight / 2));
        }
    }
}
