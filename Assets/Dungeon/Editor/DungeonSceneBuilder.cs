using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dungeon.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungeon.Editor
{
    /// <summary>
    /// Generates the play scene from code and registers it in Build Settings.
    /// </summary>
    /// <remarks>
    /// The scene holds only a camera and a <see cref="GameController"/>; everything else is built at
    /// runtime. That is deliberate. Serialized scene values silently outrank code defaults, so a
    /// hand-dressed scene drifts away from the simulation and the drift is invisible until a build
    /// behaves differently from a test. Regenerating the scene from one function keeps a single
    /// source of truth.
    /// </remarks>
    public static class DungeonSceneBuilder
    {
        /// <summary>Where the generated play scene lives.</summary>
        public const string ScenePath = "Assets/Dungeon/Application/Game/Scenes/Raid.unity";

        /// <summary>
        /// Rebuilds the play scene, overwriting whatever was there.
        /// </summary>
        [MenuItem("Tools/Dungeon/Rebuild Play Scene")]
        public static void BuildScene()
        {
            string directory = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.625f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x15, 0x10, 0x1D, 0xFF);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            var controller = new GameObject("GameController");
            controller.AddComponent<GameController>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            Debug.Log($"[Dungeon] Play scene rebuilt at {ScenePath}");
        }

        /// <summary>
        /// Makes the play scene the first (and only) enabled scene in Build Settings.
        /// </summary>
        /// <remarks>
        /// A WebGL build with no scene registered produces a black page rather than an error, which
        /// is a slow thing to diagnose from a browser tab.
        /// </remarks>
        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(ScenePath, true)
            };

            scenes.AddRange(EditorBuildSettings.scenes
                .Where(existing => existing.path != ScenePath)
                .Select(existing => new EditorBuildSettingsScene(existing.path, false)));

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
