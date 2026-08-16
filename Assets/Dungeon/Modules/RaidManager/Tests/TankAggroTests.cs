using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Pins the tank preference, and watches the thing it is most likely to break.
    /// </summary>
    /// <remarks>
    /// Until M9 the game had no aggro system at all. <c>Party.DistributeDamage</c> carries a 60/40
    /// tank split and has <b>zero non-test callers</b>; live combat came through
    /// <c>Raid.SwingMobs</c>, which hit whoever was nearest. The tank was usually hit only because it
    /// walks at the front, which the code comment admitted: <i>"that falls out of the marching order
    /// rather than being a rule"</i>.
    /// <para>
    /// The danger in making it a rule is not that it fails — it is that it works too well. Nearly all
    /// the money in this game is in the wound multiplier, which reads the party's <i>worst</i>
    /// survivor. A tank that soaks everything, on top of its existing 50% damage reduction, keeps
    /// that worst survivor healthy and flattens the curve the whole design rests on. So the income
    /// check below matters more than the targeting ones.
    /// </para>
    /// </remarks>
    public sealed class TankAggroTests
    {
        /// <summary>Plays a roster and reports what it earned and how hurt it got.</summary>
        /// <param name="composition">Roster to send in.</param>
        /// <param name="worstWound">Health of the worst-off survivor, at its lowest.</param>
        /// <returns>Energy harvested.</returns>
        private static float Play(PartyComposition composition, out float worstWound)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 4);
            var raid = new Raid(layout, 0f, composition, 4242);
            worstWound = 1f;

            while (raid.IsRunning)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) == 0 &&
                        raid.TotalEnergy > Raid.SpawnCost * 2f)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);

                if (raid.Party.LivingCount > 0)
                {
                    worstWound = Mathf.Min(worstWound, raid.Party.WoundFraction);
                }
            }

            return raid.EnergyHarvested;
        }

        /// <summary>
        /// A monster in reach of both a tank and a squishier body swings at the tank.
        /// </summary>
        /// <remarks>
        /// The author's B2, stated as directly as it can be tested: the tank is targeted first.
        /// </remarks>
        [Test]
        public void AMonsterInReachOfBoth_SwingsAtTheTank()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, PartyComposition.Opening, 7);

            Adventurer tank = null;
            Adventurer squishy = null;
            foreach (Adventurer member in raid.Party.Living)
            {
                if (member.Role == AdventurerRole.Tank)
                {
                    tank = member;
                }
                else if (squishy == null && member.Role != AdventurerRole.Tank)
                {
                    squishy = member;
                }
            }

            Assert.IsNotNull(tank, "this test needs a tank");
            Assert.IsNotNull(squishy, "this test needs somebody else to hit");

            // The squishy body stands CLOSER than the tank, so distance alone would pick it.
            Mob bully = raid.Mobs.Spawn(MobKind.Skeleton, tank.Cell);
            Assert.IsNotNull(bully, "this test needs a monster");

            float tankBefore = tank.HealthFraction;
            float squishyBefore = squishy.HealthFraction;

            for (int step = 0; step < 400 && raid.IsRunning; step++)
            {
                // Offset ALONG THE CORRIDOR, which runs up the screen since 2026-08-16. These read
                // (0.6f, 0f) while the dungeon ran left to right, and leaving them on X was not a
                // cosmetic staleness: doors between stacked rooms sit in the centre column, so a
                // party crossing a threshold is in a one-cell-wide gap and an eastward offset put
                // the monster inside the rock beside it, where it cannot swing at anybody. The
                // symptom was the tank losing exactly 0%, which reads as broken aggro rather than as
                // a monster in a wall.
                bully.Position = tank.Position + new Vector2(0f, 0.6f);
                squishy.Position = tank.Position + new Vector2(0f, 0.75f);
                raid.Tick(0.02f);
            }

            float tankLost = tankBefore - tank.HealthFraction;
            float squishyLost = squishyBefore - squishy.HealthFraction;

            MooseRunnerFacade.Log(
                $"tank lost {tankLost:P0}, the nearer {squishy.Role} lost {squishyLost:P0}");

            Assert.Greater(tankLost, squishyLost,
                $"the monster hit the {squishy.Role} rather than the tank, even with the tank in "
                + "reach -- so the tank draws no aggro");
        }

        /// <summary>
        /// A tank soaking the blows must not flatten the wound curve the economy rests on.
        /// </summary>
        /// <remarks>
        /// The one that actually matters. If tank preference keeps the worst survivor healthy, the
        /// wound multiplier stays near 1 and income collapses — the game would be technically
        /// correct and financially dead. Loose bounds on purpose: this is a regression tripwire on
        /// a quantity the author tunes, not a pin on today's number.
        /// </remarks>
        [Test]
        public void TankAggro_DoesNotFlattenTheWoundCurve()
        {
            float worstSeen = 1f;
            float bestHarvest = 0f;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                float harvested = Play(composition, out float wound);
                worstSeen = Mathf.Min(worstSeen, wound);
                bestHarvest = Mathf.Max(bestHarvest, harvested);

                MooseRunnerFacade.Log(
                    $"{composition.Name}: harvested {harvested:F0}, worst survivor bottomed at "
                    + $"{wound:P0} health");
            }

            MooseRunnerFacade.Log(
                $"deepest wound across all rosters {worstSeen:P0}, best harvest {bestHarvest:F0}");

            Assert.Less(worstSeen, 0.6f,
                $"no roster's worst survivor ever fell below {worstSeen:P0} health. The wound curve "
                + "is where nearly all the money is, so a party that cannot be hurt is a party that "
                + "cannot pay -- tank preference has flattened the game's central mechanic");

            Assert.Greater(bestHarvest, 100f,
                $"the best roster managed only {bestHarvest:F0} across a whole raid");
        }

        /// <summary>
        /// A tank that cannot be reached does not hold a monster's attention.
        /// </summary>
        /// <remarks>
        /// The failure mode that preference risks reintroducing. D17 measured a skeleton spending
        /// forty-eight seconds beside a party, reversing direction forty-six times and dealing no
        /// damage at all, because it was fixated on someone it could never touch. A monster with a
        /// body on top of it must hit that body rather than stare across the room.
        /// </remarks>
        [Test]
        public void AnUnreachableTank_DoesNotFreezeAMonster()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, PartyComposition.Opening, 11);

            Adventurer tank = null;
            Adventurer squishy = null;
            foreach (Adventurer member in raid.Party.Living)
            {
                if (member.Role == AdventurerRole.Tank && tank == null)
                {
                    tank = member;
                }
                else if (squishy == null)
                {
                    squishy = member;
                }
            }

            Assert.IsNotNull(tank, "this test needs a tank");
            Assert.IsNotNull(squishy, "this test needs somebody else");

            // Spawned on a spawner cell, not on the squishy: at tick zero the followers are placed
            // outside the walkable dungeon, so Spawn refuses their cell and returns null. The
            // monster is walked onto its victim below anyway.
            Mob bully = raid.Mobs.Spawn(MobKind.Skeleton, layout.SpawnerCells[0]);
            Assert.IsNotNull(bully, "this test needs a monster");

            float squishyBefore = squishy.HealthFraction;

            // The tank has to be HELD out of reach for the premise to exist at all. The party
            // starts clustered, so without this the tank is simply in reach and correctly takes the
            // blow -- which is the other test, not this one.
            // Everyone is held in place. Movement runs before combat resolves inside a tick, so a
            // Ranged member left free simply kites out of reach before the blow lands and the test
            // measures its own kiting rather than the monster's choice of target.
            Vector2 anchor = raid.Party.Position;
            Vector2 farCorner = anchor + new Vector2(0f, 3f);

            for (int step = 0; step < 500 && raid.IsRunning; step++)
            {
                tank.Position = farCorner;
                squishy.Position = anchor;
                bully.Position = anchor + new Vector2(0.4f, 0f);
                raid.Tick(0.02f);
            }

            float squishyLost = squishyBefore - squishy.HealthFraction;
            MooseRunnerFacade.Log(
                $"with the tank out of reach, the {squishy.Role} on top of the monster lost "
                + $"{squishyLost:P0}");

            Assert.Greater(squishyLost, 0f,
                "the monster dealt no damage at all with a body standing on it, which is the "
                + "fixated-on-an-unreachable-target standoff D17 measured");
        }
    }
}
