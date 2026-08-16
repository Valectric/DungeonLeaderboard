using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.PartyManager;
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

            // THE IRONCLADS, because it is the roster that discriminates: the measurement has it
            // surviving horizontally with 434 harvested and dying vertically. If this harness is
            // now the same experiment, the horizontal recording must survive too.
            PartyComposition ironclads = null;
            foreach (PartyComposition c in PartyComposition.All)
            {
                if (c.Name == "THE IRONCLADS") { ironclads = c; }
            }

            _game.SeedOverride = 1;
            _game.NewRun();
            _game.NextParty = ironclads;
            _game.StartRaid();

            // Spawns on a FIXED interval, not per frame, and the difference is not pedantry.
            //
            // This loop used to spawn once per rendered frame -- about 60 a second against the
            // measurement's 50, since that ticks a bare Raid at a fixed 0.02s. Twenty percent more
            // monsters, and it was enough to turn a survivor into a corpse: THE IRONCLADS survives
            // horizontally in the measurement with 434 harvested and WIPED here. So the recording
            // was a harsher game than the one the numbers describe, and no frame from it could be
            // read against them. See D43 addendum 10.
            const float spawnInterval = 0.02f;
            float elapsed = 0f;
            float sinceSpawn = 0f;

            while (elapsed < 30f)
            {
                Raid raid = _game.CurrentRaid;
                sinceSpawn += Time.deltaTime;

                while (sinceSpawn >= spawnInterval)
                {
                    if (raid != null && raid.IsRunning)
                    {
                        foreach (Vector2Int spawner in raid.Layout.SpawnerCells)
                        {
                            raid.SpawnMob(spawner);
                        }
                    }

                    sinceSpawn -= spawnInterval;
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
