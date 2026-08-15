using System.Collections.Generic;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Asks whether a bigger dungeon earns more, which is what the shop sells.
    /// </summary>
    /// <remarks>
    /// A hall is the most expensive thing in the shop and the thing a competent player buys first, so
    /// "does it pay for itself" is close to the centre of the design. Nothing measured it.
    /// <para>
    /// The suspicion comes from the league sweeps: across a whole season a competent bot harvested 341
    /// in round one and 359 in round seven while buying a hall in almost every shop, and the value 246
    /// recurred in unrelated rounds. A flat earnings curve against a growing dungeon is either a
    /// balance problem or a bug, and neither is visible to any other test here.
    /// </para>
    /// </remarks>
    public sealed class RoomsPayTests
    {
        /// <summary>Health of the worst survivor below which this player stops pressing.</summary>
        private const float CeaseFireBelow = 0.6f;

        /// <summary>Ticks a raid to its end with a fixed, competent policy.</summary>
        /// <remarks>
        /// Deliberately the same policy at every size — spawn while the party can take it, shut the
        /// door they are walking at, open it again when they cannot. Holding the player constant is
        /// the only way the room count is the thing being measured.
        /// </remarks>
        /// <param name="raid">Raid to play.</param>
        /// <returns>Energy harvested.</returns>
        private static float Play(Raid raid)
        {
            DungeonLayout layout = raid.Layout;

            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                bool safeToPress = raid.Party.WoundFraction > CeaseFireBelow;

                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    int room = layout.Grid.RoomAt(spawner);
                    if (safeToPress && raid.TotalEnergy > Raid.SpawnCost * 2f &&
                        raid.Mobs.CountInRoom(room) < 2)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                foreach (Door door in layout.Grid.Doors)
                {
                    bool nearby = Vector2.Distance(raid.Party.Position, door.Cell) < 3f;
                    if (!door.IsForced && nearby && safeToPress == door.IsOpen)
                    {
                        raid.ToggleDoor(door.Cell);
                    }
                }

                raid.Tick(0.05f);
            }

            return raid.EnergyHarvested;
        }

        /// <summary>
        /// A dungeon with more rooms earns more than one with fewer, played the same way.
        /// </summary>
        /// <remarks>
        /// The assertion is deliberately weak — five rooms must beat two — because the interesting
        /// output is the printed curve, not the threshold. A dungeon that earns the same at five rooms
        /// as at two means the hall is a purchase that buys nothing, and the shop's headline item is
        /// decorative.
        /// </remarks>
        [Test]
        public void MoreRooms_EarnMore()
        {
            var earned = new Dictionary<int, float>();

            // THREE SEEDS, because a single one is how D29 was first measured and D31 is the note
            // about why that is not enough. The seed picks the party and the combat rolls, and if
            // the saturation after three rooms were an artefact of one draw it would show here.
            var seeds = new[] { 20260813, 4242, 99 };

            for (int rooms = 2; rooms <= 6; rooms++)
            {
                float total = 0f;
                int deepest = 0;
                RaidOutcome outcome = RaidOutcome.TimeExpired;
                DungeonLayout layout = null;

                foreach (int seed in seeds)
                {
                    layout = DungeonLayout.BuildCorridor(roomCount: rooms);
                    var raid = new Raid(layout, 0f, null, seed);
                    total += Play(raid);
                    deepest = Mathf.Max(deepest, raid.Party.VisitedRooms);
                    outcome = raid.Outcome;
                }

                earned[rooms] = total / seeds.Length;
                MooseRunnerFacade.Log(
                    $"{rooms} rooms: mean harvest {earned[rooms]:F0} over {seeds.Length} seeds, "
                    + $"last outcome {outcome}, deepest {deepest} of {rooms} rooms, "
                    + $"{layout.SpawnerCells.Count} spawners, {layout.Grid.Doors.Count} doors");
            }

            var curve = new List<string>();
            foreach (KeyValuePair<int, float> point in earned)
            {
                curve.Add($"{point.Key}r={point.Value:F0}");
            }

            MooseRunnerFacade.Log("curve: " + string.Join("  ", curve));

            Assert.Greater(earned[6], earned[2],
                $"a six-room dungeon harvested {earned[6]:F0} against a two-room dungeon's "
                + $"{earned[2]:F0}, so the hall — the most expensive thing in the shop — buys nothing");
        }

        /// <summary>
        /// The same dungeon played the same way twice harvests the same number.
        /// </summary>
        /// <remarks>
        /// The control for the test above, and worth having on its own: the whole project rests on
        /// seeded determinism, and a curve measured from a simulation that wanders is not a curve.
        /// </remarks>
        [Test]
        public void TheSameRaid_HarvestsTheSameTwice()
        {
            float first = Play(new Raid(DungeonLayout.BuildCorridor(roomCount: 3)));
            float second = Play(new Raid(DungeonLayout.BuildCorridor(roomCount: 3)));

            MooseRunnerFacade.Log($"same dungeon twice: {first:F2} then {second:F2}");
            Assert.AreEqual(first, second, 0.01f,
                "two identical raids harvested different amounts, so nothing measured here is a "
                + "measurement");
        }

        /// <summary>
        /// The one-room board every run actually opens on, which nothing here could reach.
        /// </summary>
        /// <remarks>
        /// <c>BuildCorridor</c> floors its room count at two, so the sweep above starts at a board the
        /// game never begins with — and the two-room case is the one that fails by letting the party
        /// ESCAPE, which is the outcome the whole design exists to prevent. That made the shipped
        /// opening the least-measured configuration in the game rather than the most.
        /// <para>
        /// Reached through <c>RoomPlan.Corridor(1)</c> and <c>DungeonLayout.Build</c>, which do allow
        /// it. Three seeds, per D31.
        /// </para>
        /// </remarks>
        [Test]
        public void TheOpeningOneRoomBoard_HoldsThePartyToTheBell()
        {
            var seeds = new[] { 20260813, 4242, 99 };
            var escapes = new List<int>();
            float total = 0f;

            foreach (int seed in seeds)
            {
                // FURNISHED THE WAY THE GAME FURNISHES IT. The first version of this built a bare
                // one-room plan, measured 7 energy and three escapes, and would have been reported as
                // a defect in the opening board -- but GameController.StockStarterRoom puts a slime
                // pit and a chest in through the loadout, so a bare room is a board the game never
                // presents. The cells are its cells: centre + (1,-2) and centre + (-1,2), off the
                // entrance-to-boss line so the chest is a detour rather than something walked over.
                DungeonLayout bare = DungeonLayout.Build(RoomPlan.Corridor(1), furnishedRooms: 1);
                Vector2Int centre = bare.RoomCentres[0];

                var placed = new Furnishings();
                placed.SlimeSpawners.Add(centre + new Vector2Int(1, -2));
                placed.Chests.Add(centre + new Vector2Int(-1, 2));

                DungeonLayout layout = DungeonLayout.Build(
                    RoomPlan.Corridor(1), placed: placed, furnishedRooms: 1);
                var raid = new Raid(layout, 0f, null, seed);
                float harvest = Play(raid);
                total += harvest;

                if (raid.Outcome == RaidOutcome.PartyEscaped)
                {
                    escapes.Add(seed);
                }

                MooseRunnerFacade.Log(
                    $"one room, seed {seed}: harvested {harvest:F0}, outcome {raid.Outcome}, "
                    + $"visited {raid.Party.VisitedRooms}, "
                    + $"{layout.SpawnerCells.Count} spawners, {layout.Grid.Doors.Count} doors");
            }

            MooseRunnerFacade.Log(
                $"one room: mean {total / seeds.Length:F0} over {seeds.Length} seeds, "
                + $"{escapes.Count} escapes");

            // The bar is the design's, not a number picked here: a party that walks out early stops
            // paying, so an opening board that leaks them is the opening board failing.
            Assert.IsEmpty(escapes,
                $"the one-room opening let the party escape on {escapes.Count} of {seeds.Length} "
                + "seeds, and a party that leaves early stops earning");
        }
    }
}
