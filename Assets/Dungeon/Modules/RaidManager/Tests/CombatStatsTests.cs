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
    /// Pins the stat system to the damage-per-second figures the game was balanced around.
    /// </summary>
    /// <remarks>
    /// The stat block is a <i>decomposition</i> of those figures, not a replacement. CLAUDE.md is
    /// explicit that fight length is a rate and that this game is made of rates: a skeleton exists to
    /// hold a party still for about thirteen seconds, and if rolled damage drifts from the old
    /// constant then every fight, every energy curve and the entire economy moves with it. These
    /// tests are the thing standing between a stat system and a quietly broken game.
    /// </remarks>
    public sealed class CombatStatsTests
    {
        /// <summary>Armour of the monster the party's damage was originally tuned against.</summary>
        private const float SkeletonArmour = 0.15f;

        /// <summary>Armour of the adventurer the monsters' damage was tuned against.</summary>
        private const float TankArmour = 0.25f;

        /// <summary>
        /// Every adventurer's stat block still averages the damage per second it always did.
        /// </summary>
        [Test]
        public void AdventurerStats_MatchTheirLegacyDamagePerSecond()
        {
            float total = 0f;
            float legacyTotal = 0f;

            foreach (AdventurerRole role in System.Enum.GetValues(typeof(AdventurerRole)))
            {
                var member = new Adventurer(role, Vector2Int.zero);
                float expected = CombatMath.ExpectedDps(
                    member.WeaponDamage, member.Might, SkeletonArmour, member.AttackInterval);

                MooseRunnerFacade.Log(
                    $"{role}: stats give {expected:F2} dps, legacy {member.DamagePerSecond:F2}");

                Assert.AreEqual(member.DamagePerSecond, expected, member.DamagePerSecond * 0.08f,
                    $"{role}'s rolled damage drifted from the value the game was balanced on");

                total += expected;
                legacyTotal += member.DamagePerSecond;
            }

            Assert.AreEqual(legacyTotal, total, legacyTotal * 0.05f,
                "the party's total output moved, which changes every fight in the game");
        }

        /// <summary>Every monster's stat block still averages the damage per second it always did.</summary>
        [Test]
        public void MonsterStats_MatchTheirLegacyDamagePerSecond()
        {
            foreach (MobKind kind in System.Enum.GetValues(typeof(MobKind)))
            {
                var mob = new Mob(kind, Vector2Int.zero, 0);
                float expected = CombatMath.ExpectedDps(
                    mob.WeaponDamage, mob.Might, TankArmour, mob.AttackInterval);

                MooseRunnerFacade.Log(
                    $"{kind}: stats give {expected:F2} dps, legacy {mob.DamagePerSecond:F2}");

                Assert.AreEqual(mob.DamagePerSecond, expected, mob.DamagePerSecond * 0.08f,
                    $"{kind}'s rolled damage drifted from the value the game was balanced on");
            }
        }

        /// <summary>
        /// A skeleton still buys the player a worthwhile handful of seconds.
        /// </summary>
        /// <remarks>
        /// The end-to-end version of the stat block, and the one that actually matters: the whole
        /// point of a skeleton is the seconds it buys, and those seconds are the player's income.
        /// <para>
        /// Bounded on the property rather than on a particular figure, because the author has asked
        /// for monsters with two and a half times less health. Measured, that takes a skeleton from
        /// about thirteen seconds to about six and a half — still worth its energy. The band below
        /// holds for both, so this test does not have to be rewritten when the nerf lands.
        /// </para>
        /// </remarks>
        [Test]
        public void ASkeleton_BuysAWorthwhileHandfulOfSeconds()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            float engagedFor = 0f;
            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                if (raid.Party.Goal == PartyGoal.Fighting)
                {
                    engagedFor += 0.02f;
                }
            }

            MooseRunnerFacade.Log($"one skeleton held the party for {engagedFor:F1}s");
            Assert.Greater(engagedFor, 4f, "the skeleton died too fast to be worth its energy");
            Assert.Less(engagedFor, 22f,
                "the skeleton is holding them far longer than the stat block intends");
        }

        /// <summary>
        /// Rolled damage varies, but stays inside the band the roll promises.
        /// </summary>
        [Test]
        public void Rolls_VaryWithinTheirStatedBand()
        {
            var random = new System.Random(1234);
            float lowest = float.MaxValue;
            float highest = 0f;
            float sum = 0f;
            const int rolls = 4000;

            for (int i = 0; i < rolls; i++)
            {
                float damage = CombatMath.Roll(15f, 4f, SkeletonArmour, random);
                lowest = Mathf.Min(lowest, damage);
                highest = Mathf.Max(highest, damage);
                sum += damage;
            }

            float average = sum / rolls;
            float expected = CombatMath.ExpectedDps(15f, 4f, SkeletonArmour, 1f);

            MooseRunnerFacade.Log(
                $"4000 rolls: {lowest:F1} to {highest:F1}, average {average:F2}, expected {expected:F2}");

            Assert.AreNotEqual(lowest, highest, "damage is not varying at all");
            Assert.AreEqual(expected, average, expected * 0.03f,
                "the average roll drifted from the expected value the balance depends on");
            Assert.GreaterOrEqual(lowest, expected * CombatMath.MinRoll * 0.99f,
                "a roll came in below the band");
            Assert.LessOrEqual(highest, expected * CombatMath.MaxRoll * 1.01f,
                "a roll came in above the band");
        }

        /// <summary>Armour reduces damage but can never make a target invulnerable.</summary>
        /// <remarks>
        /// A fight neither side can win would hang, and a hung fight in this game is a party standing
        /// still earning nothing until the clock runs out.
        /// </remarks>
        [Test]
        public void Armour_ReducesButNeverNullifies()
        {
            var random = new System.Random(7);
            float armoured = CombatMath.Roll(10f, 0f, 0.9f, random);
            float bare = CombatMath.Roll(10f, 0f, 0f, random);

            Assert.GreaterOrEqual(armoured, CombatMath.MinimumDamage,
                "armour must never reduce a blow to nothing");
            Assert.Less(armoured, bare, "armour should have reduced the blow");
        }

        /// <summary>
        /// The same seed replays the identical fight.
        /// </summary>
        /// <remarks>
        /// One of the project's hard constraints: a run has to be reproducible from a seed in a bug
        /// report. Rolled damage is the easiest way to lose that, and it would be lost silently.
        /// </remarks>
        [Test]
        public void TheSameSeed_ReplaysTheIdenticalFight()
        {
            float first = HarvestWithSeed(99);
            float again = HarvestWithSeed(99);

            MooseRunnerFacade.Log($"seed 99 harvested {first:F4} and {again:F4}");
            Assert.AreEqual(first, again, 0.0001f, "the same seed produced a different fight");
        }

        /// <summary>
        /// Different seeds really do roll differently.
        /// </summary>
        /// <remarks>
        /// Asserted against the roll sequence, not against a raid's final takings. Two seeds harvest
        /// almost exactly the same amount -- 64.73 both, when tried -- because a few hundred rolls of
        /// plus or minus fifteen percent average out to nothing across a whole raid. That assertion
        /// passed on a hair's-width float difference and proved nothing at all; it would have passed
        /// just as happily with the seed ignored entirely.
        /// </remarks>
        [Test]
        public void DifferentSeeds_RollDifferently()
        {
            var one = new System.Random(99);
            var two = new System.Random(1000);
            int differences = 0;

            for (int i = 0; i < 50; i++)
            {
                float a = CombatMath.Roll(15f, 4f, SkeletonArmour, one);
                float b = CombatMath.Roll(15f, 4f, SkeletonArmour, two);
                if (!Mathf.Approximately(a, b))
                {
                    differences++;
                }
            }

            MooseRunnerFacade.Log($"{differences} of 50 rolls differed between two seeds");
            Assert.Greater(differences, 45,
                "two different seeds produced the same rolls, so the seed is being ignored");
        }

        /// <summary>The same seed replays the identical roll sequence, blow for blow.</summary>
        [Test]
        public void TheSameSeed_ReplaysTheIdenticalRolls()
        {
            var one = new System.Random(4242);
            var two = new System.Random(4242);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(
                    CombatMath.Roll(15f, 4f, SkeletonArmour, one),
                    CombatMath.Roll(15f, 4f, SkeletonArmour, two),
                    0.0001f,
                    $"roll {i} diverged between two generators on the same seed");
            }
        }

        /// <summary>The ranged pair actually loose shots, so the player can see who is contributing.</summary>
        [Test]
        public void TheRangedPair_LooseArrowsAndBolts()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            var seen = new HashSet<ShotKind>();
            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                foreach (Shot shot in raid.Shots.Shots)
                {
                    seen.Add(shot.Kind);
                }
            }

            MooseRunnerFacade.Log($"saw shot kinds: {string.Join(", ", seen)}");
            Assert.Contains(ShotKind.Arrow, new List<ShotKind>(seen), "the archer never fired");
            Assert.Contains(ShotKind.Bolt, new List<ShotKind>(seen), "the mage never fired");
        }

        /// <summary>Shots expire, so they cannot accumulate over a raid.</summary>
        [Test]
        public void Shots_LandAndDisappear()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            int most = 0;
            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                most = Mathf.Max(most, raid.Shots.Shots.Count);
            }

            MooseRunnerFacade.Log($"most shots in the air at once: {most}");
            Assert.Less(most, 12, "shots are accumulating instead of landing");
        }

        /// <summary>Runs an identical ambush with a given seed and reports the takings.</summary>
        /// <param name="seed">Seed for the combat rolls.</param>
        /// <returns>Energy harvested.</returns>
        private static float HarvestWithSeed(int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, null, seed);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
            }

            return raid.EnergyHarvested;
        }
    }
}
