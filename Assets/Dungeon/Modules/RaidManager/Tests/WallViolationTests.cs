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
    /// Counts how often the shipped game puts a body inside a wall, walks one through a wall, or
    /// draws a shot through one.
    /// </summary>
    /// <remarks>
    /// The author reported two things he had seen: party members walking through walls, and ranged
    /// attackers shooting "through three or four walls". Both are real — <c>Party.Glide</c> is an
    /// unchecked <c>Vector2.MoveTowards</c> and ranged targeting has neither a range limit nor a
    /// line-of-sight test — but "real" is not a number, and a fix cannot be shown to work against an
    /// anecdote.
    /// <para>
    /// So this is an <b>instrument, not a guard</b>. It measures three separate violations across
    /// every roster and logs them, and its assertions are deliberately loose: they exist to stop the
    /// measurement itself rotting (a probe that silently stops sampling reads as a clean bill of
    /// health), not to pin today's numbers. When the fix lands, the counts these print should go to
    /// zero, and only then is it worth tightening them into real guards.
    /// </para>
    /// <para>
    /// This is the project's own doctrine applied before the fact rather than after: green tests
    /// hide a broken <i>rate</i>, and this game is made of rates. Measure first.
    /// </para>
    /// </remarks>
    public sealed class WallViolationTests
    {
        /// <summary>What one raid did to the geometry.</summary>
        private struct Violations
        {
            /// <summary>Ticks that ended with a body standing inside a non-walkable cell.</summary>
            public int InsideWall;

            /// <summary>Moves whose straight path crossed a non-walkable cell.</summary>
            public int CrossedWall;

            /// <summary>Shots whose flight line crossed a non-walkable cell.</summary>
            public int ShotThroughWall;

            /// <summary>Shots fired at all, so the above can be read as a share.</summary>
            public int ShotsFired;

            /// <summary>Position samples taken, so the above can be read as a share.</summary>
            public int Samples;
        }

        /// <summary>
        /// Walks a straight line between two points and reports whether it clears every cell.
        /// </summary>
        /// <remarks>
        /// Deliberately a dumb dense sample rather than a proper DDA. It runs in a test, it needs to
        /// be obviously correct rather than fast, and a supersampled line cannot miss a wall the way
        /// a hand-rolled DDA can miss a corner.
        /// </remarks>
        /// <param name="grid">Grid to test against.</param>
        /// <param name="from">Start point, in grid units.</param>
        /// <param name="to">End point, in grid units.</param>
        /// <returns>True when every sampled cell along the line is walkable.</returns>
        private static bool LineIsClear(DungeonGrid grid, Vector2 from, Vector2 to)
        {
            float span = Vector2.Distance(from, to);
            int steps = Mathf.Max(2, Mathf.CeilToInt(span * 8f));

            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)steps);
                var cell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));

                if (!grid.IsWalkable(cell))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Plays one raid and counts every geometric violation it commits.</summary>
        /// <param name="composition">Roster to send in.</param>
        /// <param name="seed">Seed, so a reported figure can be reproduced.</param>
        /// <returns>What it did.</returns>
        private static Violations Play(PartyComposition composition, int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 4);
            var raid = new Raid(layout, 0f, composition, seed);
            var counts = new Violations();

            var previous = new Dictionary<Adventurer, Vector2>();
            foreach (Adventurer member in raid.Party.Members)
            {
                previous[member] = member.Position;
            }

            int seenShots = 0;

            while (raid.IsRunning)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) == 0)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);

                foreach (Adventurer member in raid.Party.Living)
                {
                    counts.Samples++;

                    if (!layout.Grid.IsWalkable(member.Cell))
                    {
                        counts.InsideWall++;
                    }

                    if (previous.TryGetValue(member, out Vector2 was) &&
                        !LineIsClear(layout.Grid, was, member.Position))
                    {
                        counts.CrossedWall++;
                    }

                    previous[member] = member.Position;
                }

                // Shots accumulate in the feed and age out, so only the new ones are inspected.
                IReadOnlyList<Shot> shots = raid.Shots.Shots;
                for (int i = seenShots; i < shots.Count; i++)
                {
                    counts.ShotsFired++;
                    if (!LineIsClear(layout.Grid, shots[i].From, shots[i].To))
                    {
                        counts.ShotThroughWall++;
                    }
                }

                seenShots = shots.Count;
            }

            return counts;
        }

        /// <summary>
        /// Measures wall violations across every roster and reports them.
        /// </summary>
        /// <remarks>
        /// The headline instrument. It asserts almost nothing on purpose — what matters is the
        /// figures in the log, which is what a fix will be judged against.
        /// </remarks>
        [Test]
        public void EveryRoster_IsMeasuredAgainstTheWalls()
        {
            int totalInside = 0;
            int totalCrossed = 0;
            int totalShotThrough = 0;
            int totalShots = 0;
            int totalSamples = 0;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                Violations v = Play(composition, 4242);

                totalInside += v.InsideWall;
                totalCrossed += v.CrossedWall;
                totalShotThrough += v.ShotThroughWall;
                totalShots += v.ShotsFired;
                totalSamples += v.Samples;

                MooseRunnerFacade.Log(
                    $"{composition.Name}: {v.InsideWall} samples inside a wall, {v.CrossedWall} "
                    + $"moves through a wall (of {v.Samples}), {v.ShotThroughWall}/{v.ShotsFired} "
                    + "shots through a wall");
            }

            float insideShare = totalSamples == 0 ? 0f : totalInside * 100f / totalSamples;
            float crossedShare = totalSamples == 0 ? 0f : totalCrossed * 100f / totalSamples;
            float shotShare = totalShots == 0 ? 0f : totalShotThrough * 100f / totalShots;

            MooseRunnerFacade.Log(
                $"TOTAL: inside a wall {totalInside} ({insideShare:F2}% of {totalSamples} samples), "
                + $"moved through a wall {totalCrossed} ({crossedShare:F2}%), "
                + $"shot through a wall {totalShotThrough}/{totalShots} ({shotShare:F1}%)");

            // Loose, and pointed at the instrument rather than the behaviour: a probe that stops
            // sampling would otherwise report a clean game.
            Assert.Greater(totalSamples, 1000,
                "the probe barely sampled anything, so its zero counts would mean nothing");
        }

        /// <summary>
        /// A ranged attacker has no range limit at all, which is half of why shots cross walls.
        /// </summary>
        /// <remarks>
        /// Separated from the geometry count because it is a different defect with a different fix.
        /// <c>Raid.SwingParty</c> gives Ranged and Mage <c>shoots = true</c>, which short-circuits
        /// the only distance check in the method — so an archer can hit a target at any distance
        /// inside the candidate set. Line of sight is a second, independent hole.
        /// </remarks>
        [Test]
        public void ARangedAttacker_HasNoRangeLimit()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 4);
            var raid = new Raid(layout, 0f, PartyComposition.All[0], 909);

            float furthestShot = 0f;
            int seenShots = 0;

            while (raid.IsRunning)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) == 0)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);

                IReadOnlyList<Shot> shots = raid.Shots.Shots;
                for (int i = seenShots; i < shots.Count; i++)
                {
                    furthestShot = Mathf.Max(
                        furthestShot, Vector2.Distance(shots[i].From, shots[i].To));
                }

                seenShots = shots.Count;
            }

            MooseRunnerFacade.Log(
                $"the longest shot of the raid travelled {furthestShot:F1} cells "
                + $"(melee reach is {Party.MeleeReach:F2})");

            Assert.Greater(furthestShot, 0f, "nobody shot at all, so nothing was measured");
        }
    }
}
