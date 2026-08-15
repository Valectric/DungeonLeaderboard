using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using Dungeon.ShopManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Buys halls in the directions that move the whole dungeon, and checks nothing is left behind.
    /// </summary>
    /// <remarks>
    /// Growing a dungeon to the <b>right or up</b> is free: the lattice anchor does not move, every
    /// carved cell keeps its coordinates, and a spawner bought last round is still where it was.
    /// Growing <b>left or down</b> re-anchors the grid, so every cell in the dungeon shifts — and
    /// the player's purchases are stored as absolute cells. <c>GameController.BuyHallAt</c> knows
    /// this and translates the loadout by the same amount, which is exactly the kind of arithmetic
    /// that is right until it is not.
    /// <para>
    /// Nothing tested it. The existing growth coverage buys <c>Expansions()[0]</c>, which for a
    /// corridor is the room to the right — the one direction where the translation is a no-op. The
    /// failure it would miss is silent and total: furniture ends up in a different room, or in the
    /// rock where the builder drops it, and the player has paid for something that is not there.
    /// </para>
    /// </remarks>
    public sealed class GrowingLeftTests
    {
        /// <summary>The controller under test.</summary>
        private GameController _game;

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Builds a fresh controller, which starts a fresh run.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            _game = new GameObject("game").AddComponent<GameController>();
        }

        /// <summary>UI scale the controller draws the shop at.</summary>
        private static float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

        /// <summary>Screen point of a dungeon cell, in input space.</summary>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>A point on that cell.</returns>
        private static Vector2 TilePoint(Vector2Int cell)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>The same point in GUI space, which the shop lays its controls out from.</summary>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>The anchor for a marker or popup.</returns>
        private static Vector2 GuiPoint(Vector2Int cell)
        {
            Vector2 point = TilePoint(cell);
            return new Vector2(point.x, Screen.height - point.y);
        }

        /// <summary>Taps the marker for a lattice cell, buying a hall there.</summary>
        /// <param name="lattice">Lattice cell to build on.</param>
        private void BuyHallAt(Vector2Int lattice)
        {
            Rect marker = ShopScreen.HallMarkerRect(
                GuiPoint(_game.CurrentRaid.Layout.CentreOfLattice(lattice)),
                Scale, Screen.width, Screen.height);
            _game.TapShop(new Vector2(marker.center.x, Screen.height - marker.center.y));
        }

        /// <summary>
        /// A hall bought to the left leaves every purchase in the room it was bought for.
        /// </summary>
        /// <remarks>
        /// Checked by <i>room membership</i> rather than by coordinates, because the coordinates are
        /// supposed to change — that is the whole point. What must not change is which room a
        /// spawner is standing in, and that it is standing in one at all.
        /// </remarks>
        [Test]
        public async UniTask GrowingLeft_KeepsEveryPurchaseInItsRoom(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            DungeonLayout before = _game.CurrentRaid.Layout;
            int spawnersBefore = before.SpawnerCells.Count;
            int chestsBefore = before.ChestCells.Count;

            var roomsBefore = new List<int>();
            foreach (Vector2Int cell in before.SpawnerCells)
            {
                roomsBefore.Add(before.Grid.RoomAt(cell));
            }

            Vector2Int left = new(-1, 0);
            Assert.IsTrue(before.Plan.CanAdd(left),
                "the opening dungeon should be able to grow to its left");

            BuyHallAt(left);
            await UniTask.Yield(ct);

            DungeonLayout after = _game.CurrentRaid.Layout;

            MooseRunnerFacade.Log(
                $"grew left: anchor {before.LatticeAnchor} -> {after.LatticeAnchor}, "
                + $"rooms {before.RoomCentres.Count} -> {after.RoomCentres.Count}, "
                + $"spawners {spawnersBefore} -> {after.SpawnerCells.Count}, "
                + $"chests {chestsBefore} -> {after.ChestCells.Count}");

            Assert.AreNotEqual(before.LatticeAnchor, after.LatticeAnchor,
                "growing left should have moved the lattice anchor, or this tests nothing");
            Assert.AreEqual(before.RoomCentres.Count + 1, after.RoomCentres.Count,
                "the hall was not built");

            Assert.AreEqual(spawnersBefore, after.SpawnerCells.Count,
                "a spawner the player had paid for was dropped when the dungeon moved");
            Assert.AreEqual(chestsBefore, after.ChestCells.Count,
                "a chest the player had paid for was dropped when the dungeon moved");

            foreach (Vector2Int cell in after.SpawnerCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, after.Grid.RoomAt(cell),
                    $"a spawner ended up at {cell}, which belongs to no room -- it is in the rock");
            }

            foreach (Vector2Int cell in after.ChestCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, after.Grid.RoomAt(cell),
                    $"a chest ended up at {cell}, which belongs to no room");
            }
        }

        /// <summary>
        /// Growing down, which moves the dungeon on the other axis, does the same.
        /// </summary>
        /// <remarks>
        /// Separate from the left case on purpose. The translation is two-dimensional and a sign
        /// error on one axis is invisible to a test that only ever moves along the other.
        /// </remarks>
        [Test]
        public async UniTask GrowingDown_KeepsEveryPurchaseInItsRoom(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            DungeonLayout before = _game.CurrentRaid.Layout;
            int spawnersBefore = before.SpawnerCells.Count;

            var down = new Vector2Int(0, -1);
            Assert.IsTrue(before.Plan.CanAdd(down), "the dungeon should be able to grow downward");

            BuyHallAt(down);
            await UniTask.Yield(ct);

            DungeonLayout after = _game.CurrentRaid.Layout;

            MooseRunnerFacade.Log(
                $"grew down: anchor {before.LatticeAnchor} -> {after.LatticeAnchor}, "
                + $"spawners {spawnersBefore} -> {after.SpawnerCells.Count}");

            Assert.AreEqual(before.RoomCentres.Count + 1, after.RoomCentres.Count,
                "the hall was not built");
            Assert.AreEqual(spawnersBefore, after.SpawnerCells.Count,
                "a spawner was dropped when the dungeon moved downward");

            foreach (Vector2Int cell in after.SpawnerCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, after.Grid.RoomAt(cell),
                    $"a spawner ended up at {cell}, which belongs to no room");
            }
        }

        /// <summary>
        /// Growing in every direction in turn still leaves a playable dungeon.
        /// </summary>
        /// <remarks>
        /// The compounding case: each purchase re-anchors the grid again, so the translations stack.
        /// Asserted on the thing that actually matters — the party can still walk from the entrance
        /// to the deepest cell — because a dungeon whose rooms have drifted apart is not merely
        /// untidy, it is unfinishable, and the raid would end on a clock instead of a decision.
        /// </remarks>
        [Test]
        public async UniTask GrowingEveryWay_LeavesTheDungeonWalkable(CancellationToken ct)
        {
            _game.OpenShopWith(9000f);
            await UniTask.Yield(ct);

            var directions = new[]
            {
                new Vector2Int(-1, 0), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(0, 1)
            };

            int bought = 0;
            foreach (Vector2Int direction in directions)
            {
                DungeonLayout layout = _game.CurrentRaid.Layout;
                foreach (Vector2Int lattice in layout.Plan.Expansions())
                {
                    if (lattice != direction)
                    {
                        continue;
                    }

                    int rooms = layout.RoomCentres.Count;
                    BuyHallAt(lattice);
                    await UniTask.Yield(ct);

                    if (_game.CurrentRaid.Layout.RoomCentres.Count > rooms)
                    {
                        bought++;
                    }

                    break;
                }
            }

            DungeonLayout grown = _game.CurrentRaid.Layout;
            int route = grown.Grid.FindPath(grown.EntranceCell, grown.BossCell).Count;

            MooseRunnerFacade.Log(
                $"bought {bought} halls in different directions: {grown.RoomCentres.Count} rooms, "
                + $"grid {grown.Grid.Width}x{grown.Grid.Height}, entrance-to-deepest route {route}");

            Assert.Greater(bought, 1, "the test never managed to grow in more than one direction");
            Assert.Greater(route, 0,
                "after growing in several directions there is no route from the entrance to the "
                + "deepest cell, so the raid cannot be finished at all");

            foreach (Vector2Int cell in grown.SpawnerCells)
            {
                Assert.AreNotEqual(DungeonGrid.NoRoom, grown.Grid.RoomAt(cell),
                    $"a spawner ended up at {cell} after several moves, which belongs to no room");
            }
        }
    }
}
