using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Drives the whole game loop many times over, looking for a state the player cannot leave.
    /// </summary>
    /// <remarks>
    /// The flow is standings, raid, review, standings, shop, raid, and round again. Every one of
    /// those transitions is guarded by something -- a keypress, a clock, a lockout, a shift animation
    /// -- and a guard that never releases is the worst possible bug in a jam game: the screen simply
    /// stops responding and the player closes the tab.
    /// <para>
    /// These tests drive the controller the way a player does, through its public surface, and assert
    /// only that it keeps moving.
    /// </para>
    /// </remarks>
    public sealed class PhaseFlowSweepTests
    {
        private GameController _game;

        /// <summary>Loads the shipped scene once.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Fresh controller before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            _game = new GameObject("game").AddComponent<GameController>();
        }

        /// <summary>Where the controller currently is, as a readable string.</summary>
        private string Phase()
        {
            if (_game.IsShopping)
            {
                return "shop";
            }

            if (_game.IsRaiding)
            {
                return "raid";
            }

            if (_game.IsReviewing)
            {
                return "review";
            }

            return "standings";
        }

        /// <summary>
        /// A player who only ever presses keys can always get from one raid to the next.
        /// </summary>
        /// <remarks>
        /// The blunt version, and the one that matters: mash the button and the game must keep
        /// moving. Anything that wedges here is a hang the player cannot escape.
        /// </remarks>
        [Test]
        public async UniTask MashingAKey_AlwaysReachesTheNextRaid(CancellationToken ct)
        {
            var seen = new HashSet<string>();

            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Get into a raid.
                int guard = 0;
                while (!_game.IsRaiding && guard++ < 400)
                {
                    _game.Advance();
                    seen.Add(Phase());
                    await UniTask.Yield(ct);
                }

                Assert.Less(guard, 400, $"cycle {cycle}: could not reach a raid at all");
                Assert.IsTrue(_game.IsRaiding, $"cycle {cycle}: not in the raiding phase");
                MooseRunnerFacade.Log($"cycle {cycle}: reached a raid after {guard} presses");

                // End it the way the clock does.
                guard = 0;
                while (_game.CurrentRaid.IsRunning && guard++ < 5000)
                {
                    _game.CurrentRaid.Tick(0.05f);
                }

                Assert.Less(guard, 5000, $"cycle {cycle}: the raid never ended");
                await UniTask.Yield(ct);
                await UniTask.Yield(ct);
                seen.Add(Phase());
            }

            MooseRunnerFacade.Log($"phases seen: {string.Join(", ", seen)}");
        }

        /// <summary>
        /// A finished raid always leads somewhere, without the player doing anything at all.
        /// </summary>
        /// <remarks>
        /// The review has a short lockout so the stars can land before a keen player skips them. A
        /// lockout that never lifts would strand the game on the review screen forever.
        /// </remarks>
        [Test]
        public async UniTask AFinishedRaid_NeverStrandsTheGame(CancellationToken ct)
        {
            int guard = 0;
            while (!_game.IsRaiding && guard++ < 400)
            {
                _game.Advance();
                await UniTask.Yield(ct);
            }

            while (_game.CurrentRaid.IsRunning && guard++ < 6000)
            {
                _game.CurrentRaid.Tick(0.05f);
            }

            // Let the controller notice, without pressing anything.
            for (int frame = 0; frame < 10; frame++)
            {
                await UniTask.Yield(ct);
            }

            Assert.IsNotNull(_game.LastReview,
                "the raid ended and produced no review, so the player is looking at nothing");
            MooseRunnerFacade.Log(
                $"review after a finished raid: {_game.LastReview.Stars} stars, "
                + $"\"{_game.LastReview.Headline}\"");
        }

        /// <summary>
        /// Starting a new run resets everything a run owns.
        /// </summary>
        /// <remarks>
        /// A season's purchases are permanent <i>within</i> a run. Anything that survived into the
        /// next run would compound silently, and the player would eventually start a fresh game with
        /// a dungeon somebody else paid for.
        /// </remarks>
        [Test]
        public async UniTask ANewRun_StartsClean(CancellationToken ct)
        {
            // What a run opens with: one slime pit and one chest, standing in its single room. The
            // comparison below is against this rather than against zero, because "clean" means the
            // dungeon the game hands a new player, not an empty one.
            int opening = _game.Loadout.Total;

            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            BuyOntoAnEmptyTile(ShopManager.ShopItem.Chest);
            BuyOntoAnEmptyTile(ShopManager.ShopItem.Slime);
            BuyOntoAnEmptyTile(ShopManager.ShopItem.SpikeTrap);

            Assert.Greater(_game.Loadout.Total, opening, "the test needs to have bought something");
            int bought = _game.Loadout.Total;

            _game.NewRun();
            await UniTask.Yield(ct);

            MooseRunnerFacade.Log(
                $"loadout {bought} before a new run, {_game.Loadout.Total} after, "
                + $"opening kit {opening}");
            Assert.AreEqual(opening, _game.Loadout.Total, "purchases survived into a fresh run");
            Assert.AreSame(PartyManager.PartyComposition.Opening, _game.NextParty,
                "a fresh run should open on the balanced party");
        }

        /// <summary>
        /// Clicks anywhere on screen never throw, in any phase.
        /// </summary>
        /// <remarks>
        /// Screen corners, off-screen coordinates and the exact edges of the canvas are where
        /// hit-testing arithmetic goes wrong, and an exception in the input path kills the frame loop
        /// for the rest of the raid.
        /// </remarks>
        [Test]
        public async UniTask ClicksAnywhere_NeverThrow(CancellationToken ct)
        {
            var points = new List<Vector2>
            {
                new(0f, 0f),
                new(Screen.width, Screen.height),
                new(Screen.width * 0.5f, Screen.height * 0.5f),
                new(-50f, -50f),
                new(Screen.width + 500f, Screen.height + 500f),
                new(0f, Screen.height),
                new(Screen.width, 0f)
            };

            // In the shop.
            _game.OpenShopWith(500f);
            await UniTask.Yield(ct);
            foreach (Vector2 point in points)
            {
                _game.TapShop(point);
            }

            // And in a raid.
            _game.StartRaid();
            await UniTask.Yield(ct);
            foreach (Vector2 point in points)
            {
                _game.ClickAt(point);
            }

            MooseRunnerFacade.Log($"clicked {points.Count} extreme points in shop and raid");
            Assert.Pass("no exception from any click position");
        }

        /// <summary>Repeatedly starting raids leaks no scene objects.</summary>
        /// <remarks>
        /// Testing rule 16. Each raid rebuilds the whole view, so a missed teardown would pile up
        /// sprites every round and eventually crawl.
        /// </remarks>
        [Test]
        public async UniTask RepeatedRaids_DoNotLeakObjects(CancellationToken ct)
        {
            _game.StartRaid();
            await UniTask.Yield(ct);
            int afterFirst = _game.transform.childCount;

            for (int i = 0; i < 5; i++)
            {
                _game.StartRaid();
                await UniTask.Yield(ct);
            }

            int afterSix = _game.transform.childCount;
            MooseRunnerFacade.Log($"view children: {afterFirst} after one raid, {afterSix} after six");

            Assert.Less(afterSix, afterFirst * 2,
                "the view is accumulating objects across raids");
        }

        /// <summary>Buys one item onto the first free tile, through real screen coordinates.</summary>
        /// <param name="item">Item to buy.</param>
        private void BuyOntoAnEmptyTile(ShopManager.ShopItem item)
        {
            float scale = Screen.height / 720f;
            DungeonManager.DungeonLayout layout = _game.CurrentRaid.Layout;

            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!layout.CanBuildOn(cell))
                    {
                        continue;
                    }

                    Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
                    var anchor = new Vector2(screen.x, Screen.height - screen.y);
                    _game.TapShop(new Vector2(screen.x, screen.y));

                    Rect[] rows = ShopScreen.PopupRows(anchor, scale, Screen.width, Screen.height);
                    for (int i = 0; i < ShopScreen.Items.Length; i++)
                    {
                        if (ShopScreen.Items[i] == item)
                        {
                            _game.TapShop(new Vector2(
                                rows[i].center.x, Screen.height - rows[i].center.y));
                            return;
                        }
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// Ten complete rounds through every phase leave the game in the same shape it started.
        /// </summary>
        /// <remarks>
        /// The other sweeps each exercise one transition. This walks the loop a player actually
        /// walks — raid, review, bank, standings, shop, buy something, next raid — ten times over,
        /// which is a whole season. Anything that survives a phase change rather than a single
        /// screen only shows up here: a view rebuilt but never torn down, a popup that outlives the
        /// shop that opened it, a purse that ratchets.
        /// <para>
        /// The raid is fast-forwarded by ticking it directly rather than waiting sixty real seconds
        /// a round. That is a white-box shortcut and legitimate in a Play Mode test; every
        /// transition around it still goes through the controller's own handlers.
        /// </para>
        /// </remarks>
        [Test]
        public async UniTask TenWholeRounds_LeaveTheGameIntact(CancellationToken ct)
        {
            _game.Advance();
            await UniTask.Yield(ct);

            int baseline = 0;
            int rounds = 0;

            for (int round = 0; round < 10; round++)
            {
                // Pressed repeatedly, because that is what a player does. Banking a raid resets
                // the standings shift, and the first press only finishes that animation -- a single
                // Advance() after a bank looks like a dead button and left an earlier version of
                // this sweep restarting round zero forever.
                for (int press = 0; press < 4 && !_game.IsRaiding && !_game.IsShopping; press++)
                {
                    _game.Advance();
                    await UniTask.Yield(ct);
                }

                if (_game.IsShopping)
                {
                    BuyOntoAnEmptyTile(ShopManager.ShopItem.Chest);
                    _game.TapShop(ReadyPoint());
                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                Assert.IsTrue(_game.IsRaiding, $"round {round}: the raid never started");

                // Run the raid out.
                int guard = 0;
                while (_game.CurrentRaid.IsRunning && guard++ < 400)
                {
                    _game.CurrentRaid.Tick(0.25f);
                }

                Assert.Less(guard, 400, $"round {round}: the raid never ended");
                await UniTask.Yield(ct);
                await UniTask.Yield(ct);

                // The review, then bank it through the same handler a tap uses.
                Assert.IsTrue(_game.IsReviewing, $"round {round}: no review after the raid");
                await UniTask.WaitForSeconds(
                    GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);
                Assert.IsTrue(_game.DismissReview(),
                    $"round {round}: the review refused to be dismissed");
                await UniTask.Yield(ct);

                // Not round+1: the league is an elimination now, and a weak raid can knock the
                // player out and start a fresh run, which resets the round counter. What matters
                // here is that the raid was banked at all.
                Assert.Greater(_game.League.Round, 0,
                    $"round {round}: the raid was not banked into the league");

                if (round == 0)
                {
                    baseline = _game.transform.childCount;
                }

                rounds++;

                if (_game.League.PlayerRelegated)
                {
                    // Relegation ends the run. Starting a fresh one is the legitimate continuation,
                    // and the loop has to survive it -- a season that ends mid-sweep is a state
                    // transition like any other, not a reason to stop looking.
                    MooseRunnerFacade.Log($"relegated after round {round}; starting a fresh run");
                    _game.Advance();
                    await UniTask.Yield(ct);
                }
            }

            int finalCount = _game.transform.childCount;
            MooseRunnerFacade.Log(
                $"{rounds} whole rounds: {baseline} view objects after the first, {finalCount} at "
                + $"the end; league round {_game.League.Round}, loadout {_game.Loadout.Total}");

            Assert.AreEqual(10, rounds, "the loop did not complete ten rounds");
            Assert.Less(finalCount, baseline * 2,
                $"ten rounds grew the scene from {baseline} to {finalCount} objects, so something "
                + "survives a phase change that should not");
        }

        /// <summary>Centre of the Ready button, in input space.</summary>
        /// <returns>A screen point inside the Ready button.</returns>
        private static Vector2 ReadyPoint()
        {
            Rect ready = ShopScreen.ReadyRect(
                Screen.height / 720f, Screen.width, Screen.height);
            return new Vector2(ready.center.x, Screen.height - ready.center.y);
        }
    }
}
