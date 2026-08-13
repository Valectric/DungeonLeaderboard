using System;
using System.Collections.Generic;

namespace Dungeon.PartyManager
{
    /// <summary>
    /// A named party of four, and the marching order it walks in.
    /// </summary>
    /// <remarks>
    /// SPEC.md section 4: <b>"Party composition is the primary source of run-to-run variation. A
    /// tanky party and a glass-cannon party play completely differently in the same dungeon layout --
    /// exploit this before adding more dungeon content."</b> This is that lever, and it is cheaper
    /// than any amount of new dungeon.
    /// <para>
    /// Every composition here is four members, per the spec, and each one is chosen to make a
    /// different mistake attractive. The player cannot control who walks in, so the skill is reading
    /// the party at the door and deciding which of the three verbs the next minute wants.
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

        /// <summary>Creates a composition.</summary>
        /// <param name="name">Name shown to the player.</param>
        /// <param name="warning">One line of advice.</param>
        /// <param name="roles">Roles in marching order.</param>
        public PartyComposition(string name, string warning, IReadOnlyList<AdventurerRole> roles)
        {
            Name = name;
            Warning = warning;
            Roles = roles;
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

        /// <summary>
        /// Every party that can walk in.
        /// </summary>
        /// <remarks>
        /// The marching order matters as much as the roster: whoever is listed first leads, draws the
        /// mobs and takes the trap. A party led by a mage is a party that dies, which is the worst
        /// outcome in the design -- so the compositions that lead with something fragile are the ones
        /// the player has to handle gently.
        /// </remarks>
        public static readonly PartyComposition[] All =
        {
            new("THE BALANCED PARTY",
                "one of everything. they will not surprise you.",
                new[]
                {
                    AdventurerRole.Tank, AdventurerRole.Ranged,
                    AdventurerRole.Mage, AdventurerRole.Healer
                }),

            new("THE IRONCLADS",
                "two tanks. they soak everything and leave slowly -- milk them.",
                new[]
                {
                    AdventurerRole.Tank, AdventurerRole.Tank,
                    AdventurerRole.Healer, AdventurerRole.Ranged
                }),

            new("THE PILGRIMAGE",
                "two healers. your best customers. keep them alive and bleeding.",
                new[]
                {
                    AdventurerRole.Tank, AdventurerRole.Healer,
                    AdventurerRole.Healer, AdventurerRole.Ranged
                }),

            new("THE GLASS CANNONS",
                "they delete mobs and die to a trap. do not overdo it.",
                new[]
                {
                    AdventurerRole.Ranged, AdventurerRole.Mage,
                    AdventurerRole.Mage, AdventurerRole.Healer
                }),

            new("THE UNSHRIVEN",
                "no healer at all. every wound is permanent. tread carefully.",
                new[]
                {
                    AdventurerRole.Tank, AdventurerRole.Ranged,
                    AdventurerRole.Mage, AdventurerRole.Mage
                }),

            new("THE SKIRMISHERS",
                "no tank. nobody soaks, so damage lands on the fragile.",
                new[]
                {
                    AdventurerRole.Ranged, AdventurerRole.Ranged,
                    AdventurerRole.Mage, AdventurerRole.Healer
                })
        };

        /// <summary>
        /// Picks the party for a raid from a seed.
        /// </summary>
        /// <remarks>
        /// Seeded rather than random so a run can be reproduced exactly from a bug report, which is
        /// one of the project's hard constraints.
        /// </remarks>
        /// <param name="seed">Seed for this raid.</param>
        /// <returns>The composition that walks in.</returns>
        public static PartyComposition ForSeed(int seed)
        {
            // The first party is always the balanced one. A new player meeting THE UNSHRIVEN before
            // they know what a healer does will wipe them and conclude the game is unfair, when in
            // fact it is the one outcome the design most wants them to avoid.
            var random = new Random(seed);
            return All[random.Next(All.Length)];
        }

        /// <summary>The party a new player meets first.</summary>
        public static PartyComposition Opening => All[0];
    }
}
