using Dungeon.PartyManager;
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
            return CostPerTick(layout, mobsPerSpawner, label, null);
        }

        /// <summary>Cost of a tick with a given roster, so a grown party can be priced.</summary>
        /// <param name="layout">Dungeon to raid.</param>
        /// <param name="mobsPerSpawner">Monsters to seed at each spawner.</param>
        /// <param name="label">Name for the log line.</param>
        /// <param name="composition">Roster to send in, or null for the opening four.</param>
        /// <returns>Mean microseconds per tick.</returns>
        private static double CostPerTick(
            DungeonLayout layout, int mobsPerSpawner, string label, PartyComposition composition)
        {
            var raid = new Raid(layout, 0f, composition);
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
        /// Timed over <b>this test's own frames</b> rather than from MooseRunner's session
        /// telemetry. <c>GetMeanDeltaRealTime()</c> averages across everything the session has run,
        /// so the figure it returns depends on how many tests came before: this method measured
        /// 15.5 ms run on its own and 191 ms run at the end of the suite, on the same build, with
        /// the simulation cost logged two tests earlier unchanged at ~330 us/tick. What it was
        /// actually reporting was the multi-second scene loads and domain reloads of its
        /// predecessors, averaged in.
        /// <para>
        /// That is the project's own recurring failure again — a measurement whose name and whose
        /// subject had come apart — and it is worth stating plainly because the number it produced
        /// looked exactly like a catastrophic frame-rate regression. The telemetry is still logged
        /// below, as data.
        /// </para>
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

            // Let the loop settle before the clock starts: the frame that follows a scene build is
            // never representative of the ones after it.
            await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);

            var clock = Stopwatch.StartNew();
            int frames = 0;
            while (clock.Elapsed.TotalSeconds < 3.0)
            {
                await UniTask.NextFrame(ct);
                frames++;
            }

            clock.Stop();
            double mean = clock.Elapsed.TotalSeconds / Mathf.Max(1, frames);

            double sessionMean = MooseRunnerFacade.InstanceQuiet.GetMeanDeltaRealTime();
            double sessionWorst = MooseRunnerFacade.InstanceQuiet.GetMaxRealFixedDeltaTime();
            MooseRunnerFacade.Log(
                $"with {game.CurrentRaid.Mobs.Mobs.Count} mobs: {frames} frames in "
                + $"{clock.Elapsed.TotalSeconds:F1}s, mean {mean * 1000.0:F1} ms "
                + $"(session telemetry: mean {sessionMean * 1000.0:F1} ms, "
                + $"worst {sessionWorst * 1000.0:F1} ms)");

            Assert.Less(mean, 0.1, "the mean frame time is over 100 ms, which is 10 fps");
        }

        /// <summary>
        /// Shopping does not leave the scene fuller than it found it.
        /// </summary>
        /// <remarks>
        /// The spatial shop rebuilds the entire dungeon view after every purchase — tiles, props,
        /// sprites, and one marker per buildable cell — so a busy thirty seconds can trigger dozens
        /// of rebuilds. Unity's <c>Destroy</c> is deferred to the end of the frame, which is fine for
        /// one rebuild and would be a steady leak if the old objects were ever forgotten. On a WebGL
        /// heap that ends as a crash rather than as slowness.
        /// <para>
        /// Asserts the count is bounded rather than pinning a number, because the right number is a
        /// property of the dungeon and changes whenever a room gains furniture.
        /// </para>
        /// </remarks>
        [Test]
        public async UniTask ShoppingRepeatedly_DoesNotLeakObjects(CancellationToken ct)
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            game.OpenShopWith(9000f);
            await UniTask.Yield(ct);
            int afterFirst = game.transform.childCount;

            // Twenty purchases, one per frame, exactly as a fast player would make them.
            for (int i = 0; i < 20; i++)
            {
                Vector2Int cell = FirstBuildable(game);
                if (cell.x < 0)
                {
                    break;
                }

                Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
                game.TapShop(new Vector2(screen.x, screen.y));

                Rect[] rows = ShopLayout.PopupRows(
                    new Vector2(screen.x, Screen.height - screen.y),
                    Screen.height / 720f, Screen.width, Screen.height);
                game.TapShop(new Vector2(rows[0].center.x, Screen.height - rows[0].center.y));
                await UniTask.Yield(ct);
            }

            await UniTask.Yield(ct);
            int afterTwenty = game.transform.childCount;

            MooseRunnerFacade.Log(
                $"scene held {afterFirst} objects after one build, {afterTwenty} after twenty "
                + $"purchases, loadout {game.Loadout.Total}");

            Assert.Greater(game.Loadout.Total, 10, "the test needs to have actually bought things");
            Assert.Less(afterTwenty, afterFirst * 2,
                $"twenty purchases grew the scene from {afterFirst} to {afterTwenty} objects, "
                + "so each rebuild is leaving the last one behind");
        }

        /// <summary>
        /// Rebuilding the shop preview is fast enough to happen on every purchase.
        /// </summary>
        /// <remarks>
        /// A purchase that hitches is worse than one that is slow to appear: the shop has a
        /// thirty-second clock, and a player who loses a fifth of a second per tap loses several
        /// seconds of decision time to the renderer.
        /// </remarks>
        [Test]
        public async UniTask BuyingSomething_RedrawsPromptly(CancellationToken ct)
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            game.OpenShopWith(9000f);
            await UniTask.Yield(ct);

            var clock = Stopwatch.StartNew();
            const int purchases = 6;
            int made = 0;
            for (int i = 0; i < purchases; i++)
            {
                Vector2Int cell = FirstBuildable(game);
                Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
                game.TapShop(new Vector2(screen.x, screen.y));

                Rect[] rows = ShopLayout.PopupRows(
                    new Vector2(screen.x, Screen.height - screen.y),
                    Screen.height / 720f, Screen.width, Screen.height);
                game.TapShop(new Vector2(rows[0].center.x, Screen.height - rows[0].center.y));
                made++;
            }

            clock.Stop();
            double each = clock.Elapsed.TotalMilliseconds / made;
            MooseRunnerFacade.Log($"each purchase redraws the dungeon in {each:F0} ms");

            Assert.Less(each, 250.0,
                "a purchase takes a quarter second to appear, which eats the shop clock");
        }

        /// <summary>Finds the first tile the player could build on right now.</summary>
        /// <param name="game">Controller whose preview dungeon to search.</param>
        /// <returns>A buildable cell, or a negative cell when there is none.</returns>
        private static Vector2Int FirstBuildable(GameController game)
        {
            DungeonManager.DungeonLayout layout = game.CurrentRaid.Layout;
            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.CanBuildOn(cell))
                    {
                        return cell;
                    }
                }
            }

            return new Vector2Int(-1, -1);
        }

        /// <summary>
        /// A party of nine costs no more per tick than the budget allows.
        /// </summary>
        /// <remarks>
        /// The league grows parties to nine (D39), and <b>nothing measured what that costs</b> --
        /// <c>CostPerTick</c> built its raid with the default roster, which is the opening four, so
        /// this sweep has only ever priced a party size the game no longer only sends. That is the
        /// same gap that hid the economy effect in D42 and the wall behaviour in the M9 suite.
        /// <para>
        /// The expectation going in was that this would be expensive -- movement, AI and the energy
        /// sum are all per member, so more than doubling the party looked like the largest single
        /// increase in tick cost the game had taken. <b>Measured, it is 6%</b>: 334us at four against
        /// 355us at nine. The tick is dominated by the monsters and their pathing, not by the party,
        /// and the ramp is effectively free.
        /// </para>
        /// </remarks>
        [Test]
        public void APartyOfNine_StaysWithinTheTickBudget()
        {
            PartyComposition four = PartyComposition.Opening;
            PartyComposition nine = four.Grown(PartyComposition.MaxSize);

            double atFour = CostPerTick(DungeonLayout.BuildCorridor(), 2, "four", four);
            double atNine = CostPerTick(DungeonLayout.BuildCorridor(), 2, "nine", nine);

            MooseRunnerFacade.Log(
                $"tick cost: four {atFour:F0} us, nine {atNine:F0} us, "
                + $"{atNine / System.Math.Max(0.01, atFour):F2}x");

            Assert.Less(atNine, 2000d,
                $"a nine-strong party costs {atNine:F0} us a tick, which at fifty ticks a second is "
                + "a tenth of a frame budget spent on simulation alone");
        }

        /// <summary>
        /// The rendered loop keeps its budget with the dungeon full, not just busy.
        /// </summary>
        /// <remarks>
        /// The gap between the two halves of this file. <c>ACrowdOfMonsters</c> measures sixty
        /// monsters in the <b>simulation</b>, and <c>TheFrameLoop_KeepsItsBudget</c> measures the
        /// rendered loop with six — so the combination that actually ships, sixty monsters being
        /// <i>drawn</i>, was covered by neither. Drawing is a different cost from simulating: every
        /// mob is a sprite, a health bar and a share of the combat-number feed.
        /// <para>
        /// It is the WebGL case specifically. CLAUDE.md's doctrine is that the editor is not the
        /// shipping renderer and the browser runs a lower quality tier, so headroom measured here is
        /// the optimistic figure — which is the argument for measuring the crowded case rather than
        /// the comfortable one.
        /// </para>
        /// <para>
        /// Reachable in play: a late-season purse sits around 500 to 730 (D51), a spawn costs
        /// <c>Raid.SpawnCost</c>, and a player who presses every spawner every second is exactly the
        /// player this game invites. Sixty is not a stress figure, it is a Tuesday.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable measurement.</returns>
        [Test]
        public async UniTask TheFrameLoop_KeepsItsBudgetWithTheDungeonFull(CancellationToken ct)
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            game.StartRaid();
            await UniTask.Yield(ct);

            // Sixty in total, to match the figure the SIMULATION half of this file uses, and
            // divided across whatever spawners the opening dungeon has -- which is one. The first
            // version of this spawned twenty PER SPAWNER and reported "20 mobs", correctly, while
            // the name of the test promised a full dungeon: the label was honest and the load was
            // a third of what was intended.
            int spawners = Mathf.Max(1, game.CurrentRaid.Layout.SpawnerCells.Count);
            int each = Mathf.CeilToInt(60f / spawners);

            foreach (Vector2Int spawner in game.CurrentRaid.Layout.SpawnerCells)
            {
                for (int i = 0; i < each; i++)
                {
                    game.CurrentRaid.Mobs.Spawn(MobKind.Skeleton, spawner);
                }
            }

            await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);

            var clock = Stopwatch.StartNew();
            int frames = 0;
            while (clock.Elapsed.TotalSeconds < 3.0)
            {
                await UniTask.NextFrame(ct);
                frames++;
            }

            clock.Stop();
            double mean = clock.Elapsed.TotalSeconds / Mathf.Max(1, frames);
            int alive = game.CurrentRaid.Mobs.Mobs.Count;

            MooseRunnerFacade.Log(
                $"FULL dungeon: {alive} mobs over {spawners} spawner(s), {frames} frames in "
                + $"{clock.Elapsed.TotalSeconds:F1}s, mean {mean * 1000.0:F1} ms");

            Assert.GreaterOrEqual(alive, 50,
                $"only {alive} mobs were placed, so this measured a comfortable dungeon while "
                + "claiming to measure a full one");

            Assert.Less(mean, 0.1,
                $"a full dungeon renders at {mean * 1000.0:F0} ms a frame, under 10 fps in the "
                + "EDITOR -- and the browser runs a lower tier than this");
        }
    }
}
