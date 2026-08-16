using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins the stagger that keeps nine health bars readable.
    /// </summary>
    /// <remarks>
    /// D45. A party of nine bunches into a cluster, and with every bar drawn at the same height above
    /// its owner they merged into one block — measured on video, only three or four of nine were
    /// distinct, a wound in the middle of the stack was masked and a death was "completely obscured".
    /// That is the condition D8 added the bars to remove, returning by another route once the league
    /// started growing parties.
    /// <para>
    /// The fix is a per-rank vertical offset, and it is one constant. Nothing asserted it: setting
    /// <c>BarPitch</c> to zero restores the unreadable pile and no test would have noticed, which is
    /// exactly the shape of defect this project keeps finding. These are cheap and they close it.
    /// </para>
    /// </remarks>
    public sealed class BarStaggerTests
    {
        /// <summary>Height of a health bar quad, from <c>PartyBars.Draw</c>.</summary>
        private const float BarHeight = 0.10f;

        /// <summary>
        /// Consecutive bars clear each other rather than touching.
        /// </summary>
        /// <remarks>
        /// Stated against the bar's own height rather than a literal, so shrinking the bars without
        /// shrinking the pitch cannot silently reintroduce the pile.
        /// </remarks>
        [Test]
        public void ConsecutiveBars_DoNotOverlap()
        {
            MooseRunnerFacade.Log(
                $"bar pitch {PartyBars.BarPitch}, bar height {BarHeight}, "
                + $"gap {PartyBars.BarPitch - BarHeight:F3}");

            Assert.Greater(PartyBars.BarPitch, BarHeight,
                "the stagger is smaller than a bar is tall, so consecutive bars overlap and a party "
                + "that bunches up shows one block of green again -- which is the D45 defect, and the "
                + "D8 defect underneath it");
        }

        /// <summary>
        /// A full party's bars stay inside the camera's vertical margin.
        /// </summary>
        /// <remarks>
        /// The other end of the same trade. The stagger has to be big enough to separate nine bars
        /// and small enough that the top one is still on screen: <c>GameController.FrameCamera</c>
        /// allows 1.6 world units above the dungeon, and the topmost bar sits at 0.52 plus eight
        /// pitches above its owner. At the shipped 0.13 that is 1.56 against 1.6 — it fits, and it is
        /// four hundredths from not fitting, so this is the test that will say so.
        /// </remarks>
        [Test]
        public void TheTopBar_StaysInsideTheCameraMargin()
        {
            const float baseLift = 0.52f;
            const float cameraMargin = 1.6f;

            float top = baseLift + ((PartyComposition.MaxSize - 1) * PartyBars.BarPitch);
            MooseRunnerFacade.Log(
                $"top bar of {PartyComposition.MaxSize} sits {top:F2} above its owner, "
                + $"camera allows {cameraMargin:F2}");

            Assert.Less(top, cameraMargin,
                $"the top bar of a full party sits {top:F2} world units up against a camera margin "
                + $"of {cameraMargin:F2}, so the party's own health is drawn off the top of the "
                + "screen -- the bars are readable and nobody can see them");
        }
    }
}
