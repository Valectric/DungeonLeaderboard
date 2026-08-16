using System.Collections.Generic;

namespace Dungeon.PartyManager
{
    /// <summary>
    /// Every party that can walk in, and when each becomes available.
    /// </summary>
    /// <remarks>
    /// SPEC.md section 4: <b>"Party composition is the primary source of run-to-run variation."</b>
    /// This is the table that lever reads from. It lives apart from <see cref="PartyComposition"/>
    /// because it is data and that is behaviour, and together they broke the 400-line file cap.
    /// <para>
    /// The marching order matters as much as the roster: whoever is listed first leads, draws the
    /// mobs and takes the trap. A party led by a mage is a party that dies, which is the worst
    /// outcome in the design — so the rosters that lead with something fragile are the ones the
    /// player has to handle gently, and they are the ones held back to later rounds.
    /// </para>
    /// </remarks>
    public static class PartyRosters
    {
        /// <summary>Shorthand so the table below reads as a table.</summary>
        private const AdventurerRole Tank = AdventurerRole.Tank;

        /// <summary>Shorthand so the table below reads as a table.</summary>
        private const AdventurerRole Healer = AdventurerRole.Healer;

        /// <summary>Shorthand so the table below reads as a table.</summary>
        private const AdventurerRole Ranged = AdventurerRole.Ranged;

        /// <summary>Shorthand so the table below reads as a table.</summary>
        private const AdventurerRole Mage = AdventurerRole.Mage;

        /// <summary>
        /// The nine rosters, in the order they become available.
        /// </summary>
        /// <remarks>
        /// Nine rather than the original six, because the author asked for the teams to vary more and
        /// the cheapest honest way to do that is more rosters — a larger pool repeats less often on
        /// its own, with no change to the picker.
        /// <para>
        /// <b>Reinforcements are not padding.</b> Each roster grows in its own character, and two of
        /// them grow around a hole that <i>is</i> the roster: THE UNSHRIVEN never gains a healer, THE
        /// SKIRMISHERS never gains a tank. A nine-strong Unshriven that quietly acquired a healer
        /// would be a different party wearing the same name and the same warning, which is worse than
        /// no variation at all. There is a test on exactly that.
        /// </para>
        /// </remarks>
        public static readonly PartyComposition[] All =
        {
            // -- From the first raid: the three that teach the verbs without punishing a beginner --

            new("THE BALANCED PARTY",
                "one of everything. they will not surprise you.",
                new List<AdventurerRole> { Tank, Ranged, Mage, Healer },
                new List<AdventurerRole> { Tank, Ranged, Healer, Mage, Tank },
                firstRound: 0),

            new("THE IRONCLADS",
                "two tanks. they soak everything and leave slowly -- milk them.",
                new List<AdventurerRole> { Tank, Tank, Healer, Ranged },
                new List<AdventurerRole> { Tank, Healer, Tank, Ranged, Tank },
                firstRound: 0),

            new("THE PILGRIMAGE",
                "two healers. your best customers. keep them alive and bleeding.",
                new List<AdventurerRole> { Tank, Healer, Healer, Ranged },
                new List<AdventurerRole> { Healer, Ranged, Healer, Tank, Healer },
                firstRound: 0),

            // -- From the second raid --

            new("THE GLASS CANNONS",
                "they delete mobs and die to a trap. do not overdo it.",
                new List<AdventurerRole> { Ranged, Mage, Mage, Healer },
                new List<AdventurerRole> { Mage, Ranged, Mage, Healer, Mage },
                firstRound: 1),

            new("THE ARCHERY LINE",
                "four bows and no front. they clear rooms from the doorway if you let them.",
                new List<AdventurerRole> { Ranged, Ranged, Ranged, Healer },
                new List<AdventurerRole> { Ranged, Healer, Ranged, Mage, Ranged },
                firstRound: 1),

            // -- From the third raid: the ones with a hole in them --

            new("THE UNSHRIVEN",
                "no healer at all. every wound is permanent. tread carefully.",
                new List<AdventurerRole> { Tank, Ranged, Mage, Mage },
                // NEVER a healer. The absence is the roster.
                new List<AdventurerRole> { Mage, Ranged, Tank, Mage, Ranged },
                firstRound: 2),

            new("THE PHALANX",
                "three tanks and one healer. nothing you own will kill them -- that is the point.",
                new List<AdventurerRole> { Tank, Tank, Tank, Healer },
                new List<AdventurerRole> { Tank, Healer, Tank, Tank, Healer },
                firstRound: 2),

            // -- Held back to raid five: the author's "skirmish should come later" --

            new("THE SKIRMISHERS",
                "no tank. nobody soaks, so damage lands on the fragile.",
                new List<AdventurerRole> { Ranged, Ranged, Mage, Healer },
                // NEVER a tank. Same rule as THE UNSHRIVEN, the other way round.
                new List<AdventurerRole> { Ranged, Mage, Ranged, Healer, Ranged },
                firstRound: 4),

            new("THE COVEN",
                "a mage walks in front. one trap and your best customer is gone.",
                new List<AdventurerRole> { Mage, Mage, Healer, Ranged },
                new List<AdventurerRole> { Mage, Healer, Mage, Ranged, Mage },
                firstRound: 4)
        };
    }
}
