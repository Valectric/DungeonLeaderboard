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

            // Refuse to ship the wrong scene. A build whose enabled scene is the template's
            // SampleScene succeeds, weighs 13MB, loads in the browser, and renders an empty blue
            // camera — a failure that looks identical to a broken renderer and costs a full rebuild
            // to discover. Checking the list is far cheaper than that round trip.
            if (!scenes.Contains(DungeonSceneBuilder.ScenePath))
            {
                Debug.LogError(
                    $"[Dungeon] '{DungeonSceneBuilder.ScenePath}' is not an enabled scene in Build "
                    + $"Settings — enabled: [{string.Join(", ", scenes)}]. "
                    + "Rebuild the play scene first (touch .dungeon-build-scene) and try again.");
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
                MakeCanvasResponsive(output);
                Debug.Log($"[Dungeon] WebGL build succeeded: "
                    + $"{summary.totalSize / (1024f * 1024f):F1} MB in {summary.totalTime}");
                return;
            }

            Debug.LogError($"[Dungeon] WebGL build {summary.result} with {summary.totalErrors} error(s)");
        }

        /// <summary>
        /// Rewrites the built page so the canvas fills whatever frame it is embedded in.
        /// </summary>
        /// <remarks>
        /// Unity's stock template hardcodes <c>canvas.style.width = "960px"</c> and a matching height
        /// for desktop. Inside itch.io's embed — configured at <b>523x293</b> here — a 960x600 canvas
        /// simply overflows and is clipped, which is why the HUD and the shop lost their edges on the
        /// published page while looking perfect in the editor.
        /// <para>
        /// Patched after the build rather than by shipping a custom WebGL template: it is one
        /// substitution against a known line, it lives in code next to the rest of the build
        /// settings, and it cannot drift out of sync with a template file nobody remembers exists.
        /// If Unity changes that line, the log says so instead of silently doing nothing.
        /// </para>
        /// <para>
        /// The game copes with any aspect ratio already — the camera fits the dungeon to its own
        /// <c>aspect</c> — so filling the frame is strictly better than letter-boxing a fixed size.
        /// </para>
        /// </remarks>
        /// <param name="output">Folder the player was built into.</param>
        private static void MakeCanvasResponsive(string output)
        {
            string page = Path.Combine(output, "index.html");
            if (!File.Exists(page))
            {
                Debug.LogWarning("[Dungeon] no index.html to make responsive");
                return;
            }

            string html = File.ReadAllText(page);
            const string fixedSize = "canvas.style.width = \"960px\";";
            if (!html.Contains(fixedSize))
            {
                Debug.LogWarning(
                    "[Dungeon] the WebGL template no longer sizes the canvas the way this patch "
                    + "expects -- the embed may be cropped again. Check Builds/index.html.");
                return;
            }

            html = html
                .Replace(fixedSize, "canvas.style.width = \"100%\";")
                .Replace("canvas.style.height = \"600px\";", "canvas.style.height = \"100%\";");

            // The container has to fill the frame too, or a canvas at 100% is 100% of nothing.
            //
            // The selectors below deliberately repeat the template's own `.unity-desktop` class.
            // Its stylesheet centres the container with `#unity-container.unity-desktop`, which is a
            // class plus an id and therefore beats a plain `#unity-container` -- so a first attempt
            // that styled the id alone was silently overridden, and the game rendered at about 40%
            // of the frame, centred in a sea of background, instead of filling it.
            html = html.Replace("</head>",
                "  <style>\n"
                + "    html, body { margin: 0; padding: 0; width: 100%; height: 100%;\n"
                + "                 overflow: hidden; background: #15101D; }\n"
                + "    #unity-container, #unity-container.unity-desktop {\n"
                + "      position: absolute; left: 0; top: 0; transform: none;\n"
                + "      width: 100%; height: 100%; }\n"
                + "    #unity-canvas, .unity-desktop #unity-canvas {\n"
                + "      width: 100%; height: 100%; display: block; background: #15101D; }\n"
                + "    #unity-footer, .unity-desktop #unity-footer { display: none; }\n"
                + "  </style>\n"
                + "</head>");

            File.WriteAllText(page, html);
            Debug.Log("[Dungeon] canvas made responsive, so the itch embed cannot crop it");
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
