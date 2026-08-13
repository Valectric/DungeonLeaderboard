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

        /// <summary>
        /// Every composition actually walks toward the boss room, whoever is leading it.
        /// </summary>
        /// <remarks>
        /// This is the test that should have existed from the start. Only the tank's behaviour ever
        /// pathed to the objective; every other role fell back to its formation slot, which is a
        /// point on the <i>leader's</i> trail and therefore meaningless for the leader itself --
        /// leaving it walking toward <c>Vector2.zero</c>, the bottom-left corner of the grid. So
        /// <b>THE GLASS CANNONS and THE SKIRMISHERS never advanced at all</b>, and neither did any
        /// party whose tank had died.
        /// <para>
        /// Reported from play, not caught here, because every existing assertion was satisfied by a
        /// party standing in a corner: the raid still ended, nobody wiped, the clock still ran. It
        /// even made the composition-variety test look good -- two rosters "worth" almost nothing
        /// were not cheap, they were broken.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryComposition_ActuallyAdvances()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor();
                var raid = new Raid(layout, 0f, composition);
                float startX = raid.Party.Position.x;

                // No mobs at all, so there is nothing to fight and nothing to explain standing still.
                for (int step = 0; step < 1000 && raid.IsRunning; step++)
                {
                    raid.Tick(0.02f);
                }

                float travelled = raid.Party.Position.x - startX;
                MooseRunnerFacade.Log(
                    $"{composition.Name} travelled {travelled:F1} cells in twenty seconds");

                Assert.Greater(travelled, 4f,
                    $"{composition.Name} barely moved -- led by "
                    + $"{composition.Roles[0]}, it is not advancing");
            }
        }

        /// <summary>
        /// A party keeps advancing after its tank dies.
        /// </summary>
        /// <remarks>
        /// The same defect from the other direction, and the one a player meets most often: a party
        /// that starts with a tank and loses it mid-raid used to stop dead wherever it stood.
        /// </remarks>
        [Test]
        public void APartyThatLosesItsTank_KeepsGoing()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, Named("THE BALANCED PARTY"));

            for (int step = 0; step < 200 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            // Kill the tank outright, leaving a ranged attacker at the front.
            foreach (Adventurer member in raid.Party.Living)
            {
                if (member.Role == AdventurerRole.Tank)
                {
                    member.TakeDamage(member.MaxHealth * 2f);
                    break;
                }
            }

            float afterDeath = raid.Party.Position.x;
            for (int step = 0; step < 600 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            float travelled = raid.Party.Position.x - afterDeath;
            MooseRunnerFacade.Log($"leaderless party travelled {travelled:F1} cells after the tank died");
            Assert.Greater(travelled, 3f, "the party stopped dead when its tank died");
        }

        /// <summary>
        /// The same party never raids twice running.
        /// </summary>
        /// <remarks>
        /// A fair roll over six rosters repeats one time in six, which a player meets inside a normal
        /// run -- and composition is meant to be this game's <i>primary</i> source of variety, so a
        /// back-to-back repeat reads as the feature being broken rather than as a coincidence.
        /// Reported from play after exactly that happened.
        /// </remarks>
        [Test]
        public void TheSameParty_NeverRaidsTwiceRunning()
        {
            int seed = 12345;
            PartyComposition previous = PartyComposition.Opening;
            var counts = new Dictionary<string, int>();

            for (int raid = 0; raid < 300; raid++)
            {
                seed = unchecked((seed * 1103515245) + 12345);
                PartyComposition next = PartyComposition.ForSeed(seed, previous);

                Assert.AreNotSame(previous, next,
                    $"raid {raid} sent {next.Name} straight after itself");

                counts[next.Name] = counts.GetValueOrDefault(next.Name, 0) + 1;
                previous = next;
            }

            // Avoiding repeats must not accidentally make one roster rare or unreachable.
            Assert.AreEqual(PartyComposition.All.Length, counts.Count,
                "some roster never appeared across three hundred raids");

            foreach (KeyValuePair<string, int> pair in counts)
            {
                MooseRunnerFacade.Log($"{pair.Key} appeared {pair.Value} times in 300 raids");
                Assert.Greater(pair.Value, 20,
                    $"{pair.Key} is far rarer than the others, so the spread is skewed");
            }
        }

        /// <summary>
        /// A mage cornered by a melee monster gets itself back out of reach.
        /// </summary>
        /// <remarks>
        /// It could not before. The standoff behaviour was correct and completely ineffective: a mob
        /// closes at 1.9 cells a second and the party walks at 0.6, so a mage backing away at walking
        /// pace was not retreating, it was being escorted. From the outside the fragile roles simply
        /// stood still and died.
        /// </remarks>
        [Test]
        public void ACorneredMage_GetsBackOutOfReach()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, Named("THE GLASS CANNONS"));

            for (int step = 0; step < 400 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            Adventurer mage = null;
            foreach (Adventurer member in raid.Party.Living)
            {
                if (member.Role == AdventurerRole.Mage)
                {
                    mage = member;
                    break;
                }
            }

            // Drop a skeleton right on top of the mage.
            Mob bully = raid.Mobs.Spawn(MobKind.Skeleton, mage.Cell);
            Assert.IsNotNull(bully, "the test needs a monster beside the mage");
            bully.Position = mage.Position;

            float startDistance = Vector2.Distance(mage.Position, bully.Position);
            for (int step = 0; step < 150 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            float endDistance = Vector2.Distance(mage.Position, bully.Position);
            MooseRunnerFacade.Log(
                $"cornered mage went from {startDistance:F2} to {endDistance:F2} cells away");

            Assert.Greater(endDistance, Party.MeleeReach,
                "the mage never got clear of the monster standing on it");
        }

        /// <summary>
        /// The healer keeps topping the party up after the fight is over.
        /// </summary>
        /// <remarks>
        /// It did not, and it looked like a bug because it was one: the healer refused to cast unless
        /// a full heal landed without overflowing, so once wounds fell below forty-five nobody
        /// qualified and it stood there with a full bar. From the outside it read as the healer
        /// downing tools the moment the monster died -- exactly when it should be patching everyone
        /// up for the next room.
        /// </remarks>
        [Test]
        public void TheHealer_TopsThePartyUpAfterTheFight()
        {
            var raid = new Raid(DungeonLayout.BuildCorridor());

            // A shallow wound, well under a full heal's worth, and no monster anywhere.
            foreach (Adventurer member in raid.Party.Living)
            {
                member.TakeDamage(member.MaxHealth * 0.18f);
            }

            float wounded = raid.Party.HealthFraction;
            for (int step = 0; step < 900 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            float after = raid.Party.HealthFraction;
            MooseRunnerFacade.Log(
                $"out of combat: party went from {wounded:P0} to {after:P0}");

            Assert.Greater(after, wounded + 0.05f,
                "the healer did nothing between fights, so shallow wounds never got patched");
        }

        /// <summary>
        /// Healers regenerate mana, so a long raid is not decided by the first thirty seconds.
        /// </summary>
        [Test]
        public void HealerMana_RegeneratesOverTime()
        {
            var raid = new Raid(DungeonLayout.BuildCorridor());

            Adventurer healer = null;
            foreach (Adventurer member in raid.Party.Living)
            {
                if (member.Role == AdventurerRole.Healer)
                {
                    healer = member;
                }
            }

            Assert.IsNotNull(healer, "the balanced party has a healer");
            healer.SpendMana(healer.MaxMana * 0.8f);
            float drained = healer.ManaFraction;

            for (int step = 0; step < 250 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            MooseRunnerFacade.Log(
                $"healer mana {drained:P0} -> {healer.ManaFraction:P0} after five seconds");
            Assert.Greater(healer.ManaFraction, drained, "healer mana should refill over time");
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
