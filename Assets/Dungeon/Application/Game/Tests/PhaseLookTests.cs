using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.LeagueManager;
using Dungeon.ShopManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Photographs the screens no other test looks at.
    /// </summary>
    /// <remarks>
    /// The project's doctrine in one file: green assertions proved nothing about the sister
    /// project's worst bugs, and every one of them was obvious in a rendered frame. The raid and the
    /// review are photographed by <c>RaidE2E</c>; the shop and the collapse screen were not
    /// photographed by anything, and the winning ending had never been drawn at all until the test
    /// next door reached it — where it turned out to be announcing relegations at the winner.
    /// <para>
    /// These assert only that the capture happened and that drawing logged nothing. The value is the
    /// PNGs in <c>Screenshots/</c>, which a person (or an agent) reads.
    /// </para>
    /// </remarks>
    public sealed class PhaseLookTests
    {
        /// <summary>The controller under test.</summary>
        private GameController _game;

        /// <summary>Where frames are written to be looked at.</summary>
        private static string ShotDirectory =>
            Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");

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

        /// <summary>Photographs the composited frame, interface included.</summary>
        /// <param name="name">File name stem.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable capture.</returns>
        private static async UniTask Capture(string name, CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(ShotDirectory);
            string path = Path.Combine(ShotDirectory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            MooseRunnerFacade.Log($"captured {path}");
            Assert.IsTrue(File.Exists(path), $"{name} was not written to disk");
        }

        /// <summary>
        /// The shop, with a build menu open on a tile, over the dungeon the run opens with.
        /// </summary>
        /// <remarks>
        /// The shop's hit-testing is covered thoroughly and its <i>appearance</i> not at all, and the
        /// board underneath it has just changed from a three-room corridor to a single room. A menu
        /// anchored to a tile is exactly the kind of thing that is correct in arithmetic and wrong on
        /// screen.
        /// </remarks>
        [Test]
        public async UniTask TheShop_IsPhotographedWithAMenuOpen(CancellationToken ct)
        {
            _game.OpenShopWith(900f);
            await UniTask.Yield(ct);

            // Open the menu on a tile the way a player does, so the photograph shows the popup where
            // the code actually anchors it.
            for (int y = 0; y < _game.CurrentRaid.Layout.Grid.Height; y++)
            {
                for (int x = 0; x < _game.CurrentRaid.Layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_game.CurrentRaid.Layout.CanBuildOn(cell))
                    {
                        continue;
                    }

                    Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
                    _game.TapShop(new Vector2(screen.x, screen.y));
                    y = _game.CurrentRaid.Layout.Grid.Height;
                    break;
                }
            }

            await UniTask.Yield(ct);
            await Capture("06-shop-with-menu", ct);

            Assert.IsTrue(_game.IsShopping, "the shop closed while being photographed");

            // The shop is not a raid either, and it draws the dungeon the next party walks into --
            // so the same overlays that lay across the standings would lie across it. Checked rather
            // than assumed, because the rule this follows was written after three passes at fixing
            // the screen in front of me instead of the class.
            System.Collections.Generic.List<string> litInShop = LitBars();
            MooseRunnerFacade.Log(
                $"shop: {litInShop.Count} raid overlays still drawn "
                + (litInShop.Count > 0 ? string.Join(", ", litInShop) : "(none)"));
            Assert.IsEmpty(litInShop,
                $"{litInShop.Count} raid overlays are drawn over the shop");
        }

        /// <summary>
        /// The collapse screen, which is what most players will actually see.
        /// </summary>
        /// <remarks>
        /// Reached by playing badly rather than by setting a flag: a raid where nothing is spawned
        /// earns the idle floor, and the field earns far more, so the bottom of the table arrives on
        /// its own. That also makes this a check on the losing path being reachable in the ordinary
        /// way, which the winning one is not.
        /// </remarks>
        [Test]
        public async UniTask TheCollapseScreen_IsPhotographed(CancellationToken ct)
        {
            _game.Advance();
            await UniTask.Yield(ct);

            // Straight to the bottom: the rivals are given a season's worth of scores while the
            // player banks nothing.
            for (int round = 0; round < 3 && !_game.League.PlayerRelegated; round++)
            {
                _game.League.SubmitRaid(0f);
            }

            Assert.IsTrue(_game.League.PlayerRelegated,
                "harvesting nothing for three rounds left the player clear of the drop");

            for (int press = 0; press < 4 && !_game.IsRaiding && !_game.IsShopping; press++)
            {
                _game.Advance();
                await UniTask.Yield(ct);
            }

            if (_game.IsShopping)
            {
                Rect ready = ShopScreen.ReadyRect(
                    Mathf.Min(Screen.width / 1280f, Screen.height / 720f),
                    Screen.width, Screen.height);
                _game.TapShop(new Vector2(ready.center.x, Screen.height - ready.center.y));
                await UniTask.Yield(ct);
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(_game.IsRaiding, "the next raid never started");

            int guard = 0;
            while (_game.CurrentRaid.IsRunning && guard++ < 2000)
            {
                _game.CurrentRaid.Tick(0.05f);
            }

            await UniTask.Yield(ct);
            await UniTask.WaitForSeconds(
                GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);

            // The review is its own phase and had no check of its own. This test walks through it on
            // the way to the collapse, so it costs one capture to cover the last screen that had a
            // photograph and no assertion.
            Assert.IsTrue(_game.IsReviewing, "expected the adventurers' review before dismissing it");
            await Capture("12-review-screen", ct);

            System.Collections.Generic.List<string> litInReview = LitBars();
            MooseRunnerFacade.Log(
                $"review: {litInReview.Count} raid overlays still drawn "
                + (litInReview.Count > 0 ? string.Join(", ", litInReview) : "(none)"));
            Assert.IsEmpty(litInReview,
                $"{litInReview.Count} raid overlays are drawn over the adventurers' review");

            Assert.IsTrue(_game.DismissReview(), "the review refused to be dismissed");

            // Long enough for the standings to finish sliding into their new order. Photographed
            // after six frames, the picture was of twenty rows mid-flight past each other, which
            // says nothing about what the screen looks like.
            await UniTask.WaitForSeconds(1.4f, cancellationToken: ct);

            MooseRunnerFacade.Log(
                $"after banking nothing: field {_game.League.Entries.Count}, "
                + $"position {_game.League.PlayerPosition}, relegated "
                + $"{_game.League.PlayerRelegated}");

            await Capture("07-collapse", ct);

            // The same fault the standings had, on the screen most players will actually reach.
            // The first fix named Phase.Standings alone, so it shipped still broken here -- the bars
            // lay across standings rows 10 to 12, above the party's own sprites. The rule is now
            // "bars only while raiding", and this is the second screen it has to cover.
            System.Collections.Generic.List<string> lit = LitBars();
            MooseRunnerFacade.Log(
                $"collapse screen: {lit.Count} party bars still drawn "
                + (lit.Count > 0 ? string.Join(", ", lit) : "(none)"));

            Assert.IsEmpty(lit,
                $"{lit.Count} party health/mana bars are drawn over the collapse screen's standings, "
                + "where under its darkening they are the brightest thing on it");
        }

        /// <summary>
        /// The standings mid-season, when the field has shrunk but the run is still alive.
        /// </summary>
        /// <remarks>
        /// The state in which the standings strip used to throw out of <c>OnGUI</c>, because it
        /// indexed a twenty-row window into a table that no longer had twenty rows. Worth a
        /// photograph as well as a fix: the layout around a shrinking table is full of numbers
        /// written for the size it started at.
        /// </remarks>
        [Test]
        public async UniTask TheMidSeasonStandings_ArePhotographed(CancellationToken ct)
        {
            _game.Advance();
            await UniTask.Yield(ct);

            // A strong player, five rounds in: the field is half gone and the player is top.
            for (int round = 0; round < 5 && _game.League.Entries.Count > 6; round++)
            {
                _game.League.SubmitRaid(4000f);
                _game.League.CollapseRelegated();
            }

            MooseRunnerFacade.Log(
                $"mid-season: {_game.League.Entries.Count} dungeons left, player "
                + $"{_game.League.PlayerPosition}, eliminations this round "
                + $"{_game.League.EliminationsThisRound}");

            for (int frame = 0; frame < 4; frame++)
            {
                await UniTask.NextFrame(ct);
            }

            await Capture("08-mid-season-standings", ct);

            Assert.Less(_game.League.Entries.Count, LeagueTable.Size,
                "the field never shrank, so this photographed the opening table");
        }

        /// <summary>
        /// The league screen shows no health bars, because nobody on it is raiding.
        /// </summary>
        /// <remarks>
        /// A regression test for a fault found in the shipped WebGL build rather than in the editor,
        /// on the one screen SPEC.md calls the ten-second hook. The league draws the player's own
        /// dungeon behind the standings and darkens it with a quad at 82% opacity. Masonry near
        /// luminance 0.12 falls to about 0.02 under that; a saturated bar at 0.90 green keeps about
        /// 0.16. So the brightest thing on the title screen was a health bar for a party that is not
        /// raiding, lying across the standings rows.
        /// <para>
        /// Asserts the renderers rather than the pixels. A pixel threshold on a screen that also
        /// draws torchlight would be the kind of measurement that passes for the wrong reason.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask TheLeagueScreen_ShowsNoHealthBars(CancellationToken ct)
        {
            // Deliberately NO Advance() here. Advance is what LEAVES the standings for the raid, so
            // pressing it first walks straight past the screen under test -- which is how the first
            // version of this test failed. The loading screen enters the standings by itself once it
            // has held itself for its few seconds.
            for (int frame = 0; frame < 20 && !_game.IsShowingStandings; frame++)
            {
                await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);
            }

            Assert.IsTrue(_game.IsShowingStandings,
                "the run never reached the standings, so this proves nothing either way");

            await UniTask.NextFrame(ct);
            await Capture("10-league-screen", ct);

            SpriteRenderer[] bars = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsSortMode.None);
            var lit = new System.Collections.Generic.List<string>();
            foreach (SpriteRenderer bar in bars)
            {
                bool isBar = bar.name.StartsWith("hpfill") || bar.name.StartsWith("hpback")
                    || bar.name.StartsWith("manafill") || bar.name.StartsWith("manaback");
                if (isBar && bar.enabled)
                {
                    lit.Add(bar.name);
                }
            }

            MooseRunnerFacade.Log(
                $"league screen: {lit.Count} party bars still drawn "
                + (lit.Count > 0 ? string.Join(", ", lit) : "(none)"));

            Assert.IsEmpty(lit,
                $"{lit.Count} party health/mana bars are drawn over the standings, and under the "
                + "screen's darkening they are the brightest thing on it");
        }

        /// <summary>
        /// Every prefix belonging to a bar that only means something during a raid.
        /// </summary>
        /// <remarks>
        /// The party's own bars were the ones found on the standings, but they are not the only
        /// world-space overlay the raid draws: a monster's health, a door being forced, a trap being
        /// disarmed and a shot in flight are all quads made the same way, by the same workshop, and
        /// left enabled the same way. Checking only the four that were noticed would be repeating
        /// the mistake that put the first fix on one screen out of two.
        /// <para>
        /// <c>buildable_</c> is deliberately absent — those markers belong to the shop, which is not
        /// a raid but is the one phase where they are correct.
        /// </para>
        /// </remarks>
        private static readonly string[] RaidOnlyBars =
        {
            "hpfill", "hpback", "manafill", "manaback",
            "mobhpfill", "mobhpback", "disarmfill", "disarmback",
            "doorworkfill", "doorworkback", "shot_"
        };

        /// <summary>Names every raid-only bar currently drawn.</summary>
        /// <returns>The enabled bar renderers, by name.</returns>
        private static System.Collections.Generic.List<string> LitBars()
        {
            var lit = new System.Collections.Generic.List<string>();
            foreach (SpriteRenderer bar in Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsSortMode.None))
            {
                if (!bar.enabled)
                {
                    continue;
                }

                foreach (string prefix in RaidOnlyBars)
                {
                    if (bar.name.StartsWith(prefix))
                    {
                        lit.Add(bar.name);
                        break;
                    }
                }
            }

            return lit;
        }


        /// <summary>
        /// A raid in progress DOES draw the party's bars, which is the other half of the rule.
        /// </summary>
        /// <remarks>
        /// Every other check here asserts an overlay is absent. Alone, they are satisfied by a game
        /// that never draws a health bar at all — and the fix they guard is a single condition,
        /// <c>_phase != Phase.Raiding</c>, which one edit could invert while leaving all of them
        /// green.
        /// <para>
        /// This is the day's lesson applied to its own tests: three separate times a measurement was
        /// believed because it had never been asked to tell two known-different cases apart. A suite
        /// that only proves bars can be hidden is exactly that measurement.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask ARaidInProgress_DoesDrawThePartysBars(CancellationToken ct)
        {
            _game.Advance();
            await UniTask.Yield(ct);

            for (int press = 0; press < 4 && !_game.IsRaiding && !_game.IsShopping; press++)
            {
                _game.Advance();
                await UniTask.Yield(ct);
            }

            if (_game.IsShopping)
            {
                Rect ready = ShopScreen.ReadyRect(
                    Mathf.Min(Screen.width / 1280f, Screen.height / 720f),
                    Screen.width, Screen.height);
                _game.TapShop(new Vector2(ready.center.x, Screen.height - ready.center.y));
                await UniTask.Yield(ct);
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(_game.IsRaiding, "the raid never started, so this proves nothing");

            // A few ticks, so the party is alive and walking rather than mid-spawn.
            for (int frame = 0; frame < 4; frame++)
            {
                await UniTask.NextFrame(ct);
            }

            System.Collections.Generic.List<string> lit = LitBars();
            MooseRunnerFacade.Log($"raiding: {lit.Count} party bars drawn (expected: some)");

            Assert.IsNotEmpty(lit,
                "no party health bars are drawn during a raid, so the rule that hides them outside "
                + "one has swallowed the raid as well");
        }
    }
}
