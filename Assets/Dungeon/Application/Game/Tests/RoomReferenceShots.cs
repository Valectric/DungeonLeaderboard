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
    /// Photographs three specific room shapes, to be used as layout references for new art.
    /// </summary>
    /// <remarks>
    /// Art has been generated twice against a written description of a room, and twice the geometry
    /// came back plausible but not ours — a picture of a dungeon rather than a picture of <i>this</i>
    /// dungeon. These shots invert that: the game draws the room it actually builds, and the drawing
    /// is what the generator is asked to restyle. The layout stops being something the model has to
    /// imagine.
    /// <para>
    /// Rendered through the camera rather than the screen, so no HUD, no standings strip and no
    /// hint text land in the frame. What comes out is the board alone.
    /// </para>
    /// </remarks>
    public sealed class RoomReferenceShots
    {
        /// <summary>Where the reference frames are written.</summary>
        private static string ShotDirectory =>
            Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots", "rooms");

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Clears the scene before each shot so nothing from the last one survives.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// The scene camera, made if the teardown took it.
        /// </summary>
        /// <remarks>
        /// <c>CleanSceneImmediate</c> destroys everything not marked to survive, and the shipped
        /// scene's camera is not marked -- so the first shot after a clean has nothing to render
        /// with. The controller has the same fallback in <c>Awake</c> for the same reason.
        /// </remarks>
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
        /// Draws a layout, frames it, and writes a camera-only PNG.
        /// </summary>
        /// <param name="layout">Dungeon to draw.</param>
        /// <param name="name">File name stem.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable capture.</returns>
        private static async UniTask Shoot(DungeonLayout layout, string name, CancellationToken ct)
        {
            var root = new GameObject($"room_{name}");
            var view = new DungeonView(root.transform);
            view.BuildStatic(layout);

            Camera camera = Rig();
            DungeonGrid grid = layout.Grid;

            // EXACTLY 64 screen pixels per dungeon cell, which is what makes this whole approach
            // work: the restyled image comes back the same size, so cell (x, y) can be cut straight
            // back out of it with no resampling and no guessing where the grid falls.
            const int pixelsPerCell = 64;
            int width = grid.Width * pixelsPerCell;
            int height = grid.Height * pixelsPerCell;

            camera.orthographic = true;
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

            Directory.CreateDirectory(ShotDirectory);
            string path = Path.Combine(ShotDirectory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            WriteManifest(layout, name, pixelsPerCell, width, height);

            MooseRunnerFacade.Log($"captured {path} ({width}x{height}, {pixelsPerCell}px per cell)");
            Assert.IsTrue(File.Exists(path), $"{name} was not written");
        }

        /// <summary>
        /// Writes what every cell of the shot IS, so the restyled image can be cut back up.
        /// </summary>
        /// <remarks>
        /// The point of the whole exercise. A generator is good at painting a room and bad at
        /// drawing forty-eight independent tiles -- two attempts proved that, the second measured at
        /// thirty-eight of forty-two cell seams running continuously. So let it paint the room, and
        /// slice the tiles out afterwards at coordinates we already know, because we built the room.
        /// <para>
        /// Rows are written top-down to match image space, which is flipped from grid space. Getting
        /// that wrong would mirror the whole tileset vertically and every lit edge would point the
        /// wrong way.
        /// </para>
        /// </remarks>
        /// <param name="layout">Dungeon that was drawn.</param>
        /// <param name="name">File name stem.</param>
        /// <param name="pixelsPerCell">Scale the shot was rendered at.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        private static void WriteManifest(
            DungeonLayout layout, string name, int pixelsPerCell, int width, int height)
        {
            DungeonGrid grid = layout.Grid;
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"# {name}");
            lines.AppendLine($"image {width}x{height}, {pixelsPerCell} px per cell, "
                             + $"grid {grid.Width}x{grid.Height}");
            lines.AppendLine("# px_x px_y cell_x cell_y kind detail");

            for (int y = grid.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    CellKind kind = grid.KindAt(cell);

                    string detail = kind switch
                    {
                        CellKind.Wall => $"mask={DungeonScenery.WallMask(grid, cell)}",
                        CellKind.Doorway => grid.DoorAt(cell) != null ? "door" : "threshold",
                        _ => cell == layout.EntranceCell ? "entrance"
                            : cell == layout.BossCell ? "deepest" : "floor"
                    };

                    int pixelX = x * pixelsPerCell;
                    int pixelY = (grid.Height - 1 - y) * pixelsPerCell;
                    lines.AppendLine($"{pixelX} {pixelY} {x} {y} {kind} {detail}");
                }
            }

            File.WriteAllText(Path.Combine(ShotDirectory, $"{name}.cells.txt"), lines.ToString());
        }

        /// <summary>A single room with no doors at all — the shape the game now opens on.</summary>
        /// <remarks>
        /// One room has no threshold, so <see cref="PlanBuilder"/> creates no doors. This is the
        /// plain interior: four wall runs, four corners, and floor. The tile set has to get this
        /// right before anything else, because it is most of what a player looks at.
        /// </remarks>
        [Test]
        public async UniTask RoomWithNoDoors(CancellationToken ct)
        {
            DungeonLayout layout = DungeonLayout.Build(RoomPlan.Corridor(1), furnishedRooms: 1);

            Assert.AreEqual(0, layout.Grid.Doors.Count, "a single room should have no doors");
            await Shoot(layout, "01-room-no-doors", ct);
        }

        /// <summary>
        /// A room with a door in each of the four walls.
        /// </summary>
        /// <remarks>
        /// Built as a plus: a centre room with a neighbour north, east, south and west, so the
        /// centre gets a doorway on all four thresholds. It is the hardest case for a wall set —
        /// every kind of junction appears at once — and the case the current art has never been
        /// asked to draw.
        /// </remarks>
        [Test]
        public async UniTask RoomWithDoorsOnAllFourSides(CancellationToken ct)
        {
            var plan = new RoomPlan();
            plan.Add(new Vector2Int(1, 0));
            plan.Add(new Vector2Int(-1, 0));
            plan.Add(new Vector2Int(0, 1));
            plan.Add(new Vector2Int(0, -1));

            DungeonLayout layout = DungeonLayout.Build(plan, furnishedRooms: 1);

            MooseRunnerFacade.Log(
                $"plus-shaped plan: {layout.RoomCentres.Count} rooms, {layout.Grid.Doors.Count} doors");
            Assert.AreEqual(4, layout.Grid.Doors.Count, "the centre room should have four doors");

            await Shoot(layout, "02-room-four-doors", ct);
        }

        /// <summary>
        /// The room the party actually walks into, with the entrance arch on its left.
        /// </summary>
        /// <remarks>
        /// The same single room as the first shot, framed to include the approach outside the
        /// entrance — the forest forecourt and the archway. This is the join the author has flagged
        /// twice as looking bolted on, so it is the one frame where both sides of it are visible at
        /// once and a generator can be asked to make them agree.
        /// </remarks>
        [Test]
        public async UniTask RoomWithEntranceOnTheLeft(CancellationToken ct)
        {
            DungeonLayout layout = DungeonLayout.Build(RoomPlan.Corridor(2), furnishedRooms: 1);

            var root = new GameObject("room_entrance");
            var view = new DungeonView(root.transform);
            view.BuildStatic(layout);

            Camera camera = Rig();
            Bounds world = view.WorldBounds;

            // Framed on everything drawn, so the forecourt and the arch are both in shot.
            camera.orthographic = true;
            camera.transform.position = new Vector3(world.center.x, world.center.y, -10f);
            camera.orthographicSize = Mathf.Max(
                world.extents.y + 1f, (world.extents.x + 1f) / camera.aspect);

            await UniTask.NextFrame(ct);

            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            camera.targetTexture = null;

            Directory.CreateDirectory(ShotDirectory);
            string path = Path.Combine(ShotDirectory, "03-room-entrance-left.png");
            File.WriteAllBytes(path, image.EncodeToPNG());

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);

            MooseRunnerFacade.Log($"captured {path}");
            Assert.IsTrue(File.Exists(path), "the entrance shot was not written");
        }
    }
}
