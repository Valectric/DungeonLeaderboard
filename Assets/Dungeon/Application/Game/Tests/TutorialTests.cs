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
    /// Pins when the first-raid instruction appears and when it goes away.
    /// </summary>
    /// <remarks>
    /// The author asked for two things that pull against each other: the instruction must be big
    /// enough not to be missed, and it must stop covering the game once it has been read. "Once the
    /// chest is gone" is the signal they chose, and it is a good one — looting is the moment the
    /// party has crossed the room, met the spawner and found the thing worth stopping for.
    /// <para>
    /// Asserted rather than watched, because the failure is a caption that never leaves, and by the
    /// time anybody notices that in play they have stopped reading it anyway.
    /// </para>
    /// </remarks>
    public sealed class TutorialTests
    {
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
        /// The instruction is up at the start of the first raid and gone once the chest is looted.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask TheInstruction_ClearsWhenTheChestIsLooted(CancellationToken ct)
        {
            var placed = new Furnishings();
            DungeonLayout bare = DungeonLayout.Build(RoomPlan.Corridor(1), furnishedRooms: 1);
            Vector2Int centre = bare.RoomCentres[0];
            placed.SlimeSpawners.Add(centre + new Vector2Int(1, -2));
            placed.Chests.Add(centre + new Vector2Int(-1, 2));

            DungeonLayout layout = DungeonLayout.Build(
                RoomPlan.Corridor(1), placed: placed, furnishedRooms: 1);
            var raid = new Raid(layout);

            Assert.IsTrue(Hints.HeadlineWanted(raid),
                "the instruction is not up at the start of the first raid, which is the one moment "
                + "it exists for");

            int guard = 0;
            while (raid.IsRunning && raid.Party.LootedCount == 0 && guard++ < 4000)
            {
                raid.Tick(0.05f);
            }

            MooseRunnerFacade.Log(
                $"looted {raid.Party.LootedCount} chest(s) after {guard} ticks, "
                + $"{raid.TimeRemaining:F1}s left, instruction up={Hints.HeadlineWanted(raid)}");

            Assert.Greater(raid.Party.LootedCount, 0,
                "the party never looted the chest, so this proves nothing either way");
            Assert.IsFalse(Hints.HeadlineWanted(raid),
                "the instruction is still up after the chest was looted, so it now covers the game "
                + "rather than teaching it");

            await UniTask.Yield(ct);
        }

        /// <summary>
        /// Walking into a room pays the clock back, which is what makes a bought hall worth having.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask EnteringARoom_PaysTheClockBack(CancellationToken ct)
        {
            var raid = new Raid(DungeonLayout.BuildCorridor(roomCount: 3));

            int guard = 0;
            while (raid.IsRunning && raid.Party.VisitedRooms < 2 && guard++ < 4000)
            {
                raid.Tick(0.05f);
            }

            MooseRunnerFacade.Log(
                $"after reaching room {raid.Party.VisitedRooms}: {raid.SecondsAwarded:F0}s awarded, "
                + $"{raid.TimeRemaining:F1}s left of a {Raid.RaidSeconds:F0}s raid");

            Assert.GreaterOrEqual(raid.Party.VisitedRooms, 2, "the party never left the first room");
            Assert.GreaterOrEqual(raid.SecondsAwarded, Raid.NewRoomSeconds,
                "walking into a second room paid no time back, so a bought hall still buys nothing");

            await UniTask.Yield(ct);
        }

        /// <summary>
        /// The hint clock measures how long the raid has been running, not how much of a fixed
        /// minute is left.
        /// </summary>
        /// <remarks>
        /// These are the same number only while nothing adds time. The room bonus adds time, so
        /// <c>TimeRemaining</c> can now exceed <c>RaidSeconds</c> and the old expression
        /// <c>RaidSeconds - TimeRemaining</c> runs backwards — every room entered rewinds the
        /// tutorial's sense of its own age by two seconds, and the caption outstays its welcome by
        /// however much time the party has earned.
        /// <para>
        /// Worth a test rather than a careful reading, because nothing about it is visible: no
        /// exception, no artefact, and the fade still completes, just later than the number in the
        /// source says. The elapsed time the hints want is
        /// <c>RaidSeconds + SecondsAwarded - TimeRemaining</c>, which is what <c>Raid.Elapsed</c>
        /// now is.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        [Test]
        public async UniTask TheHintClock_TracksRealElapsedTime(CancellationToken ct)
        {
            var raid = new Raid(DungeonLayout.BuildCorridor(roomCount: 3));

            float ticked = 0f;
            int guard = 0;
            while (raid.IsRunning && raid.SecondsAwarded <= 0f && guard++ < 4000)
            {
                raid.Tick(0.05f);
                ticked += 0.05f;
            }

            // Keep going a little past the award so the skew, if present, has room to show.
            for (int i = 0; i < 40 && raid.IsRunning; i++)
            {
                raid.Tick(0.05f);
                ticked += 0.05f;
            }

            float naive = Raid.RaidSeconds - raid.TimeRemaining;

            MooseRunnerFacade.Log(
                $"ticked {ticked:F1}s, raid.Elapsed={raid.Elapsed:F1}s, "
                + $"naive RaidSeconds-TimeRemaining={naive:F1}s, "
                + $"awarded={raid.SecondsAwarded:F0}s");

            Assert.Greater(raid.SecondsAwarded, 0f,
                "no room bonus was awarded, so this proves nothing either way");
            Assert.AreEqual(ticked, raid.Elapsed, 0.2f,
                "the raid's own sense of elapsed time has drifted from the time actually ticked "
                + "into it, so every hint timed off it fires at the wrong moment");
            Assert.AreEqual(raid.SecondsAwarded, raid.Elapsed - naive, 0.2f,
                "the naive expression should lag real elapsed time by exactly the time awarded -- "
                + "if it does not, the room bonus is not the only thing moving the clock");

            await UniTask.Yield(ct);
        }
    }
}
