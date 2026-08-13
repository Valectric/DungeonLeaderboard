namespace Dungeon.RaidManager
{
    /// <summary>
    /// The damage roll, and the seeded generator behind it.
    /// </summary>
    /// <remarks>
    /// <b>Deterministic on purpose.</b> The project's hard constraint is that a run can be reproduced
    /// exactly from a seed in a bug report, so combat rolls come from a generator the raid owns and
    /// seeds -- never <c>UnityEngine.Random</c>, which would make two runs of the same seed diverge
    /// the moment a blow landed differently.
    /// <para>
    /// The stat model is deliberately a <i>decomposition</i> of the damage-per-second figures the
    /// game was already balanced around, not a replacement for them. Fight length is the rate this
    /// whole game is built on -- a skeleton exists to hold a party still for about thirteen seconds --
    /// so the expected output of the new formula matches the old constant to within a few percent,
    /// and <c>CombatStatsTests</c> fails if it drifts.
    /// </para>
    /// </remarks>
    public static class CombatMath
    {
        /// <summary>Widest a roll can swing below its average.</summary>
        public const float MinRoll = 0.85f;

        /// <summary>Widest a roll can swing above its average.</summary>
        public const float MaxRoll = 1.15f;

        /// <summary>Damage a blow always does, however well armoured the target.</summary>
        /// <remarks>
        /// Armour reduces; it never nullifies. A fight in which one side literally cannot hurt the
        /// other would hang forever, and in this game a hung fight is a party standing still earning
        /// nothing.
        /// </remarks>
        public const float MinimumDamage = 1f;

        /// <summary>
        /// Rolls one blow.
        /// </summary>
        /// <param name="weapon">The weapon's base damage.</param>
        /// <param name="might">The attacker's own strength, added to the weapon.</param>
        /// <param name="armour">The target's damage reduction, 0 to 1.</param>
        /// <param name="random">The raid's seeded generator.</param>
        /// <returns>Damage dealt.</returns>
        public static float Roll(float weapon, float might, float armour, System.Random random)
        {
            float swing = MinRoll + ((float)random.NextDouble() * (MaxRoll - MinRoll));
            float raw = (weapon + might) * swing;
            float reduced = raw * (1f - UnityEngine.Mathf.Clamp01(armour));
            return UnityEngine.Mathf.Max(MinimumDamage, reduced);
        }

        /// <summary>
        /// The damage per second a combatant averages against a given target.
        /// </summary>
        /// <remarks>
        /// The balance anchor. Every stat block in the game is chosen so this matches the
        /// damage-per-second the design was tuned around, and the test suite asserts it.
        /// </remarks>
        /// <param name="weapon">Weapon base damage.</param>
        /// <param name="might">Attacker strength.</param>
        /// <param name="armour">Target damage reduction.</param>
        /// <param name="interval">Seconds between blows.</param>
        /// <returns>Expected damage per second.</returns>
        public static float ExpectedDps(float weapon, float might, float armour, float interval)
        {
            if (interval <= 0f)
            {
                return 0f;
            }

            return (weapon + might) * (1f - UnityEngine.Mathf.Clamp01(armour)) / interval;
        }
    }
}
