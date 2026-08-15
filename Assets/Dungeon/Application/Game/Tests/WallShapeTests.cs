using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins the wall shapes the dungeon would draw, ahead of art that can draw them.
    /// </summary>
    /// <remarks>
    /// The tileset's real failure is structural rather than tonal: every wall cell has always been
    /// given the same sprite regardless of its neighbours, so the masonry reads as a repeated band
    /// and no corner ever turns. <c>DungeonScenery.WallMask</c> is the missing half — the standard
    /// four-bit blob numbering (N 1, E 2, S 4, W 8) that any tileset shipping edges and corners is
    /// drawn against.
    /// <para>
    /// Worth pinning <b>now</b>, while the art does not exist. The lookup is inert until sixteen
    /// <c>tiles/wall-N</c> sprites are imported, which means a mistake in it would be invisible
    /// until the day somebody drops a tileset in and the dungeon comes out inside out. These cases
    /// are the ones a person would check by eye against a picture, written down instead.
    /// </para>
    /// </remarks>
    public sealed class WallShapeTests
    {
        /// <summary>A three-room corridor, which is the shape the game grew out of.</summary>
        private static DungeonGrid Corridor() => DungeonLayout.BuildCorridor(roomCount: 3).Grid;

        /// <summary>The first floor cell found, walking from the bottom left.</summary>
        /// <param name="grid">Grid to search.</param>
        /// <returns>A floor cell.</returns>
        private static Vector2Int AnyFloor(DungeonGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.KindAt(new Vector2Int(x, y)) == CellKind.Floor)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }

            Assert.Fail("the dungeon has no floor at all");
            return default;
        }

        /// <summary>
        /// A wall with nothing but rock around it is the fully-enclosed case.
        /// </summary>
        /// <remarks>
        /// The corner of the map: off-grid neighbours count as solid, because the dungeon is
        /// surrounded by rock and treating the void as open floor would draw a lit edge along the
        /// outside of the map, facing nothing.
        /// </remarks>
        [Test]
        public void AWallSurroundedByRock_IsFullyEnclosed()
        {
            DungeonGrid grid = Corridor();
            var corner = new Vector2Int(0, 0);

            Assert.AreEqual(CellKind.Wall, grid.KindAt(corner), "the map corner should be wall");
            Assert.AreEqual(15, DungeonScenery.WallMask(grid, corner),
                "a wall in the corner of the map has rock on all four sides");
        }

        /// <summary>The wall directly below a floor cell reports open floor to its north.</summary>
        /// <remarks>
        /// The case that decides whether a tileset comes out right way up. North is bit 1, so a wall
        /// facing a room from below must have that bit <b>clear</b> — the piece drawn there is the
        /// one with its lit top edge showing.
        /// </remarks>
        [Test]
        public void AWallBelowAFloor_HasItsNorthSideOpen()
        {
            DungeonGrid grid = Corridor();
            Vector2Int floor = AnyFloor(grid);
            Vector2Int below = floor + Vector2Int.down;

            Assert.AreEqual(CellKind.Wall, grid.KindAt(below),
                "the cell below the lowest floor should be wall");

            int mask = DungeonScenery.WallMask(grid, below);
            MooseRunnerFacade.Log($"floor at {floor}, wall below at {below}, mask {mask}");

            Assert.AreEqual(0, mask & 1,
                $"the wall at {below} has floor to its north and reported mask {mask}, which says "
                + "it is enclosed that way -- a tileset would draw its lit edge on the wrong side");
            Assert.AreNotEqual(0, mask & 4, "and rock to its south");
        }

        /// <summary>Every mask the shipped dungeons actually use is a legal blob number.</summary>
        /// <remarks>
        /// A sweep rather than a spot check, and it doubles as the shopping list: the set of masks
        /// printed here is exactly the set of wall pieces a candidate tileset has to contain for
        /// this dungeon. Anything it does not include is a piece we would be paying for and never
        /// drawing.
        /// </remarks>
        [Test]
        public void EveryWallShapeUsed_IsALegalBlobNumber()
        {
            var seen = new System.Collections.Generic.SortedSet<int>();

            foreach (int rooms in new[] { 1, 3, 5 })
            {
                DungeonGrid grid = DungeonLayout.BuildCorridor(roomCount: rooms).Grid;

                for (int y = 0; y < grid.Height; y++)
                {
                    for (int x = 0; x < grid.Width; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (grid.KindAt(cell) != CellKind.Wall)
                        {
                            continue;
                        }

                        int mask = DungeonScenery.WallMask(grid, cell);
                        Assert.GreaterOrEqual(mask, 0);
                        Assert.LessOrEqual(mask, 15);
                        seen.Add(mask);
                    }
                }
            }

            MooseRunnerFacade.Log(
                $"wall shapes used by 1-, 3- and 5-room dungeons: {string.Join(", ", seen)} "
                + $"({seen.Count} of 16)");

            Assert.Greater(seen.Count, 4,
                "the dungeons use only a handful of wall shapes, so either the geometry is simpler "
                + "than it looks or the mask is not distinguishing them");
        }
    }
}
