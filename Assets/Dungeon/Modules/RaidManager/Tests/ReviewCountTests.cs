using MooseRunner;
using NUnit.Framework;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Checks the wipe review reports as many deaths as there were adventurers.
    /// </summary>
    /// <remarks>
    /// The line read "filing four death notices" and was correct for every party the game could send
    /// until the league started growing them on 2026-08-16. From raid six a wipe is five notices and
    /// from raid eighteen it is nine, so the screen was about to start miscounting the bodies it
    /// reports on — quietly, on the one screen whose whole job is to tell the player they did the
    /// worst possible thing.
    /// </remarks>
    public sealed class ReviewCountTests
    {
        /// <summary>The wipe review names the right number of dead, at every party size.</summary>
        [Test]
        public void AWipeReview_CountsTheWholeParty()
        {
            var expected = new (int Size, string Word)[]
            {
                (4, "four"), (5, "five"), (6, "six"), (7, "seven"), (8, "eight"), (9, "nine")
            };

            foreach ((int size, string word) in expected)
            {
                RaidReview review = RaidReview.For(RaidOutcome.PartyWiped, 0f, 0, size);
                MooseRunnerFacade.Log($"party of {size}: \"{review.Quip}\"");

                StringAssert.Contains($"{word} death notices", review.Quip,
                    $"a party of {size} wiped and the review reported a different number, so the "
                    + "screen is miscounting the bodies it is reporting on");
            }
        }
    }
}
