using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Stress-tests the monsters, especially the one rule the whole design leans on.
    /// </summary>
    /// <remarks>
    /// SPEC.md calls room-bounded pursuit <b>load-bearing, not polish</b>: the player has no verb for
    /// recalling a monster, so the only way to rescue a losing party is to open a door and let them
    /// run somewhere the mobs will not follow. If a mob ever crosses a threshold, the game's single
    /// safety valve is gone and with it its central regret.
    /// </remarks>
    public sealed class MobBehaviourSweepTests
    {
        /// <summary>
        /// No monster ever leaves the room it spawned in, under any amount of pressure.
        /// </summary>
        [Test]
        public void NoMonster_EverLeavesItsRoom()
        {
            // Rooms arrive empty now -- everything in a dungeon is placed by the player -- so this
            // has to furnish its own spawners or it ticks a raid that never sees a monster and
            // proves containment zero times.
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 4, extraSkeletonSpawners: 4, extraSlimeSpawners: 4);
            var raid = new Raid(layout);
            var homes = new Dictionary<Mob, int>();
            int checks = 0;

            while (raid.IsRunning)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    Mob spawned = raid.SpawnMob(spawner)
                        ? raid.Mobs.Mobs[raid.Mobs.Mobs.Count - 1]
                        : null;

                    if (spawned != null)
                    {
                        homes[spawned] = spawned.HomeRoom;
                    }
                }

                raid.Tick(0.02f);

                foreach (Mob mob in raid.Mobs.Living)
                {
                    if (!homes.TryGetValue(mob, out int home))
                    {
                        continue;
                    }

                    int room = layout.Grid.RoomAt(mob.Cell);
                    checks++;

                    // A mob straddling a threshold may momentarily round into the doorway cell,
                    // which belongs to no room -- that is not an escape, it is arithmetic.
                    Assert.IsTrue(room == home || room == DungeonGrid.NoRoom,
                        $"a {mob.Kind} left room {home} and reached room {room} at {mob.Position}");
                }
            }

            MooseRunnerFacade.Log($"checked mob containment {checks} times across a raid");
            Assert.Greater(checks, 1000, "not enough mobs lived long enough to prove anything");
        }

        /// <summary>
        /// A monster ignores a straggler standing in the next room, however close they are.
        /// </summary>
        /// <remarks>
        /// The soak found this one: <i>"a Skeleton left room 1 for room 0"</i>. Monsters chase the
        /// NEAREST party member, but the room check was on the party <i>leader</i> — so a member left
        /// behind across the threshold could be the nearest body, and the mob would charge straight
        /// out of its room after them. That is the retreat valve failing: the player opens a door
        /// behind a losing party, the party falls back, and the monsters follow them through it.
        /// <para>
        /// Two cells away and one room over is the exact case. It is inside the range where a mob
        /// abandons cell-by-cell pathing and charges directly, which is the code path that had no
        /// room check at all.
        /// </para>
        /// </remarks>
        [Test]
        public void AStragglerInTheNextRoom_IsNotWorthLeavingTheRoomFor()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 3);
            var pack = new MobPack(layout.Grid);

            Vector2Int spawner = layout.SpawnerCells[0];
            Mob mob = pack.Spawn(MobKind.Skeleton, spawner);
            Assert.IsNotNull(mob, "the test needs a monster");
            int home = mob.HomeRoom;

            // A straggler just over the threshold, and the leader standing on the monster's own
            // square so the leader's room is never the thing that stops the chase.
            Vector2 straggler = FirstCellOfAnotherRoom(layout, home);
            var positions = new List<Vector2> { straggler, mob.Position };

            for (int step = 0; step < 400; step++)
            {
                pack.Tick(0.02f, positions);

                int room = layout.Grid.RoomAt(mob.Cell);
                Assert.IsTrue(room == home || room == DungeonGrid.NoRoom,
                    $"the monster left room {home} for room {room} chasing a straggler at "
                    + $"{straggler}");
            }

            MooseRunnerFacade.Log(
                $"monster held room {home} for four seconds with a straggler at {straggler}");
        }

        /// <summary>Finds a walkable cell belonging to some room other than the one given.</summary>
        /// <param name="layout">Dungeon to search.</param>
        /// <param name="notThisRoom">Room to avoid.</param>
        /// <returns>A cell in a different room.</returns>
        private static Vector2 FirstCellOfAnotherRoom(DungeonLayout layout, int notThisRoom)
        {
            foreach (Vector2 centre in layout.RoomCentres)
            {
                var cell = new Vector2Int(Mathf.RoundToInt(centre.x), Mathf.RoundToInt(centre.y));
                if (layout.Grid.RoomAt(cell) != notThisRoom &&
                    layout.Grid.RoomAt(cell) != DungeonGrid.NoRoom)
                {
                    return centre;
                }
            }

            Assert.Fail("the dungeon has only one room, so this test cannot mean anything");
            return Vector2.zero;
        }

        /// <summary>
        /// Opening a door behind a losing party actually saves it.
        /// </summary>
        /// <remarks>
        /// The retreat valve, end to end. This is the design's central regret -- the same door that
        /// stalls them is the one that rescues them -- and it only exists because mobs stop at a
        /// threshold.
        /// </remarks>
        [Test]
        public void RetreatingThroughADoor_EscapesTheMonsters()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            // Walk them into the second room and set a trap of monsters on them.
            while (raid.IsRunning && layout.Grid.RoomAt(raid.Party.Cell) < 1)
            {
                raid.Tick(0.02f);
            }

            for (int i = 0; i < 4; i++)
            {
                raid.Mobs.Spawn(MobKind.Skeleton, raid.Party.Cell);
            }

            // Wound them into a retreat directly rather than waiting for the monsters to manage it.
            // Relying on the fight was flaky in both directions: it depended on how the mobs happened
            // to spread out, and when it failed the test could only bail with Assert.Ignore -- which
            // this runner reports as a failure anyway.
            int guard = 0;
            while (raid.IsRunning && raid.Party.Goal != PartyManager.PartyGoal.Retreating &&
                   guard++ < 3000)
            {
                raid.Party.DistributeDamage(8f);
                raid.Tick(0.02f);
            }

            Assert.AreEqual(PartyManager.PartyGoal.Retreating, raid.Party.Goal,
                "the party never chose to retreat even when wounded to the threshold");

            MooseRunnerFacade.Log($"party began retreating at {raid.Party.HealthFraction:P0} health");

            // The question is whether a monster reaches a room that is not its own. Comparing
            // against "the room the party started in" was wrong: the party was standing on the
            // threshold at that moment, which belongs to no room, so the guard never fired and the
            // test flagged a skeleton standing legitimately at home.
            var homes = new HashSet<int>();
            foreach (Mob mob in raid.Mobs.Living)
            {
                homes.Add(mob.HomeRoom);
            }

            bool everGotClear = false;
            for (int step = 0; step < 1500 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);

                int partyRoom = layout.Grid.RoomAt(raid.Party.Cell);
                if (partyRoom == DungeonGrid.NoRoom || homes.Contains(partyRoom))
                {
                    continue;
                }

                everGotClear = true;
                foreach (Mob mob in raid.Mobs.Living)
                {
                    Assert.AreNotEqual(partyRoom, layout.Grid.RoomAt(mob.Cell),
                        $"a {mob.Kind} whose home is room {mob.HomeRoom} reached room {partyRoom} "
                        + "with the party -- the safety valve is gone");
                }
            }

            Assert.IsTrue(everGotClear,
                "the party never made it to a room the monsters did not own, so nothing was proven");

            MooseRunnerFacade.Log(
                $"after retreating: {raid.Party.LivingCount} alive, {raid.Outcome}");
        }

        /// <summary>Monsters never stack on one square, however many are crammed in.</summary>
        /// <remarks>
        /// Separation runs for every mob every tick. Two spawned on the same cell have no separating
        /// direction, which is exactly where a naive implementation welds them together forever.
        /// </remarks>
        [Test]
        public void Monsters_PushApartRatherThanStacking()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            Vector2Int cell = layout.SpawnerCells[0];

            for (int i = 0; i < 8; i++)
            {
                raid.Mobs.Spawn(MobKind.Slime, cell);
            }

            for (int step = 0; step < 200; step++)
            {
                raid.Tick(0.02f);
            }

            float closest = float.MaxValue;
            var living = new List<Mob>(raid.Mobs.Living);
            for (int i = 0; i < living.Count; i++)
            {
                for (int j = i + 1; j < living.Count; j++)
                {
                    closest = Mathf.Min(closest,
                        Vector2.Distance(living[i].Position, living[j].Position));
                }
            }

            MooseRunnerFacade.Log(
                $"eight slimes on one cell settled {closest:F2} cells apart after four seconds");
            Assert.Greater(closest, 0.15f, "monsters are welded together on one square");
        }

        /// <summary>A crowd of monsters does not send the raid's cost through the roof.</summary>
        /// <remarks>
        /// Separation is O(n squared) per tick. Twenty monsters is well beyond anything the shop can
        /// produce, and it must still finish a raid in reasonable time.
        /// </remarks>
        [Test]
        public void ACrowdOfMonsters_StillFinishesTheRaid()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                for (int i = 0; i < 10; i++)
                {
                    raid.Mobs.Spawn(MobKind.Skeleton, spawner);
                }
            }

            int guard = 0;
            while (raid.IsRunning && guard++ < 5000)
            {
                raid.Tick(0.02f);
            }

            MooseRunnerFacade.Log(
                $"{raid.Mobs.Mobs.Count} monsters: {raid.Outcome} after {guard} ticks, "
                + $"harvested {raid.EnergyHarvested:F0}");
            Assert.Less(guard, 5000, "a crowded raid never ended");
        }

        /// <summary>Killing every monster leaves the party free to walk out.</summary>
        [Test]
        public void AClearedRoom_LetsThePartyThrough()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Slime, layout.SpawnerCells[0]);

            int guard = 0;
            while (raid.IsRunning && guard++ < 5000)
            {
                raid.Tick(0.02f);
            }

            MooseRunnerFacade.Log($"one slime, then: {raid.Outcome}");
            Assert.AreEqual(RaidOutcome.PartyEscaped, raid.Outcome,
                "the party could not leave after clearing the room");
        }
    }
}
