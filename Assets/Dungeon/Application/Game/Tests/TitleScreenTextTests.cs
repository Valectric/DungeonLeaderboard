using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Checks that the title screen's longest line fits the narrowest screen the game ships to.
    /// </summary>
    /// <remarks>
    /// SPEC.md makes the standings the title screen — no menu, no logo — so it is the first thing a
    /// jam voter sees, and on a phone. The line naming the next party is drawn centred across the
    /// full width with no wrapping, and <b>both halves of it grew on 2026-08-17</b>: rosters can now
    /// field up to nine, so the size clause appears, and it is appended to whichever roster name is
    /// longest.
    /// <para>
    /// This is the third measurement of this kind today and the technique keeps paying: text width
    /// is not guessable from character counts, and the two phone defects of 2026-08-16 were both
    /// text that did not fit. Measured with <c>GUIStyle.CalcSize</c> against the game's own font,
    /// from inside an <c>OnGUI</c> pass, asking <c>LeagueScreen</c> for both the string and the size
    /// it will be drawn at rather than rebuilding either.
    /// </para>
    /// </remarks>
    public sealed class TitleScreenTextTests
    {
        /// <summary>
        /// The party announcement fits the screen, for every roster at full strength.
        /// </summary>
        /// <remarks>
        /// Every roster is checked at <c>MaxSize</c>, because the size clause only appears above the
        /// base four and the longest name paired with " NINE STRONG" is the worst case the game can
        /// actually produce. A line that overruns is not clipped by IMGUI — it is centred, so it
        /// spills off <i>both</i> edges and the player loses the ends of it.
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask TheNextPartyLine_FitsEveryScreen(CancellationToken ct)
        {
            var where = new List<Vector2Int>();
            var lines = new List<string>();
            var widths = new List<float>();

            foreach (Vector2Int size in Screens.All)
            {
                foreach (PartyComposition roster in PartyComposition.All)
                {
                    where.Add(size);
                    lines.Add(LeagueScreen.Announcement(roster.Grown(PartyComposition.MaxSize)));
                }
            }

            await GuiPass.Run(() =>
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    int font = LeagueScreen.FittedAnnouncementFontSize(
                        Screens.ScaleFor(where[i]), lines[i], where[i].x);
                    widths.Add(GuiPass.Width(lines[i], font));
                }
            }, ct);

            float worstOverflow = float.MinValue;
            string worstLine = string.Empty;
            Vector2Int worstAt = Screens.All[0];

            for (int i = 0; i < widths.Count; i++)
            {
                float overflow = widths[i] - where[i].x;
                if (overflow > worstOverflow)
                {
                    worstOverflow = overflow;
                    worstLine = lines[i];
                    worstAt = where[i];
                }
            }

            MooseRunnerFacade.Log(
                $"longest title line: \"{worstLine}\" at {worstAt.x}x{worstAt.y} -- "
                + $"{(worstOverflow > 0f ? "OVERFLOWS by" : "spare")} {Mathf.Abs(worstOverflow):F0}px");


            Assert.Less(worstOverflow, 0f,
                $"\"{worstLine}\" is {worstOverflow:F0}px wider than a {worstAt.x}px screen, and it "
                + "is centred rather than clipped, so it spills off both edges of the title screen "
                + "-- the first thing a jam voter sees");
        }
    }
}
