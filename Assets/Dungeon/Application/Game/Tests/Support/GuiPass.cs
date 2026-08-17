using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Runs a block of code inside a real <c>OnGUI</c> pass, once.
    /// </summary>
    /// <remarks>
    /// <c>GUI.skin</c> throws "You can only call GUI functions from inside OnGUI" anywhere else, so a
    /// test that wants the true width of a label — or that wants to ask the game what font size it
    /// will draw at — has to get itself into a real IMGUI frame. Four test classes each grew their
    /// own <c>MonoBehaviour</c> to do that during the mobile sweep of 2026-08-17.
    /// <para>
    /// Deliberately a <b>general pass</b> rather than a text-measuring service. The first attempt at
    /// sharing this took a list of strings and sizes, and could not replace three of the four copies:
    /// they call <c>ShopLayout.NameFontSize</c>, <c>ShopScreen.ReadyFontSize</c> and
    /// <c>LeagueScreen.FittedAnnouncementFontSize</c>, which measure the font themselves and so must
    /// run inside the pass too. Asking production what it will draw, rather than restating its
    /// arithmetic, is the property that made those tests find real defects — so the helper has to
    /// accommodate it.
    /// </para>
    /// <para>
    /// Why text width has to be measured at all: it is not guessable from character counts. The
    /// first estimate made during that sweep was out by thirty per cent, and the defects it found —
    /// a name overflowing its box by 219 pixels, a title line spilling off both edges of a phone —
    /// were differences a guess would have missed.
    /// </para>
    /// </remarks>
    public sealed class GuiPass : MonoBehaviour
    {
        private Action _work;

        /// <summary>Whether the pass has run.</summary>
        public bool Done { get; private set; }

        /// <summary>Runs the queued work inside a real IMGUI frame, once.</summary>
        private void OnGUI()
        {
            if (Done)
            {
                return;
            }

            _work?.Invoke();
            Done = true;
        }

        /// <summary>
        /// Runs a block inside <c>OnGUI</c> and returns when it has happened.
        /// </summary>
        /// <remarks>
        /// The host object is destroyed before returning, so a test that fails an assertion on the
        /// result does not leave a <c>MonoBehaviour</c> behind for the next one to trip over.
        /// </remarks>
        /// <param name="work">What to run inside the pass.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable pass.</returns>
        public static async UniTask Run(Action work, CancellationToken ct)
        {
            var host = new GameObject("gui-pass");
            var pass = host.AddComponent<GuiPass>();
            pass._work = work;

            for (int frame = 0; frame < 30 && !pass.Done; frame++)
            {
                await UniTask.Yield(ct);
            }

            bool ran = pass.Done;
            UnityEngine.Object.DestroyImmediate(host);

            Assert.IsTrue(ran, "the GUI pass never ran, so nothing was measured");
        }

        /// <summary>
        /// Width of a string as the game would draw it.
        /// </summary>
        /// <remarks>Call from inside <see cref="Run"/>; it touches <c>GUI.skin</c>.</remarks>
        /// <param name="text">The text.</param>
        /// <param name="size">Font size in pixels.</param>
        /// <param name="bold">Whether it is drawn bold.</param>
        /// <returns>Width in pixels.</returns>
        public static float Width(string text, int size, bool bold = true)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal
            };

            return style.CalcSize(new GUIContent(text)).x;
        }
    }
}
