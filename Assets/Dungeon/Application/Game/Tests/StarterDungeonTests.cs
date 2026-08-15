using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using Dungeon.RaidManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins what the first dungeon of a run contains, and measures how long a raid on it lasts.
    /// </summary>
    /// <remarks>
    /// The author's instruction was exact: <i>"the starter dungeon should just be one room with one
    /// slime pit and one chest"</i>. That is a shape, and shapes are cheap to assert — but the thing
    /// it puts at risk is a <b>duration</b>, which nothing else in the suite watches. One room means
    /// the entrance and the far wall are a few cells apart, so a party with nothing to detain it can
    /// walk in and back out in seconds, and a raid that ends at eight seconds is a round the player
    /// cannot lose slowly enough to learn anything from.
    /// <para>
    /// So the shape assertions here are the easy half. The measurements below are the half that
    /// matters, and they are logged in full: this project's own doctrine is that green tests hide a
    /// broken rate, and the opening minute of a jam game is the one the most players will ever see.
    /// </para>
    /// </remarks>
    public sealed class StarterDungeonTests
    {
        /// <summary>The controller under test, rebuilt for each case.</summary>
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
        /// The run opens on one room holding one slime pit and one chest, and nothing else.
        /// </summary>
        /// <remarks>
        /// Asserted against the layout the raid is actually built from rather than against the
        /// loadout, because the loadout is a shopping list and the dungeon is what the party walks
        /// into — and the two have disagreed before, when a purchase landed on a cell the builder
        /// then refused.
        /// </remarks>
        [Test]
        public void TheOpeningDungeon_IsOneRoomASlimePitAndAChest()
        {
            DungeonLayout layout = _game.CurrentRaid.Layout;

            MooseRunnerFacade.Log(
                $"opening dungeon: {layout.RoomCentres.Count} room(s), "
                + $"{layout.SpawnerCells.Count} spawner(s), {layout.ChestCells.Count} chest(s), "
                + $"{layout.TrapCells.Count} trap(s), {layout.Grid.Doors.Count} door(s)");

            Assert.AreEqual(1, layout.RoomCentres.Count, "the run must open on a single room");
            Assert.AreEqual(1, layout.SpawnerCells.Count, "with exactly one spawner");
            Assert.AreEqual(0, layout.SpawnerTierAt(layout.SpawnerCells[0]),
                "and it must be a slime pit rather than a skeleton crypt");
            Assert.AreEqual(1, layout.ChestCells.Count, "and exactly one chest");
            Assert.AreEqual(0, layout.TrapCells.Count, "and no traps the player did not buy");
        }

        /// <summary>Both opening fittings stand on floor the party can reach.</summary>
        /// <remarks>
        /// A fitting placed in the rock is silently dropped by the builder, so the kit could go
        /// missing without anything failing. Neither may sit on the entrance or the deepest cell
        /// either: the builder refuses those outright, which would drop the item just as quietly.
        /// </remarks>
        [Test]
        public void TheOpeningFittings_StandOnReachableFloor()
        {
            DungeonLayout layout = _game.CurrentRaid.Layout;

            foreach (Vector2Int cell in new[] { layout.SpawnerCells[0], layout.ChestCells[0] })
            {
                Assert.IsTrue(layout.Grid.IsWalkable(cell), $"{cell} is not walkable floor");
                Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                    $"{cell} belongs to no room");
                Assert.Greater(layout.Grid.FindPath(layout.EntranceCell, cell).Count, 0,
                    $"there is no route from the entrance to {cell}");
            }
        }

        /// <summary>
        /// The coaching text is on for the first raid of a run and off for every one after it.
        /// </summary>
        /// <remarks>
        /// Both halves matter. A hint that never arrives teaches nobody; a hint that never leaves is
        /// clutter over the board for the rest of the run, and the board is the game.
        /// </remarks>
        [Test]
        public void TheHints_ShowOnlyInTheFirstRaid()
        {
            Assert.AreEqual(0, _game.League.Round, "a fresh run has banked nothing");
            Assert.IsTrue(Hints.ShouldShow(_game.League.Round), "the first raid is coached");

            Assert.IsFalse(Hints.ShouldShow(1), "the second raid is not");
            Assert.IsFalse(Hints.ShouldShow(9), "and neither is the tenth");
        }

        /// <summary>Runs a raid to its end and reports how it went.</summary>
        /// <param name="spawn">Whether the player taps the spawner whenever they can afford to.</param>
        /// <param name="harvested">Receives the energy harvested.</param>
        /// <returns>Seconds the raid lasted.</returns>
        private float PlayOut(bool spawn, out float harvested)
        {
            Raid raid = _game.CurrentRaid;
            float elapsed = 0f;

            while (raid.IsRunning && elapsed < Raid.RaidSeconds + 1f)
            {
                if (spawn)
                {
                    foreach (Vector2Int cell in raid.Layout.SpawnerCells)
                    {
                        if (raid.TotalEnergy > Raid.SpawnCost * 2f &&
                            raid.Mobs.CountInRoom(raid.Layout.Grid.RoomAt(cell)) < 2)
                        {
                            raid.SpawnMob(cell);
                        }
                    }
                }

                raid.Tick(0.02f);
                elapsed += 0.02f;
            }

            harvested = raid.EnergyHarvested;
            return elapsed;
        }

        /// <summary>
        /// A player who does nothing still gets a raid rather than a formality.
        /// </summary>
        /// <remarks>
        /// The failure this guards is not subtle and it is invisible to every other test: with one
        /// room, "every room visited" is true on the first tick and the party is standing on the
        /// entrance, so the escape condition fires before anybody moves and the raid ends at zero
        /// seconds. The bound is deliberately generous — the point is to catch a collapse, not to
        /// pin a walk speed.
        /// </remarks>
        [Test]
        public void APassiveOpeningRaid_LastsLongEnoughToReadTheBoard()
        {
            float seconds = PlayOut(spawn: false, out float harvested);

            MooseRunnerFacade.Log(
                $"passive opening raid: {seconds:F1}s, harvested {harvested:F0}, "
                + $"outcome {_game.CurrentRaid.Outcome}");

            Assert.Greater(seconds, 8f,
                $"an untouched opening raid was over in {seconds:F1}s, which is not a round of a "
                + "game -- the party walked in and straight back out");
        }

        /// <summary>
        /// Using the one tool on the board keeps the party inside longer and pays better.
        /// </summary>
        /// <remarks>
        /// The whole lesson of the first raid, stated as a measurement. If tapping the slime pit did
        /// not visibly beat doing nothing, the hints drawn over this room would be teaching the
        /// player something the game does not actually reward.
        /// </remarks>
        [Test]
        public void SpawningSlimes_BeatsDoingNothing()
        {
            float idleSeconds = PlayOut(spawn: false, out float idleHarvest);

            SetUp();
            float busySeconds = PlayOut(spawn: true, out float busyHarvest);

            MooseRunnerFacade.Log(
                $"opening raid: untouched {idleSeconds:F1}s / {idleHarvest:F0} energy, "
                + $"played {busySeconds:F1}s / {busyHarvest:F0} energy");

            Assert.Greater(busyHarvest, idleHarvest * 1.5f,
                $"spawning slimes earned {busyHarvest:F0} against {idleHarvest:F0} for doing "
                + "nothing, so the first raid does not reward using the only tool on the board");
        }
    }
}
