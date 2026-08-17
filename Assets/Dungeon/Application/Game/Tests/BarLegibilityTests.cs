using System.Collections.Generic;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Measures whether nine health bars can be read in <b>pixels</b>, and proves they cannot on a
    /// phone.
    /// </summary>
    /// <remarks>
    /// <c>BarStaggerTests</c> pins the stagger in world units — pitch greater than bar height, top
    /// bar inside the camera margin — and both are resolution-independent, so both stay green on a
    /// screen where the whole stack rasterises to a smear. Readability is a pixel question and
    /// nothing asked it.
    /// <para>
    /// It became worth asking on 2026-08-17, when D47 fixed the growth curve: until then the party
    /// never reached nine in a season, so the tightest case in the stagger's own reasoning had never
    /// once been drawn.
    /// </para>
    /// <para>
    /// <b>The answer is that it is geometrically impossible, not badly tuned.</b> These tests
    /// therefore pin the constraint rather than assert a target the design cannot meet — including
    /// one that FAILS if the impossibility ever lifts, so the next person to widen the camera margin
    /// or shrink the party is told that the phone case has become winnable.
    /// </para>
    /// </remarks>
    public sealed class BarLegibilityTests
    {
        /// <summary>Height of a bar quad in cells, from <c>PartyBars.Draw</c>.</summary>
        private const float BarHeightCells = 0.10f;

        /// <summary>Cells above its owner that the first bar sits, from <c>PartyBars.Draw</c>.</summary>
        private const float BaseLift = 0.52f;

        /// <summary>Cells of headroom the camera leaves above the dungeon, from <c>FrameCamera</c>.</summary>
        private const float CameraMargin = 1.6f;

        /// <summary>Device pixels a bar needs to survive rasterising as more than a hairline.</summary>
        private const float ReadableBarPixels = 2f;

        /// <summary>Device pixels of clear space two bars need to read as two.</summary>
        private const float ReadableGapPixels = 1f;


        /// <summary>
        /// How many pixels one dungeon cell covers, framed the way the game frames a raid.
        /// </summary>
        /// <param name="size">Screen size in pixels.</param>
        /// <returns>Pixels per cell.</returns>
        private static float PixelsPerCell(Vector2Int size)
        {
            // A five-room corridor with its approach, in cells: the widest the shop can build, and
            // so the most zoomed-out the camera ever gets. Matches ResolutionSweepTests.
            const float halfHeight = 4.5f;
            const float halfWidth = 15f;

            float aspect = size.x / (float)size.y;
            float orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
            return size.y / (orthographicSize * 2f);
        }

        /// <summary>
        /// The stagger is the largest one that still fits under the camera margin.
        /// </summary>
        /// <remarks>
        /// Every pixel of pitch is bought from the headroom above the party, so the readable choice
        /// is the biggest pitch that keeps the ninth bar on screen. This pins that the shipped value
        /// <i>is</i> that choice — so a future reduction has to be argued for rather than drifted
        /// into, and so the "it is four hundredths from not fitting" note in
        /// <c>BarStaggerTests</c> is understood as deliberate rather than lucky.
        /// </remarks>
        [Test]
        public void TheStagger_UsesAllTheHeadroomItHas()
        {
            float available = CameraMargin - BaseLift;
            float largestFittingPitch = available / (PartyComposition.MaxSize - 1);

            MooseRunnerFacade.Log(
                $"headroom {available:F2} cells over {PartyComposition.MaxSize - 1} gaps allows a "
                + $"pitch of {largestFittingPitch:F3}; shipped pitch is {PartyBars.BarPitch}");

            Assert.LessOrEqual(PartyBars.BarPitch, largestFittingPitch + 0.001f,
                $"a pitch of {PartyBars.BarPitch} puts the ninth bar off the top of the screen");

            Assert.GreaterOrEqual(PartyBars.BarPitch, largestFittingPitch * 0.9f,
                $"the pitch is {PartyBars.BarPitch} where {largestFittingPitch:F3} would fit, so the "
                + "bars are harder to tell apart than they need to be and the headroom is wasted");
        }

        /// <summary>
        /// Nine readable bars over the party will not fit on a phone, and this is by how much.
        /// </summary>
        /// <remarks>
        /// <b>This test passes because the game cannot do it.</b> That is deliberate: the finding is
        /// a design constraint worth keeping, and an assertion is the only form of documentation
        /// that gets re-checked. It fails if the contradiction ever lifts — a wider camera margin, a
        /// smaller party, a shorter bar — which is exactly when somebody should be told that bars
        /// over heads have become viable on a phone and the alternative is no longer needed.
        /// <para>
        /// Measured 2026-08-17 at 360x780, where a cell is 12.0px: a bar is <b>1.2px</b> tall and
        /// consecutive bars are <b>0.36px</b> apart. To reach two pixels and one pixel of gap the
        /// pitch would have to be 0.25 cells, which puts the ninth bar 2.52 cells above its owner
        /// against 1.6 cells of headroom — <b>57% over</b>. No tuning of the stagger reaches it,
        /// because the stagger is already using all the room there is (see the test above).
        /// </para>
        /// <para>
        /// So the phone case needs a different answer, and D45 already listed one: a HUD roster
        /// panel, which was offered to the author alongside the lateral fan they chose. The fan
        /// fixed the desktop case. This is the measurement that says it cannot fix this one.
        /// </para>
        /// </remarks>
        [Test]
        public void NineReadableBars_CannotFitOnAPhone()
        {
            var rows = new List<string>();
            float worstBar = float.MaxValue;
            float worstGap = float.MaxValue;
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                float cell = PixelsPerCell(size);
                float barPixels = cell * BarHeightCells;
                float gapPixels = cell * (PartyBars.BarPitch - BarHeightCells);
                rows.Add($"{size.x}x{size.y}: cell {cell:F1}px, bar {barPixels:F2}px, gap {gapPixels:F2}px");

                if (barPixels < worstBar)
                {
                    worstBar = barPixels;
                    worstGap = gapPixels;
                    worstAt = size;
                }
            }

            MooseRunnerFacade.Log("nine bars in pixels -- " + string.Join("  |  ", rows));

            float cellAtWorst = PixelsPerCell(worstAt);
            float neededHeight = ReadableBarPixels / cellAtWorst;
            float neededPitch = neededHeight + (ReadableGapPixels / cellAtWorst);
            float neededTop = BaseLift + ((PartyComposition.MaxSize - 1) * neededPitch);

            MooseRunnerFacade.Log(
                $"at {worstAt.x}x{worstAt.y}: bar {worstBar:F2}px, gap {worstGap:F2}px. "
                + $"Readable would need pitch {neededPitch:F3} cells, putting the top bar at "
                + $"{neededTop:F2} against {CameraMargin} of headroom "
                + $"({(neededTop / CameraMargin) - 1f:P0} over)");

            Assert.Greater(neededTop, CameraMargin,
                $"a readable nine-bar stack now fits in {CameraMargin} cells of headroom "
                + $"({neededTop:F2} needed), so bars over heads have become viable on a phone and "
                + "the roster-panel alternative in D45 is no longer the only answer -- this test is "
                + "the notification that the constraint has lifted");
        }
    }
}
