using System.Collections.Generic;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Builds dungeons across the whole range the shop and the code can produce, and checks each one
    /// is actually a dungeon.
    /// </summary>
    /// <remarks>
    /// <c>BuildCorridor</c> takes seven parameters and the shop drives four of them from a purse that
    /// grows all season. A combination that produces an unwalkable dungeon, a trap inside a wall or a
    /// boss room nobody can reach would strand a raid — and it would only appear once a player had
    /// bought their way into that corner.
    /// </remarks>
    public sealed class LayoutSweepTests
    {
        /// <summary>Every layout the shop can build is crossable, and everything sits on floor.</summary>
        [Test]
        public void EveryBuildableDungeon_IsWellFormed()
        {
            int built = 0;

            foreach (int rooms in new[] { 2, 3, 4, 5 })
            {
                foreach (int slimes in new[] { 0, 3, 8 })
                {
                    foreach (int skeletons in new[] { 0, 3, 8 })
                    {
                        foreach (int traps in new[] { 0, 4, 10 })
                        {
                            foreach (int chests in new[] { 0, 2, 6 })
                            {
                                DungeonLayout layout = DungeonLayout.BuildCorridor(
                                    roomCount: rooms,
                                    extraSlimeSpawners: slimes,
                                    extraSkeletonSpawners: skeletons,
                                    extraTraps: traps,
                                    chests: chests);

                                string what = $"{rooms} rooms, {slimes}+{skeletons} spawners, "
                                              + $"{traps} traps, {chests} chests";
                                Check(layout, what);
                                built++;
                            }
                        }
                    }
                }
            }

            MooseRunnerFacade.Log($"checked {built} distinct dungeons");
            Assert.Greater(built, 100, "the sweep did not cover much");
        }

        /// <summary>Asserts one layout is sane.</summary>
        /// <param name="layout">Dungeon to check.</param>
        /// <param name="what">Description for failure messages.</param>
        private static void Check(DungeonLayout layout, string what)
        {
            DungeonGrid grid = layout.Grid;

            Assert.IsTrue(grid.IsWalkable(layout.EntranceCell), $"{what}: entrance is not walkable");
            Assert.IsTrue(grid.IsWalkable(layout.BossCell), $"{what}: boss room is not walkable");

            List<Vector2Int> path = grid.FindPath(layout.EntranceCell, layout.BossCell);
            Assert.Greater(path.Count, 0, $"{what}: the boss room cannot be reached at all");

            foreach (Vector2Int cell in layout.SpawnerCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, grid.RoomAt(cell),
                    $"{what}: a spawner at {cell} is not in any room");
            }

            foreach (Vector2Int cell in layout.TrapCells)
            {
                Assert.IsTrue(grid.IsWalkable(cell),
                    $"{what}: a trap at {cell} is in a wall, so nobody can ever stand on it");
            }

            foreach (Vector2Int cell in layout.ChestCells)
            {
                Assert.IsTrue(grid.IsWalkable(cell),
                    $"{what}: a chest at {cell} is in a wall");
            }

            Assert.AreEqual(layout.SpawnerCells.Count, layout.SpawnerTiers.Count,
                $"{what}: a spawner has no tier, so the wrong monster comes out");

            // Nothing may share a cell -- two things on one square draw over each other and a single
            // tap would fire whichever the code happened to test first.
            var taken = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in layout.SpawnerCells)
            {
                Assert.IsTrue(taken.Add(cell), $"{what}: two spawners on {cell}");
            }

            foreach (Vector2Int cell in layout.TrapCells)
            {
                Assert.IsTrue(taken.Add(cell), $"{what}: something else already occupies {cell}");
            }

            foreach (Vector2Int cell in layout.ChestCells)
            {
                Assert.IsTrue(taken.Add(cell), $"{what}: something else already occupies {cell}");
            }
        }

        /// <summary>
        /// Hostile room counts are clamped rather than producing a broken dungeon.
        /// </summary>
        [Test]
        public void HostileRoomCounts_AreClamped()
        {
            foreach (int rooms in new[] { -5, 0, 1, 2 })
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: rooms);
                Assert.GreaterOrEqual(layout.RoomCentres.Count, 2,
                    $"asking for {rooms} rooms produced a dungeon with no corridor");
                Check(layout, $"{rooms} rooms requested");
            }

            MooseRunnerFacade.Log("hostile room counts clamp to a playable corridor");
        }

        /// <summary>
        /// An unbought dungeon can always be crossed inside the clock, at every size.
        /// </summary>
        /// <remarks>
        /// This is the baseline the shop modifies, and it must hold: a new player's first raids need
        /// both endings on the table, or the design's stated tension — they might leave early — never
        /// exists at all.
        /// <para>
        /// Deliberately <b>not</b> asserted for a fully-bought dungeon. Measured, chests and halls
        /// stack up until the party cannot finish: 2 rooms 24.8s, 3 rooms 38.2s, 4 rooms 56.5s, and a
        /// fully-kitted 5-room corridor runs out the clock. Two honest readings, and it is the
        /// author's call — either the player has spent real money to guarantee the full minute, which
        /// is coherent progression and simply moves the tension from "will they escape" to "can I
        /// hold one bar at 5%", or a permanent purchase quietly deleting an ending is too strong.
        /// See the note at the end of D12.
        /// </para>
        /// </remarks>
        [Test]
        public void AnUnboughtDungeon_CanAlwaysBeCrossedInTime()
        {
            foreach (int rooms in new[] { 2, 3, 4, 5 })
            {
                var raid = new Raid(DungeonLayout.BuildCorridor(roomCount: rooms));
                float elapsed = 0f;
                while (raid.IsRunning && elapsed < Raid.RaidSeconds)
                {
                    raid.Tick(0.02f);
                    elapsed += 0.02f;
                }

                MooseRunnerFacade.Log($"{rooms} rooms unbought: {raid.Outcome} after {elapsed:F1}s");
                Assert.AreEqual(RaidOutcome.PartyEscaped, raid.Outcome,
                    $"an unopposed party could not cross a plain {rooms}-room dungeon in the clock");
            }
        }

        /// <summary>
        /// Records how long a fully-bought dungeon takes to cross, without demanding an answer.
        /// </summary>
        /// <remarks>
        /// Data rather than a verdict. The numbers move whenever the shop, the walk speed or the
        /// chest timer changes, and the author needs to see the trend rather than have a test pick a
        /// side for them.
        /// </remarks>
        [Test]
        public void HowLongAFullyBoughtDungeonTakes_IsRecorded()
        {
            foreach (int rooms in new[] { 2, 3, 4, 5 })
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor(
                    roomCount: rooms, extraSlimeSpawners: 6, extraSkeletonSpawners: 6,
                    extraTraps: 8, chests: 4);

                var raid = new Raid(layout);
                float elapsed = 0f;
                while (raid.IsRunning && elapsed < Raid.RaidSeconds)
                {
                    raid.Tick(0.02f);
                    elapsed += 0.02f;
                }

                MooseRunnerFacade.Log(
                    $"{rooms} rooms fully bought: {raid.Outcome} after {elapsed:F1}s");

                // The only hard requirement: it must still be walkable, so nothing is ever trapped.
                Assert.Greater(
                    layout.Grid.FindPath(layout.EntranceCell, layout.BossCell).Count, 0,
                    $"a fully-bought {rooms}-room dungeon has no route to the boss room at all");
            }
        }

        /// <summary>Doors join rooms that actually exist, and sit on the corridor.</summary>
        [Test]
        public void EveryDoor_JoinsTwoRealRooms()
        {
            for (int rooms = 2; rooms <= 5; rooms++)
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: rooms);
                Assert.AreEqual(rooms - 1, layout.Grid.Doors.Count,
                    $"{rooms} rooms should be joined by {rooms - 1} doors");

                foreach (Door door in layout.Grid.Doors)
                {
                    Assert.AreNotEqual(door.RoomA, door.RoomB, "a door joins a room to itself");
                    Assert.GreaterOrEqual(door.RoomA, 0, "a door joins a room that does not exist");
                    Assert.Less(door.RoomB, rooms, "a door joins a room beyond the corridor");
                    Assert.AreEqual(CellKind.Doorway, layout.Grid.KindAt(door.Cell),
                        $"the cell at {door.Cell} is not a doorway");
                }
            }
        }
    }
}
