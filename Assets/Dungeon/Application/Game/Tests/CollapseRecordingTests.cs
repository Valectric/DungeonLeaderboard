using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.RaidManager;
using MooseRunner;
using MooseRunner.SessionRecorder;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Records the six seconds in which a vertical party collapses.
    /// </summary>
    /// <remarks>
    /// D43 addendum 7: under a maximal ambush the vertical party is healthier at every ten-second
    /// sample than the horizontal one, and then goes from four bodies at 80% to wiped in about six
    /// seconds. Four hypotheses have now been built from summary statistics and all four died. The
    /// one real finding of the day came from looking at a frame, so this looks at the frames.
    /// </remarks>
    public sealed class CollapseRecordingTests
    {
        /// <summary>Where the session is written.</summary>
        private const string SessionPath = ".mooserunner/Recordings/collapse";

        /// <summary>The controller under test.</summary>
        private GameController _game;

        /// <summary>Loads the play scene once.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Cleans before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            _game = new GameObject("game").AddComponent<GameController>();
        }

        /// <summary>Records a raid under constant spawning, through the collapse.</summary>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask TheCollapse_IsRecorded(CancellationToken ct)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "no main camera");

            var api = SessionRecorderFacade.Instance;
            SessionInfo info = await api.StartRecordingAsync(
                new SessionRecordingConfig(camera, outputPath: SessionPath, videoFrameRate: 30), ct);

            _game.SeedOverride = 1;
            _game.NewRun();
            _game.StartRaid();

            // The maximal ambush the measurement used: a monster at every spawner, every frame.
            float elapsed = 0f;
            while (elapsed < 30f)
            {
                Raid raid = _game.CurrentRaid;
                if (raid != null && raid.IsRunning)
                {
                    foreach (Vector2Int spawner in raid.Layout.SpawnerCells)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                await UniTask.Yield(ct);
                elapsed += Time.deltaTime;
            }

            api.StopRecording();

            MooseRunnerFacade.Log(
                $"collapse recorded to {info.SessionPath}; "
                + $"outcome {_game.CurrentRaid?.Outcome}");

            Assert.IsTrue(File.Exists(Path.Combine(info.SessionPath, "video.mp4")),
                "no video written");
        }
    }
}
