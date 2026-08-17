using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// The screens this game is expected to run on, and the interface scale each produces.
    /// </summary>
    /// <remarks>
    /// One copy, because there were seven. The mobile sweep of 2026-08-17 added six test classes in
    /// a day and each carried its own literal list of resolutions and its own <c>ScaleFor</c> — so a
    /// screen added to the sweep would have been added to one file and silently missing from the
    /// rest, and a change to how the game computes its scale would have had to be chased through six.
    /// <para>
    /// That is the same failure the tests themselves keep finding in production: a formula with more
    /// than one copy is a formula that will disagree with itself. Worth fixing in the test code for
    /// exactly the reason it is worth fixing in the game.
    /// </para>
    /// </remarks>
    public static class Screens
    {
        /// <summary>
        /// Every size the game is checked against, from a desktop monitor to the narrowest phone.
        /// </summary>
        /// <remarks>
        /// The two upright phones and the 523x293 itch embed are the ones that find things: the
        /// embed because it is what a jam voter sees first, and the phones because the interface
        /// scale there is 0.28 and anything unfloored draws at a quarter size.
        /// </remarks>
        public static readonly Vector2Int[] All =
        {
            new(1920, 1080), new(1280, 720), new(1024, 768), new(800, 480),
            new(768, 1024), new(390, 844), new(360, 780), new(523, 293)
        };

        /// <summary>The upright phones, where the interface is tightest.</summary>
        public static readonly Vector2Int[] Phones =
        {
            new(390, 844), new(360, 780)
        };

        /// <summary>
        /// The interface scale the game uses at a given size.
        /// </summary>
        /// <remarks>
        /// <b>Asks the game rather than mirroring it.</b> This used to restate
        /// <c>GameController</c>'s arithmetic, with a comment saying so, because the rule was
        /// private — which meant every legibility test in the sweep was checking a copy of the
        /// formula against itself and would have passed with production broken. The rule is now
        /// <c>internal</c> and answered for an arbitrary size, so these tests measure the scale the
        /// game will actually lay out at.
        /// </remarks>
        /// <param name="size">Screen size in pixels.</param>
        /// <returns>Scale factor.</returns>
        public static float ScaleFor(Vector2Int size)
        {
            return GameController.ScaleFor(size.x, size.y);
        }
    }
}
