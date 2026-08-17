using System.IO;
using System.Linq;
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

        /// <summary>The controller in the loaded scene.</summary>
        private static GameController Controller => Object.FindFirstObjectByType<GameController>();

        /// <summary>Renders the active camera to a PNG so the frame can actually be inspected.</summary>
        /// <param name="name">File name stem.</param>
        private static void CaptureTo(string name)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "the scene must ship with a camera");

            Directory.CreateDirectory(Frames.Directory);
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

            string path = Path.Combine(Frames.Directory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            MooseRunnerFacade.Log($"captured {path}");
        }

        /// <summary>
        /// Photographs the whole screen, HUD included.
        /// </summary>
        /// <remarks>
        /// <see cref="Capture"/> renders the camera into a RenderTexture, which draws the dungeon and
        /// <b>nothing else</b>. Every piece of this game's UI is IMGUI — the energy figure, the
        /// pulsing rate, the clock, the standings strip, the shop — and IMGUI is drawn by the
        /// player loop, not by <c>camera.Render()</c>. So every frame this project has ever
        /// inspected has been a picture of the dungeon with the interface cropped out, while
        /// CLAUDE.md said the Look tests "capture the HUD and the dungeon".
        /// <para>
        /// That is the project's own recurring failure in miniature: the check existed, was believed,
        /// and was measuring something narrower than its name. <c>CaptureScreenshotAsTexture</c>
        /// grabs the composited frame, so what lands on disk is what a player sees.
        /// </para>
        /// </remarks>
        /// <param name="name">File name stem.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable capture.</returns>
        private static async UniTask CaptureScreen(string name, CancellationToken ct)
        {
            // Must be after everything has drawn for the frame, or the grab races the UI.
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(Frames.Directory);
            string path = Path.Combine(Frames.Directory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());

            MooseRunnerFacade.Log($"captured {path} ({image.width}x{image.height}, HUD included)");
            Object.DestroyImmediate(image);
        }

        /// <summary>Loads the real shipped scene. Nothing is built or wired by the test.</summary>
        [Test, Order(0)]
        public async UniTask Step0_LoadsTheShippedScene(CancellationToken ct)
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid", forceReload: true);
            await UniTask.WaitForSeconds(0.5f, cancellationToken: ct);

            // Half a second in, the loading screen is still up and its party is mid-stride. This is
            // the only chance to photograph it -- it hands over to the standings at two seconds --
            // and photographing it is the point: the walk frames are loaded by NAME, and a wrong
            // name draws nothing at all rather than failing, so a green test proves very little.
            await CaptureScreen("00-loading", ct);

            Assert.IsNotNull(Controller, "the play scene must contain a GameController");
            Assert.IsNotNull(Controller.League, "the game must open on a league");
            Assert.IsNotNull(Controller.CurrentRaid, "the dungeon is built behind the standings");

            // The game now opens on the standings, which are the title screen, and a key press
            // starts the raid. Keyboard input cannot be synthesised here -- the project's testing
            // doctrine forbids raw Input System device events as too fragile -- so the raid is
            // started through the same public entry point the key press uses.
            Controller.StartRaid();
            await UniTask.WaitForSeconds(0.2f, cancellationToken: ct);
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

            CaptureTo("01-raid-opening");
            await CaptureScreen("01-raid-opening-hud", ct);
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

            CaptureTo("02-party-advancing");
            await CaptureScreen("02-party-advancing-hud", ct);
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
            Vector2 pit = camera.WorldToScreenPoint(DungeonView.CellToWorld(spawner));

            Controller.ClickAt(pit);
            await UniTask.NextFrame(ct);
            Assert.Greater(raid.Mobs.Mobs.Count, 0, "clicking the spawner should have spawned a mob");

            // Wait for contact rather than a fixed sleep, and keep tapping the pit while waiting.
            // The run now opens on a single room, so a party nobody detains is out of the dungeon in
            // about twelve seconds -- one slime, spawned once, can be killed and walked past well
            // inside that. A player holding a party in the opening room taps the pit repeatedly, so
            // this does too, in short steps rather than second-long sleeps.
            //
            // The PEAK is what gets asserted, not the reading at the end of the loop. The rate is a
            // curve that moves every tick with how hurt the party is and how many monsters are on
            // them, and sampling it once catches whichever moment the loop happened to stop in --
            // twice in a row that was a lull just after a slime died.
            float peak = idleRate;
            for (int i = 0; i < 80 && peak <= idleRate * 1.5f && raid.IsRunning; i++)
            {
                if (raid.Mobs.Living.Count() < 2 && raid.TotalEnergy > Raid.SpawnCost)
                {
                    Controller.ClickAt(pit);
                }

                await UniTask.WaitForSeconds(0.25f, cancellationToken: ct);
                peak = Mathf.Max(peak, raid.CurrentRate);
            }

            MooseRunnerFacade.Log($"rate {idleRate:F2}/s idle -> {peak:F2}/s peak, " +
                                  $"harvested {raid.EnergyHarvested:F1}");

            // Half again over idle, not the five times this asked for when the game opened on three
            // rooms of skeletons. The opening dungeon is one room and one SLIME pit -- the weak
            // monster, deliberately, because round one is where a wipe teaches a new player the
            // wrong lesson. Slimes hold a party and barely wound it, and nearly all of the rate is
            // in the wound curve, so the opening board cannot reach the old figure and should not.
            // The multiple raids' worth of harvest that comes out of it is measured in
            // StarterDungeonTests, which compares playing against doing nothing.
            Assert.Greater(peak, idleRate * 1.5f,
                "engaging the party must visibly lift the rate off its idle floor");

            CaptureTo("03-engaged");
            await CaptureScreen("03-engaged-hud", ct);
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

            CaptureTo("04-raid-over");

            // The stars land one at a time over about a second, and the photograph was racing them:
            // measured off the PNG, all five came out at an identical (60,54,68), because none had
            // landed yet. A picture of the payoff screen taken before the payoff happens cannot show
            // whether the payoff works.
            await UniTask.WaitForSeconds(1.6f, cancellationToken: ct);
            await CaptureScreen("04-raid-over-hud", ct);
        }
    }
}
