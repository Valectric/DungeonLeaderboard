using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.LeagueManager.Tests
{
    /// <summary>
    /// Measures what the competition actually asks of the player, per round.
    /// </summary>
    /// <remarks>
    /// The soak plays twelve competitions with a competent bot and the bot wins all twelve. That is
    /// either a well-tuned league answering good play, or a league that is not a contest at all, and
    /// the soak cannot tell the difference — it only asserts that each competition <i>resolves</i>.
    /// <para>
    /// These run the league arithmetic directly against a player who harvests a fixed amount every
    /// round, which is the cheapest way to ask the question the soak cannot: how well do you have to
    /// play to win, and does playing badly actually lose?
    /// </para>
    /// </remarks>
    public sealed class CompetitivenessTests
    {
        /// <summary>How a competition ended for a player of a given standard.</summary>
        private struct Outcome
        {
            /// <summary>Whether the player was the last dungeon standing.</summary>
            public bool Won;

            /// <summary>Round the competition ended on.</summary>
            public int Rounds;
        }

        /// <summary>
        /// Runs a whole competition for a player who harvests the same amount every round.
        /// </summary>
        /// <param name="perRaid">Energy the player banks each round.</param>
        /// <param name="seed">Seed for the rivals' rolls.</param>
        /// <returns>How it ended.</returns>
        private static Outcome Compete(float perRaid, int seed)
        {
            var league = new LeagueTable(seed);
            int guard = 0;

            while (league.Remaining > 1 && !league.PlayerRelegated && guard++ < 40)
            {
                league.SubmitRaid(perRaid);
                if (league.PlayerRelegated)
                {
                    break;
                }

                league.CollapseRelegated();
            }

            return new Outcome { Won = league.PlayerWon, Rounds = league.Round };
        }

        /// <summary>Wins across a spread of seeds, for one standard of play.</summary>
        /// <param name="perRaid">Energy the player banks each round.</param>
        /// <returns>How many of twenty competitions the player won.</returns>
        private static int WinsOverTwentySeeds(float perRaid)
        {
            int won = 0;
            for (int seed = 500; seed < 520; seed++)
            {
                if (Compete(perRaid, seed).Won)
                {
                    won++;
                }
            }

            return won;
        }

        /// <summary>
        /// A player who barely plays does not win the competition.
        /// </summary>
        /// <remarks>
        /// The floor. If a raid the player hardly touches still takes the trophy, nothing above it
        /// means anything.
        /// </remarks>
        [Test]
        public void BarelyPlaying_LosesTheCompetition()
        {
            int won = WinsOverTwentySeeds(LeagueTable.BadRun);
            MooseRunnerFacade.Log($"harvesting {LeagueTable.BadRun:F0} a round won {won}/20");

            Assert.LessOrEqual(won, 1,
                $"a player who barely plays won {won} of twenty competitions");
        }

        /// <summary>
        /// A player who plays genuinely well wins the competition.
        /// </summary>
        /// <remarks>
        /// The ceiling, and the promise the handicap makes: rivals top out at ninety per cent of a
        /// good raid, so a good raid every round cannot be beaten.
        /// </remarks>
        [Test]
        public void PlayingWell_WinsTheCompetition()
        {
            int won = WinsOverTwentySeeds(LeagueTable.GoodRun);
            MooseRunnerFacade.Log($"harvesting {LeagueTable.GoodRun:F0} a round won {won}/20");

            Assert.GreaterOrEqual(won, 19,
                $"a player at the top of their game won only {won} of twenty competitions, so the "
                + "league does not answer skill");
        }

        /// <summary>
        /// The competition is decided somewhere in the middle of the range, not at either end.
        /// </summary>
        /// <remarks>
        /// The real question the soak cannot answer. A league where every standard of play above
        /// hopeless wins is a formality with extra steps; one where only a perfect raid survives is a
        /// wall. Sweeping the range shows where the line actually sits and fails if there is no line
        /// at all.
        /// </remarks>
        [Test]
        public void TheCompetition_IsDecidedInTheMiddleOfTheRange()
        {
            var curve = new List<string>();
            float firstWinning = -1f;

            for (float perRaid = 25f; perRaid <= 500f; perRaid += 25f)
            {
                int won = WinsOverTwentySeeds(perRaid);
                curve.Add($"{perRaid:F0}:{won}");

                if (firstWinning < 0f && won >= 10)
                {
                    firstWinning = perRaid;
                }
            }

            MooseRunnerFacade.Log($"wins out of 20 by harvest -- {string.Join(" ", curve)}");
            MooseRunnerFacade.Log($"the competition turns at a harvest of {firstWinning:F0} a round");

            Assert.Greater(firstWinning, LeagueTable.BadRun * 2f,
                $"the player starts winning at a harvest of {firstWinning:F0}, barely above a raid "
                + "they did not play -- the competition is a formality");

            Assert.Less(firstWinning, LeagueTable.GoodRun,
                $"the player only starts winning at {firstWinning:F0}, at or beyond the best raid "
                + "the game measures -- the competition is unwinnable");
        }
    }
}
