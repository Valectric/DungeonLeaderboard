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
            Vector2Int worstAt = Screens.All[0];

            foreach (Vector2Int size in Screens.All)
            {
                Rect ready = ShopLayout.ReadyRect(Screens.ScaleFor(size), size.x, size.y);
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
            const string caption = "READY  -  OPEN THE DOORS NOW FOR +120 STARTING ENERGY";
            var fonts = new List<int>();
            var overflow = new List<float>();

            await GuiPass.Run(() =>
            {
                foreach (Vector2Int size in Screens.All)
                {
                    Rect ready = ShopLayout.ReadyRect(Screens.ScaleFor(size), size.x, size.y);
                    int font = ShopScreen.ReadyFontSize(Screens.ScaleFor(size), caption, ready.width);
                    fonts.Add(font);
                    overflow.Add(GuiPass.Width(caption, font) - ready.width);
                }
            }, ct);

            float worst = float.MinValue;
            Vector2Int worstAt = Screens.All[0];
            int worstFont = 0;

            for (int i = 0; i < overflow.Count; i++)
            {
                if (overflow[i] > worst)
                {
                    worst = overflow[i];
                    worstAt = Screens.All[i];
                    worstFont = fonts[i];
                }
            }

            MooseRunnerFacade.Log(
                $"tightest Ready caption: {worstAt.x}x{worstAt.y} at {worstFont}px -- "
                + $"{(worst > 0f ? "OVERFLOWS by" : "spare")} {Mathf.Abs(worst):F0}px");


            Assert.Less(worst, 0f,
                $"Ready's caption is {worst:F0}px wider than the button at {worstAt.x}x{worstAt.y}, "
                + "so the words run out past both ends of the thing they label");
        }
    }
}
