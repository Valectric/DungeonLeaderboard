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
    }
}
