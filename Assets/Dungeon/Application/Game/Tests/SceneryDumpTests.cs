using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Names every sprite the dungeon puts on screen, so a visual fault can be traced to an object.
    /// </summary>
    /// <remarks>
    /// Written for a specific defect and kept because the capability was missing. Two pale bands
    /// render across the top and bottom of every room in world space, and identifying them by reading
    /// code and measuring PNGs failed: doors, decoration props, the generated glow, the wall tiles'
    /// own lit cap, the hints and the reverted rim highlight were each ruled out, and no sprite in
    /// <c>Resources</c> matches the measured colour at all.
    /// <para>
    /// Every one of those was a guess checked from the outside. A renderer has a name, bounds, colour
    /// and sorting order at runtime, and asking it directly is both cheaper and conclusive — the
    /// project had simply never had a way to ask.
    /// </para>
    /// </remarks>
    public sealed class SceneryDumpTests
    {
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
        /// Renders the camera to a PNG, so the pixels and the renderer list are the same frame.
        /// </summary>
        /// <remarks>
        /// Deliberately the camera rather than <c>ScreenCapture</c>: IMGUI must stay out of the
        /// image, or a HUD element over the dungeon becomes another candidate to rule out.
        /// </remarks>
        /// <param name="camera">Camera to render.</param>
        /// <param name="name">File name, without extension.</param>
        private static void Capture(Camera camera, string name)
        {
            string directory = Path.Combine(
                UnityEngine.Application.dataPath, "..", "Screenshots");
            Directory.CreateDirectory(directory);

            var target = new RenderTexture(Screen.width, Screen.height, 24);
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;

            string path = Path.Combine(directory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            MooseRunnerFacade.Log($"captured {path} ({Screen.width}x{Screen.height})");
        }

        /// <summary>
        /// Every renderer wider than a couple of cells is named, with its colour and order.
        /// </summary>
        /// <remarks>
        /// The bands measured 353 screen pixels across, about 5.5 cells, which is far wider than any
        /// tile, prop or door and is the property worth filtering on. Anything legitimately that wide
        /// — the entrance approach, a backdrop — will show up too and is easy to discount by name.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask EveryWideRenderer_IsNamed(CancellationToken ct)
        {
            _game.Advance();
            await UniTask.Yield(ct);

            for (int press = 0; press < 4 && !_game.IsRaiding; press++)
            {
                _game.Advance();
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(_game.IsRaiding, "the raid never started, so there is no scenery to dump");
            await UniTask.NextFrame(ct);

            SpriteRenderer[] all = Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsSortMode.None);
            MooseRunnerFacade.Log($"{all.Length} sprite renderers in the scene");

            // A cell is one world unit at this project's 64 pixels per unit, so "wider than two
            // cells" is simply width > 2.
            List<SpriteRenderer> wide = all
                .Where(r => r.enabled && r.bounds.size.x > 2f)
                .OrderByDescending(r => r.bounds.size.x)
                .ToList();

            MooseRunnerFacade.Log($"--- {wide.Count} renderers wider than two cells:");
            foreach (SpriteRenderer r in wide)
            {
                Color c = r.color;
                MooseRunnerFacade.Log(
                    $"  {r.name,-26} sprite={r.sprite?.name ?? "<none>"} "
                    + $"size=({r.bounds.size.x:F2},{r.bounds.size.y:F2}) "
                    + $"centre=({r.bounds.center.x:F2},{r.bounds.center.y:F2}) "
                    + $"rgba=({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2}) order={r.sortingOrder}");
            }

            // Pale is the other half of the signature: the bands measured rgb ~ (140,134,166) against
            // walls near 30, so anything light in this dungeon is worth naming whatever its width.
            List<SpriteRenderer> pale = all
                .Where(r => r.enabled && (r.color.r + r.color.g + r.color.b) / 3f > 0.5f)
                .ToList();

            MooseRunnerFacade.Log($"--- {pale.Count} renderers tinted lighter than mid grey:");
            foreach (SpriteRenderer r in pale.Take(30))
            {
                Color c = r.color;
                MooseRunnerFacade.Log(
                    $"  {r.name,-26} sprite={r.sprite?.name ?? "<none>"} "
                    + $"size=({r.bounds.size.x:F2},{r.bounds.size.y:F2}) "
                    + $"centre=({r.bounds.center.x:F2},{r.bounds.center.y:F2}) "
                    + $"rgba=({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2}) order={r.sortingOrder}");
            }

            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "no camera to map screen coordinates with");

            // ONE FRAME, PHOTOGRAPHED HERE. The previous version of this test mapped the band's
            // coordinates out of a screenshot another fixture had taken, which silently assumed both
            // runs framed the camera identically. They did not, and it produced a confident wrong
            // answer -- it named a wall tile whose brightest pixel is 63 as the source of a band
            // measuring 100. Capturing in this test removes the assumption entirely.
            Capture(camera, "09-scenery-dump");
            MooseRunnerFacade.Log(
                $"camera ortho={camera.orthographicSize:F3} "
                + $"pos=({camera.transform.position.x:F2},{camera.transform.position.y:F2}) "
                + $"screen={Screen.width}x{Screen.height} "
                + $"px per cell={Screen.height / (2f * camera.orthographicSize):F2}");

            // A named tile's exact screen rect, so the PNG can be indexed with no arithmetic of mine
            // in between.
            foreach (string named in new[] { "tile_3_6", "tile_3_0", "tile_3_3" })
            {
                SpriteRenderer tile = all.FirstOrDefault(r => r.name == named);
                if (tile == null)
                {
                    MooseRunnerFacade.Log($"  {named} not present");
                    continue;
                }

                Vector3 low = camera.WorldToScreenPoint(tile.bounds.min);
                Vector3 high = camera.WorldToScreenPoint(tile.bounds.max);
                MooseRunnerFacade.Log(
                    $"  {named,-10} sprite={tile.sprite?.name} screen x {low.x:F0}..{high.x:F0} "
                    + $"y {low.y:F0}..{high.y:F0}  (png rows {Screen.height - high.y:F0}.."
                    + $"{Screen.height - low.y:F0})");
            }

            foreach ((string label, Vector2 point) in new[]
            {
                ("top band   ", new Vector2(675f, 720f - 124f)),
                ("bottom band", new Vector2(675f, 720f - 539f))
            })
            {
                Vector3 world = camera.ScreenToWorldPoint(new Vector3(point.x, point.y, 10f));
                var hits = all
                    .Where(r => r.enabled && r.bounds.Contains(new Vector3(world.x, world.y,
                        r.bounds.center.z)))
                    .OrderByDescending(r => r.sortingOrder)
                    .ToList();

                MooseRunnerFacade.Log(
                    $"{label} screen({point.x:F0},{point.y:F0}) -> world({world.x:F2},{world.y:F2}) "
                    + $"cell(~{Mathf.RoundToInt(world.x)},{Mathf.RoundToInt(world.y)}) "
                    + $"{hits.Count} renderer(s):");
                foreach (SpriteRenderer r in hits)
                {
                    MooseRunnerFacade.Log(
                        $"    {r.name,-24} sprite={r.sprite?.name ?? "<none>"} order={r.sortingOrder}");
                }
            }

            Assert.Greater(all.Length, 0, "the dungeon drew nothing at all");
        }
    }
}
