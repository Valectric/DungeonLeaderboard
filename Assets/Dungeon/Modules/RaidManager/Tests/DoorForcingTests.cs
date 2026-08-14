using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Verifies that a shut door is a delay rather than a wall.
    /// </summary>
    /// <remarks>
    /// A door the party could never get past would break the game in the player's favour: shut one
    /// and farm a trapped party for the whole minute with no decision left to make. An archer picks
    /// the lock in a few seconds; a party without one batters through twice a skeleton's health.
    /// Either way the door is then <b>jammed open for good</b>, so every door is worth a finite
    /// number of seconds and spending them is a real choice.
    /// </remarks>
    public sealed class DoorForcingTests
    {
        /// <summary>Finds a composition by name.</summary>
        private static PartyComposition Named(string name)
        {
            foreach (PartyComposition composition in PartyComposition.All)
            {
                if (composition.Name == name)
                {
                    return composition;
                }
            }

            Assert.Fail($"no composition named {name}");
            return null;
        }

        /// <summary>Runs a raid with the first door shut, and reports what happened to it.</summary>
        /// <param name="composition">Party to send in.</param>
        /// <param name="seconds">How long to simulate.</param>
        /// <param name="door">Receives the door that was shut.</param>
        /// <returns>The raid.</returns>
        private static Raid RaidAgainstAShutDoor(
            PartyComposition composition, float seconds, out Door door)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout, 0f, composition);

            door = layout.Grid.Doors[0];
            door.IsOpen = false;

            for (float t = 0f; t < seconds && raid.IsRunning; t += 0.02f)
            {
                raid.Tick(0.02f);
            }

            return raid;
        }

        /// <summary>An archer picks the lock, and reasonably quickly.</summary>
        [Test]
        public void AnArcher_PicksTheLock()
        {
            Raid raid = RaidAgainstAShutDoor(Named("THE BALANCED PARTY"), 40f, out Door door);

            MooseRunnerFacade.Log(
                $"balanced party: forced={door.IsForced} pick={door.PickFraction:P0} "
                + $"damage={door.DamageFraction:P0}");

            Assert.IsTrue(door.IsForced, "the archer never got the door open");
            Assert.Less(door.DamageFraction, 0.5f,
                "the door should have been picked, not battered down");
            _ = raid;
        }

        /// <summary>
        /// A party with no archer breaks the door down instead, and it costs them.
        /// </summary>
        [Test]
        public void APartyWithNoArcher_BattersItDown()
        {
            // THE IRONCLADS are two tanks, a healer and an archer, so build a roster with none.
            var noArcher = new PartyComposition("TEST NO ARCHER", "test",
                new[]
                {
                    AdventurerRole.Tank, AdventurerRole.Tank,
                    AdventurerRole.Mage, AdventurerRole.Healer
                });

            Raid raid = RaidAgainstAShutDoor(noArcher, 55f, out Door door);

            MooseRunnerFacade.Log(
                $"no archer: forced={door.IsForced} damage={door.DamageFraction:P0} "
                + $"pick={door.PickFraction:P0}");

            Assert.AreEqual(0f, door.PickFraction, 0.001f, "nobody there can pick a lock");
            Assert.Greater(door.DamageFraction, 0.2f, "they should be hitting the door");
            _ = raid;
        }

        /// <summary>Smashing a door takes much longer than picking it.</summary>
        /// <remarks>
        /// The whole point of the two routes. If they cost the same, the archer stops mattering and
        /// so does the roster.
        /// </remarks>
        [Test]
        public void Battering_TakesLongerThanPicking()
        {
            Assert.Greater(Door.MaxHealth / 20f, Door.PickSeconds * 2f,
                "a party's whole damage output should take far longer than one archer's lockpick");
            MooseRunnerFacade.Log(
                $"pick={Door.PickSeconds}s, batter at 20dps={Door.MaxHealth / 20f:F0}s");
        }

        /// <summary>A forced door cannot be shut again by the player.</summary>
        /// <remarks>
        /// Without this the mechanic is theatre: the player shuts it the instant the archer finishes
        /// and one door stalls the party forever.
        /// </remarks>
        [Test]
        public void AForcedDoor_CannotBeShutAgain()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            Door door = layout.Grid.Doors[0];

            Assert.IsTrue(raid.ToggleDoor(door.Cell), "an intact door should toggle");

            door.Pick(Door.PickSeconds + 1f);
            Assert.IsTrue(door.IsForced, "the door should now be forced");
            Assert.IsTrue(door.IsOpen, "and stuck open");

            Assert.IsFalse(raid.ToggleDoor(door.Cell), "a forced door must refuse to shut");
            Assert.IsTrue(door.IsOpen, "and must stay open");
        }

        /// <summary>Picking and battering both stop once the door is forced.</summary>
        [Test]
        public void AForcedDoor_TakesNoMoreWork()
        {
            var door = new Door(Vector2Int.zero, 0, 1, false);
            door.Pick(Door.PickSeconds + 1f);

            Assert.IsFalse(door.Pick(5f), "a forced door should not report being picked again");
            Assert.IsFalse(door.Batter(9999f), "nor being broken again");
        }

        /// <summary>
        /// Only the rosters with an archer can get through a shut door inside a raid.
        /// </summary>
        /// <remarks>
        /// This test used to be called <c>EveryComposition_EventuallyGetsThrough</c> and asserted
        /// that each roster made more than 5% progress on the door. Its name claimed a property it
        /// never checked, and the property is false: measured over a full sixty seconds, THE
        /// IRONCLADS reach 64% of the door's health and THE PILGRIMAGE 43%. Neither gets through at
        /// all.
        /// <para>
        /// That is not obviously wrong — the door's 520 health is the author's own figure, twice a
        /// skeleton's, and those two rosters are built around soaking and healing rather than
        /// damage. But it means shutting one door in front of them converts a whole raid into
        /// <b>3 energy</b>: they batter for 55.7 seconds while unengaged, which pays the 0.05/s idle
        /// floor exactly as SPEC.md intends. A door alone is not a stall, it is a wall.
        /// </para>
        /// <para>
        /// Pinned as it stands rather than "fixed", because every lever — door health, those
        /// rosters' damage, or letting a party give up and turn around — is a balance decision. See
        /// D19.
        /// </para>
        /// </remarks>
        [Test]
        public void OnlyRostersWithAnArcher_GetThroughAShutDoor()
        {
            int through = 0;
            int stuck = 0;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                Raid raid = RaidAgainstAShutDoor(composition, Raid.RaidSeconds, out Door door);

                MooseRunnerFacade.Log(
                    $"{composition.Name}: forced={door.IsForced} "
                    + $"pick={door.PickFraction:P0} damage={door.DamageFraction:P0} "
                    + $"harvested={raid.EnergyHarvested:F0}");

                if (door.IsForced)
                {
                    through++;
                }
                else
                {
                    stuck++;

                    // A roster that cannot open it must at least be trying, or the party is simply
                    // standing still and the door has become a soft lock rather than a cost.
                    Assert.Greater(door.DamageFraction, 0.2f,
                        $"{composition.Name} neither picked nor meaningfully battered the door, so "
                        + "it is stuck against it doing nothing at all");
                }
            }

            MooseRunnerFacade.Log($"{through} rosters got through, {stuck} did not");
            Assert.Greater(through, 0, "no roster could open a shut door");
            Assert.Less(stuck, PartyComposition.All.Length,
                "no roster could get through a shut door, so a door is an unconditional wall");
        }

        /// <summary>A shut door genuinely delays the party, which is what it is for.</summary>
        [Test]
        public void AShutDoor_CostsThePartyTime()
        {
            DungeonLayout open = DungeonLayout.BuildCorridor();
            var openRaid = new Raid(open);
            float openTime = 0f;
            while (openRaid.IsRunning && openTime < Raid.RaidSeconds)
            {
                openRaid.Tick(0.02f);
                openTime += 0.02f;
            }

            Raid shutRaid = RaidAgainstAShutDoor(
                Named("THE BALANCED PARTY"), Raid.RaidSeconds, out Door door);

            MooseRunnerFacade.Log(
                $"crossing: door open={openTime:F1}s, door shut then forced={door.IsForced}, "
                + $"harvest open={openRaid.EnergyHarvested:F1} shut={shutRaid.EnergyHarvested:F1}");

            Assert.IsTrue(shutRaid.Outcome != RaidOutcome.PartyEscaped ||
                          shutRaid.EnergyHarvested >= openRaid.EnergyHarvested,
                "shutting a door should never earn the player less than leaving it open");
        }
    }
}
