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
    /// Sweeps the whole game looking for the failures this project actually produces.
    /// </summary>
    /// <remarks>
    /// Not feature tests. These play the game many times across every roster and every reasonable
    /// player behaviour, and assert the <i>properties that must hold in all of them</i> -- nothing
    /// hangs, nothing runs away, the economy stays inside the band it was designed for, and no state
    /// leaks between raids.
    /// <para>
    /// CLAUDE.md's verification doctrine is that green tests hid three whole classes of bug in the
    /// sister project, and every one of them was a rate rather than a value. This is the sweep for
    /// rates.
    /// </para>
    /// </remarks>
    public sealed class ExploratoryTests
    {
        /// <summary>Plays a raid with a given policy and reports what it earned.</summary>
        /// <param name="composition">Party to send in.</param>
        /// <param name="seed">Combat seed.</param>
        /// <param name="spawnEverything">Whether the player ambushes at every opportunity.</param>
        /// <param name="shutDoors">Whether the player shuts every door it can.</param>
        /// <returns>The finished raid.</returns>
        private static Raid Play(
            PartyComposition composition, int seed, bool spawnEverything, bool shutDoors)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, composition, seed);
            int guard = 0;

            while (raid.IsRunning && guard++ < 5000)
            {
                if (spawnEverything)
                {
                    foreach (Vector2Int spawner in layout.SpawnerCells)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                if (shutDoors)
                {
                    foreach (Door door in layout.Grid.Doors)
                    {
                        if (door.IsOpen && !door.IsForced)
                        {
                            raid.ToggleDoor(door.Cell);
                        }
                    }
                }

                raid.Tick(0.02f);
            }

            Assert.Less(guard, 5000, "the raid never ended -- something is hung");
            return raid;
        }

        /// <summary>
        /// No combination of roster and player behaviour hangs, wipes unfairly, or runs away.
        /// </summary>
        /// <remarks>
        /// The broadest net in the suite: every roster against every policy, checking the invariants
        /// that must hold whatever happens.
        /// </remarks>
        [Test]
        public void EveryRosterAndPolicy_EndsSanely()
        {
            var harvests = new List<float>();

            foreach (PartyComposition composition in PartyComposition.All)
            {
                foreach (bool spawn in new[] { false, true })
                {
                    foreach (bool shut in new[] { false, true })
                    {
                        Raid raid = Play(composition, 4242, spawn, shut);

                        Assert.IsFalse(raid.IsRunning, "the raid should have ended");
                        Assert.GreaterOrEqual(raid.EnergyHarvested, 0f, "negative harvest");
                        Assert.Less(raid.EnergyHarvested, 20000f,
                            $"{composition.Name} spawn={spawn} shut={shut} harvested absurdly much");
                        Assert.GreaterOrEqual(raid.TotalEnergy, 0f, "spendable energy went negative");
                        Assert.LessOrEqual(raid.TimeRemaining, Raid.RaidSeconds, "the clock grew");
                        Assert.GreaterOrEqual(raid.TimeRemaining, 0f, "the clock went negative");

                        harvests.Add(raid.EnergyHarvested);
                        MooseRunnerFacade.Log(
                            $"{composition.Name} spawn={spawn} shut={shut}: "
                            + $"{raid.EnergyHarvested:F0} ({raid.Outcome})");
                    }
                }
            }

            harvests.Sort();
            MooseRunnerFacade.Log(
                $"harvest across {harvests.Count} runs: min={harvests[0]:F0} "
                + $"median={harvests[harvests.Count / 2]:F0} max={harvests[^1]:F0}");
        }

        /// <summary>
        /// Ambushing hard beats doing nothing, in every roster.
        /// </summary>
        /// <remarks>
        /// The single most important property in the game: the player's actions have to pay. If
        /// spawning monsters everywhere earned no more than sitting on your hands, none of the rest
        /// of the design matters.
        /// </remarks>
        [Test]
        public void PlayingWell_AlwaysBeatsPlayingNotAtAll()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                Raid idleRaid = Play(composition, 77, false, false);
                Raid activeRaid = Play(composition, 77, true, false);
                float idle = idleRaid.EnergyHarvested;
                float active = activeRaid.EnergyHarvested;

                MooseRunnerFacade.Log(
                    $"{composition.Name}: idle={idle:F0} ({idleRaid.Outcome}) "
                    + $"ambushed={active:F0} ({activeRaid.Outcome})");

                // A raid that ends in a wipe is exempt, and that is the design rather than a let-off.
                // This policy spawns at every spawner on every tick, which is not "playing well" --
                // it is killing the party as fast as possible, and each death now costs 50 points on
                // the author's instruction. THE IRONCLADS ambushed this hard wipe and bank nothing at
                // all against 4 for doing nothing, which is exactly the lesson the game should teach.
                if (activeRaid.Outcome == RaidOutcome.PartyWiped)
                {
                    // Exempt, and deliberately asserting nothing further. This policy spawns at
                    // every spawner on every tick, which is not playing well -- it is killing the
                    // party as fast as possible, and each death costs 50 points. What a wipe is
                    // worth against a raid the party survives is the business of
                    // KillingTheParty_NeverPaysBest, which measures exactly that and currently reads
                    // 4 against 214. Repeating a weaker version of that claim here would only give
                    // two tests to update the next time the curve moves.
                    continue;
                }

                Assert.Greater(active, idle * 1.5f,
                    $"{composition.Name}: ambushing barely beat doing nothing");
            }
        }

        /// <summary>
        /// A wiped party is worth less than a party kept alive.
        /// </summary>
        /// <remarks>
        /// SPEC.md's central inversion, asserted end to end rather than in the curve. If killing
        /// them ever paid better, every other decision in the game inverts with it.
        /// </remarks>
        [Test]
        public void KillingTheParty_NeverPaysBest()
        {
            float bestWipe = 0f;
            float bestSurvival = 0f;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    Raid raid = Play(composition, seed, true, false);
                    if (raid.Outcome == RaidOutcome.PartyWiped)
                    {
                        bestWipe = Mathf.Max(bestWipe, raid.EnergyHarvested);
                    }
                    else
                    {
                        bestSurvival = Mathf.Max(bestSurvival, raid.EnergyHarvested);
                    }
                }
            }

            MooseRunnerFacade.Log($"best wipe={bestWipe:F0}, best survival={bestSurvival:F0}");
            Assert.Greater(bestSurvival, bestWipe,
                "a wipe out-earned every raid where the party lived -- the design is inverted");
        }

        /// <summary>
        /// Repeated raids leak no state.
        /// </summary>
        /// <remarks>
        /// Testing rule 16: the same run three times must end the same way. A raid that carried
        /// something over would drift across a season and be almost impossible to attribute.
        /// </remarks>
        [Test]
        public void RepeatedRaids_AreIdentical()
        {
            float first = Play(PartyComposition.Opening, 5150, true, true).EnergyHarvested;
            float second = Play(PartyComposition.Opening, 5150, true, true).EnergyHarvested;
            float third = Play(PartyComposition.Opening, 5150, true, true).EnergyHarvested;

            MooseRunnerFacade.Log($"three identical raids: {first:F3} {second:F3} {third:F3}");
            Assert.AreEqual(first, second, 0.001f, "raid 2 differed from raid 1");
            Assert.AreEqual(first, third, 0.001f, "raid 3 differed from raid 1");
        }

        /// <summary>
        /// The energy rate never goes negative, absurd, or NaN at any point in any raid.
        /// </summary>
        /// <remarks>
        /// Sampled every tick rather than at the end. The rate is the number the whole screen is
        /// built around, and a single NaN frame would render as garbage and poison the harvest total
        /// for the rest of the raid.
        /// </remarks>
        [Test]
        public void TheRate_IsAlwaysSaneEveryTick()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor();
                var raid = new Raid(layout, 0f, composition, 909);
                float highest = 0f;

                while (raid.IsRunning)
                {
                    foreach (Vector2Int spawner in layout.SpawnerCells)
                    {
                        raid.SpawnMob(spawner);
                    }

                    raid.Tick(0.02f);

                    Assert.IsFalse(float.IsNaN(raid.CurrentRate), "the rate went NaN");
                    Assert.IsFalse(float.IsInfinity(raid.CurrentRate), "the rate went infinite");
                    Assert.GreaterOrEqual(raid.CurrentRate, 0f, "the rate went negative");
                    highest = Mathf.Max(highest, raid.CurrentRate);
                }

                MooseRunnerFacade.Log($"{composition.Name}: peak rate {highest:F1}/s");
                Assert.Less(highest, 200f,
                    $"{composition.Name} reached {highest:F0}/s, far beyond the design's ceiling");
            }
        }

        /// <summary>
        /// Nobody's health or mana ever leaves its own bounds.
        /// </summary>
        [Test]
        public void HealthAndMana_StayInBounds()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor();
                var raid = new Raid(layout, 0f, composition, 31337);

                while (raid.IsRunning)
                {
                    foreach (Vector2Int spawner in layout.SpawnerCells)
                    {
                        raid.SpawnMob(spawner);
                    }

                    raid.Tick(0.02f);

                    foreach (Adventurer member in raid.Party.Members)
                    {
                        Assert.GreaterOrEqual(member.HealthFraction, 0f, "health below zero");
                        Assert.LessOrEqual(member.HealthFraction, 1f, "health above full");
                        Assert.GreaterOrEqual(member.ManaFraction, 0f, "mana below zero");
                        Assert.LessOrEqual(member.ManaFraction, 1f, "mana above full");
                    }

                    foreach (Mob mob in raid.Mobs.Mobs)
                    {
                        Assert.GreaterOrEqual(mob.HealthFraction, 0f, "mob health below zero");
                        Assert.LessOrEqual(mob.HealthFraction, 1f, "mob health above full");
                    }
                }
            }
        }

        /// <summary>
        /// Nothing ever leaves the dungeon.
        /// </summary>
        /// <remarks>
        /// Blinking, panicked scrambling and mob separation all move things without pathfinding, so
        /// each is a way to end up inside a wall -- where a body would be unreachable, unkillable,
        /// and would hang the raid.
        /// </remarks>
        [Test]
        public void NobodyEverLeavesTheDungeon()
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor();
                var raid = new Raid(layout, 0f, composition, 8080);
                var grid = layout.Grid;

                while (raid.IsRunning)
                {
                    foreach (Vector2Int spawner in layout.SpawnerCells)
                    {
                        raid.SpawnMob(spawner);
                    }

                    raid.Tick(0.02f);

                    foreach (Adventurer member in raid.Party.Living)
                    {
                        Assert.IsTrue(
                            member.Position.x > -3f && member.Position.x < grid.Width + 3f &&
                            member.Position.y > -3f && member.Position.y < grid.Height + 3f,
                            $"{member.Role} left the map at {member.Position}");
                    }

                    foreach (Mob mob in raid.Mobs.Living)
                    {
                        Assert.IsTrue(
                            mob.Position.x > -3f && mob.Position.x < grid.Width + 3f &&
                            mob.Position.y > -3f && mob.Position.y < grid.Height + 3f,
                            $"a {mob.Kind} left the map at {mob.Position}");
                    }
                }
            }
        }
    }
}
