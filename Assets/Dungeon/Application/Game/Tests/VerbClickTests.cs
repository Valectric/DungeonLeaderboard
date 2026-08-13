using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Drives the three verbs through the shipped click handler, at real screen coordinates.
    /// </summary>
    /// <remarks>
    /// These exist because the E2E suite went green while <b>every verb in the game was dead</b>.
    /// The project runs the Input System package, so each legacy <c>UnityEngine.Input</c> call threw
    /// once per frame; the tests never noticed because they drove the raid object directly instead
    /// of clicking anything. A test that reaches past the input layer cannot fail when the input
    /// layer is what broke.
    /// <para>
    /// Clicks enter through <see cref="GameController.ClickAt"/> with a screen position obtained
    /// from the real camera, so world projection, cell resolution and verb dispatch all run exactly
    /// as they do for a player. Only the device poll itself is bypassed -- synthesising raw Input
    /// System device events is banned by the project's testing doctrine as too fragile.
    /// </para>
    /// </remarks>
    public sealed class VerbClickTests
    {
        /// <summary>The controller in the loaded scene.</summary>
        private static GameController Controller => Object.FindFirstObjectByType<GameController>();

        /// <summary>Loads the shipped play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid", forceReload: true);
        }

        /// <summary>Restarts the raid so each test begins from a known board.</summary>
        [SetUp]
        public void SetUp()
        {
            Controller.StartRaid();
        }

        /// <summary>Converts a grid cell into the screen point a player would click.</summary>
        /// <param name="cell">Cell to aim at.</param>
        /// <returns>Screen-space position over that cell.</returns>
        private static Vector2 ScreenPointOver(Vector2Int cell)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "the scene must ship with a camera");
            return camera.WorldToScreenPoint(DungeonView.CellToWorld(cell));
        }

        /// <summary>Clicking a door toggles it -- the primary verb and the game's safety valve.</summary>
        [Test]
        public async UniTask ClickingADoor_TogglesIt(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            Door door = raid.Layout.Grid.Doors[0];
            bool before = door.IsOpen;

            Controller.ClickAt(ScreenPointOver(door.Cell));
            await UniTask.NextFrame(ct);

            MooseRunnerFacade.Log($"door at {door.Cell}: {before} -> {door.IsOpen}");
            Assert.AreNotEqual(before, door.IsOpen, "clicking a door must toggle it");
        }

        /// <summary>Clicking twice returns the door to where it started.</summary>
        [Test]
        public async UniTask ClickingADoorTwice_ReturnsItToTheStart(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            Door door = raid.Layout.Grid.Doors[0];
            bool before = door.IsOpen;

            Controller.ClickAt(ScreenPointOver(door.Cell));
            await UniTask.NextFrame(ct);
            Controller.ClickAt(ScreenPointOver(door.Cell));
            await UniTask.NextFrame(ct);

            Assert.AreEqual(before, door.IsOpen);
        }

        /// <summary>Clicking a spawner spends energy and puts a monster in that room.</summary>
        [Test]
        public async UniTask ClickingASpawner_SpawnsAMob(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            Vector2Int spawner = raid.Layout.SpawnerCells[0];
            int before = raid.Mobs.Mobs.Count;
            float energyBefore = raid.TotalEnergy;

            Controller.ClickAt(ScreenPointOver(spawner));
            await UniTask.NextFrame(ct);

            MooseRunnerFacade.Log($"mobs {before} -> {raid.Mobs.Mobs.Count}, " +
                                  $"energy {energyBefore:F1} -> {raid.TotalEnergy:F1}");
            Assert.AreEqual(before + 1, raid.Mobs.Mobs.Count, "clicking a spawner must spawn a mob");
            Assert.Less(raid.TotalEnergy, energyBefore, "spawning must cost energy");
        }

        /// <summary>A spawned mob is bound to the room its spawner sits in.</summary>
        [Test]
        public async UniTask ClickingASpawner_BindsTheMobToThatRoom(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            Vector2Int spawner = raid.Layout.SpawnerCells[0];
            int expected = raid.Layout.Grid.RoomAt(spawner);

            Controller.ClickAt(ScreenPointOver(spawner));
            await UniTask.NextFrame(ct);

            Assert.AreEqual(expected, raid.Mobs.Mobs.Last().HomeRoom);
        }

        /// <summary>Clicking a trap spends energy and puts the trap on cooldown.</summary>
        [Test]
        public async UniTask ClickingATrap_FiresIt(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            Vector2Int trap = raid.Layout.TrapCells[0];
            float before = raid.TotalEnergy;

            Controller.ClickAt(ScreenPointOver(trap));
            await UniTask.NextFrame(ct);

            MooseRunnerFacade.Log($"trap energy {before:F1} -> {raid.TotalEnergy:F1}, " +
                                  $"ready {raid.IsTrapReady}");
            Assert.Less(raid.TotalEnergy, before, "firing a trap must cost energy");
            Assert.IsFalse(raid.IsTrapReady, "a fired trap must go on cooldown");
        }

        /// <summary>Clicking bare floor does nothing at all -- no verb, no cost.</summary>
        [Test]
        public async UniTask ClickingEmptyFloor_DoesNothing(CancellationToken ct)
        {
            Raid raid = Controller.CurrentRaid;
            float energyBefore = raid.TotalEnergy;
            int mobsBefore = raid.Mobs.Mobs.Count;

            Controller.ClickAt(ScreenPointOver(raid.Layout.EntranceCell));
            await UniTask.NextFrame(ct);

            Assert.AreEqual(mobsBefore, raid.Mobs.Mobs.Count);
            Assert.AreEqual(energyBefore, raid.TotalEnergy, 0.5f);
        }

        /// <summary>
        /// The frame loop itself must not throw. This is the assertion that would have caught the
        /// dead-input bug directly: the exception fired from Update, not from any verb.
        /// </summary>
        [Test]
        public async UniTask TheGameLoop_RunsWithoutLoggingErrors(CancellationToken ct)
        {
            bool sawError = false;

            void Watch(string condition, string trace, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    sawError = true;
                    MooseRunnerFacade.Log($"error during play: {condition}");
                }
            }

            UnityEngine.Application.logMessageReceived += Watch;
            try
            {
                await UniTask.WaitForSeconds(3f, cancellationToken: ct);
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= Watch;
            }

            Assert.IsFalse(sawError, "the game loop logged an error while simply running");
        }
    }
}
