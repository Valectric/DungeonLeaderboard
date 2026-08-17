using System.Collections.Generic;
using System.Linq;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.PartyManager.Tests
{
    /// <summary>
    /// Measures the shape of the party as it grows, not just that it moves.
    /// </summary>
    /// <remarks>
    /// A nine-strong party in single file trails its ninth member 4.96 cells behind the tank — a
    /// whole room. The player's move is to fill the room the party is <i>in</i>, so a tail that long
    /// means mobs are spent on a fight most of the party has not reached, and the wound curve that
    /// pays the player is applied to a fraction of the roster. The author asked for the formation to
    /// fan out sideways instead.
    /// <para>
    /// The risk that buys is members standing in rock, which no existing assertion would have caught
    /// for a <i>follower</i>: they glide to a formation point rather than pathfind to it. So both
    /// halves are measured here — the tail gets shorter, and nobody leaves the floor.
    /// </para>
    /// </remarks>
    public sealed class FormationTests
    {
        /// <summary>Builds a party of the given size on a real dungeon layout.</summary>
        /// <param name="size">How many members to field.</param>
        /// <param name="grid">The grid the party walks on.</param>
        /// <returns>A party standing at the entrance, in marching order.</returns>
        private static Party PartyOf(int size, out DungeonGrid grid)
        {
            DungeonLayout layout = DungeonLayout.Build(RoomPlan.Corridor(3));
            grid = layout.Grid;

            var roles = new List<AdventurerRole>();
            for (int i = 0; i < size; i++)
            {
                roles.Add(i switch
                {
                    0 => AdventurerRole.Tank,
                    1 => AdventurerRole.Healer,
                    2 => AdventurerRole.Ranged,
                    _ => AdventurerRole.Mage
                });
            }

            return new Party(
                grid,
                layout.EntranceCell,
                layout.BossCell,
                new PartyComposition("TEST PARTY", "walks in a line", roles),
                layout.RoomCentres);
        }

        /// <summary>Walks a party forward for a while, returning every position seen.</summary>
        /// <param name="party">The party to walk.</param>
        /// <param name="seconds">How long to simulate.</param>
        /// <returns>Each tick's member positions.</returns>
        private static List<Vector2[]> Walk(Party party, float seconds)
        {
            var frames = new List<Vector2[]>();
            var noThreats = new List<Vector2>();
            var noTraps = new List<Vector2Int>();
            var noChests = new List<Vector2Int>();

            for (float t = 0f; t < seconds; t += 0.02f)
            {
                party.Tick(0.02f, noThreats, noTraps, noChests);
                frames.Add(party.Living.Select(m => m.Position).ToArray());
            }

            return frames;
        }

        /// <summary>
        /// How far the last member trails the leader, by party size.
        /// </summary>
        /// <remarks>
        /// The claim the change is making. A nine-strong party must not string out across a room; it
        /// should be about as deep as a four-strong one, and wider instead.
        /// </remarks>
        [Test]
        public void AGrownParty_DoesNotStringOutAcrossTheDungeon()
        {
            var depths = new List<string>();
            float nine = 0f;
            float four = 0f;

            foreach (int size in new[] { 4, 6, 9 })
            {
                Party party = PartyOf(size, out DungeonGrid _);
                List<Vector2[]> frames = Walk(party, 12f);

                float worst = 0f;
                foreach (Vector2[] frame in frames.Skip(100))
                {
                    for (int i = 1; i < frame.Length; i++)
                    {
                        worst = Mathf.Max(worst, Vector2.Distance(frame[0], frame[i]));
                    }
                }

                depths.Add($"{size}:{worst:F2}");
                if (size == 9)
                {
                    nine = worst;
                }

                if (size == 4)
                {
                    four = worst;
                }
            }

            MooseRunnerFacade.Log($"furthest member from the tank, by party size -- {string.Join("  ", depths)}");

            Assert.Less(nine, 3.2f,
                $"a nine-strong party trails its last member {nine:F2} cells behind the tank, so the "
                + "room the player is filling does not contain the party");

            Assert.Less(nine, four * 2.2f,
                $"a nine-strong party is {nine / four:F1} times as deep as a four-strong one "
                + $"({nine:F2} against {four:F2}), which is most of the way back to single file");
        }

        /// <summary>
        /// Nobody in a fanned-out party ever stands inside rock.
        /// </summary>
        /// <remarks>
        /// The cost of widening the formation, and the one an existing suite would miss: followers
        /// glide to a formation point instead of pathfinding, so an unwalkable point is simply
        /// walked to. Corridors here are a single cell wide, so this fails immediately if the
        /// lateral offset is forced rather than given up.
        /// </remarks>
        [Test]
        public void NoMemberOfAFannedParty_EverStandsInRock()
        {
            // Four is the control. It is the untouched single-file path, so if it also stands in
            // rock the fan is not what put anyone there and tightening the fan would fix nothing.
            var rates = new List<string>();
            float fanned = 0f;
            float singleFile = 0f;

            foreach (int size in new[] { 4, 9 })
            {
                Party party = PartyOf(size, out DungeonGrid grid);
                List<Vector2[]> frames = Walk(party, 25f);

                int violations = 0;
                int samples = 0;
                foreach (Vector2[] frame in frames)
                {
                    foreach (Vector2 position in frame)
                    {
                        samples++;
                        var cell = new Vector2Int(
                            Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
                        if (!grid.IsWalkable(cell))
                        {
                            violations++;
                        }
                    }
                }

                float rate = violations / (float)samples;
                rates.Add($"{size}:{violations}/{samples} ({rate:P1})");
                if (size == 9)
                {
                    fanned = rate;
                }
                else
                {
                    singleFile = rate;
                }
            }

            MooseRunnerFacade.Log($"member-ticks inside rock -- {string.Join("  ", rates)}");

            // A comparison, not a bound. Measured 2026-08-17: single file 2.6 %, fanned 3.1 %. The
            // absolute figure is a PRE-EXISTING defect this test found rather than caused -- a
            // follower glides to its formation point instead of pathfinding to it, so it clips the
            // inside of a corner as the trail rounds it, and that happens in single file too. The
            // first version of this test asserted zero and would have charged the fan for all of it.
            Assert.LessOrEqual(fanned, singleFile + 0.01f,
                $"a fanned-out party stands in rock {fanned:P1} of the time against {singleFile:P1} "
                + "for the single-file party it is compared with, so widening the formation is what "
                + "walks people through walls");
        }

        /// <summary>
        /// Members never end up standing on top of one another.
        /// </summary>
        /// <remarks>
        /// The failure mode the fallback invites, and the reason to look for it rather than wait for
        /// it: when a flank cell is rock the offset is given up and the member drops onto the centre
        /// line — so in a corridor all three members of a rank want the <i>same</i> point, and three
        /// sprites at one spot read as one adventurer. Single file never had this, because every
        /// rank stood at its own distance back along the trail.
        /// </remarks>
        [Test]
        public void MembersOfARank_DoNotStackOnOneAnother()
        {
            // The control first, and it is the third time today that the control has been the whole
            // answer: four members on the untouched single-file path. If they close up too, the fan
            // is not what does it.
            Party control = PartyOf(4, out DungeonGrid _);
            float controlWorst = float.MaxValue;
            foreach (Vector2[] frame in Walk(control, 25f).Skip(400))
            {
                for (int i = 0; i < frame.Length; i++)
                {
                    for (int j = i + 1; j < frame.Length; j++)
                    {
                        controlWorst = Mathf.Min(controlWorst, Vector2.Distance(frame[i], frame[j]));
                    }
                }
            }

            Party party = PartyOf(9, out DungeonGrid _);
            List<Vector2[]> frames = Walk(party, 25f);

            // Reported in two windows on purpose. The trail is seeded only two cells long, and nine
            // members in single file want 4.96 -- so every rank past the end of it clamps onto the
            // trail's first point and they genuinely are on one square, before anything about the
            // fan is involved. Splitting the windows says whether stacking is a formation fault or
            // simply the party being longer than the path it is following.
            float earlyWorst = float.MaxValue;
            foreach (Vector2[] frame in frames.Take(300))
            {
                for (int i = 0; i < frame.Length; i++)
                {
                    for (int j = i + 1; j < frame.Length; j++)
                    {
                        earlyWorst = Mathf.Min(earlyWorst, Vector2.Distance(frame[i], frame[j]));
                    }
                }
            }

            float worst = float.MaxValue;
            int touching = 0;
            int pairs = 0;

            foreach (Vector2[] frame in frames.Skip(400))
            {
                for (int i = 0; i < frame.Length; i++)
                {
                    for (int j = i + 1; j < frame.Length; j++)
                    {
                        float gap = Vector2.Distance(frame[i], frame[j]);
                        worst = Mathf.Min(worst, gap);
                        pairs++;
                        if (gap < 0.2f)
                        {
                            touching++;
                        }
                    }
                }
            }

            MooseRunnerFacade.Log(
                $"closest two members stood -- single-file control of four: {controlWorst:F3}; "
                + $"nine, first 6s: {earlyWorst:F3}, after 8s: {worst:F3}; "
                + $"{touching}/{pairs} pairs within 0.2");

            // Name the culprits rather than the symptom: which ranks, and where they were standing.
            for (int f = 400; f < frames.Count; f++)
            {
                Vector2[] frame = frames[f];
                bool found = false;
                for (int i = 0; i < frame.Length && !found; i++)
                {
                    for (int j = i + 1; j < frame.Length && !found; j++)
                    {
                        if (Vector2.Distance(frame[i], frame[j]) < 0.05f)
                        {
                            MooseRunnerFacade.Log(
                                $"tick {f}: ranks {i} and {j} both at {frame[j]}");
                            MooseRunnerFacade.Log(
                                "  whole party: "
                                + string.Join("  ", frame.Select((p, k) => $"{k}{p}")));
                            found = true;
                        }
                    }
                }

                if (found)
                {
                    break;
                }
            }

            // Compared against the control, not against zero: a party whose leader turns back on
            // its own trail closes up whatever shape it is in, and four members in single file do it
            // too. What must not happen is the FAN making it materially worse.
            Assert.GreaterOrEqual(worst, controlWorst - 0.02f,
                $"nine members closed to {worst:F3} cells against {controlWorst:F3} for four in "
                + "single file, so the fan is what puts one sprite on top of another");
        }

        /// <summary>
        /// Draws the formation over the dungeon floor so its shape can be read, not inferred.
        /// </summary>
        /// <remarks>
        /// A distance in cells says the tail is closer; it does not say the party is a block rather
        /// than a zigzag, and this project's own doctrine is that composition faults are only ever
        /// visible in a picture. This is the cheapest picture that fits in a log.
        /// </remarks>
        /// <param name="grid">Grid to draw as floor and rock.</param>
        /// <param name="members">Member positions, leader first.</param>
        /// <returns>One line per row of the map.</returns>
        private static List<string> Plot(DungeonGrid grid, Vector2[] members)
        {
            // Half-cell resolution. At whole cells the lateral offsets -- half a cell either side of
            // the centre line -- land exactly on a cell boundary and round back onto it, so the fan
            // is invisible and members stack on one mark. The first version of this plot did that
            // and read as "the fan never opens" while the depth measurement said it plainly had.
            var marks = new Dictionary<Vector2Int, char>();
            for (int i = 0; i < members.Length; i++)
            {
                var half = new Vector2Int(
                    Mathf.RoundToInt(members[i].x * 2f), Mathf.RoundToInt(members[i].y * 2f));
                marks[half] = i == 0 ? '@' : (char)('1' + (i - 1));
            }

            int minX = marks.Keys.Min(c => c.x) - 4;
            int maxX = marks.Keys.Max(c => c.x) + 4;
            int minY = marks.Keys.Min(c => c.y) - 4;
            int maxY = marks.Keys.Max(c => c.y) + 4;

            var rows = new List<string>();
            for (int y = maxY; y >= minY; y--)
            {
                var row = new System.Text.StringBuilder();
                for (int x = minX; x <= maxX; x++)
                {
                    var half = new Vector2Int(x, y);
                    var cell = new Vector2Int(
                        Mathf.RoundToInt(x * 0.5f), Mathf.RoundToInt(y * 0.5f));
                    row.Append(marks.TryGetValue(half, out char mark)
                        ? mark
                        : grid.IsWalkable(cell) ? '.' : '#');
                }

                rows.Add(row.ToString());
            }

            return rows;
        }

        /// <summary>
        /// Photographs the formation of a nine-strong party mid-walk.
        /// </summary>
        /// <remarks>
        /// No assertion beyond "it drew something": the point is the map in the test log, which is
        /// where a zigzag, a party walking in a wall, or a fan that never opens would show up.
        /// </remarks>
        [Test]
        public void TheShapeOfANineStrongParty_IsDrawnForReading()
        {
            Party party = PartyOf(9, out DungeonGrid grid);
            List<Vector2[]> frames = Walk(party, 14f);

            foreach (int at in new[] { 200, 450, 690 })
            {
                if (at >= frames.Count)
                {
                    continue;
                }

                MooseRunnerFacade.Log($"--- nine strong, tick {at} ---");
                foreach (string row in Plot(grid, frames[at]))
                {
                    MooseRunnerFacade.Log(row);
                }
            }

            Assert.Greater(frames.Count, 0, "the party never took a step");
        }

        /// <summary>
        /// A party cut down to four walks in single file again.
        /// </summary>
        /// <remarks>
        /// The formation is chosen from the <i>living</i> count, so losses tighten it back up rather
        /// than leaving four survivors spread across a room in a shape built for nine.
        /// </remarks>
        [Test]
        public void TheFormation_TightensAsThePartyIsCutDown()
        {
            Assert.AreEqual(1, MarchingOrder.AbreastFor(4), "an opening party must still walk in single file");
            Assert.AreEqual(1, MarchingOrder.AbreastFor(3), "three survivors must walk in single file");
            Assert.AreEqual(2, MarchingOrder.AbreastFor(6), "a six-strong party walks two abreast");
            Assert.AreEqual(3, MarchingOrder.AbreastFor(9), "a nine-strong party walks three abreast");
        }
    }
}
