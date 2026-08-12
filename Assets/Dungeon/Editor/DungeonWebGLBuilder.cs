using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dungeon.Editor
{
    /// <summary>
    /// Builds the WebGL player into the repository's <c>Builds/</c> folder, which is published to
    /// itch.io by <c>Tools/publish-itch.sh</c>.
    /// </summary>
    /// <remarks>
    /// Every setting here is a decision, and several are settings that look harmless and are not.
    /// They are carried over from the sister project, where each was paid for once already.
    /// </remarks>
    public static class DungeonWebGLBuilder
    {
        /// <summary>Where the player is written, relative to the project root.</summary>
        private const string OutputFolder = "Builds";

        /// <summary>
        /// Builds the WebGL player.
        /// </summary>
        [MenuItem("Dungeon/Build WebGL")]
        public static void BuildWebGL()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[Dungeon] No enabled scenes in Build Settings — nothing to build.");
                return;
            }

            ApplySettings();

            string output = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", OutputFolder));
            Directory.CreateDirectory(output);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Dungeon] WebGL build succeeded: "
                    + $"{summary.totalSize / (1024f * 1024f):F1} MB in {summary.totalTime}");
                return;
            }

            Debug.LogError($"[Dungeon] WebGL build {summary.result} with {summary.totalErrors} error(s)");
        }

        /// <summary>
        /// Applies the player settings the build depends on.
        /// </summary>
        private static void ApplySettings()
        {
            // Bump every build. Unity's data caching keys on this, and without a bump a returning
            // player is served the previous build out of their browser cache — forever. This is the
            // single most confusing bug to diagnose, because it only affects people who played before.
            PlayerSettings.bundleVersion = "0.1." + DateTime.Now.ToString("yyMMddHHmm");

            // GitHub Pages cannot send Content-Encoding, and some itch configurations are similar,
            // so the loader has to be able to decompress the payload itself.
            PlayerSettings.WebGL.decompressionFallback = true;

            // Never None. It turns every runtime crash into "The error was: undefined", which is
            // indistinguishable from the game simply not starting.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // High stripping plus stripEngineCode removes reflection-reached URP code and shows up as
            // "shader not supported" and a frozen player. Minimal is not laziness, it is the level
            // that works.
            PlayerSettings.SetManagedStrippingLevel(
                UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = false;

            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);

            Debug.Log($"[Dungeon] Building version {PlayerSettings.bundleVersion}");
        }
    }
}
