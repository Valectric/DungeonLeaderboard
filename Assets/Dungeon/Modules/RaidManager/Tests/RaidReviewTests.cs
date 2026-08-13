using MooseRunner;
using NUnit.Framework;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Verifies that the star rating rewards the play the design actually wants.
    /// </summary>
    /// <remarks>
    /// The review is the game's teaching moment, so a rating that merely looked plausible would be
    /// worse than none: it would teach the wrong lesson confidently. Every assertion here is about
    /// the ordering between outcomes, which is the part a player learns from.
    /// </remarks>
    public sealed class RaidReviewTests
    {
        /// <summary>A perfect raid: alive, engaged and badly wounded for the full minute.</summary>
        private static RaidReview Perfect() =>
            RaidReview.For(RaidOutcome.TimeExpired, 480f, 4);

        /// <summary>
        /// Killing the party is the worst review available, whatever it earned on the way.
        /// </summary>
        /// <remarks>
        /// SPEC.md's central inversion, and the single most important thing the screen has to teach.
        /// A dungeon can have a magnificent minute and still ruin it in the last two seconds.
        /// </remarks>
        [Test]
        public void AWipe_IsTheWorstReviewEvenAfterAHugeHarvest()
        {
            RaidReview wiped = RaidReview.For(RaidOutcome.PartyWiped, 900f, 0);
            RaidReview quiet = RaidReview.For(RaidOutcome.TimeExpired, 10f, 4);

            MooseRunnerFacade.Log(
                $"wiped after 900 harvested = {wiped.Stars} stars, quiet raid = {quiet.Stars}");

            Assert.AreEqual(1, wiped.Stars, "killing the party must be a one-star raid");
            Assert.LessOrEqual(wiped.Stars, quiet.Stars,
                "a wipe after a huge harvest must not out-rate a dull raid where everyone lived");
        }

        /// <summary>No survivors is a wipe however the outcome was labelled.</summary>
        [Test]
        public void NoSurvivors_IsAlwaysAWipe()
        {
            RaidReview review = RaidReview.For(RaidOutcome.TimeExpired, 500f, 0);
            Assert.AreEqual(1, review.Stars, "nobody walked out, so nobody can recommend the place");
        }

        /// <summary>
        /// A party that escapes early rates below one held for the full minute.
        /// </summary>
        /// <remarks>
        /// The second losing move in the design: reaching the boss room closes the earning window,
        /// and the review has to make that as clear as the wipe.
        /// </remarks>
        [Test]
        public void EscapingEarly_RatesBelowAFullMinute()
        {
            RaidReview escaped = RaidReview.For(RaidOutcome.PartyEscaped, 60f, 4);
            RaidReview held = RaidReview.For(RaidOutcome.TimeExpired, 200f, 4);

            MooseRunnerFacade.Log($"escaped = {escaped.Stars} stars, held = {held.Stars} stars");
            Assert.Less(escaped.Stars, held.Stars,
                "letting them reach the boss room should rate below holding them the whole minute");
        }

        /// <summary>Stars rise with the takings, because the takings are the score.</summary>
        [Test]
        public void MoreHarvest_MeansMoreStars()
        {
            int previous = 0;
            foreach (float harvested in new[] { 10f, 100f, 200f, 480f })
            {
                RaidReview review = RaidReview.For(RaidOutcome.TimeExpired, harvested, 4);
                MooseRunnerFacade.Log($"{harvested:F0} harvested = {review.Stars} stars");

                Assert.GreaterOrEqual(review.Stars, previous,
                    $"earning more at {harvested:F0} rated worse than earning less");
                previous = review.Stars;
            }

            Assert.AreEqual(5, previous, "the best possible raid should reach five stars");
        }

        /// <summary>Every rating stays inside one to five, so the star row never overflows.</summary>
        [Test]
        public void EveryRating_IsBetweenOneAndFive()
        {
            foreach (RaidOutcome outcome in System.Enum.GetValues(typeof(RaidOutcome)))
            {
                foreach (float harvested in new[] { 0f, 39f, 40f, 69f, 70f, 159f, 320f, 5000f })
                {
                    foreach (int survivors in new[] { 0, 1, 4 })
                    {
                        RaidReview review = RaidReview.For(outcome, harvested, survivors);

                        Assert.GreaterOrEqual(review.Stars, 1,
                            $"{outcome} at {harvested} with {survivors} left rated below one star");
                        Assert.LessOrEqual(review.Stars, 5,
                            $"{outcome} at {harvested} with {survivors} left rated above five");
                        Assert.AreEqual(9, review.StarBar().Length,
                            "the star row must always be five slots wide");
                    }
                }
            }
        }

        /// <summary>Every review carries a headline, a quip and an instruction.</summary>
        /// <remarks>
        /// The instruction is the point of the screen. A review with an empty lesson would look
        /// finished and teach nothing.
        /// </remarks>
        [Test]
        public void EveryReview_TellsThePlayerWhatToDoNext()
        {
            foreach (RaidOutcome outcome in System.Enum.GetValues(typeof(RaidOutcome)))
            {
                foreach (float harvested in new[] { 0f, 60f, 120f, 250f, 600f })
                {
                    RaidReview review = RaidReview.For(outcome, harvested, 4);

                    Assert.IsNotEmpty(review.Headline, $"{outcome} at {harvested} has no headline");
                    Assert.IsNotEmpty(review.Quip, $"{outcome} at {harvested} has no review body");
                    Assert.IsNotEmpty(review.Lesson, $"{outcome} at {harvested} teaches nothing");
                }
            }
        }

        /// <summary>The star row fills from the left and reads at a glance.</summary>
        [Test]
        public void TheStarBar_FillsFromTheLeft()
        {
            Assert.AreEqual("* * * * *", Perfect().StarBar(), "five stars should fill every slot");
            Assert.AreEqual("* . . . .",
                RaidReview.For(RaidOutcome.PartyWiped, 0f, 0).StarBar(),
                "one star should fill only the first");
        }
    }
}
