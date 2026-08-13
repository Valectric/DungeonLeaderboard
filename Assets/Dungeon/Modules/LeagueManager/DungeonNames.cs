using System;
using System.Collections.Generic;

namespace Dungeon.LeagueManager
{
    /// <summary>
    /// Makes procedural names for rival dungeons.
    /// </summary>
    /// <remarks>
    /// The joke the whole game runs on is that an ancient eldritch horror is anxious about its
    /// performance review, so the names have to sit between menacing and municipal -- "Gloomspire"
    /// next to "Basement of Bob". Half the standings screen's character comes from this list, and it
    /// is the first thing a player reads.
    /// <para>
    /// Seeded, because a run must be reproducible from a seed in a bug report.
    /// </para>
    /// </remarks>
    public static class DungeonNames
    {
        private static readonly string[] Adjectives =
        {
            "Moldy", "Weeping", "Whispering", "Rotbottom", "Damp", "Crumbling", "Leaky",
            "Gloomy", "Festering", "Sullen", "Draughty", "Clammy", "Forgotten", "Reeking",
            "Shuddering", "Mildewed", "Grasping", "Peckish"
        };

        private static readonly string[] Places =
        {
            "Maw", "Hollow", "Pit", "Burrow", "Lair", "Cellar", "Warren", "Grotto", "Sump",
            "Crypt", "Den", "Undercroft", "Cistern", "Oubliette", "Nook", "Cavern"
        };

        private static readonly string[] Compounds =
        {
            "Gloomspire", "Crumblekeep", "Murkholm", "Dreadnook", "Bleakmoor", "Sogfen",
            "Grimhollow", "Duskwarren", "Nettlemire", "Cragmaw", "Blightwick", "Mirefall"
        };

        private static readonly string[] Owners =
        {
            "Bob", "Deborah", "Gary", "Susan", "Keith", "Moira", "Trevor", "Janice"
        };

        /// <summary>
        /// Builds a set of distinct dungeon names.
        /// </summary>
        /// <param name="count">How many names to produce.</param>
        /// <param name="seed">Seed, so the same run reproduces exactly.</param>
        /// <returns>Distinct names, in generation order.</returns>
        public static List<string> Generate(int count, int seed)
        {
            var random = new Random(seed);
            var names = new List<string>();
            var used = new HashSet<string>();
            int guard = 0;

            while (names.Count < count && guard++ < count * 50)
            {
                string name = Compose(random);
                if (used.Add(name))
                {
                    names.Add(name);
                }
            }

            // Fall back to numbering if the pools ever run dry, so this cannot loop forever or
            // silently return fewer standings than the league needs.
            while (names.Count < count)
            {
                names.Add($"Hole {names.Count + 1}");
            }

            return names;
        }

        /// <summary>Composes one name in one of four shapes.</summary>
        private static string Compose(Random random) => random.Next(10) switch
        {
            0 or 1 => Compounds[random.Next(Compounds.Length)],
            2 => $"{Places[random.Next(Places.Length)]} of {Owners[random.Next(Owners.Length)]}",
            3 => $"The {Adjectives[random.Next(Adjectives.Length)]} "
                 + $"{Places[random.Next(Places.Length)]}",
            _ => $"{Adjectives[random.Next(Adjectives.Length)]} "
                 + $"{Places[random.Next(Places.Length)]}"
        };
    }
}
