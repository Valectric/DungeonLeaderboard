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
            return Play(composition, seed, DungeonLayout.BuildCorridor(roomCount: 4));
        }

        /// <summary>Plays one raid on a given dungeon and counts what it does to the geometry.</summary>
        /// <param name="composition">Roster to send in.</param>
        /// <param name="seed">Seed, so a reported figure can be reproduced.</param>
        /// <param name="layout">Dungeon to raid.</param>
        /// <returns>What it did.</returns>
        private static Violations Play(PartyComposition composition, int seed, DungeonLayout layout)
        {
            var raid = new Raid(layout, 0f, composition, seed);
            var counts = new Violations();

            var previous = new Dictionary<Adventurer, Vector2>();
            foreach (Adventurer member in raid.Party.Members)
            {
                previous[member] = member.Position;
            }

            // Who has actually set foot in the dungeon. Nobody is measured before they do.
            var entered = new HashSet<Adventurer>();

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
                    // The procession into the dungeon does not count. A party is deliberately strung
                    // out along the approach at tick zero -- the trail is seeded outside the
                    // entrance so they read as marching in -- and the approach is scenery rather
                    // than grid, so every follower still on it is standing on an unwalkable cell by
                    // construction.
                    //
                    // Left in, it is a fixed cost of about 130 samples per raid that shows up as 2%
                    // of a long corridor run and 13.5% of a short one-room run. The census breaking
                    // it down was unambiguous: all of it inside the first three seconds, none after,
                    // all of it at (0,3) and (-1,3) with the entrance at (1,3), and all of it the
                    // back of the column -- healer, mage, archer, in that order.
                    if (!entered.Contains(member))
                    {
                        if (!layout.Grid.IsWalkable(member.Cell))
                        {
                            previous[member] = member.Position;
                            continue;
                        }

                        entered.Add(member);
                    }

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
        /// Reports who ends up inside a wall, when, and where.
        /// </summary>
        /// <remarks>
        /// A share of samples says a number is bad and nothing about what to change. This breaks it
        /// down by role, by second, and by cell, which is the difference between "the geometry is
        /// broken" and "the party files in through an approach that is outside the grid".
        /// </remarks>
        /// <param name="layout">Dungeon to play.</param>
        private static void Census(DungeonLayout layout)
        {
            var raid = new Raid(layout, 0f, PartyComposition.Opening, 4242);
            var byRole = new Dictionary<AdventurerRole, int>();
            var byCell = new Dictionary<Vector2Int, int>();
            int early = 0;
            int late = 0;
            int samples = 0;
            float elapsed = 0f;

            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                elapsed += 0.02f;

                foreach (Adventurer member in raid.Party.Living)
                {
                    samples++;
                    if (layout.Grid.IsWalkable(member.Cell))
                    {
                        continue;
                    }

                    byRole[member.Role] = byRole.GetValueOrDefault(member.Role) + 1;
                    byCell[member.Cell] = byCell.GetValueOrDefault(member.Cell) + 1;

                    if (elapsed < 3f)
                    {
                        early++;
                    }
                    else
                    {
                        late++;
                    }
                }
            }

            var worst = new List<KeyValuePair<Vector2Int, int>>(byCell);
            worst.Sort((a, b) => b.Value.CompareTo(a.Value));

            var roles = new List<string>();
            foreach (KeyValuePair<AdventurerRole, int> pair in byRole)
            {
                roles.Add($"{pair.Key} {pair.Value}");
            }

            var cells = new List<string>();
            for (int i = 0; i < worst.Count && i < 6; i++)
            {
                cells.Add($"{worst[i].Key} x{worst[i].Value}");
            }

            MooseRunnerFacade.Log(
                $"census of {samples} samples: first 3s {early}, after {late}; "
                + $"by role [{string.Join(", ", roles)}]; worst cells [{string.Join(", ", cells)}]; "
                + $"entrance {layout.EntranceCell}, grid {layout.Grid.Width}x{layout.Grid.Height}");
        }

        /// <summary>
        /// The single room the game now opens on is measured against its own walls.
        /// </summary>
        /// <remarks>
        /// A new geometry the instrument has never seen. Every figure this file has ever produced
        /// came from a corridor, where the party spends most of a raid walking a straight line
        /// between rooms — and the shipped opening dungeon is now one five-by-five box in which four
        /// adventurers, their formation spacing, a chest detour and a stream of slimes are all
        /// pressed together against four walls at once. Standing off from a threat, backing away
        /// wounded and kiting are exactly the behaviours that push a body into masonry, and they
        /// have never been measured anywhere this tight.
        /// </remarks>
        [Test]
        public void TheOpeningRoom_IsMeasuredAgainstItsWalls()
        {
            // The shape the game ships: one room, a slime pit deep in it, a chest off the walking
            // line. Rebuilt here rather than imported, because DungeonManager must not learn what a
            // shop item is and this assembly cannot see the controller that places them.
            var furniture = new Furnishings();
            furniture.SlimeSpawners.Add(new Vector2Int(4, 1));
            furniture.Chests.Add(new Vector2Int(2, 5));

            DungeonLayout layout = DungeonLayout.Build(
                RoomPlan.Corridor(1), placed: furniture, furnishedRooms: 1);

            int totalInside = 0;
            int totalCrossed = 0;
            int totalShotThrough = 0;
            int totalShots = 0;
            int totalSamples = 0;

            foreach (PartyComposition composition in PartyComposition.All)
            {
                Violations v = Play(composition, 4242, layout);

                totalInside += v.InsideWall;
                totalCrossed += v.CrossedWall;
                totalShotThrough += v.ShotThroughWall;
                totalShots += v.ShotsFired;
                totalSamples += v.Samples;
            }

            // WHERE, and WHO. A share on its own cannot tell a party still filing in through the
            // entrance from a healer backing into masonry mid-fight, and those are opposite
            // problems: the first is the approach being outside the grid by construction, the
            // second is the positioning rules failing in a room too small to back off in.
            Census(layout);

            float insideShare = totalSamples == 0 ? 0f : totalInside * 100f / totalSamples;
            float crossedShare = totalSamples == 0 ? 0f : totalCrossed * 100f / totalSamples;
            float shotShare = totalShots == 0 ? 0f : totalShotThrough * 100f / totalShots;

            MooseRunnerFacade.Log(
                $"ONE ROOM: inside a wall {totalInside} ({insideShare:F2}% of {totalSamples} "
                + $"samples), moved through a wall {totalCrossed} ({crossedShare:F2}%), "
                + $"shot through a wall {totalShotThrough}/{totalShots} ({shotShare:F1}%)");

            Assert.Greater(totalSamples, 1000,
                "the probe barely sampled anything, so its zero counts would mean nothing");

            // A guard rather than an instrument this time. Counting only adventurers who have
            // actually entered, a corridor sits at nearly nothing; a single room coming back worse
            // would mean the geometry fixes hold only where there is room to manoeuvre, which is
            // the opposite of where they are needed.
            Assert.Less(insideShare, 2f,
                $"{insideShare:F1}% of position samples in the opening room are inside a wall -- "
                + "the geometry fixes do not survive a room this small");
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
