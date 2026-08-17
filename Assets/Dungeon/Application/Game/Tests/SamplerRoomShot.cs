using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Builds one dungeon containing every piece the tileset needs, and photographs it.
    /// </summary>
    /// <remarks>
    /// The repaint-and-slice loop works — a room rendered at exactly 64 pixels per cell, repainted
    /// by a generator, cut back up at coordinates the game recorded — but a single room only
    /// exercises <b>five</b> of the sixteen wall shapes: the four runs whose open side faces each
    /// direction, and the enclosed one. Corners, stubs and free-standing blocks never appear, so
    /// they can never be cut.
    /// <para>
    /// This is a sampler rather than a playable dungeon: a grid carved by hand so that every wall
    /// mask, both door states and the entrance all appear in one frame. One repaint of this image
    /// yields the whole tileset, drawn by one hand in one pass, which is also the only way the
    /// pieces end up consistent with each other.
    /// </para>
    /// <para>
    /// It is deliberately not built from a <see cref="RoomPlan"/>. A plan puts rooms on a lattice
    /// and a lattice cannot produce a wall stub or an isolated pillar, which is precisely why those
    /// masks have never been drawn.
    /// </para>
    /// </remarks>
    public sealed class SamplerRoomShot
    {

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Clears the scene so nothing from another fixture is in shot.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>The scene camera, made if the teardown took it.</summary>
        /// <returns>An orthographic main camera.</returns>
        private static Camera Rig()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var holder = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = holder.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x15, 0x10, 0x1D, 0xFF);
            return camera;
        }

        /// <summary>
        /// Carves the sampler.
        /// </summary>
        /// <remarks>
        /// Laid out so that each feature is separated from the next by solid rock, because two
        /// features that touch produce a junction neither of them was meant to demonstrate.
        /// <list type="bullet">
        /// <item>a plain chamber, for the four wall runs and the enclosed interior;</item>
        /// <item>a chamber with a free-standing pillar and a pair of wall stubs, for the masks a
        /// lattice can never make;</item>
        /// <item>an L-bent corridor, for outer corners in all four directions;</item>
        /// <item>a cross junction, for T-pieces;</item>
        /// <item>two doorways side by side, one open and one shut, so both states are drawn by the
        /// same hand in the same light.</item>
        /// </list>
        /// </remarks>
        /// <returns>The carved grid.</returns>
        private static DungeonGrid CarveSampler()
        {
            var grid = new DungeonGrid(26, 20);

            // Rock that must stay standing INSIDE open floor. A room lattice can never make these,
            // which is exactly why their wall shapes have never been drawn: an isolated pillar, four
            // bars, and four L-bends in all four rotations so every outer corner appears -- up,
            // down, left and right.
            var islands = new System.Collections.Generic.HashSet<Vector2Int>
            {
                new(4, 4),                                        // lone pillar          -> mask 0
                new(8, 4), new(9, 4),                             // two-cell bar, E-W    -> 2 and 8
                new(13, 3), new(13, 4),                           // two-cell bar, N-S    -> 1 and 4
                new(17, 4), new(18, 4), new(19, 4),               // three-cell bar E-W   -> 10
                new(23, 3), new(23, 4), new(23, 5),               // three-cell bar N-S   -> 5

                new(4, 9), new(5, 9), new(4, 10),                 // L, corner opening SE -> 9
                new(9, 9), new(10, 9), new(10, 10),               // L, corner opening SW -> 12
                new(14, 10), new(15, 10), new(14, 9),             // L, corner opening NE -> 3
                new(19, 10), new(20, 10), new(20, 9),             // L, corner opening NW -> 6
            };

            // The chamber, carved around the islands one row at a time so the rock survives.
            var floor = new RectInt(2, 2, 22, 12);
            for (int y = floor.yMin; y < floor.yMax; y++)
            {
                int runStart = -1;
                for (int x = floor.xMin; x <= floor.xMax; x++)
                {
                    bool solid = x == floor.xMax || islands.Contains(new Vector2Int(x, y));
                    if (solid)
                    {
                        if (runStart >= 0)
                        {
                            grid.CarveRoom(new RectInt(runStart, y, x - runStart, 1), 0);
                            runStart = -1;
                        }
                    }
                    else if (runStart < 0)
                    {
                        runStart = x;
                    }
                }
            }

            // A second chamber above, so there is a wall between two rooms to put doors in.
            grid.CarveRoom(new RectInt(2, 16, 22, 2), 1);

            // Both door states, side by side, drawn by one hand in one light.
            grid.AddDoor(new Vector2Int(6, 15), 0, 1, false);
            grid.AddDoor(new Vector2Int(12, 15), 0, 1, true);
            grid.AddDoor(new Vector2Int(18, 15), 0, 1, false);

            return grid;
        }

        /// <summary>
        /// Photographs the sampler and writes the manifest that lets it be cut back up.
        /// </summary>
        [Test]
        public async UniTask SamplerContainsEveryPiece(CancellationToken ct)
        {
            DungeonGrid grid = CarveSampler();

            // Reopen the cells that were carved and then wanted back as rock, so the pillar and the
            // stubs stand in open floor. Done through the layout's own constructor path rather than
            // by poking cells, so the sampler is a dungeon the rest of the code accepts.
            DungeonLayout sampler = DungeonLayout.FromGrid(
                grid, new Vector2Int(2, 3), new Vector2Int(23, 13));

            var root = new GameObject("sampler");
            var view = new DungeonView(root.transform);
            view.BuildStatic(sampler);

            Camera camera = Rig();
            const int pixelsPerCell = 64;
            int width = grid.Width * pixelsPerCell;
            int height = grid.Height * pixelsPerCell;

            camera.aspect = width / (float)height;
            camera.orthographicSize = grid.Height * DungeonView.CellSize * 0.5f;
            camera.transform.position = new Vector3(
                (grid.Width - 1) * 0.5f * DungeonView.CellSize,
                (grid.Height - 1) * 0.5f * DungeonView.CellSize, -10f);

            await UniTask.NextFrame(ct);

            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            camera.targetTexture = null;
            camera.ResetAspect();

            Directory.CreateDirectory(Frames.Directory);
            File.WriteAllBytes(
                Path.Combine(Frames.Directory, "04-sampler.png"), image.EncodeToPNG());

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            // The manifest, and the count that says whether this was worth building.
            var seen = new System.Collections.Generic.SortedSet<int>();
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("# 04-sampler");
            lines.AppendLine($"image {width}x{height}, {pixelsPerCell} px per cell, "
                             + $"grid {grid.Width}x{grid.Height}");
            lines.AppendLine("# px_x px_y cell_x cell_y kind detail");

            for (int y = grid.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    CellKind kind = grid.KindAt(cell);
                    string detail;

                    if (kind == CellKind.Wall)
                    {
                        int mask = DungeonScenery.WallMask(grid, cell);
                        seen.Add(mask);
                        detail = $"mask={mask}";
                    }
                    else if (kind == CellKind.Doorway)
                    {
                        Door door = grid.DoorAt(cell);
                        detail = door == null ? "threshold" : door.IsOpen ? "door-open" : "door-shut";
                    }
                    else
                    {
                        detail = "floor";
                    }

                    lines.AppendLine(
                        $"{x * pixelsPerCell} {(grid.Height - 1 - y) * pixelsPerCell} "
                        + $"{x} {y} {kind} {detail}");
                }
            }

            File.WriteAllText(
                Path.Combine(Frames.Directory, "04-sampler.cells.txt"), lines.ToString());

            MooseRunnerFacade.Log(
                $"sampler {width}x{height} contains wall masks: {string.Join(", ", seen)} "
                + $"({seen.Count} of 16)");

            Assert.Greater(seen.Count, 10,
                $"the sampler only produced {seen.Count} of the sixteen wall shapes, so a repaint "
                + "of it still could not supply a complete tileset");
        }
    }
}
