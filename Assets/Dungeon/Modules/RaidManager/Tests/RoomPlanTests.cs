using System.Collections.Generic;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Dungeons that grow in any direction, not only to the right.
    /// </summary>
    /// <remarks>
    /// The dungeon was a strictly horizontal run, so "buy another hall" was one button and the
    /// party's route was a straight line. A plan is a set of lattice coordinates, and any room that
    /// is not boxed in can sprout a neighbour in any of the four directions.
    /// <para>
    /// What these check is the property the rest of the game leans on: whatever shape is planned, the
    /// carved result is a single connected dungeon that can be walked from the entrance to every
    /// room. A plan that produced two disconnected halves would strand the party somewhere it could
    /// never leave, and nothing else in the game checks for it.
    /// </para>
    /// </remarks>
    public sealed class RoomPlanTests
    {
        /// <summary>A plan starts with one room and grows only where a room already touches.</summary>
        [Test]
        public void APlan_OnlyGrowsFromExistingRooms()
        {
            var plan = new RoomPlan();

            Assert.AreEqual(1, plan.Count, "a plan should start with a single room");
            Assert.IsFalse(plan.CanAdd(Vector2Int.zero), "the first room's cell is taken");
            Assert.IsFalse(plan.CanAdd(new Vector2Int(4, 4)),
                "a room floating away from the dungeon would be unreachable");
            Assert.IsFalse(plan.CanAdd(new Vector2Int(1, 1)),
                "a diagonal neighbour shares no wall, so no door could join it");

            Assert.IsTrue(plan.Add(Vector2Int.right), "a room to the east should be allowed");
            Assert.IsTrue(plan.Add(Vector2Int.up), "and one to the north");
            Assert.AreEqual(3, plan.Count);
        }

        /// <summary>Every direction from every room is offered, once each.</summary>
        [Test]
        public void Expansions_OfferEveryFreeNeighbour()
        {
            var plan = new RoomPlan();
            List<Vector2Int> offered = plan.Expansions();

            MooseRunnerFacade.Log($"a single room offers {offered.Count} places to build");
            Assert.AreEqual(4, offered.Count, "a lone room should offer all four directions");

            plan.Add(Vector2Int.right);
            offered = plan.Expansions();

            Assert.AreEqual(6, offered.Count,
                "two rooms in a line should offer six places, sharing none");
            CollectionAssert.AllItemsAreUnique(offered, "a cell was offered twice");

            foreach (Vector2Int cell in offered)
            {
                Assert.IsTrue(plan.CanAdd(cell), $"{cell} was offered but cannot be built on");
            }
        }

        /// <summary>
        /// A dungeon grown in all four directions is one connected, walkable place.
        /// </summary>
        /// <remarks>
        /// The property everything else depends on. A plus-shaped plan puts rooms above, below and
        /// either side of the first — a shape the corridor builder could not express — and every one
        /// of them has to be reachable on foot from the entrance.
        /// </remarks>
        [Test]
        public void ADungeonGrownInEveryDirection_IsWalkableThroughout()
        {
            var plan = new RoomPlan();
            foreach (Vector2Int direction in RoomPlan.Directions)
            {
                Assert.IsTrue(plan.Add(direction), $"could not add a room {direction}");
            }

            DungeonLayout layout = DungeonLayout.Build(plan);

            MooseRunnerFacade.Log(
                $"plus-shaped dungeon: {plan.Count} rooms, grid {layout.Grid.Width}x"
                + $"{layout.Grid.Height}, {layout.Grid.Doors.Count} doors");

            Assert.AreEqual(plan.Count, layout.RoomCentres.Count, "a room lost its centre");
            Assert.AreEqual(4, layout.Grid.Doors.Count,
                "four arms off one room should need exactly four doors");

            foreach (Vector2Int centre in layout.RoomCentres)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(centre),
                    $"the centre {centre} was not carved into any room");

                List<Vector2Int> path = layout.Grid.FindPath(layout.EntranceCell, centre);
                Assert.Greater(path.Count, 0,
                    $"no route from the entrance to the room at {centre}, so it is unreachable");
            }
        }

        /// <summary>
        /// A dungeon that doubles back on itself still connects, and shares no doorway twice.
        /// </summary>
        /// <remarks>
        /// A loop is the shape most likely to produce a duplicate door, because two rooms can be
        /// neighbours by more than one route. Two doors on one cell would be firable twice and drawn
        /// on top of each other.
        /// </remarks>
        [Test]
        public void ALoopingDungeon_ConnectsWithoutDuplicateDoors()
        {
            var plan = new RoomPlan();
            plan.Add(new Vector2Int(1, 0));
            plan.Add(new Vector2Int(1, 1));
            plan.Add(new Vector2Int(0, 1));

            DungeonLayout layout = DungeonLayout.Build(plan);

            var seen = new HashSet<Vector2Int>();
            foreach (Door door in layout.Grid.Doors)
            {
                Assert.IsTrue(seen.Add(door.Cell),
                    $"two doors share the cell {door.Cell}, so one is drawn under the other");
            }

            MooseRunnerFacade.Log(
                $"square dungeon: {plan.Count} rooms, {layout.Grid.Doors.Count} doors, "
                + $"grid {layout.Grid.Width}x{layout.Grid.Height}");

            Assert.AreEqual(4, layout.Grid.Doors.Count,
                "a closed square of four rooms has four shared walls");

            foreach (Vector2Int centre in layout.RoomCentres)
            {
                Assert.Greater(layout.Grid.FindPath(layout.EntranceCell, centre).Count, 0,
                    $"the room at {centre} is cut off from the entrance");
            }
        }

        /// <summary>The corridor the game ships with is just one plan among many.</summary>
        /// <remarks>
        /// Guards the refactor rather than the feature: <c>BuildCorridor</c> routes through a plan
        /// now, so it has to produce exactly what it always did.
        /// </remarks>
        [Test]
        public void TheShippedCorridor_IsUnchangedByThePlan()
        {
            DungeonLayout corridor = DungeonLayout.BuildCorridor(roomCount: 3);
            DungeonLayout planned = DungeonLayout.Build(RoomPlan.Corridor(3));

            Assert.AreEqual(corridor.Grid.Width, planned.Grid.Width, "the grid changed width");
            Assert.AreEqual(corridor.Grid.Height, planned.Grid.Height, "the grid changed height");
            Assert.AreEqual(corridor.EntranceCell, planned.EntranceCell, "the entrance moved");
            Assert.AreEqual(corridor.BossCell, planned.BossCell, "the boss room moved");
            CollectionAssert.AreEqual(corridor.RoomCentres, planned.RoomCentres, "a room moved");
            CollectionAssert.AreEqual(
                corridor.SpawnerCells, planned.SpawnerCells, "a spawner moved");
            CollectionAssert.AreEqual(corridor.TrapCells, planned.TrapCells, "a trap moved");
        }
    }
}
