using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.SessionRecorder;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Records a raid to video so it can be watched, and inspected frame by frame afterwards.
    /// </summary>
    /// <remarks>
    /// Asked for by the author. It is also the right instrument for the change it is recording: the
    /// dungeon has just been turned to run bottom to top, and this project's own doctrine is that a
    /// layout change is the class of defect that passes every assertion and looks wrong on screen —
    /// five such defects were found here by looking at frames and none was visible to 331 green
    /// tests.
    /// <para>
    /// The session folder holds the video, per-object motion in <c>transforms.jsonl</c>, and a
    /// camera-view analysis per tagged object. <c>recording_extract_frame</c> pulls a single PNG out
    /// of it and <c>recording_extract_and_analyze</c> sends a segment to Gemini, both from the CLI
    /// without touching this file again.
    /// </para>
    /// </remarks>
    public sealed class RaidRecordingTests
    {
        /// <summary>Where the session is written, relative to the project root.</summary>
        private const string SessionPath = ".mooserunner/Recordings/vertical-raid";

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

        /// <summary>
        /// Records a whole opening raid, and leaves a session on disk to inspect.
        /// </summary>
        /// <remarks>
        /// Deliberately records the <b>raid</b> rather than the title screen: the party walking in
        /// from the bottom of the screen and climbing through the rooms is the thing that changed,
        /// and a still frame cannot show whether they move sensibly on the way.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask ARaid_IsRecordedForReview(CancellationToken ct)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "no main camera, so there is nothing to record from");

            var api = SessionRecorderFacade.Instance;
            var config = new SessionRecordingConfig(
                camera, outputPath: SessionPath, videoFrameRate: 30);

            SessionInfo info = await api.StartRecordingAsync(config, ct);
            MooseRunnerFacade.Log($"recording into {info.SessionPath}");

            _game.SeedOverride = 20260816;
            _game.NewRun();
            _game.StartRaid();

            // Long enough to show the party arriving, crossing the first room and meeting whatever
            // is in the second. The whole clock is not needed and costs a minute of wall time.
            await UniTask.WaitForSeconds(22f, cancellationToken: ct);

            api.StopRecording();

            string video = Path.Combine(info.SessionPath, "video.mp4");
            MooseRunnerFacade.Log(
                $"session at {info.SessionPath}; video exists={File.Exists(video)}; "
                + $"analysis complete={info.AnalysisComplete}");

            Assert.IsTrue(Directory.Exists(info.SessionPath),
                "the recorder wrote no session folder at all");
            Assert.IsTrue(File.Exists(video),
                $"no video.mp4 in {info.SessionPath} -- the Unity Recorder package is present in the "
                + "manifest, so this is a licence or configuration failure rather than a missing "
                + "dependency");
        }
    }
}
