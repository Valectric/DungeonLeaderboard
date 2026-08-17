using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Measures real text the only way Unity allows: from inside an <c>OnGUI</c> pass.
    /// </summary>
    /// <remarks>
    /// <c>GUI.skin</c> throws "You can only call GUI functions from inside OnGUI" anywhere else, so a
    /// test that wants the true width of a label has to get itself into a real IMGUI frame. Four
    /// test classes each grew their own copy of that machinery during the mobile sweep; this is the
    /// one they now share.
    /// <para>
    /// Why it matters that the measurement is real: text width is not guessable from character
    /// counts. The first estimate made during that sweep was out by thirty per cent, and every
    /// defect it found — a name overflowing its box by 219 pixels, a title line spilling off both
    /// edges of a phone — was a difference a guess would have missed.
    /// </para>
    /// </remarks>
    public sealed class TextMeasure : MonoBehaviour
    {
        /// <summary>One string to measure, at one size.</summary>
        private struct Request
        {
            /// <summary>The text.</summary>
            public string Text;

            /// <summary>Font size in pixels.</summary>
            public int Size;

            /// <summary>Whether it is drawn bold.</summary>
            public bool Bold;
        }

        private readonly List<Request> _requests = new();

        /// <summary>Measured widths, in request order, once the pass has run.</summary>
        public readonly List<float> Widths = new();

        /// <summary>Whether the measuring pass has run.</summary>
        public bool Done { get; private set; }

        /// <summary>Queues a string to be measured.</summary>
        /// <param name="text">The text.</param>
        /// <param name="size">Font size in pixels.</param>
        /// <param name="bold">Whether it is drawn bold.</param>
        public void Add(string text, int size, bool bold = true)
        {
            _requests.Add(new Request { Text = text, Size = size, Bold = bold });
        }

        /// <summary>Measures every queued string, once.</summary>
        private void OnGUI()
        {
            if (Done)
            {
                return;
            }

            foreach (Request request in _requests)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = request.Size,
                    fontStyle = request.Bold ? FontStyle.Bold : FontStyle.Normal
                };

                Widths.Add(style.CalcSize(new GUIContent(request.Text)).x);
            }

            Done = true;
        }

        /// <summary>
        /// Builds a measurer, lets a caller queue work, and runs the pass.
        /// </summary>
        /// <remarks>
        /// The host object is destroyed before returning, so a test that fails an assertion on the
        /// result does not leave a <c>MonoBehaviour</c> behind for the next one to trip over.
        /// </remarks>
        /// <param name="queue">Callback that adds the strings to measure.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The measured widths, in the order they were queued.</returns>
        public static async UniTask<List<float>> Run(
            System.Action<TextMeasure> queue, CancellationToken ct)
        {
            var host = new GameObject("text-measure");
            var measure = host.AddComponent<TextMeasure>();
            queue(measure);

            for (int frame = 0; frame < 30 && !measure.Done; frame++)
            {
                await UniTask.Yield(ct);
            }

            bool ran = measure.Done;
            var widths = new List<float>(measure.Widths);
            Object.DestroyImmediate(host);

            Assert.IsTrue(ran, "the measuring pass never ran, so nothing was measured");
            return widths;
        }
    }
}
