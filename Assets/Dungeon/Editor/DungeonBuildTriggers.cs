using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dungeon.Editor
{
    /// <summary>
    /// Watches for sentinel files at the project root and runs the matching editor action, so an
    /// agent with only a shell can drive the editor.
    /// </summary>
    /// <remarks>
    /// <c>touch .dungeon-build-webgl</c> and within ~30 seconds the build starts. The alternative —
    /// asking a human to click a menu item — is what makes an agent unable to finish anything.
    /// <para>
    /// <b>The ordering trap:</b> a sentinel cannot be acted on while the editor is in Play Mode,
    /// which is exactly where every test run leaves it. This poller therefore <b>defers</b> rather
    /// than consuming the sentinel — an earlier version consumed it either way, so the action
    /// silently never happened while the loop looked green. Even with the deferral, the correct
    /// order is <c>force-recompile</c> first (which exits Play Mode) and <i>then</i> touch the
    /// sentinel, because touching first means the currently-loaded, possibly stale assembly is what
    /// runs.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    public static class DungeonBuildTriggers
    {
        /// <summary>Sentinel that triggers a WebGL build.</summary>
        private const string BuildWebGL = ".dungeon-build-webgl";

        /// <summary>Sentinel that triggers a scene rebuild, once there is a scene builder.</summary>
        private const string BuildScene = ".dungeon-build-scene";

        /// <summary>Seconds between checks. The poll is a directory stat, so this is nearly free.</summary>
        private const double IntervalSeconds = 2d;

        /// <summary>When the next check is due, on the editor's clock.</summary>
        private static double _nextCheck;

        /// <summary>
        /// Subscribes the poller when the editor loads.
        /// </summary>
        static DungeonBuildTriggers()
        {
            EditorApplication.update += Poll;
        }

        /// <summary>
        /// Checks for sentinels and runs whatever they ask for.
        /// </summary>
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextCheck) return;
            _nextCheck = EditorApplication.timeSinceStartup + IntervalSeconds;

            // Deferred, not consumed: acting now would fail, and eating the sentinel would hide it.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            TryRun(BuildWebGL, DungeonWebGLBuilder.BuildWebGL);
            TryRun(BuildScene, DungeonSceneBuilder.BuildScene);
        }

        /// <summary>
        /// Runs an action if its sentinel is present, and removes the sentinel first so a failing
        /// action cannot loop forever.
        /// </summary>
        /// <param name="sentinel">File name to look for at the project root.</param>
        /// <param name="action">What to do when it is there.</param>
        private static void TryRun(string sentinel, Action action)
        {
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", sentinel));
            if (!File.Exists(path)) return;

            try
            {
                File.Delete(path);
            }
            catch (IOException error)
            {
                Debug.LogWarning($"[Dungeon] Could not clear {sentinel}: {error.Message}");
                return;
            }

            Debug.Log($"[Dungeon] {sentinel} picked up");
            action();
        }
    }
}
