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
            _game.OpenShopWith(5000f);
            await UniTask.Yield(ct);

            foreach (ShopManager.ShopItem item in ShopScreen.Items)
            {
                float scale = Screen.height / 720f;
                Rect[] cards = ShopScreen.Cards(scale, out _);
                for (int i = 0; i < ShopScreen.Items.Length; i++)
                {
                    if (ShopScreen.Items[i] == item)
                    {
                        _game.TapShop(new Vector2(
                            cards[i].center.x, Screen.height - cards[i].center.y));
                    }
                }
            }

            Assert.Greater(_game.Loadout.Total, 0, "the test needs to have bought something");
            int bought = _game.Loadout.Total;

            _game.NewRun();
            await UniTask.Yield(ct);

            MooseRunnerFacade.Log($"loadout {bought} before a new run, {_game.Loadout.Total} after");
            Assert.AreEqual(0, _game.Loadout.Total, "purchases survived into a fresh run");
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
    }
}
