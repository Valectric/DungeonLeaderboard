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
    /// Photographs and checks the loading card — the first thing every player sees.
    /// </summary>
    /// <remarks>
    /// <b>The last phase with no frame and no check.</b> CLAUDE.md asks for a photographed frame per
    /// entry in <c>GameController.Phase</c>, by name, and lists Loading first; every other phase had
    /// one and this had neither. It was invisible for the same reason <c>Phase.Won</c> was before it
    /// — nothing could observe the phase from outside, so nothing asserted it.
    /// <para>
    /// Worth more than tidiness here. This screen is up for six seconds on a desktop and was
    /// measured at roughly <b>twenty</b> on an itch embed whose animation frames the browser is
    /// throttling, so it is a large share of a jam voter's first impression — and the one screen
    /// where "the art did not load" produces a plausible-looking result rather than an obvious one,
    /// because a dark game and a black screen are hard to tell apart from a description.
    /// </para>
    /// </remarks>
    public sealed class OpeningCardLookTests
    {
        /// <summary>The controller under test.</summary>
        private GameController _game;

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Builds a fresh controller, which starts a fresh run on its opening card.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            _game = new GameObject("game").AddComponent<GameController>();
        }

        /// <summary>
        /// The game opens on the loading card, and it is not a blank screen.
        /// </summary>
        /// <remarks>
        /// The pixel check is deliberately a <i>spread</i> rather than a brightness threshold. This
        /// game is legitimately very dark — the palette is violet-black and CLAUDE.md records a
        /// whole investigation lost to reading absolute luminance off a screenshot — so "is it
        /// bright enough" is the wrong question and would fail for the wrong reasons. "Does the
        /// frame contain more than one colour" is the right one: it separates a drawn scene from
        /// the failure this screen actually has, which is art that did not load and a flat fill
        /// where the dungeon should be.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask TheGameOpens_OnACardThatIsActuallyDrawn(CancellationToken ct)
        {
            Assert.IsTrue(_game.IsLoading, "a fresh run should open on the loading card");

            await UniTask.WaitForSeconds(1f, cancellationToken: ct);
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            Color[] pixels = frame.GetPixels();

            float min = 1f;
            float max = 0f;
            double total = 0d;
            foreach (Color pixel in pixels)
            {
                float value = (0.2126f * pixel.r) + (0.7152f * pixel.g) + (0.0722f * pixel.b);
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
                total += value;
            }

            float mean = (float)(total / pixels.Length);
            int width = frame.width;
            int height = frame.height;
            Object.DestroyImmediate(frame);

            MooseRunnerFacade.Log(
                $"opening card: {width}x{height}, luminance min {min:F3} "
                + $"max {max:F3} mean {mean:F3}");

            Assert.Greater(max - min, 0.1f,
                $"the opening card spans only {max - min:F3} of luminance, so the first screen of "
                + "the game is a flat fill -- the scene or its art did not draw");

            await Frames.Capture("00-opening-card", ct);
        }

        /// <summary>
        /// The card hands over to the standings by itself, without the player pressing anything.
        /// </summary>
        /// <remarks>
        /// The screen is deliberately not skippable, so nothing but the clock can end it — which
        /// makes "the clock ends it" the whole contract. If this ever stopped being true the game
        /// would open on a still image and never start, and no other test would notice, because
        /// every one of them waits past this screen rather than asserting it.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask TheCard_HandsOverToTheStandingsOnItsOwn(CancellationToken ct)
        {
            Assert.IsTrue(_game.IsLoading, "a fresh run should open on the loading card");

            // A margin over the stated duration, because this waits in real seconds against a
            // screen that advances on frame deltas -- the two are the same only while frames are
            // arriving normally, which is exactly the assumption the itch embed breaks.
            await UniTask.WaitForSeconds(LoadingScreen.Seconds + 3f, cancellationToken: ct);

            MooseRunnerFacade.Log(
                $"after {LoadingScreen.Seconds + 3f:F0}s: loading={_game.IsLoading}, "
                + $"standings={_game.IsShowingStandings}");

            Assert.IsFalse(_game.IsLoading,
                $"the opening card was still up {LoadingScreen.Seconds + 3f:F0} seconds in, and "
                + "nothing else can end it -- the game would never reach the standings");
            Assert.IsTrue(_game.IsShowingStandings,
                "the card should hand over to the standings, which are this game's title screen");
        }
    }
}
