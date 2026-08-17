using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.PartyManager.Tests
{
    /// <summary>
    /// Measures which party arrives in which raid, across many runs.
    /// </summary>
    /// <remarks>
    /// Written to check a report from play — <i>"skirmish is always third"</i> — <b>before</b>
    /// changing the picker, because the two possible causes want opposite repairs and picking the
    /// wrong one would hide the other. If the run seed is not spreading the parties, gating a roster
    /// to later rounds papers over a broken chain; if it spreads fine, the complaint is about the
    /// roster being too punishing that early and gating is exactly right.
    /// </remarks>
    public sealed class PartySequenceTests
    {
        /// <summary>Replays the controller's own seed chain without needing a controller.</summary>
        /// <param name="runSeed">The seed a run starts from.</param>
        /// <param name="raids">How many raids to walk forward.</param>
        /// <returns>The composition names in the order they would walk in.</returns>
        private static List<string> Sequence(int runSeed, int raids)
        {
            // Mirrors GameController.RollNextParty: raid one is always the opening roster, and every
            // later party is picked from an LCG step of the run seed, never repeating the one that
            // just raided.
            var names = new List<string> { PartyComposition.Opening.Name };
            PartyComposition previous = PartyComposition.Opening;
            int seed = runSeed;

            for (int raid = 2; raid <= raids; raid++)
            {
                seed = unchecked((seed * 1103515245) + 12345);
                previous = PartyComposition.ForSeed(seed, previous);
                names.Add(previous.Name);
            }

            return names;
        }

        /// <summary>
        /// No roster owns a fixed slot in the running order.
        /// </summary>
        /// <remarks>
        /// The report was specifically about the third party. A fair chain gives any one roster a
        /// share of roughly <c>1/(All.Length - 1)</c> at each position, given the no-immediate-repeat
        /// rule. The bar here is 34%, loose enough not to fire on ordinary clustering across a few
        /// hundred runs and far tighter than the "always" being reported.
        /// </remarks>
        [Test]
        public void NoRoster_OwnsAPositionInTheRunningOrder()
        {
            const int runs = 240;
            const int raids = 6;

            var counts = new Dictionary<int, Dictionary<string, int>>();
            for (int position = 2; position <= raids; position++)
            {
                counts[position] = new Dictionary<string, int>();
            }

            for (int run = 0; run < runs; run++)
            {
                // Spread the run seeds the way a clock does, rather than 0,1,2..., which would be a
                // kinder input than the game ever actually gets.
                List<string> sequence = Sequence(unchecked(run * 7919), raids);
                for (int position = 2; position <= raids; position++)
                {
                    string name = sequence[position - 1];
                    counts[position].TryGetValue(name, out int seen);
                    counts[position][name] = seen + 1;
                }
            }

            var complaints = new List<string>();
            foreach (KeyValuePair<int, Dictionary<string, int>> position in counts)
            {
                foreach (KeyValuePair<string, int> roster in position.Value)
                {
                    float share = roster.Value / (float)runs;
                    MooseRunnerFacade.Log(
                        $"raid {position.Key}: {roster.Key} in {roster.Value}/{runs} "
                        + $"({share * 100f:F0}%)");

                    if (share > 0.34f)
                    {
                        complaints.Add(
                            $"{roster.Key} is raid {position.Key} in {share * 100f:F0}% of runs");
                    }
                }
            }

            Assert.IsEmpty(complaints,
                "a roster owns a fixed slot in the running order, so the run seed is not spreading "
                + "the parties -- gating rosters by round would paper over that rather than fix it: "
                + string.Join("; ", complaints));
        }
    }
}
