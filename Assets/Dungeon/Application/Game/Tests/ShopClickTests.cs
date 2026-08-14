using System.Collections.Generic;
using System.Linq;
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
    /// Drives the spatial shop through real screen coordinates rather than through its model.
    /// </summary>
    /// <remarks>
    /// The whole reason this file exists: a shipped build once had all three raid verbs dead while
    /// every test was green, because the tests called <c>raid.SpawnMob(...)</c> instead of clicking.
    /// The shop is now clicked <i>on the dungeon itself</i>, which multiplies the ways a control can
    /// be drawn in one place and hit-tested in another — a tile, a popup row anchored to that tile,
    /// and a marker floating past the end of the corridor. Each is pressed here at the coordinate the
    /// drawing code puts it at, so the disagreement fails here instead of on itch.io.
    /// </remarks>
    public sealed class ShopClickTests
    {
        private GameController _game;

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Rebuilds a controller in a shop phase before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            _game = new GameObject("game").AddComponent<GameController>();
        }

        /// <summary>UI scale the controller uses.</summary>
        private static float Scale => Screen.height / 720f;

        /// <summary>Screen point of a dungeon cell, in input space.</summary>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>A screen point on that cell.</returns>
        private static Vector2 TilePoint(Vector2Int cell)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>The same point in GUI space, which the drawing code measures from the top.</summary>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>The anchor the popup and marker are laid out from.</returns>
        private static Vector2 GuiPoint(Vector2Int cell)
        {
            Vector2 point = TilePoint(cell);
            return new Vector2(point.x, Screen.height - point.y);
        }

        /// <summary>Screen point of one row of the menu opened on a cell.</summary>
        /// <param name="cell">Cell the menu is open on.</param>
        /// <param name="item">Item whose row is wanted.</param>
        /// <returns>A screen point inside that row.</returns>
        private static Vector2 PopupRowPoint(Vector2Int cell, ShopItem item)
        {
            Rect[] rows = ShopScreen.PopupRows(
                GuiPoint(cell), Scale, Screen.width, Screen.height);

            for (int i = 0; i < ShopScreen.Items.Length; i++)
            {
                if (ShopScreen.Items[i] == item)
                {
                    return new Vector2(rows[i].center.x, Screen.height - rows[i].center.y);
                }
            }

            return Vector2.zero;
        }

        /// <summary>Screen point of a marker that buys another hall.</summary>
        /// <param name="index">Which of the offered directions to press.</param>
        /// <returns>A screen point inside that marker.</returns>
        private Vector2 HallMarkerPoint(int index = 0)
        {
            List<Vector2Int> offered = _game.CurrentRaid.Layout.Plan.Expansions();
            Vector2Int lattice = offered[Mathf.Clamp(index, 0, offered.Count - 1)];
            Rect rect = ShopScreen.HallMarkerRect(
                GuiPoint(_game.CurrentRaid.Layout.CentreOfLattice(lattice)),
                Scale, Screen.width, Screen.height);
            return new Vector2(rect.center.x, Screen.height - rect.center.y);
        }

        /// <summary>Centre of the Ready button, in input space.</summary>
        /// <returns>A screen point inside the Ready button.</returns>
        private static Vector2 ReadyPoint()
        {
            Rect ready = ShopScreen.ReadyRect(Scale, Screen.width, Screen.height);
            return new Vector2(ready.center.x, Screen.height - ready.center.y);
        }

        /// <summary>Finds an empty tile the player is allowed to build on.</summary>
        /// <param name="skip">How many candidates to pass over, for tests wanting several.</param>
        /// <returns>A buildable cell.</returns>
        private Vector2Int BuildableCell(int skip = 0)
        {
            DungeonLayout layout = _game.CurrentRaid.Layout;
            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!layout.CanBuildOn(cell))
                    {
                        continue;
                    }

                    if (skip-- > 0)
                    {
                        continue;
                    }

                    return cell;
                }
            }

            Assert.Fail("the dungeon offered nowhere to build");
            return default;
        }

        /// <summary>Tapping a tile with an empty purse opens the menu but buys nothing.</summary>
        [Test]
        public async UniTask TappingATile_WithNoMoney_BuysNothing(CancellationToken ct)
        {
            _game.OpenShop();
            await UniTask.Yield(ct);

            Assert.IsTrue(_game.IsShopping, "the controller should be in the shop");

            Vector2Int cell = BuildableCell();
            _game.TapShop(TilePoint(cell));
            _game.TapShop(PopupRowPoint(cell, ShopItem.Chest));

            Assert.AreEqual(0, _game.Loadout.Count(ShopItem.Chest),
                "the shop opened with an empty purse, so the tap should have bought nothing");
        }

        /// <summary>
        /// Every placeable item can be bought onto a tile, and lands on the tile it was bought for.
        /// </summary>
        /// <remarks>
        /// The assertion that matters is <i>where</i>. Selling a count and scattering it by formula
        /// is what this rework replaced, so a purchase that appears somewhere other than the tapped
        /// cell is the whole feature failing quietly.
        /// </remarks>
        [Test]
        public async UniTask EveryItem_BuysOntoTheTappedTile(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            var placed = new List<(ShopItem item, Vector2Int cell)>();
            for (int i = 0; i < ShopScreen.Items.Length; i++)
            {
                ShopItem item = ShopScreen.Items[i];
                Vector2Int cell = BuildableCell();

                _game.TapShop(TilePoint(cell));
                _game.TapShop(PopupRowPoint(cell, item));

                MooseRunnerFacade.Log($"bought {item} onto {cell}");
                Assert.AreEqual(1, _game.Loadout.Count(item),
                    $"tapping the {item} row should have bought exactly one {item}");
                placed.Add((item, cell));
            }

            Assert.AreEqual(ShopScreen.Items.Length, _game.Loadout.Total,
                "one purchase per placeable item");

            DungeonLayout layout = _game.CurrentRaid.Layout;
            foreach ((ShopItem item, Vector2Int cell) in placed)
            {
                bool present = item switch
                {
                    ShopItem.Chest => layout.ChestCells.Contains(cell),
                    ShopItem.SpikeTrap or ShopItem.PoisonDart => layout.TrapCells.Contains(cell),
                    _ => layout.SpawnerCells.Contains(cell)
                };

                Assert.IsTrue(present, $"the {item} bought on {cell} is not standing there");
            }
        }

        /// <summary>A slime pit bought on a tile spawns slimes, not skeletons.</summary>
        /// <remarks>
        /// The two spawners cost different money and hold a party for very different lengths of time.
        /// Placement now carries the tier, so getting it wrong would sell the expensive one and build
        /// the cheap one — invisible in the loadout, and only noticeable mid-raid.
        /// </remarks>
        [Test]
        public async UniTask ASlimePitBoughtOnATile_IsASlimePit(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            Vector2Int cell = BuildableCell();
            _game.TapShop(TilePoint(cell));
            _game.TapShop(PopupRowPoint(cell, ShopItem.Slime));

            Assert.AreEqual(0, _game.CurrentRaid.Layout.SpawnerTierAt(cell),
                "a slime pit must spawn slimes");
        }

        /// <summary>Tapping the hall marker extends the corridor by a room.</summary>
        [Test]
        public async UniTask TappingTheHallMarker_AddsARoom(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            int before = _game.CurrentRaid.Layout.RoomCentres.Count;
            _game.TapShop(HallMarkerPoint());

            Assert.AreEqual(before + 1, _game.CurrentRaid.Layout.RoomCentres.Count,
                "buying the section should have extended the dungeon");
            Assert.AreEqual(1, _game.Loadout.Count(ShopItem.Door), "and charged for one hall");
        }

        /// <summary>
        /// The corridor stops offering halls once it has reached its cap.
        /// </summary>
        /// <remarks>
        /// A corridor that keeps growing eventually cannot be crossed in sixty seconds, at which
        /// point buying another hall stops being a purchase and becomes a guarantee. Offering a
        /// marker that takes money and changes nothing would be worse than not offering one.
        /// </remarks>
        [Test]
        public async UniTask TheHallMarker_StopsAtTheCap(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            for (int i = 0; i < 6; i++)
            {
                _game.TapShop(HallMarkerPoint());
            }

            Assert.AreEqual(5, _game.CurrentRaid.Layout.RoomCentres.Count,
                "the corridor must stop at its cap");
            Assert.AreEqual(2, _game.Loadout.Count(ShopItem.Door),
                "and must not keep charging for halls it will not build");
        }

        /// <summary>Two purchases cannot be stacked on one tile.</summary>
        [Test]
        public async UniTask ATileTakesOneThing(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            Vector2Int cell = BuildableCell();
            _game.TapShop(TilePoint(cell));
            _game.TapShop(PopupRowPoint(cell, ShopItem.Chest));

            Assert.IsFalse(_game.CurrentRaid.Layout.CanBuildOn(cell),
                "an occupied tile must stop offering to be built on");

            float purse = _game.CurrentShop.Purse;
            _game.TapShop(TilePoint(cell));
            _game.TapShop(PopupRowPoint(cell, ShopItem.Slime));

            Assert.AreEqual(purse, _game.CurrentShop.Purse, 0.01f,
                "tapping a full tile must not spend anything");
        }

        /// <summary>Tapping away from an open menu dismisses it rather than buying.</summary>
        /// <remarks>
        /// Backing out has to be as cheap as opening, or every mis-tap costs energy the player was
        /// saving for something else — and with a thirty-second clock there is no time to regret it.
        /// </remarks>
        [Test]
        public async UniTask TappingAwayFromTheMenu_BuysNothing(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            Vector2Int cell = BuildableCell();
            _game.TapShop(TilePoint(cell));
            _game.TapShop(new Vector2(2f, Screen.height - 4f));

            Assert.AreEqual(0, _game.Loadout.Total, "dismissing must not spend the player's energy");
        }

        /// <summary>Tapping solid rock buys nothing and opens nothing.</summary>
        [Test]
        public async UniTask TappingOutsideTheDungeon_BuysNothing(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            _game.TapShop(TilePoint(new Vector2Int(-8, -8)));
            _game.TapShop(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

            Assert.AreEqual(0, _game.Loadout.Total,
                "a stray tap must not spend the player's energy");
        }

        /// <summary>Ready closes the shop and starts the raid it paid for.</summary>
        [Test]
        public async UniTask TappingReady_ClosesTheShopAndStartsTheRaid(CancellationToken ct)
        {
            _game.OpenShopWith(500f);
            await UniTask.Yield(ct);

            _game.TapShop(ReadyPoint());
            Assert.IsFalse(_game.CurrentShop.IsOpen, "Ready should shut the shop");

            // The controller starts the raid on its next frame, once it notices the shop closed.
            await UniTask.Yield(ct);
            await UniTask.Yield(ct);

            Assert.IsFalse(_game.IsShopping, "the party should be coming in");
            Assert.IsTrue(_game.CurrentRaid.IsRunning, "and the raid should be running");
        }

        /// <summary>
        /// The energy Ready pays for is spendable in the raid that follows.
        /// </summary>
        /// <remarks>
        /// A bonus paid into the purse of a shop that is closing would be worth precisely nothing --
        /// the number would rise on screen and then be thrown away. This asserts it survives the
        /// phase change, which is the only thing that makes the button worth pressing.
        /// </remarks>
        [Test]
        public async UniTask ReadyBonus_IsSpendableInTheNextRaid(CancellationToken ct)
        {
            _game.OpenShopWith(0f);
            await UniTask.Yield(ct);

            float bonus = _game.CurrentShop.PendingBonus;
            Assert.Greater(bonus, 0f, "a fresh shop should offer a bonus for skipping it");

            _game.TapShop(ReadyPoint());
            await UniTask.Yield(ct);
            await UniTask.Yield(ct);

            Assert.GreaterOrEqual(_game.CurrentRaid.TotalEnergy,
                RaidManager.Raid.StartingEnergy + bonus - 1f,
                "the raid should have started richer by the bonus");
        }

        /// <summary>
        /// No decorative prop is ever drawn over something the player has to tap.
        /// </summary>
        /// <remarks>
        /// Props draw above spawners and traps, so a prop sharing a cell hides it completely. A
        /// bought slime pit landed on exactly a decoration spot and was invisible under a banner in
        /// the shipped build -- paid for, present in the layout, tappable, and impossible to see.
        /// Nothing in the model could catch that, because nothing in the model was wrong.
        /// </remarks>
        [Test]
        public async UniTask NoPropIsDrawnOverSomethingTappable(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            // Furnish a spread of tiles so fittings land in the decoration spots.
            for (int i = 0; i < ShopScreen.Items.Length; i++)
            {
                Vector2Int cell = BuildableCell(i * 3);
                _game.TapShop(TilePoint(cell));
                _game.TapShop(PopupRowPoint(cell, ShopScreen.Items[i]));
            }

            _game.TapShop(ReadyPoint());
            await UniTask.Yield(ct);
            await UniTask.Yield(ct);

            var tappable = new HashSet<Vector2Int>();
            DungeonLayout layout = _game.CurrentRaid.Layout;
            foreach (Vector2Int cell in layout.SpawnerCells) { tappable.Add(cell); }
            foreach (Vector2Int cell in layout.TrapCells) { tappable.Add(cell); }
            foreach (Vector2Int cell in layout.ChestCells) { tappable.Add(cell); }
            foreach (Door door in layout.Grid.Doors) { tappable.Add(door.Cell); }

            int checkedProps = 0;
            foreach (Transform child in _game.transform)
            {
                if (!child.name.StartsWith("prop_"))
                {
                    continue;
                }

                checkedProps++;
                Vector2Int cell = DungeonView.WorldToCell(child.position);
                Assert.IsFalse(tappable.Contains(cell),
                    $"prop {child.name} sits on {cell}, hiding something the player must tap");
            }

            MooseRunnerFacade.Log($"checked {checkedProps} props against {tappable.Count} tappables");
            Assert.Greater(checkedProps, 0, "the dungeon should have been decorated at all");
        }

        /// <summary>Anything bought in the shop is standing in the dungeon the next party enters.</summary>
        [Test]
        public async UniTask PurchasesAppearInTheNextDungeon(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            int spawnersBefore = _game.CurrentRaid.Layout.SpawnerCells.Count;

            Vector2Int spawnerCell = BuildableCell();
            _game.TapShop(TilePoint(spawnerCell));
            _game.TapShop(PopupRowPoint(spawnerCell, ShopItem.Skeleton));

            Vector2Int chestCell = BuildableCell();
            _game.TapShop(TilePoint(chestCell));
            _game.TapShop(PopupRowPoint(chestCell, ShopItem.Chest));

            _game.TapShop(ReadyPoint());
            await UniTask.Yield(ct);
            await UniTask.Yield(ct);

            Assert.AreEqual(spawnersBefore + 1, _game.CurrentRaid.Layout.SpawnerCells.Count,
                "the bone pile should be in the dungeon");
            Assert.IsTrue(_game.CurrentRaid.Layout.SpawnerCells.Contains(spawnerCell),
                "and on the tile it was bought for");
            Assert.IsTrue(_game.CurrentRaid.Layout.ChestCells.Contains(chestCell),
                "so should the chest");
        }

        /// <summary>
        /// The dungeon on screen changes the instant something is bought.
        /// </summary>
        /// <remarks>
        /// The point of a spatial shop is watching the dungeon grow under the purchases. If the new
        /// hall only appeared once the raid started, the player would be buying blind — which is the
        /// thing the rework set out to stop.
        /// </remarks>
        [Test]
        public async UniTask TheDungeonRedrawsAsItIsBought(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            int before = CountViews("tile_");
            _game.TapShop(HallMarkerPoint());
            await UniTask.Yield(ct);

            int after = CountViews("tile_");
            MooseRunnerFacade.Log($"drawn tiles {before} -> {after}");
            Assert.Greater(after, before, "the bought hall should already be on screen");
        }

        /// <summary>Counts drawn objects whose name starts with a prefix.</summary>
        /// <param name="prefix">Name prefix to match.</param>
        /// <returns>How many are currently drawn.</returns>
        private int CountViews(string prefix)
        {
            int count = 0;
            foreach (Transform child in _game.transform)
            {
                if (child.name.StartsWith(prefix))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The dungeon can be grown in more than one direction, not just along the corridor.
        /// </summary>
        /// <remarks>
        /// The shop used to draw a single marker past the right-hand end, because that was the only
        /// place a room could go. A dungeon is a lattice now, so every room that is not boxed in
        /// offers its free sides — and pressing one of them has to build a room <i>there</i>.
        /// </remarks>
        [Test]
        public async UniTask TheShop_OffersMoreThanOneDirection(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            List<Vector2Int> offered = _game.CurrentRaid.Layout.Plan.Expansions();
            MooseRunnerFacade.Log(
                $"a three-room corridor offers {offered.Count} places to build a hall");

            Assert.Greater(offered.Count, 1,
                "the shop offered only one direction, so the dungeon still only grows one way");

            bool anyOffScript = false;
            foreach (Vector2Int lattice in offered)
            {
                anyOffScript |= lattice.y != 0;
            }

            Assert.IsTrue(anyOffScript,
                "every offer was on the corridor's own row, so nothing can be built above or below");
        }

        /// <summary>
        /// Growing the dungeon does not move furniture the player already placed.
        /// </summary>
        /// <remarks>
        /// The hazard the lattice introduced. Building to the left or below re-anchors the grid and
        /// every carved cell shifts with it — so a spawner bought at an absolute cell would end up in
        /// a different room, or in the rock where nothing can reach it. Placements are translated by
        /// the same amount the grid moved.
        /// </remarks>
        [Test]
        public async UniTask GrowingTheDungeon_LeavesPurchasesWhereTheyWere(CancellationToken ct)
        {
            _game.OpenShopWith(9000f);
            await UniTask.Yield(ct);

            Vector2Int cell = BuildableCell();
            _game.TapShop(TilePoint(cell));
            _game.TapShop(PopupRowPoint(cell, ShopItem.Skeleton));
            await UniTask.Yield(ct);

            int spawnersBefore = _game.CurrentRaid.Layout.SpawnerCells.Count;

            // Every offered direction in turn, so whichever one re-anchors the grid is exercised.
            List<Vector2Int> offered = _game.CurrentRaid.Layout.Plan.Expansions();
            for (int i = 0; i < offered.Count && _game.CurrentRaid.Layout.Plan.Count < 5; i++)
            {
                _game.TapShop(HallMarkerPoint(i));
                await UniTask.Yield(ct);
            }

            DungeonLayout layout = _game.CurrentRaid.Layout;
            MooseRunnerFacade.Log(
                $"after growing to {layout.Plan.Count} rooms, "
                + $"{layout.SpawnerCells.Count} spawners stand (was {spawnersBefore})");

            Assert.GreaterOrEqual(layout.SpawnerCells.Count, spawnersBefore,
                "growing the dungeon lost a spawner the player had paid for");

            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                Assert.AreNotEqual(DungeonManager.DungeonGrid.NoRoom,
                    layout.Grid.RoomAt(spawner),
                    $"the spawner at {spawner} ended up outside every room, so nothing can reach it");
            }
        }
    }
}
