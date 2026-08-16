using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Pins what the carved entrance opening actually does, as opposed to what it looks like.
    /// </summary>
    /// <remarks>
    /// The west wall beside the entrance is carved to a <c>Doorway</c> so the first room reads as
    /// somewhere a party walks into rather than a sealed box. It carries <b>no <c>Door</c></b>,
    /// which was chosen so the player could never shut the party out — and that has a second
    /// consequence worth pinning here rather than discovering in a raid:
    /// <c>DungeonGrid.IsWalkable</c> treats a doorway as passable only when a door exists and is
    /// open, so a doorless one is passable to nobody.
    /// <para>
    /// That is the safe outcome, and it is why none of the containment suites moved when the carve
    /// landed: nothing can <i>path</i> through it, so no monster leaves the dungeon and no wounded
    /// adventurer retreats off the grid.
    /// </para>
    /// <para>
    /// It is not merely scenery, though — that was the guess, and measuring says otherwise. The
    /// party spawns west of the entrance and walks east, and because a body's cell is its rounded
    /// continuous position, the arriving adventurers really do register on the opening for the
    /// first ~1.6s of a raid. The party walks in through the hole in the wall; it just cannot walk
    /// back out through it. Both halves of that are pinned below.
    /// </para>
    /// </remarks>
    public sealed class EntranceOpeningTests
    {
        /// <summary>The opening exists on the unfurnished build path, and nothing can cross it.</summary>
        [Test]
        public void TheEntranceOpening_IsScenery_NotAWayThrough()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 3);
            AssertSceneryOpening(layout, "unfurnished corridor");
        }

        /// <summary>
        /// The opening survives the furnished build path — the one every real raid takes.
        /// </summary>
        /// <remarks>
        /// This case exists because the carve was first written below an early return that fires
        /// whenever furniture is supplied, so it appeared in every test and in no actual raid. The
        /// bug was invisible to assertions and was caught by photographing the opening frame.
        /// </remarks>
        [Test]
        public void TheEntranceOpening_SurvivesTheFurnishedPath()
        {
            DungeonLayout bare = DungeonLayout.Build(RoomPlan.Corridor(3), furnishedRooms: 1);
            Vector2Int centre = bare.RoomCentres[0];

            var placed = new Furnishings();
            placed.SlimeSpawners.Add(centre + new Vector2Int(1, -2));
            placed.Chests.Add(centre + new Vector2Int(-1, 2));

            DungeonLayout layout = DungeonLayout.Build(
                RoomPlan.Corridor(3), placed: placed, furnishedRooms: 1);
            AssertSceneryOpening(layout, "furnished corridor (the real raid path)");
        }

        /// <summary>
        /// Across a spread of seeds, only the party's arrival ever touches the opening — and no
        /// monster ever does.
        /// </summary>
        /// <remarks>
        /// The opening is off the walkable grid, yet a body still registers on it, because
        /// <c>Cell</c> rounds a continuous position and the party spawns west of the entrance and
        /// walks in. That is the entrance reading correctly: adventurers arrive *through* the hole
        /// in the wall. Two things would not be correct and are what this test actually guards:
        /// <list type="bullet">
        /// <item>a monster on the opening at any time — it has left the dungeon;</item>
        /// <item>an adventurer on it after the arrival window — the wounded-party retreat has
        /// found a way off the map, which is the risk the carve introduced.</item>
        /// </list>
        /// </remarks>
        [Test]
        public void AcrossSeeds_OnlyTheArrivalTouchesTheOpening()
        {
            // 3s against a measured worst case of 1.6s across 12 seeds. Verified to have teeth:
            // tightening this to 1s turns the test red, so it is a gate and not a formality.
            const float arrivalWindow = 3f;
            var monsters = new List<string>();
            var lateAdventurers = new List<string>();
            float latestAdventurer = 0f;

            for (int seed = 0; seed < 12; seed++)
            {
                DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 3);
                var opening = new Vector2Int(layout.EntranceCell.x, layout.EntranceCell.y - 1);
                var raid = new Raid(layout, 0f, null, seed);

                int guard = 0;
                while (raid.IsRunning && guard++ < 4000)
                {
                    raid.Tick(0.05f);
                    float t = guard * 0.05f;

                    foreach (Adventurer member in raid.Party.Members)
                    {
                        if (member.Cell != opening) { continue; }

                        latestAdventurer = Mathf.Max(latestAdventurer, t);
                        if (t > arrivalWindow)
                        {
                            lateAdventurers.Add($"seed {seed}: {member.Role} at t={t:F1}s");
                        }
                    }

                    foreach (Mob mob in raid.Mobs.Living)
                    {
                        if (mob.Cell == opening)
                        {
                            monsters.Add($"seed {seed}: {mob.Kind} at t={t:F1}s");
                        }
                    }
                }

                Assert.AreNotEqual(RaidOutcome.InProgress, raid.Outcome,
                    $"seed {seed} never resolved");
            }

            MooseRunnerFacade.Log(
                $"12 seeds: last adventurer on the opening t={latestAdventurer:F1}s "
                + $"(arrival window {arrivalWindow}s), late adventurers={lateAdventurers.Count}, "
                + $"monsters={monsters.Count}");

            Assert.IsEmpty(monsters,
                "a monster stood on the entrance opening. It is outside every room and off the "
                + "walkable grid, so a monster there has left the dungeon -- the room-bounded "
                + "pursuit rule the retreat valve depends on has been broken: "
                + string.Join("; ", monsters));
            Assert.IsEmpty(lateAdventurers,
                $"an adventurer stood on the entrance opening more than {arrivalWindow}s into the "
                + "raid, which is past the arrival walk-in. A wounded party retreats toward the "
                + "entrance, so this is the carve handing them a way off the map: "
                + string.Join("; ", lateAdventurers));
        }

        /// <summary>Asserts one layout's opening is carved, doorless, and impassable.</summary>
        /// <param name="layout">The layout to inspect.</param>
        /// <param name="what">Human label for the build path, used in failure messages.</param>
        private static void AssertSceneryOpening(DungeonLayout layout, string what)
        {
            var opening = new Vector2Int(layout.EntranceCell.x, layout.EntranceCell.y - 1);
            DungeonGrid grid = layout.Grid;

            MooseRunnerFacade.Log(
                $"{what}: entrance {layout.EntranceCell}, opening {opening}, "
                + $"kind={grid.KindAt(opening)}, hasDoor={grid.DoorAt(opening) != null}, "
                + $"walkable={grid.IsWalkable(opening)}");

            Assert.AreEqual(CellKind.Doorway, grid.KindAt(opening),
                $"{what}: the south wall below the entrance was not carved, so the starting room "
                + "is a sealed box again");
            Assert.IsNull(grid.DoorAt(opening),
                $"{what}: a Door was registered on the entrance opening, which makes it tappable -- "
                + "shutting it would strand the party outside the dungeon for the whole raid");
            Assert.IsFalse(grid.IsWalkable(opening),
                $"{what}: the entrance opening became walkable, so a monster can now leave the "
                + "dungeon and a retreating adventurer can walk off the grid");
        }
    }
}
