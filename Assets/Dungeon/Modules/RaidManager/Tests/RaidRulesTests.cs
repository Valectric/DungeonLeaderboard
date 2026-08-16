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

        /// <summary>Finds a roster by name, so a test can pick one with a known behaviour.</summary>
        /// <param name="name">Roster name.</param>
        /// <returns>That composition, or the opening one if the name is unknown.</returns>
        private static PartyComposition Named(string name)
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                if (composition.Name == name)
                {
                    return composition;
                }
            }

            return PartyComposition.Opening;
        }

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

            // Compared against the same dungeon left open, because a shut door is a COST, not a
            // wall. This used to assert the party was still in the first room after twenty seconds,
            // which was true only because of a freeze: once it had forced its own door open, the
            // next door along was not on its room's threshold, no path to the boss existed, and the
            // party stood still for the rest of the raid. The test protected that bug for as long as
            // it existed.
            var open = new Raid(Corridor());
            Advance(open, 20f);

            MooseRunnerFacade.Log(
                $"after 20s: shut doors left the party at {raid.Party.Cell} having seen "
                + $"{raid.Party.VisitedRooms} rooms; open doors at {open.Party.Cell} having seen "
                + $"{open.Party.VisitedRooms}");

            Assert.AreNotEqual(RaidOutcome.PartyEscaped, raid.Outcome,
                "a party behind a closed door must never get all the way round this quickly");

            // Rooms seen, not distance travelled: the party explores and doubles back now, so x tells
            // you which leg of the journey it is on rather than how far it has got.
            Assert.Less(raid.Party.VisitedRooms, open.Party.VisitedRooms,
                "shutting every door did not slow the party's exploration, so the verb buys nothing");
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
            // Was 18 seconds when the party walked at 0.6. The author raised the pace half again --
            // the game is called CHARGE! and they were strolling -- so a crossing is now nearer 12.
            // The floor still has to leave the player time to read the board and act; below about
            // ten seconds the opening move is a reflex rather than a decision.
            Assert.Greater(elapsed, 10f,
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
            // NARROWED BY DECISION, 2026-08-14, from 5x to 2.5x. The ORDERING is the claim and it
            // still holds; only the magnitude moved.
            //
            // The author's new-room bonus pays the whole team +2/s for three seconds each time they
            // reach somewhere new, and he chose it to credit the SCORE rather than the purse --
            // "the whole point is to make them traverse the dungeon". A party that strolls through
            // and leaves therefore collects something now, where before it collected almost
            // nothing. Measured: strolled 38.5, stalled 128.3, a gap of 3.3x where it used to be
            // over 5x.
            //
            // This was predicted before the modifier was written, in M9-PLAN.md's open question 1:
            // paying score for entering rooms pays for ADVANCING, which is the behaviour the door
            // verb exists to prevent. The author read that and chose score anyway, knowingly. So
            // this records the cost of a decision rather than a regression.
            //
            // 2.5x rather than 3x, so the bonus firing one extra time does not fail it. If this
            // ever needs lowering again, the modifier is too strong -- do not lower it twice.
            Assert.Greater(stalled.EnergyHarvested, strolled.EnergyHarvested * 2.5f,
                "stalling and fighting must dominate letting the party leave");
        }

        /// <summary>The clock runs out and ends the raid when nothing else does.</summary>
        [Test]
        public void Clock_ExpiresAfterSixtySeconds()
        {
            // THE IRONCLADS specifically: they have nobody who picks locks, and measured over a full
            // minute they batter a door to only 66% of its health. A roster with an archer picks in
            // about seven seconds and now walks the whole dungeon well inside the clock, so it can
            // no longer demonstrate the clock running out at all.
            var raid = new Raid(Corridor(), 0f, Named("THE IRONCLADS"));
            foreach (Door door in raid.Layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            // Advanced until the clock actually stops rather than for a fixed 61 seconds. A raid is
            // no longer RaidSeconds long: walking into a room pays NewRoomSeconds back, and the party
            // walks into its first room every time, so even a roster sealed behind shut doors runs 62
            // seconds. Asserting against the constant tested an arithmetic identity; this tests the
            // rule the name promises, which is that the clock runs out and ends the raid.
            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                Advance(raid, 0.05f);
            }

            MooseRunnerFacade.Log(
                $"ironclads behind shut doors: {raid.Outcome} at {raid.Party.Cell}, "
                + $"{raid.SecondsAwarded:F0}s awarded for rooms entered");
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

            // Replaced as it dies, because this test needs twenty-five seconds of HELD FIGHT to bank
            // the price of a trap, and one monster no longer lasts that long. Spawning again is what
            // a player does and what the design now expects -- several weaker monsters rather than
            // one long one.
            for (float t = 0f; t < 25f && raid.IsRunning; t += 0.02f)
            {
                if (!raid.Mobs.Living.Any())
                {
                    raid.Mobs.Spawn(MobKind.Skeleton, raid.Layout.RoomCentres[0]);
                }

                raid.Tick(0.02f);
            }

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
            // Four seconds rather than eight. The bound used to be sized on a skeleton with 260
            // health; the author has asked for two and a half times less, which takes one monster
            // from about thirteen seconds of contact to about six. The property is that a single
            // purchase buys a meaningful stretch of the raid -- not that it buys the old number.
            Assert.Greater(fighting, 4f, "a single mob must hold the party for a meaningful stretch");
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

        /// <summary>
        /// The party occupies a column of the corridor rather than stacking on one square.
        /// </summary>
        [Test]
        public void Party_MarchesInFormation_RatherThanStackingUp()
        {
            var raid = new Raid(Corridor());
            Advance(raid, 6f);

            var living = raid.Party.Living.ToList();
            for (int a = 0; a < living.Count; a++)
            {
                for (int b = a + 1; b < living.Count; b++)
                {
                    float gap = Vector2.Distance(living[a].Position, living[b].Position);
                    Assert.Greater(gap, 0.3f,
                        $"{living[a].Role} and {living[b].Role} are standing on each other");
                }
            }
        }

        /// <summary>The tank leads, and the healer walks last where it is safest.</summary>
        [Test]
        public void Party_LeadsWithTheTankAndTrailsWithTheHealer()
        {
            var raid = new Raid(Corridor());
            Advance(raid, 8f);

            var byProgress = raid.Party.Living.OrderByDescending(m => m.Position.y).ToList();
            MooseRunnerFacade.Log("order: " + string.Join(" -> ", byProgress.Select(m => m.Role)));
            Assert.AreEqual(AdventurerRole.Tank, byProgress.First().Role,
                "the tank draws aggro, so it must walk in front");
            Assert.AreEqual(AdventurerRole.Healer, byProgress.Last().Role,
                "the healer sustains the party and must walk at the back");
        }

        /// <summary>
        /// Movement is continuous. A party that jumps a whole cell per step reads as teleporting.
        /// </summary>
        [Test]
        public void Party_MovesContinuously_NotACellAtATime()
        {
            var raid = new Raid(Corridor());
            const float step = 1f / 50f;
            float biggest = 0f;

            for (int i = 0; i < 300; i++)
            {
                Vector2 before = raid.Party.Position;
                raid.Tick(step);
                biggest = Mathf.Max(biggest, Vector2.Distance(before, raid.Party.Position));
            }

            MooseRunnerFacade.Log($"largest single-tick move = {biggest:F4} cells");
            Assert.Less(biggest, 0.5f, "the party jumped most of a cell in one tick");
            Assert.Greater(biggest, 0f, "the party never moved at all");
        }

        /// <summary>A mob closes to arm's length and stops, instead of standing on the party.</summary>
        [Test]
        public void Mobs_StopBesideTheParty_NotOnTopOfIt()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.RoomCentres[0]);

            // Sampled for as long as the skeleton lives rather than at a fixed eight seconds. That
            // number was tied to how long a skeleton survives -- the comment here used to say "it
            // dies around thirteen" -- so changing monster health broke a test about POSITIONING,
            // which cannot depend on health at all. It was reading a corpse, or rather throwing on
            // an empty sequence.
            float closest = float.MaxValue;
            for (int tick = 0; tick < 900; tick++)
            {
                raid.Tick(0.02f);

                Mob living = raid.Mobs.Living.FirstOrDefault();
                if (living == null)
                {
                    break;
                }

                // Only once it has actually closed, or the approach counts as a near miss.
                float nearestNow =
                    raid.Party.Living.Min(m => Vector2.Distance(m.Position, living.Position));
                if (nearestNow < 3f)
                {
                    closest = Mathf.Min(closest, nearestNow);
                }
            }

            MooseRunnerFacade.Log($"closest an adventurer ever came to the mob: {closest:F2} cells");
            Assert.Less(closest, 3f, "the mob never reached the party, so nothing was tested");
            Assert.Greater(closest, 0.3f, "a mob stood on top of an adventurer");
        }

        /// <summary>Two mobs sharing a spawner shoulder apart instead of welding together.</summary>
        [Test]
        public void Mobs_SeparateFromEachOther()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Slime, layout.RoomCentres[1]);
            raid.Mobs.Spawn(MobKind.Slime, layout.RoomCentres[1]);

            // Sampled while both are alive rather than after a fixed eight seconds. Rooms arrive
            // empty now, so the party is not delayed on its way through and can reach these two and
            // kill one inside the old window -- at which point the measurement was reading a single
            // survivor and throwing on the second index.
            float gap = 0f;
            for (int tick = 0; tick < 400 && raid.Mobs.Living.Count() >= 2; tick++)
            {
                raid.Tick(0.02f);
                var alive = raid.Mobs.Living.ToList();
                if (alive.Count >= 2)
                {
                    gap = Mathf.Max(gap, Vector2.Distance(alive[0].Position, alive[1].Position));
                }
            }

            MooseRunnerFacade.Log($"two mobs settled {gap:F2} cells apart");
            Assert.Greater(gap, 0.25f, "mobs spawned together never separated");
        }

        /// <summary>The healer backs away from anything that gets within a cell of it.</summary>
        [Test]
        public void Healer_RunsFromAnythingThatGetsClose()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            Advance(raid, 4f);

            Adventurer healer = raid.Party.Living.First(m => m.Role == AdventurerRole.Healer);
            Mob mob = raid.Mobs.Spawn(MobKind.Slime, healer.Cell);
            Assert.IsNotNull(mob, "the test needs a mob beside the healer");

            // Measured while the mob is alive rather than after a fixed three seconds, for the same
            // reason as the test above: a slime's lifespan is a function of its health, and whether
            // a healer runs away is not.
            float before = Vector2.Distance(healer.Position, mob.Position);
            float furthest = before;

            for (int tick = 0; tick < 150 && mob.IsAlive; tick++)
            {
                raid.Tick(0.02f);
                furthest = Mathf.Max(furthest, Vector2.Distance(healer.Position, mob.Position));
            }

            MooseRunnerFacade.Log(
                $"healer distance from mob {before:F2} -> {furthest:F2} at its furthest");
            Assert.Greater(furthest, before, "the healer stood its ground instead of running");
        }

        /// <summary>The healer will not cast when the heal would overflow and waste mana.</summary>
        [Test]
        public void Healer_WillNotWasteAFullHealOnAScratch()
        {
            var raid = new Raid(Corridor());
            var allies = raid.Party.Living.ToList();
            Adventurer tank = allies.First(m => m.Role == AdventurerRole.Tank);

            tank.TakeDamage(5f);
            Assert.IsNull(AdventurerAI.ChooseHealTarget(allies, 100f),
                "a scratch must not draw a full heal");

            tank.TakeDamage(AdventurerAI.HealAmount + 10f);
            Assert.AreEqual(tank, AdventurerAI.ChooseHealTarget(allies, 100f),
                "a wound worth a full heal must be healed");
        }

        /// <summary>With no mana left, nobody gets healed.</summary>
        [Test]
        public void Healer_CannotCastWithoutMana()
        {
            var raid = new Raid(Corridor());
            var allies = raid.Party.Living.ToList();
            allies.First(m => m.Role == AdventurerRole.Tank).TakeDamage(120f);

            Assert.IsNull(AdventurerAI.ChooseHealTarget(allies, AdventurerAI.HealCost - 1f));
        }

        /// <summary>The tank outranks a squishier ally wounded by the same fraction.</summary>
        [Test]
        public void Healer_PrioritisesTheTankOverAnEquallyHurtAlly()
        {
            var raid = new Raid(Corridor());
            var allies = raid.Party.Living.ToList();
            Adventurer tank = allies.First(m => m.Role == AdventurerRole.Tank);
            Adventurer mage = allies.First(m => m.Role == AdventurerRole.Mage);

            tank.TakeDamage(tank.MaxHealth * 0.5f);
            mage.TakeDamage(mage.MaxHealth * 0.5f);

            Assert.AreEqual(tank, AdventurerAI.ChooseHealTarget(allies, 100f));
        }

        /// <summary>The rogue walks to an armed trap and defuses it when nothing is attacking.</summary>
        [Test]
        public void Ranged_DisarmsTrapsWhenThereIsNothingToShoot()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            Trap trap = layout.Traps[0];
            Assert.IsTrue(trap.IsArmed, "the trap should start armed");

            Advance(raid, 40f);

            MooseRunnerFacade.Log($"trap disarm {trap.DisarmFraction:P0} after 40s, armed={trap.IsArmed}");
            Assert.IsFalse(trap.IsArmed, "an unopposed party should have defused the first trap");
        }

        /// <summary>A trap the party defused can no longer be fired.</summary>
        [Test]
        public void FiringADisarmedTrap_Fails()
        {
            DungeonLayout layout = Corridor();
            var raid = new Raid(layout);
            layout.Traps[0].Disarm(999f);

            Assert.IsFalse(raid.FireTrap(layout.TrapCells[0]),
                "a defused trap must not fire");
        }

        /// <summary>Pathing steers around armed traps when a way round exists.</summary>
        [Test]
        public void Pathing_RoutesAroundArmedTraps()
        {
            DungeonLayout layout = Corridor();
            var avoid = new[] { layout.TrapCells[0] };

            var around = layout.Grid.FindPath(layout.EntranceCell, layout.BossCell, avoid);
            Assert.Greater(around.Count, 0, "there should still be a route");
            CollectionAssert.DoesNotContain(around, layout.TrapCells[0],
                "the route should have gone around the trap");
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
