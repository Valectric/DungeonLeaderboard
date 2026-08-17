using System;
using System.Collections.Generic;

namespace Dungeon.PartyManager
{
    /// <summary>
    /// A named party, the marching order it walks in, and how it grows as the league goes on.
    /// </summary>
    /// <remarks>
    /// SPEC.md section 4: <b>"Party composition is the primary source of run-to-run variation. A
    /// tanky party and a glass-cannon party play completely differently in the same dungeon layout --
    /// exploit this before adding more dungeon content."</b> This is that lever, and it is cheaper
    /// than any amount of new dungeon. The rosters themselves are in <see cref="PartyRosters"/>.
    /// <para>
    /// Each roster is chosen to make a different mistake attractive. The player cannot control who
    /// walks in, so the skill is reading the party at the door and deciding which of the three verbs
    /// the next minute wants.
    /// </para>
    /// </remarks>
    public sealed class PartyComposition
    {
        /// <summary>Name shown to the player before the raid.</summary>
        public string Name { get; }

        /// <summary>One line telling the player how this party will punish them.</summary>
        public string Warning { get; }

        /// <summary>Roles in marching order, front to back.</summary>
        public IReadOnlyList<AdventurerRole> Roles { get; }

        /// <summary>Roles that join, in order, as the party grows past its core four.</summary>
        /// <remarks>
        /// Each roster reinforces in its own character, which is the point of growing them this way
        /// rather than padding everything with the same filler. Two grow around a hole that <i>is</i>
        /// the roster — THE UNSHRIVEN never gains a healer, THE SKIRMISHERS never gains a tank.
        /// </remarks>
        public IReadOnlyList<AdventurerRole> Reinforcements { get; }

        /// <summary>Earliest league round (rounds completed, so zero-based) this party may appear.</summary>
        /// <remarks>
        /// The opening rounds are a tutorial the game never calls one, so rosters that punish a
        /// specific mistake are held back until the player has had a few minutes with the verbs.
        /// </remarks>
        public int FirstRound { get; }

        /// <summary>The roster this was grown from — itself, for the entries in <see cref="All"/>.</summary>
        /// <remarks>
        /// A grown party is a new object, so "did this roster just raid?" cannot be answered by
        /// reference equality against <see cref="All"/> any more. This is what it is asked of instead.
        /// </remarks>
        public PartyComposition Template { get; }

        /// <summary>Creates a composition.</summary>
        /// <param name="name">Name shown to the player.</param>
        /// <param name="warning">One line of advice.</param>
        /// <param name="roles">Roles in marching order.</param>
        /// <param name="reinforcements">Roles that join as the party grows; empty means it cannot.</param>
        /// <param name="firstRound">Earliest zero-based round this party may appear in.</param>
        /// <param name="template">Roster this was grown from; null means this is a template itself.</param>
        public PartyComposition(
            string name, string warning, IReadOnlyList<AdventurerRole> roles,
            IReadOnlyList<AdventurerRole> reinforcements = null, int firstRound = 0,
            PartyComposition template = null)
        {
            Name = name;
            Warning = warning;
            Roles = roles;
            Reinforcements = reinforcements ?? Array.Empty<AdventurerRole>();
            FirstRound = firstRound;
            Template = template ?? this;
        }

        /// <summary>How many of a role this party holds.</summary>
        /// <param name="role">Role to count.</param>
        /// <returns>The count.</returns>
        public int Count(AdventurerRole role)
        {
            int total = 0;
            foreach (AdventurerRole member in Roles)
            {
                if (member == role)
                {
                    total++;
                }
            }

            return total;
        }

        /// <summary>Every party that can walk in.</summary>
        public static PartyComposition[] All => PartyRosters.All;

        /// <summary>Members a party has before the league starts growing them.</summary>
        public const int BaseSize = 4;

        /// <summary>Most members a party can ever field.</summary>
        public const int MaxSize = 9;

        /// <summary>
        /// Raids between each extra member, once growth has started.
        /// </summary>
        /// <remarks>
        /// <b>Was 3, which made the nine-strong party unreachable.</b> Three raids a member puts the
        /// ninth adventurer at raid 18, and the doc below justified that with "a full run is nineteen
        /// raids" — a figure from the one-eliminated-a-round league that
        /// <see cref="LeagueManager.LeagueTable.RelegationCount"/> records as <i>rejected</i>. Two go
        /// out a round, so twenty dungeons reach a winner in <b>ten</b>. Measured 2026-08-17: the
        /// curve ran 4,4,4,4,4,5,5,5,6,6 and the final raid fielded <b>six</b>.
        /// <para>
        /// So the author's "last should be 9" had never once happened, and neither had anything
        /// measured about parties of nine — D42's cost of growth, D45's health-bar stagger, the
        /// lateral fan — all of it tuned against a configuration the game could not produce.
        /// </para>
        /// </remarks>
        public const int RaidsPerExtraMember = 1;

        /// <summary>First raid number that fields more than <see cref="BaseSize"/>.</summary>
        public const int GrowthStartsAtRaid = 6;

        /// <summary>
        /// How many adventurers walk in on a given round.
        /// </summary>
        /// <remarks>
        /// The author's rule: <b>"make team after turn 5 increase to 5, after team 8 increase one
        /// more, and last should be 9 team."</b> Three anchors — five at raid six, one more at raid
        /// eight, and <i>nine on the last raid</i> — and the season is ten raids long, so the only
        /// reading that hits all three is <b>one more member every raid from the sixth</b>:
        /// <code>
        /// raid   1  2  3  4  5  6  7  8  9  10
        /// size   4  4  4  4  4  5  6  7  8  9
        /// </code>
        /// Five at six, an increase at eight, nine on the last. The previous reading — one every
        /// three raids — satisfied the first two anchors and missed the one that matters most,
        /// because it was calibrated against a nineteen-raid season that was never built.
        /// <para>
        /// The competing reading, six until the final raid and then a jump to nine, is still
        /// rejected: it contradicts "increase one more" and wastes the growth on a single raid.
        /// </para>
        /// <para>
        /// <b>This scales the whole economy</b>, because the energy rate sums per member: nine
        /// fighting bodies earn roughly 2.25x four. Every balance figure in the project was measured
        /// against parties of four.
        /// </para>
        /// </remarks>
        /// <param name="round">League rounds completed so far, so zero is the first raid.</param>
        /// <returns>Party size, between <see cref="BaseSize"/> and <see cref="MaxSize"/>.</returns>
        public static int SizeForRound(int round)
        {
            int raid = Math.Max(1, round + 1);
            int extra = Math.Max(0, raid - (GrowthStartsAtRaid - RaidsPerExtraMember))
                        / RaidsPerExtraMember;
            return Math.Min(MaxSize, BaseSize + extra);
        }

        /// <summary>
        /// This roster at a given size, reinforced in its own character.
        /// </summary>
        /// <param name="size">Members wanted. At or below the core four returns this instance.</param>
        /// <returns>A composition of the requested size.</returns>
        public PartyComposition Grown(int size)
        {
            if (size <= Roles.Count || Reinforcements.Count == 0)
            {
                return this;
            }

            var roles = new List<AdventurerRole>(Roles);
            while (roles.Count < size)
            {
                // Cycled, so a roster can grow past the reinforcements it lists without falling back
                // on its core four -- the extras keep arriving in the same character and same order.
                roles.Add(Reinforcements[(roles.Count - Roles.Count) % Reinforcements.Count]);
            }

            return new PartyComposition(Name, Warning, roles, Reinforcements, FirstRound, Template);
        }

        /// <summary>
        /// Picks the party for a raid from a seed, ignoring rounds.
        /// </summary>
        /// <remarks>
        /// Seeded rather than random so a run can be reproduced exactly from a bug report, which is
        /// one of the project's hard constraints. Returns a <b>template</b> from <see cref="All"/>,
        /// so the same seed gives the same instance. <see cref="ForRound"/> is what the game uses.
        /// </remarks>
        /// <param name="seed">Seed for this raid.</param>
        /// <param name="avoid">The party that just raided, which will not be sent again immediately.</param>
        /// <returns>The composition that walks in.</returns>
        public static PartyComposition ForSeed(int seed, PartyComposition avoid = null)
        {
            var random = new Random(seed);
            PartyComposition pick = All[random.Next(All.Length)];

            if (avoid == null || !ReferenceEquals(pick.Template, avoid.Template))
            {
                return pick;
            }

            // Never the same party twice running: composition is this game's PRIMARY source of
            // variety, so a back-to-back repeat reads as the feature being broken rather than as a
            // coincidence. Stepped past deterministically rather than rerolled with a fresh seed, so
            // the sequence still follows entirely from the run's one number.
            int index = Array.IndexOf(All, pick);
            return All[(index + 1 + random.Next(All.Length - 1)) % All.Length];
        }

        /// <summary>
        /// Picks the party for a round: eligible for that round, grown to that round's size.
        /// </summary>
        /// <remarks>
        /// This is what the game calls. It layers two rules on <see cref="ForSeed"/> — a roster
        /// cannot appear before its <see cref="FirstRound"/>, and the party fields
        /// <see cref="SizeForRound"/> members.
        /// </remarks>
        /// <param name="round">League rounds completed so far.</param>
        /// <param name="seed">Seed for this raid.</param>
        /// <param name="avoid">The party that just raided, which will not be sent again immediately.</param>
        /// <returns>The composition that walks in.</returns>
        public static PartyComposition ForRound(int round, int seed, PartyComposition avoid = null)
        {
            var eligible = new List<PartyComposition>();
            foreach (PartyComposition candidate in All)
            {
                if (candidate.FirstRound <= round)
                {
                    eligible.Add(candidate);
                }
            }

            var random = new Random(seed);
            PartyComposition pick = eligible[random.Next(eligible.Count)];

            // Compared by TEMPLATE, not by reference: `avoid` is last round's GROWN party, a
            // different object from the roster it came from, which would never match.
            if (avoid != null && eligible.Count > 1
                && ReferenceEquals(pick.Template, avoid.Template))
            {
                int index = eligible.IndexOf(pick);
                pick = eligible[(index + 1 + random.Next(eligible.Count - 1)) % eligible.Count];
            }

            return pick.Grown(SizeForRound(round));
        }

        /// <summary>
        /// Spells a party size as a word.
        /// </summary>
        /// <remarks>
        /// "nine strong" and "nine death notices" both read as prose; "9 strong" reads as a receipt.
        /// Shared so the standings and the raid review cannot drift into spelling it differently.
        /// </remarks>
        /// <param name="count">How many adventurers.</param>
        /// <returns>The word, or the numeral for anything outside the sizes the league sends.</returns>
        public static string SpellSize(int count)
        {
            return count switch
            {
                1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five",
                6 => "six", 7 => "seven", 8 => "eight", 9 => "nine",
                _ => count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        /// <summary>The party a new player meets first.</summary>
        public static PartyComposition Opening => All[0];
    }
}
