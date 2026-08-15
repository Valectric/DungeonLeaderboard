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
    }
}
