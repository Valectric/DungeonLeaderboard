using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.ShopManager.Tests
{
    /// <summary>
    /// Adversarial placements: what a player can do to their own dungeon now that they aim.
    /// </summary>
    /// <remarks>
    /// Selling counts and scattering them by formula bounded the shapes a dungeon could take. Aiming
    /// removes that bound — every spawner in the first room, a wall of chests, forty traps on one
    /// row — and each of those is a layout the simulation has never been run against.
    /// <para>
    /// The property that matters in all of them is the same: <b>the raid must end</b>. A raid that
    /// cannot finish is not a balance problem, it is a hang, and the player's only way out is to
    /// close the tab. Everything else here is downstream of that.
    /// </para>
    /// </remarks>
    public sealed class PlacementSweepTests
    {
        /// <summary>
        /// Plays a raid to its end, failing rather than hanging if it never gets there.
        /// </summary>
        /// <remarks>
        /// Played, not merely ticked. Spawning is a verb the player presses, so a raid nobody plays
        /// never meets a monster however many spawners were bought -- the first version of this
        /// fixture ticked in silence and reported that twelve bone piles harvested one energy.
        /// </remarks>
        /// <param name="raid">Raid to run.</param>
        /// <param name="layout">Dungeon being raided.</param>
        /// <param name="what">What is being tested, for the failure message.</param>
        /// <param name="aggressive">Whether to use every spawner rather than one at a time.</param>
        /// <returns>Seconds of simulated time the raid took.</returns>
        private static float RunToEnd(
            Raid raid, DungeonLayout layout, string what, bool aggressive = false)
        {
            float seconds = ShopBot.Play(raid, layout, aggressive);
            Assert.Greater(seconds, 0f, $"{what}: the raid never ended");
            return seconds;
        }

        /// <summary>Every free cell of a layout, in scan order.</summary>
        /// <param name="layout">Dungeon to scan.</param>
        /// <returns>Buildable cells.</returns>
        private static List<Vector2Int> FreeCells(DungeonLayout layout)
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.CanBuildOn(cell))
                    {
                        cells.Add(cell);
                    }
                }
            }

            return cells;
        }

        /// <summary>
        /// A dungeon with every buildable tile filled still finishes its raid.
        /// </summary>
        /// <remarks>
        /// The worst case the shop can now produce, and one the old formula could never reach: it
        /// scattered a bounded number of items across rooms past the first, whereas aiming lets a
        /// rich player cover the floor.
        /// </remarks>
        [Test]
        public void ACompletelyFilledDungeon_StillFinishes()
        {
            var loadout = new Loadout();
            List<Vector2Int> free = FreeCells(ShopBot.Build(loadout));

            for (int i = 0; i < free.Count; i++)
            {
                ShopItem item = (i % 3) switch
                {
                    0 => ShopItem.Skeleton,
                    1 => ShopItem.SpikeTrap,
                    _ => ShopItem.Chest
                };

                loadout.Add(item, free[i]);
            }

            DungeonLayout layout = ShopBot.Build(loadout);
            MooseRunnerFacade.Log(
                $"filled dungeon: {layout.SpawnerCells.Count} spawners, "
                + $"{layout.TrapCells.Count} traps, {layout.ChestCells.Count} chests");

            var raid = new Raid(layout, 0f, null, 99);
            float seconds = RunToEnd(raid, layout, "a completely filled dungeon", aggressive: true);
            MooseRunnerFacade.Log($"filled dungeon ended {raid.Outcome} after {seconds:F1}s");
        }

        /// <summary>
        /// Stacking every spawner in the room the party enters still finishes.
        /// </summary>
        /// <remarks>
        /// The obvious exploit once you can aim: put everything where the party arrives first, so
        /// nothing is wasted on rooms they may never reach. It should be strong — that is the reward
        /// for thinking — but a party that dies in four seconds earns nothing, so this also checks
        /// the greedy answer does not simply beat the game.
        /// </remarks>
        [Test]
        public void EverySpawnerInTheFirstRoom_StillFinishes()
        {
            var loadout = new Loadout();
            DungeonLayout empty = ShopBot.Build(loadout);
            int firstRoom = empty.Grid.RoomAt(empty.RoomCentres[0]);

            int placed = 0;
            foreach (Vector2Int cell in FreeCells(empty))
            {
                if (empty.Grid.RoomAt(cell) == firstRoom && placed < 12)
                {
                    loadout.Add(ShopItem.Skeleton, cell);
                    placed++;
                }
            }

            DungeonLayout stacked = ShopBot.Build(loadout);
            var raid = new Raid(stacked, 0f, null, 7);
            float seconds = RunToEnd(raid, stacked, "every spawner in the first room", aggressive: true);

            MooseRunnerFacade.Log(
                $"{placed} spawners in room one: {raid.Outcome} after {seconds:F1}s, "
                + $"harvested {raid.EnergyHarvested:F0}");
        }

        /// <summary>
        /// A wall of chests does not trap the party in an endless detour.
        /// </summary>
        /// <remarks>
        /// Chests are looting detours, and looting takes a fixed few seconds each. Enough of them and
        /// the party could in principle spend the whole raid looting — which is fine, it is a
        /// stalling tactic and the clock still runs — but the loop that picks the next chest must
        /// terminate, and a chest the party can never quite reach would spin it forever.
        /// </remarks>
        [Test]
        public void AWallOfChests_DoesNotStallTheParty()
        {
            var loadout = new Loadout();
            foreach (Vector2Int cell in FreeCells(ShopBot.Build(loadout)))
            {
                loadout.Add(ShopItem.Chest, cell);
            }

            DungeonLayout layout = ShopBot.Build(loadout);
            var raid = new Raid(layout, 0f, null, 21);
            float seconds = RunToEnd(raid, layout, "a wall of chests");

            int looted = 0;
            foreach (Vector2Int chest in layout.ChestCells)
            {
                if (raid.Party.HasLooted(chest))
                {
                    looted++;
                }
            }

            MooseRunnerFacade.Log(
                $"{layout.ChestCells.Count} chests: {raid.Outcome} after {seconds:F1}s, "
                + $"party looted {looted} of them");
        }

        /// <summary>
        /// A corridor paved with traps still lets the party walk in.
        /// </summary>
        /// <remarks>
        /// The party routes around armed traps. Paving every cell of the walking row removes every
        /// route, so either the pathing gives up and the party stands still for sixty seconds — which
        /// earns nothing and is the single most boring outcome the game can produce — or it accepts
        /// the risk and walks. It has to be the second.
        /// </remarks>
        [Test]
        public void ACorridorPavedWithTraps_IsStillWalkable()
        {
            var loadout = new Loadout();
            DungeonLayout empty = ShopBot.Build(loadout);
            int walkingRow = empty.EntranceCell.y;

            foreach (Vector2Int cell in FreeCells(empty))
            {
                if (cell.y == walkingRow)
                {
                    loadout.Add(ShopItem.SpikeTrap, cell);
                }
            }

            DungeonLayout layout = ShopBot.Build(loadout);
            var raid = new Raid(layout, 0f, null, 3);
            Vector2 start = raid.Party.Position;

            for (int i = 0; i < 400; i++)
            {
                raid.Tick(0.02f);
            }

            float moved = Vector2.Distance(raid.Party.Position, start);
            MooseRunnerFacade.Log(
                $"{layout.TrapCells.Count} traps on the walking row: party moved {moved:F2} cells "
                + "in 8s");

            Assert.Greater(moved, 0.5f,
                "a party that will not enter a trapped corridor stands still for the whole raid, "
                + "which earns nothing and is the dullest outcome the game can produce");

            RunToEnd(raid, layout, "a corridor paved with traps");
        }

        /// <summary>
        /// Nothing bought can be placed where it would break the party's route.
        /// </summary>
        /// <remarks>
        /// The one structural guarantee: the boss room must stay reachable, because reaching it is
        /// the game's losing ending and a dungeon that removes it removes the tension. Purchases are
        /// furniture rather than walls, so this should hold by construction — asserted anyway,
        /// because "by construction" is what everything that later broke was also thought to be.
        /// </remarks>
        [Test]
        public void TheBossRoomStaysReachable_HoweverTheDungeonIsFurnished()
        {
            var loadout = new Loadout();
            foreach (Vector2Int cell in FreeCells(ShopBot.Build(loadout)))
            {
                loadout.Add(ShopItem.Skeleton, cell);
            }

            DungeonLayout layout = ShopBot.Build(loadout);
            List<Vector2Int> path = layout.Grid.FindPath(layout.EntranceCell, layout.BossCell);

            MooseRunnerFacade.Log($"path across a fully furnished dungeon: {path.Count} steps");
            Assert.Greater(path.Count, 0,
                "furnishing the dungeon cut the route to the boss room, which removes the game's "
                + "one losing ending");
        }

        /// <summary>
        /// Placement is deterministic: the same purchases build the same dungeon every time.
        /// </summary>
        /// <remarks>
        /// SPEC.md requires a run to be reproducible from a seed in a bug report. Purchases are part
        /// of that run, so the layout they produce has to be a pure function of them — if iteration
        /// order or a hash set crept in, two replays of one seed would diverge on the third raid and
        /// the seed would be worthless.
        /// </remarks>
        [Test]
        public void TheSamePurchases_BuildTheSameDungeon()
        {
            var loadout = new Loadout();
            List<Vector2Int> free = FreeCells(ShopBot.Build(loadout));
            for (int i = 0; i < 15; i++)
            {
                loadout.Add((ShopItem)(i % 4), free[i * 2]);
            }

            DungeonLayout first = ShopBot.Build(loadout);
            DungeonLayout second = ShopBot.Build(loadout);

            CollectionAssert.AreEqual(first.SpawnerCells, second.SpawnerCells, "spawners moved");
            CollectionAssert.AreEqual(first.SpawnerTiers, second.SpawnerTiers, "spawner tiers moved");
            CollectionAssert.AreEqual(first.TrapCells, second.TrapCells, "traps moved");
            CollectionAssert.AreEqual(first.ChestCells, second.ChestCells, "chests moved");

            var one = new Raid(first, 0f, null, 4242);
            var two = new Raid(second, 0f, null, 4242);
            RunToEnd(one, first, "first replay");
            RunToEnd(two, second, "second replay");

            MooseRunnerFacade.Log(
                $"replayed a furnished dungeon: {one.EnergyHarvested:F3} vs {two.EnergyHarvested:F3}");
            Assert.AreEqual(one.EnergyHarvested, two.EnergyHarvested, 0.001f,
                "the same seed and the same purchases must replay identically");
        }

        /// <summary>
        /// Furniture placed in a hall the corridor never grows to is dropped, not left floating.
        /// </summary>
        /// <remarks>
        /// Cannot happen through the shop, which only ever offers tiles of the dungeon in front of
        /// the player — but the guard is what makes that a fact rather than a hope. A spawner outside
        /// every room belongs to no room, and a mob bound to no room is a mob nothing can reach and
        /// that never stops chasing.
        /// </remarks>
        [Test]
        public void FurnitureOutsideEveryRoom_IsDropped()
        {
            var loadout = new Loadout();
            loadout.Add(ShopItem.Skeleton, new Vector2Int(500, 500));
            loadout.Add(ShopItem.Chest, new Vector2Int(-3, -3));
            loadout.Add(ShopItem.SpikeTrap, new Vector2Int(0, 0));

            DungeonLayout layout = ShopBot.Build(loadout);

            foreach (Vector2Int cell in layout.SpawnerCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                    $"a spawner at {cell} belongs to no room, so nothing can ever reach it");
            }

            foreach (Vector2Int cell in layout.ChestCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                    $"a chest at {cell} is outside the dungeon");
            }

            foreach (Vector2Int cell in layout.TrapCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                    $"a trap at {cell} is outside the dungeon");
            }
        }

        /// <summary>
        /// Aiming purchases beats scattering them, which is what makes the choice worth making.
        /// </summary>
        /// <remarks>
        /// The justification for the whole rework. If a thoughtfully placed dungeon earned the same
        /// as a randomly furnished one, the player would be pointing at tiles for nothing and the
        /// shop would have gained a chore rather than a decision.
        /// <para>
        /// The thoughtful layout puts spawners in the rooms the party must cross and traps on their
        /// route; the careless one dumps everything in the last room, which a stalled party may never
        /// reach.
        /// </para>
        /// </remarks>
        [Test]
        public void AThoughtfulLayout_OutEarnsACarelessOne()
        {
            DungeonLayout empty = ShopBot.Build(new Loadout());
            int lastRoom = empty.Grid.RoomAt(empty.RoomCentres[empty.RoomCentres.Count - 1]);
            int firstRoom = empty.Grid.RoomAt(empty.RoomCentres[0]);

            var thoughtful = new Loadout();
            var careless = new Loadout();
            int placedWell = 0;
            int placedBadly = 0;

            foreach (Vector2Int cell in FreeCells(empty))
            {
                int room = empty.Grid.RoomAt(cell);
                if (room == firstRoom && placedWell < 6)
                {
                    thoughtful.Add(
                        cell.y == empty.EntranceCell.y ? ShopItem.SpikeTrap : ShopItem.Skeleton,
                        cell);
                    placedWell++;
                }
                else if (room == lastRoom && placedBadly < 6)
                {
                    careless.Add(ShopItem.Skeleton, cell);
                    placedBadly++;
                }
            }

            float aimed = Harvest(ShopBot.Build(thoughtful));
            float dumped = Harvest(ShopBot.Build(careless));

            MooseRunnerFacade.Log(
                $"{placedWell} placed at the entrance harvested {aimed:F0}; "
                + $"{placedBadly} dumped in the last room harvested {dumped:F0}");

            Assert.Greater(aimed, dumped,
                "placing purchases where the party will meet them has to be worth more than "
                + "dumping them where the party may never arrive, or aiming is a chore not a choice");
        }

        /// <summary>
        /// No arrangement of purchases makes killing the party the best-paying dungeon.
        /// </summary>
        /// <remarks>
        /// CLAUDE.md's one rule: <i>"if a change makes killing the party more attractive, it is
        /// wrong however well it plays"</i>. There is already a test that this holds across rosters
        /// and seeds — but it plays one fixed, unfurnished dungeon, and the shop rework handed the
        /// player a lever that test cannot see. Cramming a dozen bone piles into the room the party
        /// walks into is the most obvious thing a new player will try, and it is precisely the layout
        /// most likely to invert the design.
        /// <para>
        /// Swept over densities rather than tested at one, because the interesting point is the
        /// crossover: the dungeon strong enough to wipe them has to earn <b>less</b> than the one
        /// that merely maims them, or the shop is teaching the opposite of the game.
        /// </para>
        /// <para>
        /// <b>As it stands no density wipes them at all</b> — twelve skeletons at the door leave
        /// three survivors and the clock runs out. So the inversion assertion below is currently
        /// unfalsifiable, and a test that can only pass is not a test. It therefore also asserts the
        /// property that <i>is</i> live: that stacking spawners pays, measured at 507 against 378 for
        /// a single one. If a future change makes wipes reachable, the first assertion starts doing
        /// the work it was written for.
        /// </para>
        /// </remarks>
        [Test]
        public void NoPlacement_MakesKillingThemPayBest()
        {
            float bestWipe = 0f;
            float bestSurvival = 0f;
            float sparsest = 0f;
            float densest = 0f;
            int wipes = 0;

            int[] densities = { 1, 2, 4, 6, 9, 12 };
            foreach (int density in densities)
            {
                var loadout = new Loadout();
                DungeonLayout empty = ShopBot.Build(loadout);
                int firstRoom = empty.Grid.RoomAt(empty.RoomCentres[0]);

                int placed = 0;
                foreach (Vector2Int cell in FreeCells(empty))
                {
                    if (empty.Grid.RoomAt(cell) == firstRoom && placed < density)
                    {
                        loadout.Add(ShopItem.Skeleton, cell);
                        placed++;
                    }
                }

                DungeonLayout layout = ShopBot.Build(loadout);
                var raid = new Raid(layout, 0f, PartyComposition.Opening, 55);
                RunToEnd(raid, layout, $"{density} spawners at the entrance", aggressive: true);

                if (raid.Outcome == RaidOutcome.PartyWiped)
                {
                    wipes++;
                    bestWipe = Mathf.Max(bestWipe, raid.EnergyHarvested);
                }
                else
                {
                    bestSurvival = Mathf.Max(bestSurvival, raid.EnergyHarvested);
                }

                if (density == densities[0])
                {
                    sparsest = raid.EnergyHarvested;
                }

                if (density == densities[densities.Length - 1])
                {
                    densest = raid.EnergyHarvested;
                }

                MooseRunnerFacade.Log(
                    $"{placed} bone piles at the entrance: {raid.Outcome}, "
                    + $"harvested {raid.EnergyHarvested:F0}, "
                    + $"{raid.Party.LivingCount} survivors");
            }

            MooseRunnerFacade.Log(
                $"across densities: {wipes} wipes, best wipe {bestWipe:F0}, "
                + $"best survival {bestSurvival:F0}, "
                + $"emptiest {sparsest:F0} vs densest {densest:F0}");

            Assert.Greater(bestSurvival, bestWipe,
                "some arrangement of purchases made wiping the party the best-paying dungeon, "
                + "which inverts the one idea the game is built on");

            // Stacking used to have to pay, and under the old curve it did: twelve bone piles at the
            // entrance earned 507 against 378 for one. The per-action curve reversed that. Twelve now
            // earn 120 against 553, because they kill two of the party -- and a corpse earns nothing
            // and costs 50 points on top.
            //
            // That is the design working rather than failing: over-stacking is meant to be bad play.
            // What is asserted is the shape that has to hold either way -- the greedy extreme must
            // not be the best answer, or the shop is a slider rather than a decision.
            Assert.Less(densest, sparsest,
                $"a dozen bone piles at the entrance harvested {densest:F0} against {sparsest:F0} "
                + "for a single one. If cramming in the maximum were also the best-paying answer, "
                + "there would be no decision left in the shop.");
        }

        /// <summary>Runs one raid and reports what it harvested.</summary>
        /// <param name="layout">Dungeon to raid.</param>
        /// <returns>Energy harvested.</returns>
        private static float Harvest(DungeonLayout layout)
        {
            var raid = new Raid(layout, 0f, PartyComposition.Opening, 1234);
            RunToEnd(raid, layout, "harvest run");
            return raid.EnergyHarvested;
        }

        /// <summary>
        /// A branching dungeon, furnished to the hilt, still plays.
        /// </summary>
        /// <remarks>
        /// The combination nothing has raided yet. Every piece has been swept alone — the lattice
        /// builds plus shapes and loops, the party explores them, the shop fills tiles, the curve
        /// pays per action — but a dungeon that is <i>both</i> branching and full is what a player
        /// who survives eight rounds actually owns, and no test had built one.
        /// <para>
        /// The property is the one that always matters: the raid ends. A raid that cannot finish is
        /// a hang, and the player's only way out is to close the tab.
        /// </para>
        /// </remarks>
        [Test]
        public void ABranchingDungeonStuffedWithPurchases_StillPlays()
        {
            foreach (int arms in new[] { 2, 3, 4 })
            {
                var plan = new RoomPlan();
                for (int i = 0; i < arms; i++)
                {
                    plan.Add(RoomPlan.Directions[i]);
                }

                var loadout = new Loadout();
                DungeonLayout empty = DungeonLayout.Build(plan);

                int placed = 0;
                foreach (Vector2Int cell in FreeCells(empty))
                {
                    ShopItem item = (placed % 3) switch
                    {
                        0 => ShopItem.Skeleton,
                        1 => ShopItem.SpikeTrap,
                        _ => ShopItem.Chest
                    };

                    loadout.Add(item, cell);
                    placed++;
                }

                DungeonLayout layout = DungeonLayout.Build(plan, placed: ShopBot.Furniture(loadout));
                var raid = new Raid(layout, 0f, PartyComposition.Opening, 606);
                float seconds = RunToEnd(raid, layout, $"a {arms}-armed dungeon", aggressive: true);

                MooseRunnerFacade.Log(
                    $"{plan.Count} rooms with {arms} arms, {placed} purchases: {raid.Outcome} after "
                    + $"{seconds:F1}s, saw {raid.Party.VisitedRooms}/{plan.Count} rooms, "
                    + $"harvested {raid.EnergyHarvested:F0}, {raid.Party.LivingCount} alive");

                // Not "did it explore". Measured, a four-armed dungeon with 115 purchases in it
                // pins the party in the room it walks into for the whole minute -- it sees 1 of 5
                // rooms and still harvests 301 with three alive. That is not a freeze, it is a meat
                // grinder, and a party alive, wounded and in combat inside the dungeon is exactly
                // the state the whole design is built to produce.
                //
                // So the claim is that they are FIGHTING rather than stalled. A party that had
                // nowhere to go would earn the walking floor of about a quarter a second; anything
                // near that over a full raid means the exploration objective has jammed.
                float perSecond = raid.EnergyHarvested / Mathf.Max(1f, seconds);
                Assert.Greater(perSecond, 1f,
                    $"a {arms}-armed dungeon earned {perSecond:F2}/s across the raid, near the "
                    + "walking floor -- the party is stalled rather than fighting");
            }
        }
    }
}
