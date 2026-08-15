using System.Collections.Generic;
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

            // The band was measured in a 1280x720 screenshot at x 499..851, y 112..136 counting from
            // the TOP. Unity's screen space counts from the bottom, so that is y 584..608 here.
            // Naming whatever covers that point is the whole purpose of this test.
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "no camera to map screen coordinates with");

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
