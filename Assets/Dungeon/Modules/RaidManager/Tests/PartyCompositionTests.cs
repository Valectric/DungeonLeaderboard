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
    /// Verifies that the six party compositions genuinely play differently.
    /// </summary>
    /// <remarks>
    /// SPEC.md section 4 calls composition "the primary source of run-to-run variation". A roster
    /// that changed the names above four sprites and nothing else would satisfy every obvious
    /// assertion -- the party exists, it has four members, the healer is a healer -- while delivering
    /// exactly none of that variation. So the assertions here are about <i>outcomes</i>: how long a
    /// party survives under identical fire, and what it is worth in an identical raid.
    /// </remarks>
    public sealed class PartyCompositionTests
    {
        /// <summary>Finds a composition by name.</summary>
        /// <param name="name">Name to find.</param>
        /// <returns>The composition.</returns>
        private static PartyComposition Named(string name)
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                if (composition.Name == name)
                {
                    return composition;
                }
            }

            Assert.Fail($"no composition named {name}");
            return null;
        }

        /// <summary>Every composition is four members, as SPEC.md section 4 requires.</summary>
        [Test]
        public void EveryComposition_IsAPartyOfFour()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                Assert.AreEqual(4, composition.Roles.Count,
                    $"{composition.Name} is not a party of four");
                Assert.IsNotEmpty(composition.Warning, $"{composition.Name} tells the player nothing");
            }

            Assert.GreaterOrEqual(PartyComposition.All.Length, 4,
                "too few rosters to be a source of variation");
        }

        /// <summary>
        /// A new player always meets the balanced party first.
        /// </summary>
        /// <remarks>
        /// Meeting THE UNSHRIVEN before knowing what a healer does means wiping them and concluding
        /// the game is unfair, when a wipe is the one outcome the design most wants avoided.
        /// </remarks>
        [Test]
        public void TheOpeningParty_IsTheBalancedOne()
        {
            Assert.AreEqual(1, PartyComposition.Opening.Count(AdventurerRole.Tank),
                "the first party a player meets should have a tank to soak for them");
            Assert.AreEqual(1, PartyComposition.Opening.Count(AdventurerRole.Healer),
                "and a healer, so their first mistakes are survivable");
        }

        /// <summary>The same seed always produces the same party, so a run is reproducible.</summary>
        [Test]
        public void TheSameSeed_AlwaysSendsTheSameParty()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                Assert.AreSame(PartyComposition.ForSeed(seed), PartyComposition.ForSeed(seed),
                    $"seed {seed} produced two different parties");
            }
        }

        /// <summary>Seeds reach every composition, so no roster is unreachable in play.</summary>
        [Test]
        public void SeedsReach_EveryComposition()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 400; seed++)
            {
                seen.Add(PartyComposition.ForSeed(seed).Name);
            }

            Assert.AreEqual(PartyComposition.All.Length, seen.Count,
                "some composition can never actually walk in");
        }

        /// <summary>
        /// Healers keep the party alive under heavy fire, and a second one keeps it alive longer.
        /// </summary>
        /// <remarks>
        /// Survival is the measure, not total health restored. Restored health saturates: the healer
        /// refuses to cast unless a full heal lands without overflowing, so under light damage nobody
        /// is ever wounded enough for the second healer to have anything to do, and one healer and
        /// two measure identically (135 each, when tried). That is a real property of the design
        /// rather than a bug -- <b>a second healer is worth nothing until the player is hurting the
        /// party hard enough</b>, which is a fact about how to play, not a fact about the roster.
        /// <para>
        /// Under sustained heavy damage the pool and the cast rate bind, and the roster shows.
        /// </para>
        /// </remarks>
        [Test]
        public void Healers_KeepThePartyAliveUnderHeavyFire()
        {
            float none = SecondsSurvived(Named("THE UNSHRIVEN"), 30f);
            float one = SecondsSurvived(Named("THE BALANCED PARTY"), 30f);
            float two = SecondsSurvived(Named("THE PILGRIMAGE"), 30f);

            MooseRunnerFacade.Log(
                $"survival at 30dps: none={none:F1}s one={one:F1}s two={two:F1}s");
            Assert.Greater(one, none, "a healer should buy the party time");
            Assert.Greater(two, one, "and a second healer should buy more");
        }

        /// <summary>
        /// A party with no tank dies markedly faster than one with two.
        /// </summary>
        /// <remarks>
        /// This is the assertion that makes composition a decision rather than a decoration: the same
        /// dungeon, the same verbs and the same mobs have to produce a different survival time, or
        /// the player has nothing to read the door for.
        /// </remarks>
        [Test]
        public void SurvivalTime_FollowsTheRoster()
        {
            float ironclads = SecondsSurvived(Named("THE IRONCLADS"), 14f);
            float skirmishers = SecondsSurvived(Named("THE SKIRMISHERS"), 14f);

            MooseRunnerFacade.Log(
                $"survival under fire: ironclads={ironclads:F1}s skirmishers={skirmishers:F1}s");
            Assert.Greater(ironclads, skirmishers + 3f,
                "two tanks should outlast none by a margin a player can feel");
        }

        /// <summary>
        /// The six rosters are worth visibly different amounts in an identical raid.
        /// </summary>
        /// <remarks>
        /// The spec's claim is that compositions "play completely differently in the same dungeon
        /// layout", so this measures exactly that: one dungeon, one ambush, six parties, and the
        /// spread between best and worst has to be large enough for a player to care which one
        /// walked in.
        /// <para>
        /// Deliberately no assertion about <i>which</i> roster wins. An ordering picked from
        /// intuition and then asserted is a test fitted to a guess -- it would pass because it was
        /// written after looking, and would say nothing. The spread is the falsifiable claim.
        /// </para>
        /// </remarks>
        [Test]
        public void Compositions_AreWorthDifferentAmounts()
        {
            float lowest = float.MaxValue;
            float highest = 0f;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                float harvested = HarvestedFromAnAmbush(composition);
                MooseRunnerFacade.Log($"{composition.Name} harvested {harvested:F1}");
                lowest = Mathf.Min(lowest, harvested);
                highest = Mathf.Max(highest, harvested);
            }

            Assert.Greater(highest, lowest * 1.25f,
                $"best {highest:F1} and worst {lowest:F1} are too close -- the roster does not matter");
        }

        /// <summary>A composition never changes the raid's length or its ending conditions.</summary>
        [Test]
        public void EveryComposition_CanStillFinishARaid()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                var raid = new Raid(DungeonLayout.BuildCorridor(), 0f, composition);
                float elapsed = 0f;

                while (raid.IsRunning && elapsed < Raid.RaidSeconds + 1f)
                {
                    raid.Tick(0.02f);
                    elapsed += 0.02f;
                }

                Assert.IsFalse(raid.IsRunning, $"{composition.Name} never ended its raid");
                Assert.AreNotEqual(RaidOutcome.PartyWiped, raid.Outcome,
                    $"{composition.Name} wiped with no mobs in the dungeon");
            }
        }

        /// <summary>Seconds a party survives under constant attack.</summary>
        /// <param name="composition">Party to run.</param>
        /// <param name="damagePerSecond">Damage rained on the party every tick.</param>
        /// <returns>Seconds until the party wiped, or the raid length if it held out.</returns>
        private static float SecondsSurvived(PartyComposition composition, float damagePerSecond)
        {
            var raid = new Raid(DungeonLayout.BuildCorridor(), 0f, composition);
            float elapsed = 0f;

            while (raid.Party.LivingCount > 0 && elapsed < Raid.RaidSeconds)
            {
                // Applied directly rather than through mobs, so the only variable between runs is
                // the roster: identical damage, identical dungeon, identical clock.
                raid.Party.DistributeDamage(damagePerSecond * 0.02f);
                raid.Tick(0.02f);
                elapsed += 0.02f;
            }

            return elapsed;
        }

        /// <summary>Energy harvested from a party that walks into one ambush.</summary>
        /// <remarks>
        /// One skeleton on the first spawner, and nothing else -- identical for every roster, so the
        /// only variable is who walked in.
        /// </remarks>
        /// <param name="composition">Party to run.</param>
        /// <returns>Energy harvested.</returns>
        private static float HarvestedFromAnAmbush(PartyComposition composition)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, composition);
            raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
            }

            return raid.EnergyHarvested;
        }

    }
}
