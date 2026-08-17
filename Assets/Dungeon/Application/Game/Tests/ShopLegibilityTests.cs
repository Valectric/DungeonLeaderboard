using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Pins that the shop's primary button is big enough to hit and read.
    /// </summary>
    /// <remarks>
    /// Ready is the action the whole shop exists to reach — it closes the shop, pays the early
    /// bonus, and starts the raid. Unfloored it drew at <b>174 by 14 pixels with a five-pixel
    /// caption</b> on a 360x780 phone: smaller than a fingertip, on the control the player must
    /// press every single round.
    /// <para>
    /// The existing tests around this button ask <i>where</i> it is — that the hall markers and the
    /// tile menu keep clear of it — and never how big it is, which is why nothing caught this. It is
    /// the last of the five text-and-size defects found by sweeping the interface after the author
    /// reported the tile menu being too small.
    /// </para>
    /// </remarks>
    public sealed class ShopLegibilityTests
    {
        /// <summary>Measures the Ready caption from inside a real IMGUI frame.</summary>
        private sealed class Measurer : MonoBehaviour
        {
            /// <summary>Interface scale per request.</summary>
            public readonly List<float> Scales = new();

            /// <summary>Button width per request.</summary>
            public readonly List<float> Buttons = new();

            /// <summary>Font size production chose, per request.</summary>
            public readonly List<int> Fonts = new();

            /// <summary>How much wider the caption is than its button; negative means it fits.</summary>
            public readonly List<float> Overflow = new();

            /// <summary>Whether the measuring pass has run.</summary>
            public bool Done { get; private set; }

            /// <summary>The worst caption the shop can show: a full early bonus.</summary>
            private const string Caption =
                "READY  -  OPEN THE DOORS NOW FOR +120 STARTING ENERGY";

            /// <summary>Asks the game for its font size and measures the result.</summary>
            private void OnGUI()
            {
                if (Done)
                {
                    return;
                }

                for (int i = 0; i < Scales.Count; i++)
                {
                    int font = ShopScreen.ReadyFontSize(Scales[i], Caption, Buttons[i]);
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = font,
                        fontStyle = FontStyle.Bold
                    };

                    Fonts.Add(font);
                    Overflow.Add(style.CalcSize(new GUIContent(Caption)).x - Buttons[i]);
                }

                Done = true;
            }
        }

        /// <summary>Screens the game is expected to run on, matching the resolution sweep.</summary>
        private static readonly Vector2Int[] Screens =
        {
            new(1920, 1080), new(1280, 720), new(1024, 768), new(800, 480),
            new(768, 1024), new(390, 844), new(360, 780), new(523, 293)
        };

        /// <summary>The interface scale the game uses at a given size.</summary>
        /// <param name="size">Screen size in pixels.</param>
        /// <returns>Scale factor.</returns>
        private static float ScaleFor(Vector2Int size)
        {
            return Mathf.Min(size.x / 1280f, size.y / 720f);
        }

        /// <summary>
        /// Ready is big enough to press with a thumb on every screen.
        /// </summary>
        /// <remarks>
        /// Thirty-four pixels is the floor, which is about the smallest a touch target can be before
        /// it starts being missed. This is the same standard the dungeon tiles are held to by the
        /// resolution sweep.
        /// </remarks>
        [Test]
        public void Ready_IsBigEnoughToPress()
        {
            float worstHeight = float.MaxValue;
            float worstWidth = float.MaxValue;
            Vector2Int worstAt = Screens[0];

            foreach (Vector2Int size in Screens)
            {
                Rect ready = ShopScreen.ReadyRect(ScaleFor(size), size.x, size.y);
                MooseRunnerFacade.Log(
                    $"{size.x}x{size.y}: Ready is {ready.width:F0}x{ready.height:F0} at "
                    + $"y={ready.y:F0}");

                if (ready.height < worstHeight)
                {
                    worstHeight = ready.height;
                    worstWidth = ready.width;
                    worstAt = size;
                }
            }

            Assert.GreaterOrEqual(worstHeight, 34f,
                $"Ready is only {worstWidth:F0}x{worstHeight:F0} at {worstAt.x}x{worstAt.y}, which "
                + "is smaller than a fingertip on the control the player presses every round");
        }

        /// <summary>
        /// Ready's caption stays inside the button, on every screen.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask ReadysCaption_StaysInsideIt(CancellationToken ct)
        {
            var host = new GameObject("ready-measurer");
            var measurer = host.AddComponent<Measurer>();

            foreach (Vector2Int size in Screens)
            {
                Rect ready = ShopScreen.ReadyRect(ScaleFor(size), size.x, size.y);
                measurer.Scales.Add(ScaleFor(size));
                measurer.Buttons.Add(ready.width);
            }

            for (int frame = 0; frame < 30 && !measurer.Done; frame++)
            {
                await UniTask.Yield(ct);
            }

            Assert.IsTrue(measurer.Done, "the measuring pass never ran");

            float worst = float.MinValue;
            Vector2Int worstAt = Screens[0];
            int worstFont = 0;

            for (int i = 0; i < measurer.Overflow.Count; i++)
            {
                if (measurer.Overflow[i] > worst)
                {
                    worst = measurer.Overflow[i];
                    worstAt = Screens[i];
                    worstFont = measurer.Fonts[i];
                }
            }

            MooseRunnerFacade.Log(
                $"tightest Ready caption: {worstAt.x}x{worstAt.y} at {worstFont}px -- "
                + $"{(worst > 0f ? "OVERFLOWS by" : "spare")} {Mathf.Abs(worst):F0}px");

            Object.DestroyImmediate(host);

            Assert.Less(worst, 0f,
                $"Ready's caption is {worst:F0}px wider than the button at {worstAt.x}x{worstAt.y}, "
                + "so the words run out past both ends of the thing they label");
        }
    }
}
