using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.RaidManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Measures what the game costs, in the shapes a jam entry actually meets.
    /// </summary>
    /// <remarks>
    /// The target is a WebGL build on whatever machine a voter happens to have, quite possibly a
    /// phone. Two things in here are super-linear and both are reachable through ordinary play: mob
    /// separation compares every monster against every other every tick, and the view rebuilds the
    /// whole dungeon on each raid. Neither has ever been measured.
    /// <para>
    /// The numbers are deliberately generous. This is a floor that catches a regression of an order
    /// of magnitude, not a benchmark — a strict budget measured in the editor would fail for reasons
    /// that have nothing to do with the shipped game.
    /// </para>
    /// </remarks>
    public sealed class PerformanceSweepTests
    {
        /// <summary>Simulates a raid headlessly and reports the wall-clock cost per tick.</summary>
        /// <param name="layout">Dungeon to raid.</param>
        /// <param name="mobsPerSpawner">How many monsters to pile on each spawner.</param>
        /// <param name="label">Name for the log line.</param>
        /// <returns>Microseconds per simulated tick.</returns>
        private static double CostPerTick(DungeonLayout layout, int mobsPerSpawner, string label)
        {
            var raid = new Raid(layout);
            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                for (int i = 0; i < mobsPerSpawner; i++)
                {
                    raid.Mobs.Spawn(MobKind.Skeleton, spawner);
                }
            }

            var clock = Stopwatch.StartNew();
            int ticks = 0;
            while (raid.IsRunning && ticks < 3000)
            {
                raid.Tick(0.02f);
                ticks++;
            }

            clock.Stop();
            double perTick = clock.Elapsed.TotalMilliseconds * 1000.0 / Mathf.Max(1, ticks);
            MooseRunnerFacade.Log(
                $"{label}: {raid.Mobs.Mobs.Count} mobs, {ticks} ticks, "
                + $"{perTick:F1} us/tick, {clock.ElapsedMilliseconds} ms total");
            return perTick;
        }

        /// <summary>
        /// A normal raid simulates far faster than real time.
        /// </summary>
        /// <remarks>
        /// The simulation runs on the physics clock at 50 ticks a second, so a tick must cost well
        /// under 20 ms or the game cannot keep up with itself before a single sprite is drawn.
        /// </remarks>
        [Test]
        public void ANormalRaid_IsCheapToSimulate()
        {
            double perTick = CostPerTick(DungeonLayout.BuildCorridor(), 2, "normal raid");
            Assert.Less(perTick, 2000.0,
                "a normal raid tick costs more than 2 ms, which is a tenth of the frame budget");
        }

        /// <summary>
        /// The worst dungeon the shop can build stays affordable.
        /// </summary>
        /// <remarks>
        /// Separation is O(n squared) in living monsters, and a season of purchases plus repeated
        /// spawning is how a player reaches thirty of them in one room.
        /// </remarks>
        [Test]
        public void TheWorstBuildableRaid_StaysAffordable()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(
                roomCount: 5, extraSlimeSpawners: 8, extraSkeletonSpawners: 8,
                extraTraps: 10, chests: 6);

            double perTick = CostPerTick(layout, 3, "worst buildable raid");
            Assert.Less(perTick, 20000.0,
                "the worst raid the shop can build exceeds a whole 20 ms frame per simulation tick");
        }

        /// <summary>
        /// A pathological crowd degrades gracefully rather than falling off a cliff.
        /// </summary>
        /// <remarks>
        /// Sixty monsters is far beyond anything the economy allows, and it exists to show the shape
        /// of the curve. If this is only a few times the cost of a normal raid, the quadratic term is
        /// not the thing that will bite.
        /// </remarks>
        [Test]
        public void ACrowd_DegradesGracefully()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            double normal = CostPerTick(DungeonLayout.BuildCorridor(), 2, "baseline");
            double crowd = CostPerTick(layout, 30, "sixty monsters");

            MooseRunnerFacade.Log($"crowd costs {crowd / System.Math.Max(0.001, normal):F1}x baseline");
            Assert.Less(crowd, 40000.0, "sixty monsters costs more than two frames per tick");
        }

        /// <summary>
        /// Building the view for a raid is fast enough not to stall the transition.
        /// </summary>
        /// <remarks>
        /// The whole dungeon is rebuilt between raids, and a player meets that a dozen times a run.
        /// A long hitch there reads as the game freezing at exactly the moment it should feel snappy.
        /// </remarks>
        [Test]
        public async UniTask BuildingTheView_DoesNotStall(CancellationToken ct)
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            var clock = Stopwatch.StartNew();
            const int rebuilds = 8;
            for (int i = 0; i < rebuilds; i++)
            {
                game.StartRaid();
            }

            clock.Stop();
            double each = clock.Elapsed.TotalMilliseconds / rebuilds;
            MooseRunnerFacade.Log($"rebuilding the view costs {each:F0} ms, over {rebuilds} raids");

            Assert.Less(each, 400.0,
                "rebuilding the dungeon takes most of half a second, which reads as a freeze");
        }

        /// <summary>The frame loop itself stays inside a sensible budget during a raid.</summary>
        /// <remarks>
        /// Uses MooseRunner's own timing telemetry, which measures wall clock between FixedUpdates
        /// rather than anything the test controls.
        /// </remarks>
        [Test]
        public async UniTask TheFrameLoop_KeepsItsBudget(CancellationToken ct)
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            game.StartRaid();
            await UniTask.Yield(ct);

            // Fill the dungeon, then let the real loop run for a few seconds.
            foreach (Vector2Int spawner in game.CurrentRaid.Layout.SpawnerCells)
            {
                for (int i = 0; i < 6; i++)
                {
                    game.CurrentRaid.Mobs.Spawn(MobKind.Skeleton, spawner);
                }
            }

            await UniTask.WaitForSeconds(4f, cancellationToken: ct);

            double worst = MooseRunnerFacade.InstanceQuiet.GetMaxRealFixedDeltaTime();
            double mean = MooseRunnerFacade.InstanceQuiet.GetMeanDeltaRealTime();
            MooseRunnerFacade.Log(
                $"with {game.CurrentRaid.Mobs.Mobs.Count} mobs: mean {mean * 1000.0:F1} ms, "
                + $"worst {worst * 1000.0:F1} ms between fixed updates");

            Assert.Less(mean, 0.1, "the mean frame time is over 100 ms, which is 10 fps");
        }
    }
}
