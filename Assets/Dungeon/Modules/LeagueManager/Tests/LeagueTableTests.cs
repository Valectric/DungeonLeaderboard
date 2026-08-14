using System.Collections.Generic;
using System.Linq;
using Dungeon.LeagueManager;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.LeagueManager.Tests
{
    /// <summary>
    /// Guards the standings, which SPEC.md section 6 makes the game's opening screen.
    /// </summary>
    /// <remarks>
    /// The whole hook is one sentence a new player reads off the board: "I am 14th, 16th is death, I
    /// need to climb." Each test below defends one clause of it.
    /// </remarks>
    public sealed class LeagueTableTests
    {
        /// <summary>The league is twenty dungeons with distinct names.</summary>
        [Test]
        public void League_HasTwentyDistinctlyNamedDungeons()
        {
            var league = new LeagueTable(seed: 1);
            Assert.AreEqual(LeagueTable.Size, league.Entries.Count);

            var names = league.Entries.Select(e => e.Name).ToList();
            CollectionAssert.AllItemsAreUnique(names);
            MooseRunnerFacade.Log("top five: " + string.Join(", ", names.Take(5)));
        }

        /// <summary>Exactly one row is the player, and it opens around fourteenth.</summary>
        [Test]
        public void Player_StartsAroundFourteenth()
        {
            var league = new LeagueTable(seed: 7);
            Assert.AreEqual(1, league.Entries.Count(e => e.IsPlayer));

            MooseRunnerFacade.Log($"player opens at position {league.PlayerPosition}");
            Assert.That(league.PlayerPosition, Is.InRange(12, 16),
                "the spec puts the player around 14th so the climb is visible from the first screen");
        }

        /// <summary>The player opens clear of the relegation zone, with somewhere to fall.</summary>
        [Test]
        public void Player_StartsOutOfTheRelegationZone()
        {
            var league = new LeagueTable(seed: 3);
            Assert.IsFalse(league.PlayerRelegated);
        }

        /// <summary>Standings are always ordered by score, best first.</summary>
        [Test]
        public void Standings_AreAlwaysSortedByScore()
        {
            var league = new LeagueTable(seed: 11);
            for (int round = 0; round < 8; round++)
            {
                league.SubmitRaid(400f + (round * 120f));
                var scores = league.Entries.Select(e => e.Score).ToList();
                CollectionAssert.AreEqual(scores.OrderByDescending(s => s).ToList(), scores,
                    $"table fell out of order on round {round}");
            }
        }

        /// <summary>A good raid moves the player up; the table is a real contest.</summary>
        [Test]
        public void ABigRaid_ClimbsTheTable()
        {
            var league = new LeagueTable(seed: 5);
            int before = league.PlayerPosition;

            league.SubmitRaid(12000f);

            MooseRunnerFacade.Log($"position {before} -> {league.PlayerPosition} after a big raid");
            Assert.Less(league.PlayerPosition, before, "a huge haul must climb the table");
        }

        /// <summary>Harvesting nothing, repeatedly, drops the player into the relegation zone.</summary>
        /// <remarks>
        /// This is the pressure the whole design rests on: doing nothing must actually kill you, or
        /// the clock and the energy rate carry no weight.
        /// </remarks>
        [Test]
        public void HarvestingNothing_EventuallyRelegatesThePlayer()
        {
            var league = new LeagueTable(seed: 13);
            int rounds = 0;
            while (!league.PlayerRelegated && rounds < 40)
            {
                league.SubmitRaid(0f);
                rounds++;
            }

            MooseRunnerFacade.Log($"relegated after {rounds} empty raids at position {league.PlayerPosition}");
            Assert.IsTrue(league.PlayerRelegated, "idling must eventually relegate the player");
        }

        /// <summary>Rivals earn every round, so the table never sits still.</summary>
        [Test]
        public void Rivals_ScoreEveryRound()
        {
            var league = new LeagueTable(seed: 17);
            List<float> before = league.Entries.Where(e => !e.IsPlayer)
                .Select(e => e.Score).OrderBy(s => s).ToList();

            league.SubmitRaid(500f);

            List<float> after = league.Entries.Where(e => !e.IsPlayer)
                .Select(e => e.Score).OrderBy(s => s).ToList();
            Assert.AreEqual(before.Count, after.Count);
            Assert.IsTrue(after.Zip(before, (a, b) => a > b).All(moved => moved),
                "every rival should have earned something");
        }

        /// <summary>Previous positions are recorded, so the shift animation has something to show.</summary>
        [Test]
        public void SubmitRaid_RecordsWhereEveryoneWas()
        {
            var league = new LeagueTable(seed: 19);
            league.SubmitRaid(9000f);

            var previous = league.Entries.Select(e => e.PreviousPosition).OrderBy(p => p).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, LeagueTable.Size).ToList(), previous,
                "every position from 1..20 should be accounted for exactly once");
        }

        /// <summary>Collapsing the relegated pair puts fresh, distinct dungeons in their slots.</summary>
        [Test]
        public void CollapseRelegated_ReplacesTheBottomTwo()
        {
            var league = new LeagueTable(seed: 23);
            var doomed = league.Entries.Skip(LeagueTable.Size - LeagueTable.RelegationCount)
                .Select(e => e.Name).ToList();

            league.CollapseRelegated();

            var names = league.Entries.Select(e => e.Name).ToList();
            CollectionAssert.AllItemsAreUnique(names);
            foreach (string gone in doomed)
            {
                CollectionAssert.DoesNotContain(names, gone, $"'{gone}' should have collapsed");
            }
        }

        /// <summary>The same seed reproduces the same league exactly, for bug reports.</summary>
        [Test]
        public void SameSeed_ReproducesTheSameLeague()
        {
            var first = new LeagueTable(seed: 2026);
            var second = new LeagueTable(seed: 2026);

            for (int round = 0; round < 5; round++)
            {
                first.SubmitRaid(700f);
                second.SubmitRaid(700f);
            }

            CollectionAssert.AreEqual(
                first.Entries.Select(e => e.Name).ToList(),
                second.Entries.Select(e => e.Name).ToList());
            CollectionAssert.AreEqual(
                first.Entries.Select(e => e.Score).ToList(),
                second.Entries.Select(e => e.Score).ToList());
        }

        /// <summary>Different seeds give different leagues, so the names are not fixed.</summary>
        [Test]
        public void DifferentSeeds_GiveDifferentLeagues()
        {
            var a = new LeagueTable(seed: 1);
            var b = new LeagueTable(seed: 2);
            CollectionAssert.AreNotEqual(
                a.Entries.Select(e => e.Name).ToList(),
                b.Entries.Select(e => e.Name).ToList());
        }

        /// <summary>
        /// Everyone opens on nothing, so the first raid is the whole story.
        /// </summary>
        [Test]
        public void EveryDungeon_StartsAtZero()
        {
            var league = new LeagueTable(11);

            foreach (LeagueEntry entry in league.Entries)
            {
                Assert.AreEqual(0f, entry.Score, 0.001f,
                    $"{entry.Name} started on {entry.Score}, so the opening table is already decided");
            }

            Assert.AreEqual(LeagueTable.PlayerStartPosition, league.PlayerPosition,
                "the player should still open around fourteenth");
        }

        /// <summary>
        /// One dungeon leaves every round, and the field shrinks to a single winner.
        /// </summary>
        /// <remarks>
        /// The competition's whole shape. The old table relegated the bottom two of a fixed twenty
        /// and refilled the gaps, so it ran forever and the player could only avoid losing — there
        /// was no way to <i>win</i>, which is now the goal of the game.
        /// </remarks>
        [Test]
        public void TheFieldShrinks_UntilOneDungeonIsLeft()
        {
            var league = new LeagueTable(7);
            int before = league.Remaining;

            // A good raid every round: the player should never be the one eliminated.
            for (int round = 0; round < LeagueTable.Size + 2 && league.Remaining > 1; round++)
            {
                league.SubmitRaid(LeagueTable.GoodRun);
                Assert.IsFalse(league.PlayerRelegated,
                    $"round {round}: a good raid put the player bottom, which should be impossible "
                    + "while rivals are handicapped below what a good run earns");
                league.CollapseRelegated();
            }

            MooseRunnerFacade.Log(
                $"field went from {before} to {league.Remaining} over "
                + $"{league.Round} rounds; player won={league.PlayerWon}");

            Assert.AreEqual(1, league.Remaining, "the competition never resolved to one dungeon");
            Assert.IsTrue(league.PlayerWon,
                "playing a good raid every round did not win the competition");

            // Ten rounds, not nineteen: two dungeons leave each round until the last pair, and the
            // final round decides it. Nineteen minutes of raids plus shops was far too long for
            // somebody voting on a jam entry.
            Assert.AreEqual(10, league.Round,
                $"the competition took {league.Round} rounds; twenty dungeons losing two a round "
                + "should reach a winner in ten");
        }

        /// <summary>
        /// A really good raid always beats every rival's best possible round.
        /// </summary>
        /// <remarks>
        /// The point of the handicap. A rival rolls between a bad run and a good one and then loses a
        /// tenth, so its ceiling sits below the player's — the league answers skill directly instead
        /// of statistically, and a well-played round can never be undone by an unlucky roll.
        /// </remarks>
        [Test]
        public void AGoodRaid_OutscoresEveryRivalsBestRound()
        {
            float rivalCeiling = LeagueTable.RivalFloor + LeagueTable.RivalSpread;

            MooseRunnerFacade.Log(
                $"a good run is {LeagueTable.GoodRun:F0}; a rival's best possible round is "
                + $"{rivalCeiling:F0}");

            Assert.Less(rivalCeiling, LeagueTable.GoodRun,
                "a rival can out-earn a genuinely good raid, so skill does not decide the table");

            // And the floor stays below a bad run, so a bad raid is punished rather than carried.
            Assert.Less(LeagueTable.RivalFloor, LeagueTable.BadRun,
                "every rival earns more than a bad raid, so a bad round cannot be survived at all");
        }

        /// <summary>
        /// Playing badly every round gets the player eliminated.
        /// </summary>
        /// <remarks>
        /// The other half of the contract. If a bad run could not lose, the elimination would be
        /// theatre.
        /// </remarks>
        [Test]
        public void ABadRaidEveryRound_GetsThePlayerKnockedOut()
        {
            var league = new LeagueTable(3);

            for (int round = 0; round < LeagueTable.Size + 2; round++)
            {
                league.SubmitRaid(0f);
                if (league.PlayerRelegated)
                {
                    MooseRunnerFacade.Log(
                        $"harvesting nothing, the player went out in round {round} with "
                        + $"{league.Remaining} dungeons left");
                    Assert.Pass("a player who never scores is eliminated");
                }

                league.CollapseRelegated();
            }

            Assert.Fail("harvesting nothing every round never got the player eliminated");
        }

        /// <summary>
        /// Two leave each round until the last pair, and then one.
        /// </summary>
        /// <remarks>
        /// Taking two from a field of two would leave nobody holding the trophy, so the final round
        /// drops a single dungeon. The shape matters to the player as well as the arithmetic: they
        /// need to know when they are one place from going out, and the drop zone is two deep for
        /// nine rounds and one deep for the last.
        /// </remarks>
        [Test]
        public void TwoLeaveEachRound_UntilTheFinalPair()
        {
            var league = new LeagueTable(5);
            var sizes = new List<int>();

            while (league.Remaining > 1)
            {
                sizes.Add(league.Remaining);

                Assert.AreEqual(league.Remaining > 2 ? 2 : 1, league.EliminationsThisRound,
                    $"with {league.Remaining} left, the wrong number was about to be dropped");
                Assert.AreEqual(league.Remaining == 2, league.IsFinal,
                    $"with {league.Remaining} left, IsFinal disagreed with the field size");

                league.SubmitRaid(LeagueTable.GoodRun);
                league.CollapseRelegated();
            }

            MooseRunnerFacade.Log(
                $"field went {string.Join(" -> ", sizes)} -> {league.Remaining} "
                + $"over {league.Round} rounds");

            Assert.AreEqual(10, league.Round, "twenty dungeons should reach a winner in ten rounds");
            Assert.IsTrue(league.PlayerWon, "a good raid every round should win");
        }
    }
}
