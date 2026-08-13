using System;

namespace Dungeon.RaidManager
{
    /// <summary>
    /// The energy rate formula. This is the game.
    /// </summary>
    /// <remarks>
    /// <c>energyRate = baseRate * engagementMultiplier * woundMultiplier</c>, from SPEC.md section 3.
    /// <para>
    /// This is a pure static function on purpose, and it lives in the module's public namespace
    /// rather than under <c>.Internal</c>, so that the curve can be asserted directly at many points
    /// without standing up a raid. The project's architecture normally keeps implementation behind
    /// the Facade; the exception is deliberate and is the single most important test surface in the
    /// codebase. In the sister project a broken *rate* survived a fully green suite because every
    /// test asserted a total rather than a shape.
    /// </para>
    /// <para>
    /// The design rule the numbers encode: <b>killing the adventurers is bad play.</b> A party that
    /// is alive, in combat and badly wounded must be worth far more than any other state. An
    /// unengaged party walking a corridor earns almost nothing, and most of the money is in the last
    /// sliver of a health bar. If a change here makes killing more attractive, or makes a wounded
    /// party worth less than a healthy one, it is wrong however well it plays.
    /// </para>
    /// </remarks>
    public static class EnergyCurve
    {
        /// <summary>Energy per second before multipliers. Small: near-zero on its own.</summary>
        public const float BaseRate = 1f;

        /// <summary>
        /// Engagement multiplier when nobody is fighting. Deliberately tiny rather than zero: dead
        /// time must visibly cost the player, and a flat zero would hide the difference between
        /// "stalling badly" and "not playing".
        /// </summary>
        public const float IdleEngagement = 0.05f;

        /// <summary>Engagement multiplier added per living party member currently in combat.</summary>
        public const float EngagementPerMember = 1f;

        /// <summary>
        /// Height of the wound curve above 1. With <see cref="WoundExponent"/> this puts the spec's
        /// anchors almost exactly on target: 20% health lands on 3.95x and 5% health on 7.96x,
        /// against the spec's "around 4x" and "around 8x+".
        /// </summary>
        public const float WoundAmplitude = 9f;

        /// <summary>
        /// Steepness of the wound curve. Five is what makes the last sliver of health carry most of
        /// the money; lower values flatten the curve and quietly remove the reason to keep a party
        /// alive and bleeding rather than simply killing it.
        /// </summary>
        public const float WoundExponent = 5f;

        /// <summary>
        /// Scales earnings with how much of the party is actually fighting.
        /// </summary>
        /// <param name="engagedCount">
        /// Living party members currently in combat. Dead members must not be counted: engagement is
        /// how the design punishes a wipe, since corpses cannot fight and the rate collapses.
        /// </param>
        /// <returns>The engagement multiplier, never below <see cref="IdleEngagement"/>.</returns>
        public static float EngagementMultiplier(int engagedCount)
        {
            if (engagedCount <= 0)
            {
                return IdleEngagement;
            }

            return IdleEngagement + (engagedCount * EngagementPerMember);
        }

        /// <summary>
        /// Scales earnings sharply as the party's health drops.
        /// </summary>
        /// <param name="healthFraction">
        /// Aggregate health of the <i>living</i> party, from 1 (untouched) to 0 (about to drop).
        /// Values outside that range are clamped, so callers need not sanitise.
        /// </param>
        /// <returns>1x at full health, rising to roughly 10x as health approaches zero.</returns>
        public static float WoundMultiplier(float healthFraction)
        {
            float health = Math.Clamp(healthFraction, 0f, 1f);
            return 1f + (WoundAmplitude * (float)Math.Pow(1f - health, WoundExponent));
        }

        /// <summary>
        /// The full energy rate, in energy per second.
        /// </summary>
        /// <param name="engagedCount">Living party members currently in combat.</param>
        /// <param name="healthFraction">Aggregate health of the living party, 1 down to 0.</param>
        /// <returns>Energy per second at this instant.</returns>
        public static float Rate(int engagedCount, float healthFraction)
        {
            return BaseRate * EngagementMultiplier(engagedCount) * WoundMultiplier(healthFraction);
        }
    }
}
