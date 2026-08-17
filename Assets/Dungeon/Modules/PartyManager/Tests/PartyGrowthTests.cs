using System.Collections.Generic;
using System.Linq;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.PartyManager.Tests
{
    /// <summary>
    /// Pins how parties grow through a season, and which rosters are allowed in when.
    /// </summary>
    /// <remarks>
    /// The author's rule: <b>"make team after turn 5 increase to 5, after team 8 increase one more,
    /// and last should be 9 team"</b>, and separately that THE SKIRMISHERS should arrive later than
    /// it was. Both are exact claims about specific raid numbers, which makes them worth asserting
    /// rather than eyeballing — an off-by-one here gates every roster a raid early and starts the
    /// growth a raid late, and neither is visible in play until somebody counts.
    /// </remarks>
    public sealed class PartyGrowthTests
    {
        /// <summary>
        /// How many raids a whole season runs to.
        /// </summary>
        /// <remarks>
        /// Twenty dungeons losing two a round reach the last pair in nine rounds, and a tenth decides
        /// it — <c>LeagueTable.RelegationCount</c> and the note on it. Stated here as a literal
        /// because this assembly does not reference LeagueManager and should not start; the test
        /// below fails loudly if the two ever disagree about what a season is.
        /// </remarks>
        private const int RaidsInASeason = 10;

        /// <summary>The size ramp hits the author's three stated numbers exactly.</summary>
        /// <remarks>
        /// The last of the three is the one that matters, and the one this test used to get wrong:
        /// it asserted nine at <b>raid 18</b>, which no season ever reaches. That assertion passed
        /// for as long as the bug existed, because it was written from the same stale premise as the
        /// code — a nineteen-raid league that was designed, rejected, and never built. The fix is to
        /// state it against the length of a season rather than against a raid number.
        /// </remarks>
        [Test]
        public void TheSizeRamp_HitsTheStatedNumbers()
        {
            // round is rounds COMPLETED, so raid N is round N-1.
            Assert.AreEqual(4, PartyComposition.SizeForRound(0), "raid 1 must be four");
            Assert.AreEqual(4, PartyComposition.SizeForRound(4), "raid 5 must still be four");
            Assert.AreEqual(5, PartyComposition.SizeForRound(5), "raid 6 must be five -- 'after turn 5'");
            Assert.AreEqual(7, PartyComposition.SizeForRound(7),
                "raid 8 must have increased again -- 'after team 8 increase one more'");

            int lastRaid = PartyComposition.SizeForRound(RaidsInASeason - 1);
            MooseRunnerFacade.Log(
                $"season ramp: {string.Join(",", Enumerable.Range(0, RaidsInASeason).Select(PartyComposition.SizeForRound))}");

            Assert.AreEqual(PartyComposition.MaxSize, lastRaid,
                $"the last raid of a {RaidsInASeason}-raid season fields {lastRaid}, not the "
                + $"{PartyComposition.MaxSize} the author asked for -- 'last should be 9 team'. "
                + "Every figure this project has measured about parties of nine describes a party "
                + "the game never produces");
        }

        /// <summary>The ramp never runs backwards and never exceeds the cap.</summary>
        [Test]
        public void TheSizeRamp_OnlyEverGrows_AndIsCapped()
        {
            int previous = 0;
            for (int round = 0; round < 60; round++)
            {
                int size = PartyComposition.SizeForRound(round);
                Assert.GreaterOrEqual(size, previous, $"the party SHRANK at round {round}");
                Assert.LessOrEqual(size, PartyComposition.MaxSize,
                    $"round {round} fields {size}, past the {PartyComposition.MaxSize} cap");
                previous = size;
            }

            MooseRunnerFacade.Log(
                $"ramp: raid 1={PartyComposition.SizeForRound(0)}, 6={PartyComposition.SizeForRound(5)}, "
                + $"9={PartyComposition.SizeForRound(8)}, 12={PartyComposition.SizeForRound(11)}, "
                + $"15={PartyComposition.SizeForRound(14)}, 18={PartyComposition.SizeForRound(17)}, "
                + $"60={PartyComposition.SizeForRound(59)}");
        }

        /// <summary>A grown party really does field that many bodies, in marching order.</summary>
        [Test]
        public void AGrownParty_FieldsTheRightNumber()
        {
            foreach (PartyComposition roster in PartyComposition.All)
            {
                for (int size = 4; size <= PartyComposition.MaxSize; size++)
                {
                    PartyComposition grown = roster.Grown(size);
                    Assert.AreEqual(size, grown.Roles.Count,
                        $"{roster.Name} grown to {size} fielded {grown.Roles.Count}");
                    Assert.AreSame(roster, grown.Template,
                        $"{roster.Name} lost track of the roster it grew from");
                }
            }
        }

        /// <summary>
        /// Growing a roster never fills the hole that defines it.
        /// </summary>
        /// <remarks>
        /// THE UNSHRIVEN is "no healer at all" and THE SKIRMISHERS is "no tank". Those absences are
        /// the roster and the warning the player is shown. A nine-strong Unshriven that quietly
        /// acquired a healer would be a different party wearing the same name — and worse, the
        /// warning on screen would be a lie.
        /// </remarks>
        [Test]
        public void GrowingARoster_NeverFillsTheHoleThatDefinesIt()
        {
            var forbidden = new Dictionary<string, AdventurerRole>
            {
                { "THE UNSHRIVEN", AdventurerRole.Healer },
                { "THE SKIRMISHERS", AdventurerRole.Tank }
            };

            foreach (KeyValuePair<string, AdventurerRole> rule in forbidden)
            {
                PartyComposition roster = null;
                foreach (PartyComposition candidate in PartyComposition.All)
                {
                    if (candidate.Name == rule.Key) { roster = candidate; }
                }

                Assert.IsNotNull(roster, $"{rule.Key} is gone from the roster table");

                for (int size = 4; size <= PartyComposition.MaxSize; size++)
                {
                    PartyComposition grown = roster.Grown(size);
                    MooseRunnerFacade.Log(
                        $"{rule.Key} at {size}: {grown.Count(rule.Value)} x {rule.Value}");

                    Assert.AreEqual(0, grown.Count(rule.Value),
                        $"{rule.Key} grown to {size} picked up a {rule.Value}, which is exactly the "
                        + $"thing it is defined by not having -- its warning is now a lie");
                }
            }
        }

        /// <summary>THE SKIRMISHERS cannot be an early party any more.</summary>
        /// <remarks>The author's report was that it kept arriving third.</remarks>
        [Test]
        public void TheSkirmishers_CannotArriveEarly()
        {
            PartyComposition skirmishers = null;
            foreach (PartyComposition candidate in PartyComposition.All)
            {
                if (candidate.Name == "THE SKIRMISHERS") { skirmishers = candidate; }
            }

            Assert.IsNotNull(skirmishers, "THE SKIRMISHERS is gone from the roster table");
            Assert.GreaterOrEqual(skirmishers.FirstRound, 3,
                "THE SKIRMISHERS can still turn up in the opening raids, which is the complaint");

            // And prove the gate is actually enforced, not merely declared.
            for (int round = 0; round < skirmishers.FirstRound; round++)
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    PartyComposition pick = PartyComposition.ForRound(round, seed);
                    Assert.AreNotEqual("THE SKIRMISHERS", pick.Name,
                        $"THE SKIRMISHERS walked in at round {round} on seed {seed}, before its "
                        + $"own FirstRound of {skirmishers.FirstRound}");
                }
            }
        }

        /// <summary>Every roster becomes reachable once the league is far enough along.</summary>
        /// <remarks>
        /// Gating is only acceptable if it delays a roster rather than deleting it. A roster nobody
        /// ever meets is dead content that still costs a reader time.
        /// </remarks>
        [Test]
        public void EveryRoster_IsReachableEventually()
        {
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 600; seed++)
            {
                seen.Add(PartyComposition.ForRound(round: 10, seed: seed).Name);
            }

            MooseRunnerFacade.Log($"rosters reachable by round 10: {seen.Count} of {PartyComposition.All.Length}");

            Assert.AreEqual(PartyComposition.All.Length, seen.Count,
                "some roster can never actually walk in, so it is dead content");
        }

        /// <summary>A party rolled for a round arrives at that round's size.</summary>
        [Test]
        public void ForRound_GrowsThePartyItReturns()
        {
            foreach (int round in new[] { 0, 5, 8, 11, 14, 17, 25 })
            {
                PartyComposition pick = PartyComposition.ForRound(round, seed: 12345);
                MooseRunnerFacade.Log(
                    $"round {round} (raid {round + 1}): {pick.Name} with {pick.Roles.Count}");

                Assert.AreEqual(PartyComposition.SizeForRound(round), pick.Roles.Count,
                    $"the party for round {round} did not arrive at that round's size");
            }
        }
    }
}
