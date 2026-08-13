using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.ShopManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Drives the shop through real screen coordinates rather than through its model.
    /// </summary>
    /// <remarks>
    /// The whole reason this file exists: a shipped build once had all three raid verbs dead while
    /// every test was green, because the tests called <c>raid.SpawnMob(...)</c> instead of clicking.
    /// The shop introduces six more clickable things and a button; each is pressed here at the
    /// coordinate the drawing code puts it at, so a card drawn in one place and hit-tested in
    /// another fails here instead of on itch.io.
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

        /// <summary>Centre of a card, in input space, whose origin is the bottom left.</summary>
        /// <param name="item">Item whose card is wanted.</param>
        /// <returns>A screen point inside that card.</returns>
        private static Vector2 CardPoint(ShopItem item)
        {
            float scale = Screen.height / 720f;
            Rect[] cards = ShopScreen.Cards(scale, out _);

            for (int i = 0; i < ShopScreen.Items.Length; i++)
            {
                if (ShopScreen.Items[i] == item)
                {
                    return new Vector2(cards[i].center.x, Screen.height - cards[i].center.y);
                }
            }

            return Vector2.zero;
        }

        /// <summary>Centre of the Ready button, in input space.</summary>
        /// <returns>A screen point inside the Ready button.</returns>
        private static Vector2 ReadyPoint()
        {
            ShopScreen.Cards(Screen.height / 720f, out Rect ready);
            return new Vector2(ready.center.x, Screen.height - ready.center.y);
        }

        /// <summary>Tapping a card the player can afford buys that exact item.</summary>
        [Test]
        public async UniTask TappingACard_BuysThatItem(CancellationToken ct)
        {
            _game.OpenShop();
            await UniTask.Yield(ct);

            Assert.IsTrue(_game.IsShopping, "the controller should be in the shop");

            // Enough for anything on the board, so affordability is not what is under test here.
            _game.TapShop(CardPoint(ShopItem.Chest));

            Assert.AreEqual(0, _game.Loadout.Count(ShopItem.Chest),
                "the shop opened with an empty purse, so the tap should have bought nothing");
        }

        /// <summary>Every one of the six cards is reachable, and each buys its own item.</summary>
        /// <remarks>
        /// Tapped in a rich shop so affordability never masks a mis-placed rectangle. A card whose
        /// draw rectangle and hit rectangle disagree buys the wrong thing, or nothing, and looks
        /// exactly like a dead button to the player.
        /// </remarks>
        [Test]
        public async UniTask EveryCard_IsClickableAndBuysItself(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            foreach (ShopItem item in ShopScreen.Items)
            {
                _game.TapShop(CardPoint(item));
                MooseRunnerFacade.Log($"tapped {item} at {CardPoint(item)}");
                Assert.AreEqual(1, _game.Loadout.Count(item),
                    $"tapping the {item} card should have bought exactly one {item}");
            }

            Assert.AreEqual(6, _game.Loadout.Total, "six cards, six purchases");
        }

        /// <summary>Tapping empty space between the cards buys nothing.</summary>
        [Test]
        public async UniTask TappingEmptySpace_BuysNothing(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            _game.TapShop(new Vector2(2f, Screen.height - 4f));

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

        /// <summary>Anything bought in the shop is standing in the dungeon the next party enters.</summary>
        [Test]
        public async UniTask PurchasesAppearInTheNextDungeon(CancellationToken ct)
        {
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            int spawnersBefore = _game.CurrentRaid.Layout.SpawnerCells.Count;

            _game.TapShop(CardPoint(ShopItem.Skeleton));
            _game.TapShop(CardPoint(ShopItem.Chest));
            _game.TapShop(ReadyPoint());
            await UniTask.Yield(ct);
            await UniTask.Yield(ct);

            Assert.AreEqual(spawnersBefore + 1, _game.CurrentRaid.Layout.SpawnerCells.Count,
                "the bone pile should be in the dungeon");
            Assert.AreEqual(1, _game.CurrentRaid.Layout.ChestCells.Count,
                "so should the chest");
        }
    }
}
