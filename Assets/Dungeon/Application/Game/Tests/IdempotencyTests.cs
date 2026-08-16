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
    /// Checks that running the game repeatedly leaves the scene where it started.
    /// </summary>
    /// <remarks>
    /// TestingGuidelines rule 16 asks for this in as many words — <i>"running the same test three
    /// times in a row must end in the same scene state every time… the state after run 3 must equal
    /// the state after run 1"</i> — and nothing in the project did it. Every suite here has been run
    /// dozens of times without anyone counting what was left behind.
    /// <para>
    /// The failure it guards is quiet and cumulative: a leaked <c>DontDestroyOnLoad</c> object, a
    /// static registry still holding spawns, an event subscription re-creating what teardown removed.
    /// None of those fail a test. They make the twentieth run slower than the first, which is
    /// invisible in a suite and obvious in a long play session — and this game is a season of twenty
    /// raids.
    /// </para>
    /// </remarks>
    public sealed class IdempotencyTests
    {
        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Cleans before each test, as every suite here does.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>Everything currently alive, counted the way a leak would show.</summary>
        /// <returns>Total objects, and how many survive scene teardown.</returns>
        private static (int total, int persistent) Census()
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int persistent = 0;
            foreach (GameObject go in all)
            {
                // DontDestroyOnLoad objects report a scene named for it; those are the ones that
                // accumulate across runs, because normal teardown never touches them.
                if (go.scene.name == "DontDestroyOnLoad")
                {
                    persistent++;
                }
            }

            return (all.Length, persistent);
        }

        /// <summary>
        /// Three runs of the whole opening sequence leave the same scene behind.
        /// </summary>
        /// <remarks>
        /// Self-contained rather than relying on the runner to repeat a method, so it is deterministic
        /// and can fail on its own. Each pass builds a controller, plays into a raid, and tears down
        /// exactly as <c>[SetUp]</c> does between tests.
        /// <para>
        /// The assertion compares run 3 against run 1, which is what the rule asks. Run 1 is allowed
        /// to differ from the others — first-time allocations, a lazily-built glow sprite, the
        /// <c>TestRunnerHelper</c> singleton — and it is growth <i>after</i> that which means a leak.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask ThreeRunsOfTheOpening_LeaveTheSceneWhereItStarted(CancellationToken ct)
        {
            var census = new List<(int total, int persistent)>();

            for (int run = 1; run <= 3; run++)
            {
                DoNotDestroyOnTeardown.CleanSceneImmediate();
                await UniTask.NextFrame(ct);

                var game = new GameObject($"game_{run}").AddComponent<GameController>();
                game.Advance();
                await UniTask.Yield(ct);

                for (int press = 0; press < 4 && !game.IsRaiding && !game.IsShopping; press++)
                {
                    game.Advance();
                    await UniTask.Yield(ct);
                }

                // A few frames of an actual raid, so anything the view or the audio allocates has
                // been allocated before the count is taken.
                for (int frame = 0; frame < 6; frame++)
                {
                    await UniTask.NextFrame(ct);
                }

                Object.DestroyImmediate(game.gameObject);
                DoNotDestroyOnTeardown.CleanSceneImmediate();
                await UniTask.NextFrame(ct);

                (int total, int persistent) = Census();
                census.Add((total, persistent));
                MooseRunnerFacade.Log(
                    $"run {run}: {total} objects left, {persistent} of them DontDestroyOnLoad");
            }

            MooseRunnerFacade.Log(
                $"census: {string.Join(" -> ", census.ConvertAll(c => c.total.ToString()))} objects, "
                + $"{string.Join(" -> ", census.ConvertAll(c => c.persistent.ToString()))} persistent");

            Assert.AreEqual(census[0].total, census[2].total,
                $"the scene held {census[0].total} objects after the first run and "
                + $"{census[2].total} after the third, so something is not being cleaned up");

            Assert.AreEqual(census[0].persistent, census[2].persistent,
                $"DontDestroyOnLoad went from {census[0].persistent} to {census[2].persistent} "
                + "objects across three runs, which is the leak that survives every teardown");
        }
    }
}
