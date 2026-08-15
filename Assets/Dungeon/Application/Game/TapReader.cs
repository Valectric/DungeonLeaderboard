using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Dungeon.Game
{
    /// <summary>
    /// Turns raw pointer state into taps, and refuses to call the start of a pinch one.
    /// </summary>
    /// <remarks>
    /// Every screen in this game advances or acts on a tap, and every screen also offers pinch-zoom.
    /// Those two gestures start identically — one finger touching the glass — so a reader that fires
    /// on <i>press</i> resolves the ambiguity in the worst possible direction: the first finger of a
    /// pinch is read as a tap before the second finger has even landed.
    /// <para>
    /// That is exactly what shipped. Pinching on the standings advanced past them, and pinching in
    /// the shop opened a build menu or spent energy on whatever tile happened to be under the first
    /// finger. The player's report was that pinch-zoom "clicks forward" instead of zooming, which is
    /// precisely what the code did.
    /// </para>
    /// <para>
    /// So a touch tap is recognised on <b>release</b>, and only when the whole gesture used one
    /// finger and stayed put. A mouse keeps firing on press: there is no pinch to be confused with,
    /// and press-to-act feels better on a desktop.
    /// </para>
    /// <para>
    /// The recogniser is a plain state machine fed one frame at a time through
    /// <see cref="Feed"/>, so the decision can be tested with a synthetic pinch rather than only
    /// through a real touchscreen — which no headless test has.
    /// </para>
    /// </remarks>
    public sealed class TapReader
    {
        /// <summary>
        /// How far a finger may travel and still count as a tap, in pixels.
        /// </summary>
        /// <remarks>
        /// Generous on purpose. A finger on glass never lands and lifts on the same pixel, and this
        /// game asks for taps on tiles that are large targets — a false negative (the tap does
        /// nothing) is far more annoying here than a false positive.
        /// </remarks>
        public const float TapSlop = 40f;

        /// <summary>Whether a gesture is in progress.</summary>
        private bool _tracking;

        /// <summary>Whether this gesture has been ruled out as a tap.</summary>
        private bool _cancelled;

        /// <summary>Where the gesture started.</summary>
        private Vector2 _start;

        /// <summary>The most recent position of the gesture.</summary>
        private Vector2 _last;

        /// <summary>
        /// Reads whatever pointer this device offers, and reports a tap when there was one.
        /// </summary>
        /// <remarks>
        /// Must be called exactly once per frame: the touch half is a state machine and polling it
        /// twice would feed the same frame in twice. Both device families are checked because a
        /// WebGL build runs on either, and a tablet with a mouse attached has both.
        /// </remarks>
        /// <param name="position">Screen position of the tap, when there was one.</param>
        /// <returns>True when the player tapped or clicked this frame.</returns>
        public bool TryRead(out Vector2 position)
        {
            position = default;

            Touchscreen touch = Touchscreen.current;
            if (touch != null &&
                Feed(ActiveTouchCount(), touch.primaryTouch.position.ReadValue(), out position))
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            position = mouse.position.ReadValue();
            return true;
        }

        /// <summary>
        /// Advances the recogniser by one frame of touch state.
        /// </summary>
        /// <remarks>
        /// A second finger at any point in the gesture cancels it for good, and the cancellation
        /// outlives the finger that caused it — lifting one finger of a pinch leaves a single finger
        /// on the glass, and without the latch that leftover finger would be read as a fresh tap the
        /// moment it came up.
        /// </remarks>
        /// <param name="fingers">How many fingers are on the screen this frame.</param>
        /// <param name="position">Where the primary finger is.</param>
        /// <param name="tap">Receives the tap position, when the gesture just ended as a tap.</param>
        /// <returns>True when this frame completed a tap.</returns>
        public bool Feed(int fingers, Vector2 position, out Vector2 tap)
        {
            tap = default;

            if (fingers >= 2)
            {
                _tracking = true;
                _cancelled = true;
                _last = position;
                return false;
            }

            if (fingers == 1)
            {
                if (!_tracking)
                {
                    _tracking = true;
                    _cancelled = false;
                    _start = position;
                }
                else if (!_cancelled && Vector2.Distance(_start, position) > TapSlop)
                {
                    // A finger being dragged is a scroll or a stray swipe, not a tap.
                    _cancelled = true;
                }

                _last = position;
                return false;
            }

            bool tapped = _tracking && !_cancelled;
            _tracking = false;
            _cancelled = false;

            if (!tapped)
            {
                return false;
            }

            tap = _last;
            return true;
        }

        /// <summary>Counts fingers currently on the screen.</summary>
        /// <returns>The number of pressed touches, or zero when there is no touchscreen.</returns>
        public static int ActiveTouchCount()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TouchControl finger in touch.touches)
            {
                if (finger.press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
