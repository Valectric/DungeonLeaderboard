using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.DungeonManager.Tests
{
    /// <summary>
    /// Checks the invariants every route out of <c>DungeonGrid.FindPath</c> has to hold.
    /// </summary>
    /// <remarks>
    /// <b>Written because this module had no tests of its own.</b> Pathfinding is asked a question
    /// by almost everything in the game — the door search, the party's objective, mob pursuit, the
    /// retreat valve — and every existing test of it drives a whole <c>Raid</c> and reads the
    /// answer off the party's behaviour. That means a routing bug arrives disguised: as adventurers
    /// standing still, or as a stall the retreat valve cannot clear, several layers from the
    /// arithmetic that caused it.
    /// <para>
    /// These ask the grid directly, and they assert <i>properties of a route</i> rather than a
    /// specific list of cells. A path is only useful if you can walk it, so the two that matter are
    /// that consecutive steps are adjacent and that every step is somewhere a body can stand. A
    /// test naming exact cells would instead pin one implementation of the search and fail the
    /// first time somebody changed a tie-break.
    /// </para>
    /// </remarks>
    public sealed class PathfindingTests
    {
        /// <summary>
        /// Two rooms side by side with a single doorway between them.
        /// </summary>
        /// <remarks>
        /// The smallest shape that can express the question the game actually asks: can the party
        /// get from where it is to the next room, and does shutting one door change the answer.
        /// </remarks>
        /// <param name="doorOpen">Whether the connecting door starts open.</param>
        /// <param name="door">The connecting door.</param>
        /// <returns>The grid.</returns>
        private static DungeonGrid TwoRooms(bool doorOpen, out Door door)
        {
            var grid = new DungeonGrid(20, 10);
            grid.CarveRoom(new RectInt(1, 1, 7, 7), 0);
            grid.CarveRoom(new RectInt(9, 1, 7, 7), 1);

            // The rooms are left touching so the door cell at x=8 is the ONLY join. An earlier
            // version of this fixture padded them apart and carved the gap with CarveOpening, which
            // silently produced two sealed rooms -- see ACarvedOpening_IsSceneryNotAWayThrough. Every
            // route assertion then passed against an empty list, which is the shape of a test that
            // agrees with itself and checks nothing.
            door = grid.AddDoor(new Vector2Int(8, 4), 0, 1, doorOpen);
            return grid;
        }

        /// <summary>
        /// Every step of a route is next to the one before it.
        /// </summary>
        /// <remarks>
        /// The property that makes a route walkable at all. A search that returned the right cells
        /// in the wrong order, or that skipped one, would still satisfy "starts here, ends there"
        /// and would teleport an adventurer a cell at a time.
        /// </remarks>
        [Test]
        public void EveryStep_IsAdjacentToTheLast()
        {
            DungeonGrid grid = TwoRooms(doorOpen: true, out _);
            var from = new Vector2Int(2, 2);
            List<Vector2Int> route = grid.FindPath(from, new Vector2Int(14, 6));

            Assert.Greater(route.Count, 0, "the rooms are connected by an open door, so a route exists");

            Vector2Int previous = from;
            foreach (Vector2Int step in route)
            {
                int distance = Mathf.Abs(step.x - previous.x) + Mathf.Abs(step.y - previous.y);
                Assert.AreEqual(1, distance,
                    $"the route jumps from {previous} to {step}, which is {distance} cells apart -- "
                    + "an adventurer walking it would cross whatever is in between");
                previous = step;
            }

            MooseRunnerFacade.Log($"route of {route.Count} steps, every step adjacent");
        }

        /// <summary>
        /// No step of a route stands in rock.
        /// </summary>
        /// <remarks>
        /// The other half of walkable. Worth asserting here rather than at raid level, where the
        /// standing measurement is "member-ticks inside rock" and sits at 2.6-3.1% — that figure is
        /// the formation fanning followers into walls, not the route, and the two would be
        /// indistinguishable without a check that separates them.
        /// </remarks>
        [Test]
        public void NoStep_StandsInRock()
        {
            DungeonGrid grid = TwoRooms(doorOpen: true, out _);
            List<Vector2Int> route = grid.FindPath(new Vector2Int(2, 2), new Vector2Int(14, 6));

            foreach (Vector2Int step in route)
            {
                Assert.AreNotEqual(CellKind.Wall, grid.KindAt(step),
                    $"the route walks through the wall at {step}");
                Assert.IsTrue(grid.IsWalkable(step),
                    $"the route includes {step}, which is not walkable");
            }

            MooseRunnerFacade.Log($"{route.Count} steps, all walkable");
        }

        /// <summary>
        /// A shut door severs the route, and opening it restores one.
        /// </summary>
        /// <remarks>
        /// The single fact the whole game is built on top of: the player's only safety valve is
        /// opening a door behind a losing party, and the door search finds its target by asking
        /// whether a route exists at all. If a shut door did not sever the path, none of that works.
        /// </remarks>
        [Test]
        public void AShutDoor_SeversTheRoute()
        {
            DungeonGrid grid = TwoRooms(doorOpen: false, out Door door);
            var from = new Vector2Int(2, 2);
            var to = new Vector2Int(14, 6);

            Assert.AreEqual(0, grid.FindPath(from, to).Count,
                "the only way between the rooms is shut, so there should be no route");

            door.IsOpen = true;

            Assert.Greater(grid.FindPath(from, to).Count, 0,
                "opening the door should restore the route -- this is the retreat valve");

            MooseRunnerFacade.Log("shut: no route; open: route");
        }

        /// <summary>
        /// A route asked for the cell already stood on is empty rather than a single step.
        /// </summary>
        /// <remarks>
        /// The caller reads <c>Count == 0</c> as "nowhere to go", so returning the current cell
        /// would read as a step and make an adventurer walk on the spot.
        /// </remarks>
        [Test]
        public void ARouteToWhereYouStand_IsEmpty()
        {
            DungeonGrid grid = TwoRooms(doorOpen: true, out _);
            var here = new Vector2Int(3, 3);

            Assert.AreEqual(0, grid.FindPath(here, here).Count,
                "a route to the cell already occupied should be empty");
        }

        /// <summary>
        /// A route around an armed trap avoids it when it can.
        /// </summary>
        /// <remarks>
        /// The avoidance is a preference, not a rule — see the remark on the overload. This checks
        /// the preference is honoured when honouring it is possible, which is the case the player
        /// sees: a trap in an open room the party simply walks around.
        /// </remarks>
        [Test]
        public void ARoute_AvoidsAnArmedTrapWhenItCan()
        {
            DungeonGrid grid = TwoRooms(doorOpen: true, out _);
            var trap = new Vector2Int(4, 4);

            List<Vector2Int> route =
                grid.FindPath(new Vector2Int(2, 4), new Vector2Int(7, 4), new[] { trap });

            Assert.Greater(route.Count, 0, "there is plenty of room to walk around one cell");
            CollectionAssert.DoesNotContain(route, trap,
                "the room is wide open, so the route had no need to cross the trap");

            MooseRunnerFacade.Log($"avoided {trap} in {route.Count} steps");
        }

        /// <summary>
        /// A route through a corridor of armed traps is walked anyway.
        /// </summary>
        /// <remarks>
        /// The documented fallback, and the more important half: when no route avoids the traps the
        /// search is retried without the restriction, "because an adventurer walking through a trap
        /// is far better behaviour than one standing still forever". A party frozen by its own
        /// caution earns the idle floor, which is the failure this game can least afford.
        /// </remarks>
        [Test]
        public void ARoute_CrossesTrapsRatherThanGiveUp()
        {
            DungeonGrid grid = TwoRooms(doorOpen: true, out _);

            // The doorway is one cell wide, so trapping it leaves no alternative at all.
            var traps = new[] { new Vector2Int(8, 4) };

            List<Vector2Int> route =
                grid.FindPath(new Vector2Int(2, 4), new Vector2Int(14, 4), traps);

            Assert.Greater(route.Count, 0,
                "the only way through is trapped, and standing still forever is worse -- the "
                + "search should retry without the restriction rather than return nothing");

            MooseRunnerFacade.Log($"crossed the trapped doorway in {route.Count} steps");
        }

        /// <summary>
        /// A route to somewhere solid is empty rather than a route to somewhere near it.
        /// </summary>
        /// <remarks>
        /// Callers act on the emptiness — the door search reads "no route" as its cue to look for a
        /// door instead. A search that quietly returned a best-effort route to an adjacent cell
        /// would make an unreachable objective look reachable, which is the shape of the raid-long
        /// freeze the door search's fallback exists to prevent.
        /// </remarks>
        [Test]
        public void ARouteIntoRock_IsEmpty()
        {
            DungeonGrid grid = TwoRooms(doorOpen: true, out _);
            var solid = new Vector2Int(0, 0);

            Assert.AreEqual(CellKind.Wall, grid.KindAt(solid), "the fixture's corner should be rock");
            Assert.AreEqual(0, grid.FindPath(new Vector2Int(2, 2), solid).Count,
                "there is no route to a cell nothing can stand in");
        }

        /// <summary>
        /// A carved opening is scenery, not a way through.
        /// </summary>
        /// <remarks>
        /// <b>This is the test that made the module worth testing, and it is the opposite of what
        /// it first asserted.</b> <c>CarveOpening</c>'s remark claimed the cell it carves is
        /// "walkable", so the first two-room fixture here used two openings as its join and got two
        /// sealed rooms — every route assertion in this class passed against an empty list.
        /// <para>
        /// The docstring was the defect, not the behaviour. An opening is deliberately impassable:
        /// the only one the game carves sits a cell outside the first room, on the boundary of the
        /// grid, so a walkable one would let a monster leave the dungeon and a retreating adventurer
        /// walk off the map. <c>EntranceOpeningTests</c> says exactly that, by name, and it is the
        /// test that caught the "fix" attempted here first.
        /// </para>
        /// <para>
        /// Stated again at this level because the raid-level test proves it of the <i>entrance</i>,
        /// through a whole <c>DungeonLayout</c>. This proves it of the <i>primitive</i>, which is
        /// where the misleading documentation was.
        /// </para>
        /// </remarks>
        [Test]
        public void ACarvedOpening_IsSceneryNotAWayThrough()
        {
            var grid = new DungeonGrid(12, 8);
            grid.CarveRoom(new RectInt(1, 1, 4, 6), 0);
            grid.CarveRoom(new RectInt(6, 1, 5, 6), 1);
            var opening = new Vector2Int(5, 3);
            grid.CarveOpening(opening);

            Assert.IsNull(grid.DoorAt(opening), "an opening has no door -- that is what makes it one");
            Assert.IsFalse(grid.IsWalkable(opening),
                "an opening is a hole in a wall to look through, not a threshold to cross");

            Assert.AreEqual(0, grid.FindPath(new Vector2Int(2, 3), new Vector2Int(9, 3)).Count,
                "an opening joins nothing, so two rooms separated by one are sealed from each "
                + "other -- use AddDoor to join rooms");

            MooseRunnerFacade.Log($"opening {opening}: kind={grid.KindAt(opening)}, walkable=false");
        }

        /// <summary>
        /// Line of sight is blocked by rock and clear along an open room.
        /// </summary>
        /// <remarks>
        /// Used to decide whether a ranged adventurer can shoot, which is worth two-thirds of a
        /// melee attacker's earning rate — so a sight test that answered "yes" through a wall would
        /// pay the player for a shot that should not exist.
        /// </remarks>
        [Test]
        public void LineOfSight_StopsAtRock()
        {
            DungeonGrid grid = TwoRooms(doorOpen: false, out _);

            Assert.IsTrue(grid.HasLineOfSight(new Vector2Int(2, 4), new Vector2Int(6, 4)),
                "both cells are floor in the same open room");
            Assert.IsFalse(grid.HasLineOfSight(new Vector2Int(2, 4), new Vector2Int(14, 4)),
                "the rooms are separated by rock and a shut doorway");
        }
    }
}
