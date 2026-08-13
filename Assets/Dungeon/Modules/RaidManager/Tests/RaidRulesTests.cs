using System.Linq;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Guards the rules SPEC.md calls load-bearing rather than polish.
    /// </summary>
    /// <remarks>
    /// Three constraints are defended here because each is invisible to a casual play test yet
    /// removing any one destroys the design: mobs must not pursue past a room threshold, closing a
    /// door must actually stall the party, and no HP number may escape the party module.
    /// </remarks>
    public sealed class RaidRulesTests
    {
        /// <summary>Builds the standard Milestone 1 corridor.</summary>
        private static DungeonLayout Corridor() => DungeonLayout.BuildCorridor();

        /// <summary>Runs a raid forward by a number of seconds at a fixed step.</summary>
        private static void Advance(Raid raid, float seconds, float step = 1f / 50f)
        {
            for (float t = 0f; t < seconds; t += step)
            {
                raid.Tick(step);
            }
        }

        /// <summary>The corridor builds with rooms, doors between them, and spawners past the first.</summary>
        [Test]
        public void Corridor_BuildsRoomsDoorsAndSpawners()
        {
            DungeonLayout layout = Corridor();
            Assert.AreEqual(3, layout.RoomCentres.Count);
            Assert.AreEqual(2, layout.Grid.Doors.Count);
            Assert.AreEqual(2, layout.SpawnerCells.Count);
            Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(layout.EntranceCell));
        }

        /// <summary>A door cell belongs to no room, which is what stops pursuit at the threshold.</summary>
        [Test]
        public void Doorway_BelongsToNoRoom()
        {
            DungeonLayout layout = Corridor();
            foreach (Door door in layout.Grid.Doors)
            {
                Assert.AreEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(door.Cell),
                    "a doorway must belong to neither room or mobs would follow through it");
            }
        }

        /// <summary>An open door lets the party through; a closed one does not.</summary>
        [Test]
        public void ClosedDoor_BlocksThePath()
        {
            DungeonLayout layout = Corridor();
            var open = layout.Grid.FindPath(layout.EntranceCell, layout.BossCell);
            Assert.Greater(open.Count, 0, "the party must be able to reach the boss room by default");

            foreach (Door door in layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            var blocked = layout.Grid.FindPath(layout.EntranceCell, layout.BossCell);
            Assert.AreEqual(0, blocked.Count, "closing every door must cut the route entirely");
        }

        /// <summary>
        /// The primary verb: shutting a door in front of the party must actually stall it. This is
        /// the whole game, so it is asserted on the raid rather than on the grid alone.
        /// </summary>
        [Test]
        public void ClosingDoor_StallsTheParty()
        {
            var raid = new Raid(Corridor());
            foreach (Door door in raid.Layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            Advance(raid, 20f);

            MooseRunnerFacade.Log($"party stalled at {raid.Party.Cell}, goal {raid.Party.Goal}");
            Assert.AreNotEqual(RaidOutcome.PartyEscaped, raid.Outcome,
                "a party behind a closed door must never reach the boss room");
            Assert.AreEqual(0, raid.Layout.Grid.RoomAt(raid.Party.Cell),
                "the party must still be in the first room");
        }

        /// <summary>
        /// The safety valve. A mob must never leave its home room, even with the door wide open and
        /// the party visible on the other side. Without this the player cannot rescue a losing party
        /// and the design's central regret disappears.
        /// </summary>
        [Test]
        public void Mobs_NeverLeaveTheirHomeRoom()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            foreach (Door door in layout.Grid.Doors)
            {
                door.IsOpen = true;
            }

            Mob mob = raid.Mobs.Spawn(MobKind.Slime, layout.RoomCentres[1]);
            Assert.IsNotNull(mob);
            int home = mob.HomeRoom;

            Advance(raid, 40f);

            MooseRunnerFacade.Log($"mob home room {home}, ended at {mob.Cell} " +
                                  $"(room {layout.Grid.RoomAt(mob.Cell)})");
            Assert.AreEqual(home, layout.Grid.RoomAt(mob.Cell),
                "a mob left its home room -- the retreat valve is broken");
        }

        /// <summary>Every mob stays home across a full raid, not just the one under test.</summary>
        [Test]
        public void AllMobs_StayHome_AcrossAFullRaid()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                raid.Mobs.Spawn(MobKind.Skeleton, spawner);
            }

            Advance(raid, Raid.RaidSeconds);

            foreach (Mob mob in raid.Mobs.Mobs.Where(m => m.IsAlive))
            {
                Assert.AreEqual(mob.HomeRoom, layout.Grid.RoomAt(mob.Cell));
            }
        }

        /// <summary>Spawning binds a mob to the room it appeared in, not the party's room.</summary>
        [Test]
        public void Spawn_BindsMobToItsOwnRoom()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            Mob mob = raid.Mobs.Spawn(MobKind.Slime, layout.RoomCentres[2]);
            Assert.AreEqual(2, mob.HomeRoom);
        }

        /// <summary>Spawning in a wall or doorway yields nothing rather than an unbound mob.</summary>
        [Test]
        public void Spawn_InANonRoomCell_Fails()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            Assert.IsNull(raid.Mobs.Spawn(MobKind.Slime, new Vector2Int(0, 0)));
            Assert.IsNull(raid.Mobs.Spawn(MobKind.Slime, layout.Grid.Doors[0].Cell));
        }

        /// <summary>An undisturbed party walks to the boss room and ends the raid early.</summary>
        [Test]
        public void UndisturbedParty_ReachesTheBossRoom()
        {
            var raid = new Raid(Corridor());
            Advance(raid, Raid.RaidSeconds);

            MooseRunnerFacade.Log($"outcome {raid.Outcome}, energy {raid.TotalEnergy:F1}");
            Assert.AreEqual(RaidOutcome.PartyEscaped, raid.Outcome);
        }

        /// <summary>
        /// The crossing must take a meaningful share of the clock.
        /// </summary>
        /// <remarks>
        /// This guards the bug that <c>UndisturbedParty_ReachesTheBossRoom</c> sailed past: the party
        /// did reach the boss room, in under seven seconds, ending the raid before a player could
        /// click anything and harvesting nothing at all. Asserting the outcome without asserting the
        /// pace tests the wrong half of the behaviour.
        /// </remarks>
        [Test]
        public void UnopposedParty_TakesMostOfTheClockToCross()
        {
            var raid = new Raid(Corridor());
            const float step = 1f / 50f;

            float elapsed = 0f;
            while (raid.IsRunning && elapsed < Raid.RaidSeconds)
            {
                raid.Tick(step);
                elapsed += step;
            }

            MooseRunnerFacade.Log($"unopposed crossing took {elapsed:F1}s ({raid.Outcome})");
            Assert.Greater(elapsed, 18f,
                "an unopposed crossing must leave the player time to react");
            Assert.Less(elapsed, Raid.RaidSeconds,
                "doing nothing must still cost the player the rest of the window");
        }

        /// <summary>
        /// Letting the party stroll out earns far less than the full minute is worth. This is the
        /// spec's "ending early is a loss of earning window" expressed as a number.
        /// </summary>
        [Test]
        public void EarlyEscape_EarnsFarLessThanAFullRaid()
        {
            var strolled = new Raid(Corridor());
            Advance(strolled, Raid.RaidSeconds);

            var stalled = new Raid(Corridor());
            foreach (Door door in stalled.Layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            stalled.Mobs.Spawn(MobKind.Skeleton, stalled.Layout.RoomCentres[0]);
            Advance(stalled, Raid.RaidSeconds);

            MooseRunnerFacade.Log($"strolled harvested {strolled.EnergyHarvested:F1} ({strolled.Outcome}), " +
                                  $"stalled harvested {stalled.EnergyHarvested:F1} ({stalled.Outcome})");
            Assert.Greater(stalled.EnergyHarvested, strolled.EnergyHarvested * 5f,
                "stalling and fighting must dominate letting the party leave");
        }

        /// <summary>The clock runs out and ends the raid when nothing else does.</summary>
        [Test]
        public void Clock_ExpiresAfterSixtySeconds()
        {
            var raid = new Raid(Corridor());
            foreach (Door door in raid.Layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            Advance(raid, Raid.RaidSeconds + 1f);
            Assert.AreEqual(RaidOutcome.TimeExpired, raid.Outcome);
            Assert.AreEqual(0f, raid.TimeRemaining, 0.001f);
        }

        /// <summary>A finished raid stops earning; its rate drops to zero and stays there.</summary>
        [Test]
        public void FinishedRaid_StopsEarning()
        {
            var raid = new Raid(Corridor());
            Advance(raid, Raid.RaidSeconds + 2f);
            float banked = raid.TotalEnergy;

            Advance(raid, 5f);

            Assert.AreEqual(0f, raid.CurrentRate, 0.001f);
            Assert.AreEqual(banked, raid.TotalEnergy, 0.001f);
        }

        /// <summary>Verbs are inert once the raid is over, so a finished board cannot be poked.</summary>
        [Test]
        public void Verbs_AreInertAfterTheRaidEnds()
        {
            var raid = new Raid(Corridor());
            Advance(raid, Raid.RaidSeconds + 2f);

            Assert.IsFalse(raid.SpawnMob(raid.Layout.SpawnerCells[0]));
            Assert.IsFalse(raid.FireTrap(raid.Layout.TrapCells[0]));
        }

        /// <summary>A trap costs energy, wounds a party standing on it, and then needs to cool down.</summary>
        [Test]
        public void Trap_CostsEnergyWoundsThePartyAndThenCoolsDown()
        {
            var raid = new Raid(Corridor());
            foreach (Door door in raid.Layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            raid.Mobs.Spawn(MobKind.Skeleton, raid.Layout.RoomCentres[0]);
            Advance(raid, 25f);
            Assert.Greater(raid.TotalEnergy, Raid.TrapCost,
                "25s of a held fight must pay for a trap");

            float before = raid.TotalEnergy;
            Assert.IsTrue(raid.FireTrap(raid.Layout.TrapCells[0]), "the trap should fire");
            Assert.Less(raid.TotalEnergy, before, "firing a trap must cost energy");
            Assert.IsFalse(raid.FireTrap(raid.Layout.TrapCells[0]), "a trap must cool down");
        }

        /// <summary>Spawning is refused once the player has spent everything.</summary>
        [Test]
        public void Spawn_IsRefusedWhenEnergyIsShort()
        {
            var raid = new Raid(Corridor());
            int guard = 0;
            while (raid.SpawnMob(raid.Layout.SpawnerCells[0]) && guard++ < 100)
            {
                // Drain the starting charge without letting a bug here spin forever.
            }

            Assert.Less(raid.TotalEnergy, Raid.SpawnCost);
            Assert.IsFalse(raid.SpawnMob(raid.Layout.SpawnerCells[0]));
        }

        /// <summary>
        /// The player must be able to act on the very first frame.
        /// </summary>
        /// <remarks>
        /// This guards a bug that every other test walked past: with no starting charge, an idle
        /// party earning 0.05/s needed five hundred seconds to afford a twenty-five energy spawn,
        /// inside a sixty-second raid. The game was literally unplayable and the suite was green.
        /// </remarks>
        [Test]
        public void Player_CanAffordAVerb_OnTheFirstFrame()
        {
            var raid = new Raid(Corridor());
            Assert.GreaterOrEqual(raid.TotalEnergy, Raid.SpawnCost,
                "the core must start charged enough to spawn immediately");
            Assert.IsTrue(raid.SpawnMob(raid.Layout.SpawnerCells[0]));
        }

        /// <summary>
        /// A fight has to last long enough to be worth starting. Mobs that evaporate leave the party
        /// walking an empty corridor, which is the one state that earns nothing.
        /// </summary>
        [Test]
        public void AFight_LastsLongEnoughToEarn()
        {
            var raid = new Raid(Corridor());
            foreach (Door door in raid.Layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            raid.Mobs.Spawn(MobKind.Skeleton, raid.Layout.RoomCentres[0]);

            float fighting = 0f;
            const float step = 1f / 50f;
            for (float t = 0f; t < Raid.RaidSeconds; t += step)
            {
                raid.Tick(step);
                if (raid.Party.Goal == PartyGoal.Fighting)
                {
                    fighting += step;
                }
            }

            MooseRunnerFacade.Log($"one skeleton held the party for {fighting:F1}s, " +
                                  $"harvesting {raid.EnergyHarvested:F1}");
            Assert.Greater(fighting, 8f, "a single mob must hold the party for a meaningful stretch");
        }

        /// <summary>
        /// The party never exposes a hit-point number -- only a fraction and a coarse three-state
        /// wound level. SPEC.md forbids anything more precise reaching the screen.
        /// </summary>
        [Test]
        public void Adventurers_ExposeNoHitPointNumber()
        {
            var raid = new Raid(Corridor());
            foreach (Adventurer member in raid.Party.Members)
            {
                Assert.IsTrue(member.HealthFraction is >= 0f and <= 1f);
                Assert.IsTrue(System.Enum.IsDefined(typeof(WoundState), member.Wounds));
            }

            var readable = typeof(Adventurer).GetProperties()
                .Where(p => p.Name.Contains("Health") && p.PropertyType == typeof(float))
                .Select(p => p.Name)
                .ToList();
            Assert.That(readable, Is.EquivalentTo(new[] { "MaxHealth", "HealthFraction" }),
                "current hit points must not be readable from outside the party module");
        }

        /// <summary>Wound state tracks health downward through all three bands.</summary>
        [Test]
        public void WoundState_TracksHealthDownward()
        {
            var adventurer = new Adventurer(AdventurerRole.Tank, Vector2Int.zero);
            Assert.AreEqual(WoundState.Healthy, adventurer.Wounds);

            adventurer.TakeDamage(adventurer.MaxHealth * 0.5f);
            Assert.AreEqual(WoundState.Hurt, adventurer.Wounds);

            adventurer.TakeDamage(adventurer.MaxHealth * 0.3f);
            Assert.AreEqual(WoundState.Critical, adventurer.Wounds);
        }

        /// <summary>Aggregate health ignores the dead, so killing never inflates the wound bonus.</summary>
        [Test]
        public void PartyHealth_IgnoresTheDead()
        {
            var raid = new Raid(Corridor());
            Adventurer victim = raid.Party.Members.First(m => m.Role == AdventurerRole.Mage);
            victim.TakeDamage(victim.MaxHealth);

            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(1f, raid.Party.HealthFraction, 0.001f,
                "a corpse must not drag the party's health down and inflate the wound multiplier");
        }
    }
}
