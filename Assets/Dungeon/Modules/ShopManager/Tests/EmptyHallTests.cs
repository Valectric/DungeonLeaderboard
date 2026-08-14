using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Exercises the hall the player buys and then has to furnish themselves.
    /// </summary>
    /// <remarks>
    /// A bought hall used to arrive with a spawner and a trap in it. Now it is bare floor, which
    /// moves the whole burden onto the placement path: the room has to be buildable, what the player
    /// puts there has to land in that room, and it has to still be there next raid.
    /// <para>
    /// The risk is not the placement itself but the <b>lattice</b>. Growing left or down moves the
    /// dungeon's anchor and every carved cell with it, so an absolute cell the player bought before
    /// the move would end up in a different room — or in the rock — unless purchases are translated
    /// by the same amount. That never mattered much while every room came pre-stocked. It matters
    /// completely now.
    /// </para>
    /// </remarks>
    public sealed class EmptyHallTests
    {
        /// <summary>Cells in a room that the player could build on.</summary>
        /// <param name="layout">Dungeon to search.</param>
        /// <param name="room">Room index to search within.</param>
        /// <returns>Every buildable cell in that room.</returns>
        private static List<Vector2Int> BuildableCellsIn(DungeonLayout layout, int room)
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.Grid.RoomAt(cell) == room && layout.CanBuildOn(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// A hall the player buys arrives with nothing in it.
        /// </summary>
        /// <remarks>
        /// The ask itself. A bought hall that already contains fittings is two purchases the player
        /// did not make, in places they did not choose.
        /// </remarks>
        [Test]
        public void ABoughtHall_ArrivesEmpty()
        {
            RoomPlan plan = RoomPlan.Corridor(3);
            List<Vector2Int> offered = plan.Expansions();
            Assert.Greater(offered.Count, 0, "a three-room corridor should offer somewhere to grow");
            plan.Add(offered[0]);

            DungeonLayout layout = DungeonLayout.Build(plan, furnishedRooms: 3);
            int bought = layout.Grid.RoomAt(
                new Vector2Int(
                    Mathf.RoundToInt(layout.RoomCentres[3].x),
                    Mathf.RoundToInt(layout.RoomCentres[3].y)));

            int spawnersInside = 0;
            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                if (layout.Grid.RoomAt(spawner) == bought)
                {
                    spawnersInside++;
                }
            }

            int trapsInside = 0;
            foreach (Trap trap in layout.Traps)
            {
                if (layout.Grid.RoomAt(trap.Cell) == bought)
                {
                    trapsInside++;
                }
            }

            MooseRunnerFacade.Log(
                $"the bought hall (room {bought}) arrived with {spawnersInside} spawners and "
                + $"{trapsInside} traps");

            Assert.AreEqual(0, spawnersInside, "a bought hall should arrive with no spawner in it");
            Assert.AreEqual(0, trapsInside, "a bought hall should arrive with no trap in it");
        }

        /// <summary>
        /// The opening corridor is still furnished, so round one can be played at all.
        /// </summary>
        /// <remarks>
        /// The other half of the same decision, and the reason it is a count rather than a flag. An
        /// entirely bare dungeon has no verb the player can press and earns the idle rate, so round
        /// one would be unwinnable before it started.
        /// </remarks>
        [Test]
        public void TheOpeningCorridor_IsStillFurnished()
        {
            DungeonLayout layout = DungeonLayout.Build(RoomPlan.Corridor(3), furnishedRooms: 3);

            MooseRunnerFacade.Log(
                $"the opening corridor has {layout.SpawnerCells.Count} spawners and "
                + $"{layout.Traps.Count} traps");

            Assert.Greater(layout.SpawnerCells.Count, 0,
                "a dungeon with no spawner has no monsters and no way to make any");
            Assert.Greater(layout.Traps.Count, 0, "a dungeon with no trap is down to two verbs");
        }

        /// <summary>
        /// Every room of a bought hall can actually be built on.
        /// </summary>
        /// <remarks>
        /// If the placement predicate said no, the hall would be a permanently empty room the player
        /// paid for and can never use — the worst possible outcome of making halls arrive empty.
        /// </remarks>
        [Test]
        public void ABoughtHall_CanBeBuiltOn()
        {
            foreach (Vector2Int direction in RoomPlan.Corridor(3).Expansions())
            {
                RoomPlan plan = RoomPlan.Corridor(3);
                plan.Add(direction);
                DungeonLayout layout = DungeonLayout.Build(plan, furnishedRooms: 3);

                List<Vector2Int> buildable = BuildableCellsIn(layout, plan.Count - 1);
                Assert.Greater(buildable.Count, 0,
                    $"a hall bought at {direction} has nowhere the player can place anything");
            }

            MooseRunnerFacade.Log("every direction a hall can be bought in is buildable");
        }

        /// <summary>
        /// A spawner placed in a bought hall stays in that hall, and works.
        /// </summary>
        /// <remarks>
        /// The end-to-end version, and the one that would catch a lattice translation bug: place into
        /// the new room, rebuild the dungeon from the loadout the way the game does between raids,
        /// and check the spawner is still in the room it was put in and still fires.
        /// </remarks>
        [Test]
        public void ASpawnerPlacedInABoughtHall_StaysThereAndFires()
        {
            foreach (Vector2Int direction in RoomPlan.Corridor(3).Expansions())
            {
                RoomPlan plan = RoomPlan.Corridor(3);
                plan.Add(direction);

                DungeonLayout bare = DungeonLayout.Build(plan, furnishedRooms: 3);
                int hall = plan.Count - 1;
                List<Vector2Int> buildable = BuildableCellsIn(bare, hall);
                Assert.Greater(buildable.Count, 0, $"nowhere to build in the hall at {direction}");

                Vector2Int chosen = buildable[buildable.Count / 2];

                var furniture = new Furnishings();
                furniture.SkeletonSpawners.Add(chosen);

                // Rebuilt from scratch, exactly as the game does between raids.
                DungeonLayout furnished = DungeonLayout.Build(
                    plan, placed: furniture, furnishedRooms: 3);

                Assert.Contains(chosen, (System.Collections.ICollection)furnished.SpawnerCells,
                    $"the spawner placed at {chosen} in the hall at {direction} did not survive the "
                    + "rebuild");
                Assert.AreEqual(hall, furnished.Grid.RoomAt(chosen),
                    $"the spawner placed at {chosen} ended up in a different room after the rebuild");

                var raid = new Raid(furnished);
                Assert.IsTrue(raid.SpawnMob(chosen),
                    $"the spawner the player placed at {chosen} does not fire");
            }

            MooseRunnerFacade.Log(
                "a spawner placed in a bought hall survived the rebuild and fired, in every "
                + "direction a hall can be bought");
        }

        /// <summary>
        /// A hall the player never furnishes earns them almost nothing.
        /// </summary>
        /// <remarks>
        /// Not a defect — the intended consequence, and worth pinning. An empty room is corridor the
        /// party walks through, and walking is the lowest-paying action in the game. Buying floor
        /// without buying anything to put on it should be a bad purchase, or the placement decision
        /// carries no weight.
        /// </remarks>
        [Test]
        public void AnUnfurnishedHall_EarnsAlmostNothing()
        {
            RoomPlan plan = RoomPlan.Corridor(3);
            plan.Add(plan.Expansions()[0]);
            DungeonLayout layout = DungeonLayout.Build(plan, furnishedRooms: 3);

            int hall = plan.Count - 1;
            var raid = new Raid(layout);

            float earnedInHall = 0f;
            float earnedElsewhere = 0f;
            float before = raid.EnergyHarvested;

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                float earned = raid.EnergyHarvested - before;
                before = raid.EnergyHarvested;

                if (layout.Grid.RoomAt(raid.Party.Cell) == hall)
                {
                    earnedInHall += earned;
                }
                else
                {
                    earnedElsewhere += earned;
                }
            }

            MooseRunnerFacade.Log(
                $"the party earned {earnedInHall:F0} in the empty hall and {earnedElsewhere:F0} "
                + "everywhere else");

            Assert.LessOrEqual(earnedInHall, earnedElsewhere,
                "an empty room the player bought and never furnished out-earned the rest of the "
                + "dungeon, which would make placement pointless");
        }
    }
}
