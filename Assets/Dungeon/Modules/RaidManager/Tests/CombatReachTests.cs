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
    /// Verifies that only adventurers a monster can actually reach get hurt, and that the healer
    /// consequently has something to do.
    /// </summary>
    /// <remarks>
    /// Both behaviours were reported from play: the back rank bled while the tank was the one in the
    /// fight, and the healer never cast. They turned out to be one bug. Damage was shared across the
    /// whole party wherever it stood, so it arrived spread thin, and the healer refuses to cast
    /// unless a full heal lands without overflowing -- nobody was ever wounded enough to qualify.
    /// </remarks>
    public sealed class CombatReachTests
    {
        /// <summary>
        /// Runs a raid until a skeleton is genuinely in contact, then fights for a while.
        /// </summary>
        /// <remarks>
        /// Waiting for actual contact matters. Spawning a mob and ticking a fixed six seconds looked
        /// like a fight and was not one: the party was still crossing into the mob's room, two points
        /// of damage had landed in total, and the tests drew confident conclusions from a fight that
        /// had barely started.
        /// </remarks>
        /// <param name="seconds">Seconds of fighting to simulate once contact is made.</param>
        /// <returns>The raid, mid-fight.</returns>
        private static Raid FightingRaid(float seconds)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            while (raid.IsRunning &&
                   Vector2.Distance(raid.Party.Position, layout.SpawnerCells[0]) > 2.5f)
            {
                raid.Tick(0.02f);
            }

            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            for (float t = 0f; t < 20f && raid.IsRunning && !InContact(raid); t += 0.02f)
            {
                raid.Tick(0.02f);
            }

            Assert.IsTrue(InContact(raid), "the monster never reached the party, so nothing is proven");

            // Kept under pressure rather than left with the one monster. A skeleton dies in a few
            // seconds, so "a thirty-second fight" spawned once is really a short fight followed by
            // twenty-odd seconds of walking -- during which a healer tops everyone up and the tank
            // stops being the one bleeding. Every test built on this helper is about what happens
            // DURING sustained combat, and a player producing sustained combat replaces what dies.
            for (float t = 0f; t < seconds && raid.IsRunning; t += 0.02f)
            {
                if (!raid.Mobs.Living.Any())
                {
                    raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);
                }

                raid.Tick(0.02f);
            }

            return raid;
        }

        /// <summary>Whether any living mob is within melee reach of the party's leader.</summary>
        /// <param name="raid">Raid to inspect.</param>
        /// <returns>True when the fight has actually joined.</returns>
        private static bool InContact(Raid raid)
        {
            foreach (Mob mob in raid.Mobs.Living)
            {
                if (Vector2.Distance(mob.Position, raid.Party.Position) <= Party.MeleeReach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Finds a party member by role.</summary>
        private static Adventurer RoleIn(Party party, AdventurerRole role)
        {
            foreach (Adventurer member in party.Members)
            {
                if (member.Role == role)
                {
                    return member;
                }
            }

            return null;
        }

        /// <summary>
        /// A melee monster cannot wound someone standing well out of its reach.
        /// </summary>
        /// <remarks>
        /// This is the reported bug in its plainest form: the healer flees to the back of the room by
        /// design, and it was bleeding anyway.
        /// </remarks>
        [Test]
        public void AMonster_CannotHurtWhatItCannotReach()
        {
            Raid raid = FightingRaid(6f);
            Adventurer healer = RoleIn(raid.Party, AdventurerRole.Healer);
            Adventurer tank = RoleIn(raid.Party, AdventurerRole.Tank);

            float distance = float.MaxValue;
            foreach (Mob mob in raid.Mobs.Living)
            {
                distance = Mathf.Min(distance, Vector2.Distance(healer.Position, mob.Position));
            }

            MooseRunnerFacade.Log(
                $"healer {distance:F2} cells away at {healer.HealthFraction:P0}, " +
                $"tank at {tank.HealthFraction:P0}");

            if (distance <= Party.MeleeReach)
            {
                Assert.Ignore("the healer ended up inside melee reach, so this proves nothing");
            }

            Assert.AreEqual(1f, healer.HealthFraction, 0.001f,
                $"the healer is {distance:F2} cells from the nearest monster and still took damage");
        }

        /// <summary>
        /// The tank soaks the fight, which is what SPEC.md says a tank is for.
        /// </summary>
        [Test]
        public void TheTank_TakesTheDamage()
        {
            Raid raid = FightingRaid(6f);
            Adventurer tank = RoleIn(raid.Party, AdventurerRole.Tank);

            MooseRunnerFacade.Log($"tank at {tank.HealthFraction:P0} after six seconds of fighting");
            Assert.Less(tank.HealthFraction, 1f,
                "the tank leads and stands in reach, so the tank should be the one bleeding");
        }

        /// <summary>
        /// The healer actually heals during a fight.
        /// </summary>
        /// <remarks>
        /// It did not, and the reason was upstream: damage spread across four people meant nobody was
        /// ever missing a full heal's worth, and the healer will not cast into an overflow. With
        /// damage landing on whoever is in reach, the tank drops fast enough to be worth a cast.
        /// </remarks>
        [Test]
        public void TheHealer_HealsDuringALongFight()
        {
            Raid raid = FightingRaid(30f);
            Adventurer healer = RoleIn(raid.Party, AdventurerRole.Healer);

            MooseRunnerFacade.Log(
                $"after thirty seconds: mana {raid.Party.ManaFraction:P0}, " +
                $"healer alive={healer.IsAlive}");

            Assert.Less(raid.Party.ManaFraction, 1f,
                "the healer spent no mana at all in a thirty-second fight");
        }

        /// <summary>
        /// Damage from a source with no position still hits the whole party.
        /// </summary>
        /// <remarks>
        /// The reach rule must not quietly disarm anything that wounds without standing anywhere --
        /// a trap being the case that exists today. Asserted directly rather than by walking a party
        /// onto a trap, because the party <i>routes around armed traps by design</i> and never stands
        /// on one: an earlier version of this test waited for that to happen and simply spun until
        /// the raid timed out.
        /// </remarks>
        [Test]
        public void DamageWithNoAttackerPosition_StillHitsEveryone()
        {
            var raid = new Raid(DungeonLayout.BuildCorridor());
            float before = raid.Party.HealthFraction;

            raid.Party.DistributeDamage(60f);

            MooseRunnerFacade.Log(
                $"position-less damage took the party from {before:P0} to {raid.Party.HealthFraction:P0}");
            Assert.Less(raid.Party.HealthFraction, before,
                "a trap, or anything else without a position, must still wound");
        }

        /// <summary>A trap the party is standing on still wounds whoever is on the plate.</summary>
        [Test]
        public void AFiredTrap_StillWounds()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            Vector2Int trap = layout.TrapCells[0];

            // Put a member on the plate directly. The party will not walk onto an armed trap, which
            // is the whole point of the disarm mechanic, so waiting for it is waiting forever.
            Adventurer victim = RoleIn(raid.Party, AdventurerRole.Tank);
            victim.Position = trap;

            float before = victim.HealthFraction;
            Assert.IsTrue(raid.FireTrap(trap), "the trap should have fired");

            MooseRunnerFacade.Log($"trap took the tank from {before:P0} to {victim.HealthFraction:P0}");
            Assert.Less(victim.HealthFraction, before, "a fired trap must wound whoever stands on it");
        }

        /// <summary>
        /// Damage and healing both reach the feed the view draws numbers from.
        /// </summary>
        /// <remarks>
        /// Watched <i>while</i> the fight runs. Numbers live about a second, so inspecting the feed
        /// after a raid has finished finds it empty and the assertion passes for the wrong reason --
        /// which is exactly what an earlier version of this test did, reporting a healthy feed that
        /// had never contained anything.
        /// </remarks>
        [Test]
        public void TheCombatFeed_ReportsDamageAndHealing()
        {
            Raid raid = FightingRaid(0f);
            bool sawHurtAdventurer = false;
            bool sawHurtMonster = false;
            bool sawHealing = false;
            int mostAtOnce = 0;

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                mostAtOnce = Mathf.Max(mostAtOnce, raid.Feed.Numbers.Count);

                foreach (CombatNumber number in raid.Feed.Numbers)
                {
                    sawHealing |= number.IsHeal;
                    sawHurtAdventurer |=
                        !number.IsHeal && number.Target == CombatTarget.Adventurer;
                    sawHurtMonster |= !number.IsHeal && number.Target == CombatTarget.Monster;
                }
            }

            MooseRunnerFacade.Log(
                $"feed saw adventurer-hurt={sawHurtAdventurer} monster-hurt={sawHurtMonster} " +
                $"healing={sawHealing}, peak {mostAtOnce} at once");

            // Both directions must be distinguishable. An adventurer bleeding is the player earning;
            // their monster bleeding is the stall ending. Reporting both as plain "damage" left the
            // player unable to tell a good fight from a losing one.
            Assert.IsTrue(sawHurtAdventurer, "no adventurer damage was ever reported");
            Assert.IsTrue(sawHurtMonster, "no monster damage was ever reported");
            Assert.IsTrue(sawHealing, "no healing number was ever produced during a fight");

            // Numbers expire, so the feed must never accumulate over a whole raid.
            Assert.Less(mostAtOnce, 40,
                "the feed is accumulating numbers instead of expiring them");
        }
    }
}
