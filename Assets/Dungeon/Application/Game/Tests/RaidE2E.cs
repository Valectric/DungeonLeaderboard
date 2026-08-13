using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// End-to-end run of the shipped play scene, with frames written to <c>Screenshots/</c>.
    /// </summary>
    /// <remarks>
    /// Ordered and chained: each step depends on the state the previous one left behind, so this
    /// fixture must be run with <c>--class</c> or <c>--assembly</c> and never <c>--method</c>.
    /// <para>
    /// The screenshots are the point. Green assertions proved nothing about the sister project's
    /// worst bugs -- a rate that was never met, art that shipped magenta, a scene that rendered
    /// black -- and every one of those was obvious in a single rendered frame. These tests assert
    /// what they can and then photograph the rest.
    /// </para>
    /// </remarks>
    public sealed class RaidE2E
    {
        /// <summary>Where frames are written for a human (or an agent) to look at.</summary>
        private static string ShotDirectory =>
            Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");

        /// <summary>The controller in the loaded scene.</summary>
        private static GameController Controller => Object.FindFirstObjectByType<GameController>();

        /// <summary>Renders the active camera to a PNG so the frame can actually be inspected.</summary>
        /// <param name="name">File name stem.</param>
        private static void Capture(string name)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "the scene must ship with a camera");

            Directory.CreateDirectory(ShotDirectory);
            var target = new RenderTexture(1280, 720, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            camera.targetTexture = previous;

            string path = Path.Combine(ShotDirectory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            MooseRunnerFacade.Log($"captured {path}");
        }

        /// <summary>Loads the real shipped scene. Nothing is built or wired by the test.</summary>
        [Test, Order(0)]
        public async UniTask Step0_LoadsTheShippedScene(CancellationToken ct)
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid", forceReload: true);
            await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);

            Assert.IsNotNull(Controller, "the play scene must contain a GameController");
            Assert.IsNotNull(Controller.CurrentRaid, "the controller must start a raid on Awake");
        }

        /// <summary>The dungeon builds itself, with tiles, doors and a party on screen.</summary>
        [Test, Order(1)]
        public async UniTask Step1_BuildsTheDungeon(CancellationToken ct)
        {
            await UniTask.WaitForSeconds(0.4f, cancellationToken: ct);

            var renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
            MooseRunnerFacade.Log($"{renderers.Length} sprite renderers in the scene");
            Assert.Greater(renderers.Length, 40, "the tiled dungeon should be on screen");

            foreach (SpriteRenderer renderer in renderers)
            {
                Assert.IsNotNull(renderer.sprite,
                    $"'{renderer.name}' has no sprite -- a Resources path is wrong");
            }

            Capture("01-raid-opening");
        }

        /// <summary>
        /// The raid runs: the clock falls and the party advances. Read-only -- the test never drives
        /// the simulation, it watches the shipped one.
        /// </summary>
        [Test, Order(2)]
        public async UniTask Step2_ClockRunsAndPartyAdvances(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            float startTime = raid.TimeRemaining;
            Vector2Int startCell = raid.Party.Cell;

            await UniTask.WaitForSeconds(3f, cancellationToken: ct);

            MooseRunnerFacade.Log($"clock {startTime:F1} -> {raid.TimeRemaining:F1}, " +
                                  $"party {startCell} -> {raid.Party.Cell}");
            Assert.Less(raid.TimeRemaining, startTime, "the clock must run");
            Assert.AreNotEqual(startCell, raid.Party.Cell, "an unopposed party must advance");

            Capture("02-party-advancing");
        }

        /// <summary>
        /// A spawned mob engages the party and the energy rate climbs off its idle floor. This is
        /// the loop the whole game is built on, verified in the shipped scene rather than a stand.
        /// </summary>
        [Test, Order(3)]
        public async UniTask Step3_EngagementLiftsTheRate(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            float idleRate = raid.CurrentRate;

            // Clicked, not called. An earlier version invoked raid.SpawnMob directly and so kept
            // passing while every verb in the shipped game was throwing on the input layer.
            Camera camera = Camera.main;
            Vector2Int spawner = raid.Layout.SpawnerCells[0];
            Controller.ClickAt(camera.WorldToScreenPoint(DungeonView.CellToWorld(spawner)));
            await UniTask.NextFrame(ct);
            Assert.Greater(raid.Mobs.Mobs.Count, 0, "clicking the spawner should have spawned a mob");

            // Wait for contact rather than a fixed sleep. The mob is bound to its own room and the
            // party now walks deliberately slowly, so how long it takes them to meet depends on
            // pacing constants -- a hard-coded delay here would break every time those are tuned.
            for (int i = 0; i < 40 && raid.CurrentRate <= idleRate * 5f && raid.IsRunning; i++)
            {
                await UniTask.WaitForSeconds(1f, cancellationToken: ct);
            }

            MooseRunnerFacade.Log($"rate {idleRate:F2}/s -> {raid.CurrentRate:F2}/s, " +
                                  $"harvested {raid.EnergyHarvested:F1}");
            Assert.Greater(raid.CurrentRate, idleRate * 5f,
                "engaging the party must lift the rate far above idle");

            Capture("03-engaged");
        }

        /// <summary>The raid reaches an end state within its sixty seconds and stops earning.</summary>
        [Test, Order(4)]
        public async UniTask Step4_RaidEnds(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            for (int i = 0; i < 70 && raid.IsRunning; i++)
            {
                await UniTask.WaitForSeconds(1f, cancellationToken: ct);
            }

            MooseRunnerFacade.Log($"outcome {raid.Outcome}, harvested {raid.EnergyHarvested:F1}");
            Assert.IsFalse(raid.IsRunning, "a raid must end within its own clock");
            Assert.AreNotEqual(RaidOutcome.InProgress, raid.Outcome);

            Capture("04-raid-over");
        }
    }
}
