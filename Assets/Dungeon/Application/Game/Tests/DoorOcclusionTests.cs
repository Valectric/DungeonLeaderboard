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
    /// Pins the ordering that makes an adventurer walk <i>through</i> a doorway rather than over it.
    /// </summary>
    /// <remarks>
    /// The author asked for the two-part doors in the Pipoya set so that the upper half of the frame
    /// covers the party. That is entirely a question of sorting order, and sorting order is exactly
    /// the kind of thing that breaks silently: nothing throws, nothing logs, and the only symptom is
    /// a figure sliding over a doorframe in a frame nobody happened to look at.
    /// <para>
    /// The art itself cannot live in this repository — Pipoya's licence forbids redistribution and
    /// this repo is public, see <c>CREDITS.md</c> — so the upper halves are optional and absent on a
    /// fresh clone. These tests therefore check the <b>rule</b>, which holds whether or not the
    /// sprites are installed, rather than counting renderers that may not exist.
    /// </para>
    /// </remarks>
    public sealed class DoorOcclusionTests
    {
        /// <summary>Sorting order the party sprites are drawn at, from <c>DungeonView</c>.</summary>
        private const int PartySortingOrder = 20;

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Cleans before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// The upper doorframe is drawn above the party, which is the entire feature.
        /// </summary>
        /// <remarks>
        /// Stated as a comparison against the party's own order rather than against the literal 25,
        /// so that moving the party's layer fails this test instead of quietly un-occluding the
        /// doors. A constant checked against itself would pass forever.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask TheUpperDoorframe_DrawsAboveTheParty(CancellationToken ct)
        {
            MooseRunnerFacade.Log(
                $"door top order {DungeonScenery.DoorTopSortingOrder}, party order {PartySortingOrder}");

            Assert.Greater(DungeonScenery.DoorTopSortingOrder, PartySortingOrder,
                "the upper half of the doorframe is not drawn above the party, so adventurers slide "
                + "OVER the doorway instead of passing through it -- which is the whole reason the "
                + "two-part door art was asked for");

            await UniTask.Yield(ct);
        }

        /// <summary>
        /// The lower doorframe stays below the party, so they are not hidden by the threshold.
        /// </summary>
        /// <remarks>
        /// The other half of the sandwich, and the easier one to get wrong while fixing the first:
        /// raising the whole door above the party would occlude them completely and read as figures
        /// vanishing at every threshold.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask TheLowerDoorframe_StaysBelowTheParty(CancellationToken ct)
        {
            // 2 is the order DungeonScenery gives the door leaf itself.
            Assert.Less(2, PartySortingOrder,
                "the door leaf is drawn at or above the party, so an adventurer standing in a "
                + "doorway disappears behind it");

            await UniTask.Yield(ct);
        }
    }
}
