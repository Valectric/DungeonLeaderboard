using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins that a point anywhere inside a tile maps back to that tile.
    /// </summary>
    /// <remarks>
    /// <b>Every tap in the game goes through this pair.</b> The three verbs are all "point at a
    /// thing on the board": the player touches a door, a spawner or a trap, the screen point becomes
    /// a world point, and <c>WorldToCell</c> decides which square that was. If it disagreed with
    /// <c>CellToWorld</c> by half a cell, taps near the edge of a tile would open the door next to
    /// the one under the finger.
    /// <para>
    /// <b>No existing test could see that, and it is worth being precise about why.</b> Ten of them
    /// drive real clicks — the shop, the verbs, the E2E, the sweeps — and every one computes its
    /// target as <c>Camera.WorldToScreenPoint(DungeonView.CellToWorld(cell))</c>, which is the exact
    /// <i>centre</i> of the tile. A centre survives almost any rounding rule. A player's thumb does
    /// not land on centres, and on a phone a tile is a few dozen pixels wide.
    /// </para>
    /// <para>
    /// So this walks the inside of the tile instead, and includes a boundary case as its control:
    /// without one, a <c>WorldToCell</c> that returned the same cell for everything would pass every
    /// assertion here.
    /// </para>
    /// </remarks>
    public sealed class CellRoundTripTests
    {
        /// <summary>Cells worth checking, including the negative ones the approach scenery uses.</summary>
        private static readonly Vector2Int[] Cells =
        {
            new(0, 0), new(1, 3), new(7, 4), new(12, 9), new(-1, 3), new(-2, 0), new(23, 23)
        };

        /// <summary>
        /// The centre of a cell converts back to that cell.
        /// </summary>
        /// <remarks>
        /// The weakest of the three and the one everything else already relies on, stated here so a
        /// failure separates "the round trip is broken" from "the edges are".
        /// </remarks>
        [Test]
        public void ACellCentre_RoundTrips()
        {
            foreach (Vector2Int cell in Cells)
            {
                Vector2Int back = DungeonView.WorldToCell(DungeonView.CellToWorld(cell));
                Assert.AreEqual(cell, back,
                    $"the centre of {cell} converted back to {back}");
            }
        }

        /// <summary>
        /// Any point inside a cell converts back to that cell, not just its centre.
        /// </summary>
        /// <remarks>
        /// The one that matters, and the one nothing was asking. Sampled to just inside the
        /// half-cell boundary in every direction, including the diagonals, because a rule that
        /// floors instead of rounding would be correct at the centre and wrong across a whole half
        /// of every tile.
        /// </remarks>
        [Test]
        public void AnyPointInsideACell_RoundTripsToIt()
        {
            float half = DungeonView.CellSize * 0.5f;
            float[] offsets = { -0.49f, -0.25f, 0f, 0.25f, 0.49f };
            int checkedPoints = 0;

            foreach (Vector2Int cell in Cells)
            {
                Vector3 centre = DungeonView.CellToWorld(cell);

                foreach (float dx in offsets)
                {
                    foreach (float dy in offsets)
                    {
                        var point = new Vector3(
                            centre.x + (dx * DungeonView.CellSize),
                            centre.y + (dy * DungeonView.CellSize),
                            0f);

                        Vector2Int back = DungeonView.WorldToCell(point);
                        checkedPoints++;

                        Assert.AreEqual(cell, back,
                            $"a point {dx:+0.00;-0.00} x {dy:+0.00;-0.00} of a cell from the centre "
                            + $"of {cell} landed on {back} -- a tap that far inside a tile hits the "
                            + "wrong one, and half of every tile is further from its centre than "
                            + $"{half:F2} world units");
                    }
                }
            }

            MooseRunnerFacade.Log(
                $"{checkedPoints} points across {Cells.Length} cells all round-tripped");
        }

        /// <summary>
        /// A point past the halfway line belongs to the next cell along.
        /// </summary>
        /// <remarks>
        /// The control. Everything above is satisfied by a <c>WorldToCell</c> that has stopped
        /// discriminating at all — one that always returned the cell it was asked about would pass
        /// each of those assertions — so this checks the boundary actually moves the answer. It is
        /// the same lesson as the renderer-count test earlier today: a flat result and a dead
        /// instrument look identical until something known-different is measured.
        /// </remarks>
        [Test]
        public void APointPastTheBoundary_BelongsToTheNextCell()
        {
            var cell = new Vector2Int(4, 4);
            Vector3 centre = DungeonView.CellToWorld(cell);

            Vector2Int right = DungeonView.WorldToCell(
                new Vector3(centre.x + (0.6f * DungeonView.CellSize), centre.y, 0f));
            Vector2Int up = DungeonView.WorldToCell(
                new Vector3(centre.x, centre.y + (0.6f * DungeonView.CellSize), 0f));

            MooseRunnerFacade.Log(
                $"from {cell}: 0.6 right reads {right}, 0.6 up reads {up}");

            Assert.AreEqual(new Vector2Int(5, 4), right,
                $"a point six tenths of a cell to the right of {cell} read as {right}, so the "
                + "conversion is not discriminating between neighbouring tiles at all");
            Assert.AreEqual(new Vector2Int(4, 5), up,
                $"a point six tenths of a cell above {cell} read as {up}");
        }
    }
}
