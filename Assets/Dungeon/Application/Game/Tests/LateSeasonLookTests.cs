using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using System.Linq;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
using Dungeon.ShopManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Photographs a raid late in a season, when the party is nine strong.
    /// </summary>
    /// <remarks>
    /// Every frame this project has ever captured shows a party of <b>four</b>. Parties grow to nine
    /// from raid six, they now walk three abreast rather than in single file, and each carries a
    /// staggered health bar — three changes to the same few hundred pixels, none of which has been
    /// looked at together. This project's own history is that composition faults are invisible to
    /// assertions and obvious in a frame: five were found that way on 2026-08-15 against a suite of
    /// 331 green tests.
    /// <para>
    /// Reached by playing the season rather than by setting a party size, because the size is
    /// decided by <c>PartyComposition.SizeForRound</c> and a test that hand-built a nine-strong party
    /// would photograph a configuration the game never produces.
    /// </para>
    /// </remarks>
    public sealed class LateSeasonLookTests
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
        /// A raid with a full nine-strong party, photographed once it is inside the dungeon.
        /// </summary>
        /// <remarks>
        /// The frame is the point. The assertions only guarantee it is the frame that was wanted —
        /// that the party really is nine, and really is in a raid — because a photograph of a
        /// four-strong party filed under this name would be worse than no photograph at all.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask ANineStrongParty_IsPhotographedMidRaid(CancellationToken ct)
        {
            // Drive the league forward with scores that keep the player comfortably up, so the run
            // survives to the round where the party is at full strength. The raids themselves are
            // skipped; only the round counter matters for the party size.
            int target = 0;
            for (int round = 0; round < 20; round++)
            {
                if (PartyComposition.SizeForRound(round) >= PartyComposition.MaxSize)
                {
                    target = round;
                    break;
                }
            }

            string sizes = string.Join(
                ",", Enumerable.Range(0, 10).Select(PartyComposition.SizeForRound));
            MooseRunnerFacade.Log(
                $"party reaches {PartyComposition.MaxSize} at round {target} (sizes: {sizes})");

            Assert.Greater(target, 0, "no round ever fields a full-strength party");

            _game.Advance();
            await UniTask.Yield(ct);

            int guard = 0;
            while (_game.League.Round < target && guard++ < 120)
            {
                if (_game.IsRaiding)
                {
                    // Run the clock out rather than reach for a shortcut: ending a raid is the
                    // controller's job and there is no seam for it, which is correct.
                    Raid raid = _game.CurrentRaid;
                    int spin = 0;
                    while (raid.IsRunning && spin++ < 4000)
                    {
                        raid.Tick(0.05f);
                    }

                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                if (_game.IsReviewing)
                {
                    await UniTask.WaitForSeconds(
                        GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);
                    _game.DismissReview();
                    await UniTask.Yield(ct);
                }

                // The shop does not leave on Advance -- it closes when Ready is pressed, and the
                // controller starts the next raid on the tick that sees it closed. Pressed through
                // the shop itself rather than through a screen coordinate: the tap path is covered
                // by its own tests, and the tap this test first used missed, because it recomputed
                // the interface scale instead of using the controller's. That left the fixture
                // sitting in the shop for all 120 of its guard iterations.
                if (_game.IsShopping)
                {
                    _game.CurrentShop.Ready();
                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                // Keep the player clear of the drop. These raids are ticked with nobody playing
                // them, so they harvest the idle floor and the run is eliminated around round two --
                // which is why the first two versions of this test sat at round 1 until the guard
                // ran out. The subject here is what a late-season raid LOOKS like, not whether this
                // fixture can play one, so the standing is granted rather than earned.
                if (_game.League.Entries.Count > 1)
                {
                    float leader = _game.League.Entries.Max(e => e.Score);
                    _game.League.Player.Score = leader + 1000f;
                }

                // Advance from anything that is NOT a raid, a review or a shop -- which means the
                // standings and the loading screen. Advance() calls OpenShop() unconditionally once
                // the run is past its first round, so calling it every iteration re-opened the shop
                // the line above had just closed; and calling it only from the standings left the
                // fixture sitting in Loading, which is a phase and looks like none of them.
                if (!_game.IsRaiding && !_game.IsReviewing && !_game.IsShopping)
                {
                    _game.Advance();
                }

                await UniTask.Yield(ct);

                if (guard < 12 || guard % 20 == 0)
                {
                    MooseRunnerFacade.Log(
                        $"  guard {guard}: round {_game.League.Round}, "
                        + $"raiding={_game.IsRaiding} reviewing={_game.IsReviewing} "
                        + $"shopping={_game.IsShopping} standings={_game.IsShowingStandings} "
                        + $"won={_game.HasWon} field={_game.League.Entries.Count}");
                }
            }

            // Into the raid itself, then let the party walk in so they are inside the dungeon and
            // strung out in marching order rather than stacked on the entrance.
            guard = 0;
            bool shopShot = false;
            while (!_game.IsRaiding && guard++ < 30)
            {
                if (_game.IsShopping)
                {
                    // The shop standing between the player and the last raid of the season. The one
                    // AFTER it does not exist -- round nine is the final, so the run ends on the
                    // review rather than opening another shop, which is why the first version of
                    // this capture never fired.
                    if (!shopShot)
                    {
                        shopShot = true;
                        MooseRunnerFacade.Log(
                            $"shop before the last raid: purse {_game.CurrentShop.Purse:F0}, "
                            + $"a door costs {_game.CurrentShop.Price(ShopItem.Door):F0}");
                        await Capture("15-late-season-shop", ct);
                    }

                    _game.CurrentShop.Ready();
                }
                else if (_game.IsReviewing)
                {
                    _game.DismissReview();
                }
                else
                {
                    _game.Advance();
                }

                await UniTask.Yield(ct);
            }

            Assert.IsTrue(_game.IsRaiding,
                $"never reached a raid at round {_game.League.Round}, so there is nothing to "
                + "photograph");

            for (int frame = 0; frame < 240; frame++)
            {
                await UniTask.Yield(ct);
            }

            int size = _game.CurrentRaid.Party.Living.Count();
            MooseRunnerFacade.Log(
                $"round {_game.League.Round}: {size} living, "
                + $"{Party.AbreastFor(size)} abreast, goal {_game.CurrentRaid.Party.Goal}");

            Assert.GreaterOrEqual(size, 7,
                $"the party at round {_game.League.Round} is only {size} strong, so this frame is "
                + "not the late-season party the test is named for");

            await Capture("13-late-season-raid", ct);

            // On through the end of that raid, so the review and the shop are photographed with a
            // nine-strong party too. Both have only ever been captured at four, and both put a
            // party-sized number on screen -- the review spells the death notices, the shop draws
            // the purse a nine-strong raid earned.
            Raid finalRaid = _game.CurrentRaid;
            int finalSpin = 0;
            while (finalRaid.IsRunning && finalSpin++ < 4000)
            {
                finalRaid.Tick(0.05f);
            }

            await UniTask.Yield(ct);
            await UniTask.Yield(ct);

            if (_game.IsReviewing)
            {
                MooseRunnerFacade.Log(
                    $"review after a party of {size}: harvested {finalRaid.EnergyHarvested:F0}, "
                    + $"outcome {finalRaid.Outcome}, {finalRaid.Party.Living.Count()} still standing");
                await Capture("14-late-season-review", ct);

                await UniTask.WaitForSeconds(
                    GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);
                _game.DismissReview();
                await UniTask.Yield(ct);
            }

            MooseRunnerFacade.Log(
                $"after the last raid: won={_game.HasWon}, field={_game.League.Entries.Count}, "
                + $"round {_game.League.Round}");
        }
    }
}
