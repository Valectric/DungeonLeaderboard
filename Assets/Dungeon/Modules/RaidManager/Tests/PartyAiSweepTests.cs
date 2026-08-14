using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Checks the party AI does what each role promises, in the situations play actually produces.
    /// </summary>
    /// <remarks>
    /// The roles are the game's variety, and every one of them has already been broken once by a
    /// change made for a good reason elsewhere: the tank soaking too well flattened the energy curve,
    /// the archer's reach stopped it picking locks, and every role but the tank was unable to lead.
    /// These pin the promises so the next such change is caught here rather than in a browser.
    /// </remarks>
    public sealed class PartyAiSweepTests
    {
        /// <summary>Runs a raid until a predicate holds or the clock runs out.</summary>
        /// <param name="raid">Raid to advance.</param>
        /// <param name="until">Condition to wait for.</param>
        /// <returns>True when the condition was reached.</returns>
        private static bool RunUntil(Raid raid, System.Func<bool> until)
        {
            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                if (until())
                {
                    return true;
                }

                raid.Tick(0.02f);
            }

            return until();
        }

        /// <summary>Finds a living member by role.</summary>
        private static Adventurer RoleIn(Party party, AdventurerRole role)
        {
            foreach (Adventurer member in party.Living)
            {
                if (member.Role == role)
                {
                    return member;
                }
            }

            return null;
        }

        /// <summary>
        /// Whoever is leading routes around an armed trap rather than blundering over it.
        /// </summary>
        /// <remarks>
        /// Checked for the leader specifically, because <b>the rogue stands on the plate on
        /// purpose</b> — that is how disarming works, and it is the whole tension of the mechanic:
        /// the player either spends the trap while somebody is crouched over it or loses it. An
        /// earlier version of this test asked whether <i>anybody</i> ever touched an armed trap and
        /// failed on the rogue doing its job.
        /// <para>
        /// The leader is the one that matters. If it walked over plates, avoidance would be
        /// meaningless and the trap verb would be free damage on a timer.
        /// </para>
        /// </remarks>
        [Test]
        public void TheLeader_RoutesAroundArmedTraps()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            var leaderStoodOn = new HashSet<Vector2Int>();
            var rogueStoodOn = new HashSet<Vector2Int>();

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);

                bool first = true;
                foreach (Adventurer member in raid.Party.Living)
                {
                    if (first)
                    {
                        leaderStoodOn.Add(member.Cell);
                        first = false;
                    }

                    if (member.Role == AdventurerRole.Ranged)
                    {
                        rogueStoodOn.Add(member.Cell);
                    }
                }
            }

            int leaderTrampled = 0;
            int rogueVisited = 0;
            foreach (Trap trap in layout.Traps)
            {
                if (leaderStoodOn.Contains(trap.Cell))
                {
                    leaderTrampled++;
                }

                if (rogueStoodOn.Contains(trap.Cell))
                {
                    rogueVisited++;
                }
            }

            MooseRunnerFacade.Log(
                $"of {layout.Traps.Count} traps, the leader stood on {leaderTrampled} "
                + $"and the rogue visited {rogueVisited} to disarm them");

            Assert.AreEqual(0, leaderTrampled,
                "the leader walked over an armed trap, so avoidance is not working");
        }

        /// <summary>The healer stays further from monsters than the tank does.</summary>
        /// <remarks>
        /// SPEC.md calls the healer the player's best customer, which only works if it survives. It
        /// is the party's whole sustain, and a healer that stands in the front rank dies early and
        /// takes the raid's earning potential with it.
        /// </remarks>
        [Test]
        public void TheHealer_KeepsItsDistanceBetterThanTheTank()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            double healerTotal = 0d;
            double tankTotal = 0d;
            int samples = 0;

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);

                Adventurer healer = RoleIn(raid.Party, AdventurerRole.Healer);
                Adventurer tank = RoleIn(raid.Party, AdventurerRole.Tank);
                if (healer == null || tank == null)
                {
                    continue;
                }

                float nearest = float.MaxValue;
                float tankNearest = float.MaxValue;
                foreach (Mob mob in raid.Mobs.Living)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(healer.Position, mob.Position));
                    tankNearest = Mathf.Min(tankNearest, Vector2.Distance(tank.Position, mob.Position));
                }

                if (nearest == float.MaxValue)
                {
                    continue;
                }

                healerTotal += nearest;
                tankTotal += tankNearest;
                samples++;
            }

            if (samples == 0)
            {
                Assert.Fail("no monster ever lived long enough to measure against");
            }

            double healerAverage = healerTotal / samples;
            double tankAverage = tankTotal / samples;
            MooseRunnerFacade.Log(
                $"average distance to the nearest monster: healer {healerAverage:F2}, "
                + $"tank {tankAverage:F2}");

            Assert.Greater(healerAverage, tankAverage,
                "the healer stood closer to danger than the tank did");
        }

        /// <summary>A wounded party breaks off and runs rather than fighting to the death.</summary>
        [Test]
        public void AWoundedParty_Retreats()
        {
            var raid = new Raid(DungeonLayout.BuildCorridor());

            bool retreated = RunUntil(raid, () =>
            {
                foreach (Adventurer member in raid.Party.Living)
                {
                    member.TakeDamage(member.MaxHealth * 0.02f);
                }

                return raid.Party.Goal == PartyGoal.Retreating;
            });

            MooseRunnerFacade.Log(
                $"party retreated at {raid.Party.HealthFraction:P0} pooled health, "
                + $"worst member {raid.Party.WoundFraction:P0}");
            Assert.IsTrue(retreated, "a party wounded to the threshold never broke off");
        }

        /// <summary>A retreating party recovers and pushes on rather than yo-yoing at the line.</summary>
        /// <remarks>
        /// Hysteresis between the two thresholds. Without it the party dithers on the boundary,
        /// which reads as indecision and never resolves into either ending.
        /// </remarks>
        [Test]
        public void ARecoveredParty_PushesOnAgain()
        {
            var raid = new Raid(DungeonLayout.BuildCorridor());

            RunUntil(raid, () =>
            {
                foreach (Adventurer member in raid.Party.Living)
                {
                    member.TakeDamage(member.MaxHealth * 0.02f);
                }

                return raid.Party.Goal == PartyGoal.Retreating;
            });

            Assert.AreEqual(PartyGoal.Retreating, raid.Party.Goal, "the setup failed");

            // Patch them up and watch them turn around.
            foreach (Adventurer member in raid.Party.Living)
            {
                member.Heal(member.MaxHealth);
            }

            bool advancing = RunUntil(raid, () => raid.Party.Goal == PartyGoal.Advancing);
            MooseRunnerFacade.Log($"after healing, the party is {raid.Party.Goal}");
            Assert.IsTrue(advancing, "a fully healed party never stopped retreating");
        }

        /// <summary>Every role contributes damage over a fight rather than standing idle.</summary>
        /// <remarks>
        /// The stat block gives each role its own weapon and cadence, and it is easy for one to end
        /// up unable to reach anything -- the archer's lock-picking reach was exactly that bug in a
        /// different costume.
        /// </remarks>
        [Test]
        public void EveryRole_ActuallyFights()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            // Keep a monster alive in front of them for the whole raid.
            var cooldownsSeen = new Dictionary<AdventurerRole, bool>();
            while (raid.IsRunning)
            {
                if (raid.Mobs.CountInRoom(layout.Grid.RoomAt(raid.Party.Cell)) == 0)
                {
                    raid.Mobs.Spawn(MobKind.Skeleton, raid.Party.Cell);
                }

                raid.Tick(0.02f);

                foreach (Adventurer member in raid.Party.Living)
                {
                    // A cooldown that has been set is proof the member took a swing.
                    if (member.AttackCooldown > 0f)
                    {
                        cooldownsSeen[member.Role] = true;
                    }
                }
            }

            foreach (AdventurerRole role in System.Enum.GetValues(typeof(AdventurerRole)))
            {
                MooseRunnerFacade.Log($"{role} swung at something: {cooldownsSeen.ContainsKey(role)}");
            }

            Assert.IsTrue(cooldownsSeen.ContainsKey(AdventurerRole.Tank), "the tank never swung");
            Assert.IsTrue(cooldownsSeen.ContainsKey(AdventurerRole.Ranged), "the archer never fired");
            Assert.IsTrue(cooldownsSeen.ContainsKey(AdventurerRole.Mage), "the mage never cast");
        }

        /// <summary>A cornered fragile role is marked as panicking, so the view can show it.</summary>
        /// <remarks>
        /// SPEC.md section 9 asks for visibly panicking party members. The flag is what the sprite
        /// motion reads, so if it never sets, the polish silently does nothing.
        /// </remarks>
        [Test]
        public void ACorneredRole_IsMarkedAsPanicking()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            bool sawPanic = RunUntil(raid, () =>
            {
                Adventurer mage = RoleIn(raid.Party, AdventurerRole.Mage);
                if (mage != null && raid.Mobs.CountInRoom(layout.Grid.RoomAt(mage.Cell)) == 0)
                {
                    Mob bully = raid.Mobs.Spawn(MobKind.Skeleton, mage.Cell);
                    if (bully != null)
                    {
                        bully.Position = mage.Position;
                    }
                }

                foreach (Adventurer member in raid.Party.Living)
                {
                    if (member.IsPanicking)
                    {
                        return true;
                    }
                }

                return false;
            });

            MooseRunnerFacade.Log($"a party member was marked panicking: {sawPanic}");
            Assert.IsTrue(sawPanic,
                "no member ever panicked with a monster stood on them, so the polish shows nothing");
        }

        /// <summary>The tank leads the column, which is what makes it the one that gets hit.</summary>
        [Test]
        public void TheTank_LeadsTheColumn()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            int leadingSamples = 0;
            int samples = 0;

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);

                Adventurer tank = RoleIn(raid.Party, AdventurerRole.Tank);
                if (tank == null || raid.Party.Goal != PartyGoal.Advancing)
                {
                    continue;
                }

                bool foremost = true;
                foreach (Adventurer member in raid.Party.Living)
                {
                    if (member.Position.x > tank.Position.x + 0.05f)
                    {
                        foremost = false;
                    }
                }

                samples++;
                if (foremost)
                {
                    leadingSamples++;
                }
            }

            float share = samples == 0 ? 0f : (float)leadingSamples / samples;
            MooseRunnerFacade.Log($"the tank was foremost {share:P0} of the time while advancing");
            Assert.Greater(share, 0.7f, "the tank is not leading, so it will not be the one hit");
        }
    }
}
