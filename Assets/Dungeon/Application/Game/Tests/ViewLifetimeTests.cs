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
    /// Measures whether the dungeon's renderers accumulate as a season is played.
    /// </summary>
    /// <remarks>
    /// <b>The board is torn down and rebuilt constantly</b> — every raid, every shop preview, every
    /// purchase that changes the dungeon — and each rebuild creates a sprite for every floor tile,
    /// wall, door, prop and light. A rebuild that failed to clear the last one would not look wrong
    /// in a single frame, because the new board is drawn exactly on top of the old: the symptom is
    /// a scene that grows quietly for ten rounds.
    /// <para>
    /// That matters here more than in most projects. This ships to <b>WebGL</b>, where memory is
    /// tight and the tab dies rather than degrades, and a jam voter meets the failure as a browser
    /// crash several minutes in — the hardest possible bug to report and the easiest to blame on
    /// the player's machine.
    /// </para>
    /// <para>
    /// So this is the testing guidelines' idempotency rule aimed at the renderer: run the same thing
    /// repeatedly and require the scene to end up the same size every time. <c>RebuildView</c> does
    /// clear its children first, which is the reason to expect a pass — but <c>Destroy</c> is
    /// deferred to the end of the frame while the replacement is built immediately, and the scenery
    /// parents itself under a root of its own, so reading the code is not the same as measuring it.
    /// </para>
    /// </remarks>
    public sealed class ViewLifetimeTests
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

        /// <summary>Counts every sprite currently alive in the scene.</summary>
        /// <returns>How many renderers exist.</returns>
        private static int RenderersAlive()
        {
            return Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// Rebuilding the board repeatedly leaves the scene the same size it started.
        /// </summary>
        /// <remarks>
        /// Driven through <c>NewRun</c>, which is the heaviest rebuild the game does — a whole new
        /// season, dungeon, party and view. A seed is fixed so every pass builds the same board and
        /// the counts are comparable; without it a run with more rooms would look like a leak.
        /// <para>
        /// A frame is awaited between passes because Unity's <c>Destroy</c> is deferred to the end
        /// of the current frame. Counting without that would measure the overlap rather than the
        /// leak, and would report a doubling every time — an instrument answering a question next
        /// to the one being asked, which this repository has been bitten by often enough to name.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable measurement.</returns>
        [Test]
        public async UniTask RebuildingTheBoard_DoesNotGrowTheScene(CancellationToken ct)
        {
            _game.SeedOverride = 4242;

            var counts = new int[6];
            for (int pass = 0; pass < counts.Length; pass++)
            {
                _game.NewRun();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitForEndOfFrame(ct);
                counts[pass] = RenderersAlive();
            }

            MooseRunnerFacade.Log(
                "renderers after each rebuild: " + string.Join(", ", counts));

            // The control, and the reason this test is worth trusting. A flat sequence of counts is
            // what a correct rebuild looks like AND what a counter that never moves looks like, and
            // those are not the same result. Tearing the scene down and reading zero proves the
            // instrument responds to the thing it claims to measure, so the flatness above is a
            // fact about the game rather than about FindObjectsByType.
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            int afterTeardown = RenderersAlive();
            MooseRunnerFacade.Log($"renderers after clearing the scene: {afterTeardown}");

            Assert.Greater(counts[1], 0,
                "no sprites were found at all, so this test cannot detect a leak either");
            Assert.Less(afterTeardown, counts[1],
                $"clearing the scene left {afterTeardown} renderers against {counts[1]} while it "
                + "was built, so the count is not tracking the board and every number above is "
                + "meaningless");

            // Compared against the SECOND pass, not the first. The first run builds the scene from
            // an empty scene; every later one replaces a board that is already there, so pass zero
            // is a different measurement and would make a steady state look like growth.
            int settled = counts[1];
            int worst = counts[^1];

            Assert.LessOrEqual(worst, settled + (settled / 10),
                $"the scene held {settled} renderers after the second rebuild and {worst} after "
                + $"{counts.Length}, so the board is not being cleared between runs -- on WebGL "
                + "that is a tab that dies partway through a season");
        }

        /// <summary>
        /// Starting raid after raid leaves the scene the same size too.
        /// </summary>
        /// <remarks>
        /// The path a player actually takes. <c>NewRun</c> above is the big hammer and is only hit
        /// on a collapse; a season is ten of these, each rebuilding the board through a different
        /// route, so the two are worth measuring separately.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable measurement.</returns>
        [Test]
        public async UniTask StartingRaidAfterRaid_DoesNotGrowTheScene(CancellationToken ct)
        {
            _game.SeedOverride = 4242;
            _game.NewRun();
            await UniTask.WaitForEndOfFrame(ct);

            var counts = new int[6];
            for (int pass = 0; pass < counts.Length; pass++)
            {
                _game.StartRaid();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitForEndOfFrame(ct);
                counts[pass] = RenderersAlive();
            }

            MooseRunnerFacade.Log(
                "renderers after each raid start: " + string.Join(", ", counts));

            int settled = counts[1];
            int worst = counts[^1];

            Assert.LessOrEqual(worst, settled + (settled / 10),
                $"the scene held {settled} renderers after the second raid and {worst} after "
                + $"{counts.Length}, so starting a raid leaves the last one's board behind");
        }
    }
}
