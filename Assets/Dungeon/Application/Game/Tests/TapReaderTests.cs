using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins the one decision that separates a tap from the start of a pinch.
    /// </summary>
    /// <remarks>
    /// The shipped reader fired on <i>press</i>, so the first finger of a two-finger zoom was a tap
    /// before the second finger arrived. On the standings that advanced past them; in the shop it
    /// opened a build menu or spent energy on whichever tile was under the finger. The player's
    /// report — pinch-zoom "clicks forward" instead of zooming — described the code exactly.
    /// <para>
    /// These drive <see cref="TapReader.Feed"/> with synthetic frames rather than a touchscreen,
    /// which no headless test has. That is the seam the class was built around: the device poll is
    /// three lines and the gesture decision is all of the risk.
    /// </para>
    /// </remarks>
    public sealed class TapReaderTests
    {
        /// <summary>A point well inside the screen, used as the resting finger position.</summary>
        private static readonly Vector2 Somewhere = new(400f, 300f);

        /// <summary>Feeds a frame and reports whether it completed a tap.</summary>
        /// <param name="reader">Reader under test.</param>
        /// <param name="fingers">Fingers on the glass this frame.</param>
        /// <param name="at">Where the primary finger is.</param>
        /// <returns>True when the frame produced a tap.</returns>
        private static bool Frame(TapReader reader, int fingers, Vector2 at)
        {
            return reader.Feed(fingers, at, out _);
        }

        /// <summary>One finger down and up is a tap, reported at the point it was lifted.</summary>
        [Test]
        public void AFingerDownAndUp_IsATap()
        {
            var reader = new TapReader();

            Assert.IsFalse(Frame(reader, 1, Somewhere), "a finger going down is not yet a tap");
            Assert.IsTrue(reader.Feed(0, Somewhere, out Vector2 tap), "lifting it should tap");
            Assert.AreEqual(Somewhere, tap, "the tap belongs where the finger was");
        }

        /// <summary>
        /// A pinch never produces a tap, however it is unwound.
        /// </summary>
        /// <remarks>
        /// The exact sequence a phone reports: one finger lands, the second joins it, they move,
        /// then they come off one at a time. Every frame of that must stay silent -- including the
        /// stretch at the end where a single finger is on the glass again, which is what a
        /// press-triggered reader could not tell from a fresh tap.
        /// </remarks>
        [Test]
        public void APinch_NeverTaps()
        {
            var reader = new TapReader();
            var second = new Vector2(600f, 320f);

            Assert.IsFalse(Frame(reader, 1, Somewhere), "first finger down");
            Assert.IsFalse(Frame(reader, 2, Somewhere), "second finger joins");
            Assert.IsFalse(Frame(reader, 2, new Vector2(360f, 300f)), "fingers spread");
            Assert.IsFalse(Frame(reader, 2, second), "and spread further");
            Assert.IsFalse(Frame(reader, 1, second), "one finger comes off");
            Assert.IsFalse(Frame(reader, 0, second),
                "and the last one leaving must NOT count as a tap -- this is the bug");
        }

        /// <summary>A gesture ruled out by a second finger does not poison the next one.</summary>
        /// <remarks>
        /// The other half of the latch. Cancelling for good is only correct if it is also cleared
        /// for good: a player who pinches to zoom and then taps a spawner has to have the spawner
        /// respond, or the fix trades one dead control for another.
        /// </remarks>
        [Test]
        public void AfterAPinch_TheNextTapStillWorks()
        {
            var reader = new TapReader();

            Frame(reader, 1, Somewhere);
            Frame(reader, 2, Somewhere);
            Frame(reader, 0, Somewhere);

            Assert.IsFalse(Frame(reader, 1, Somewhere), "a fresh finger goes down");
            Assert.IsTrue(Frame(reader, 0, Somewhere), "and lifting it taps again");
        }

        /// <summary>A finger dragged across the screen is not a tap.</summary>
        [Test]
        public void ADraggedFinger_IsNotATap()
        {
            var reader = new TapReader();
            Vector2 far = Somewhere + new Vector2(TapReader.TapSlop * 3f, 0f);

            Frame(reader, 1, Somewhere);
            Frame(reader, 1, Somewhere + new Vector2(TapReader.TapSlop * 1.5f, 0f));
            Frame(reader, 1, far);

            Assert.IsFalse(Frame(reader, 0, far), "a swipe must not fire a verb");
        }

        /// <summary>A finger that wobbles a little still taps.</summary>
        /// <remarks>
        /// A finger on glass never lands and lifts on the same pixel. A reader that demands one
        /// would leave the game feeling broken on a phone in a way that is very hard to describe.
        /// </remarks>
        [Test]
        public void AWobblyFinger_StillTaps()
        {
            var reader = new TapReader();
            Vector2 nudged = Somewhere + new Vector2(TapReader.TapSlop * 0.4f, 3f);

            Frame(reader, 1, Somewhere);
            Frame(reader, 1, nudged);

            Assert.IsTrue(reader.Feed(0, nudged, out Vector2 tap), "a normal tap wobbles");
            Assert.AreEqual(nudged, tap);
        }

        /// <summary>
        /// A second finger landing on a later frame still cancels the gesture.
        /// </summary>
        /// <remarks>
        /// The timing that produced the bug. Fingers do not land on the same frame, so there is
        /// always a window in which a pinch looks exactly like a tap -- and the whole point of
        /// deciding on release is that the window has closed by then.
        /// </remarks>
        [Test]
        public void ASecondFingerArrivingLate_StillCancels()
        {
            var reader = new TapReader();

            for (int frame = 0; frame < 6; frame++)
            {
                Assert.IsFalse(Frame(reader, 1, Somewhere), $"frame {frame} of one finger resting");
            }

            Frame(reader, 2, Somewhere);
            Assert.IsFalse(Frame(reader, 0, Somewhere),
                "the gesture became a pinch, so it cannot end as a tap");

            MooseRunnerFacade.Log("a late second finger cancels a gesture that had looked like a tap");
        }
    }
}
