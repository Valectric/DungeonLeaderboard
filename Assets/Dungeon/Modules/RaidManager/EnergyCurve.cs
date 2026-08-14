using System;
using Dungeon.PartyManager;

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
        /// What one member earns a second for doing something, before its wound multiplier.
        /// </summary>
        /// <remarks>
        /// The rate is summed per living member now rather than gated on a single party-wide "is it
        /// in combat" flag. Two things forced it. A party that cannot be hurt earned a ninth of one
        /// that can, because the wound multiplier was the only term that ever moved; and both
        /// attempts to let fragile parties survive — archers that outrun a monster, monsters with
        /// less health — <b>inverted the design's one rule</b>, since anything keeping the party
        /// healthier made killing it relatively more attractive.
        /// <para>
        /// Scaled to land on the ceiling the old curve had. The first pass used 1.0 for melee on the
        /// arithmetic that four members at eight times wound is 32 — but the wound multiplier is per
        /// member now, so three healthy allies contribute at 1x however badly the fourth is hurt, and
        /// the measured peak came out at 11.9. These are three times that pass. Four members walking
        /// is 0.24 a second against a peak near 35, still the "almost nothing" the spec demands of an
        /// unengaged party crossing a corridor.
        /// </para>
        /// <para>
        /// Shooting pays well short of melee. An archer at range is safe, and the design should keep
        /// preferring the party that is standing in reach of something and bleeding — it just should
        /// not pay a kiting archer <i>nothing</i>.
        /// </para>
        /// </remarks>
        /// <param name="action">What the member is doing.</param>
        /// <returns>Energy per second for that member, before wounds.</returns>
        public static float ActionRate(AdventurerAction action) => action switch
        {
            AdventurerAction.Walking => 0.06f,
            AdventurerAction.Idle => 0.04f,
            AdventurerAction.Fleeing => 0.75f,
            AdventurerAction.Working => 1.05f,
            AdventurerAction.Shooting => 2.1f,
            AdventurerAction.Fighting => 3f,
            _ => 0.04f
        };

        /// <summary>
        /// What one living member earns a second, action and wounds together.
        /// </summary>
        /// <param name="action">What the member is doing.</param>
        /// <param name="healthFraction">That member's own health, 1 down to 0.</param>
        /// <returns>Energy per second from this member.</returns>
        public static float MemberRate(AdventurerAction action, float healthFraction)
        {
            return BaseRate * ActionRate(action) * WoundMultiplier(healthFraction);
        }

        /// <summary>
        /// What a death costs the player, in banked score.
        /// </summary>
        /// <remarks>
        /// A corpse stops earning on its own — it contributes no action rate — but that alone only
        /// makes a wipe <i>less good</i>, and the design needs it to be actively bad. Fifty points is
        /// roughly a fifth of a decent raid, so losing the party outright wipes out most of what it
        /// earned on the way down. That is the term that keeps "killing them is bad play" true even
        /// when the party is healthy enough to be cheap to keep alive.
        /// </remarks>
        public const float DeathPenalty = 50f;

        /// <summary>
        /// Extra energy a second for the whole team after a chest is opened.
        /// </summary>
        /// <remarks>
        /// A chest is a detour, and a detour is time the party is not advancing — but under a
        /// per-action curve that time is only paid at the "working" rate, which would make looting a
        /// worse use of the party than walking into a fight. The bonus makes finding treasure a
        /// genuine moment rather than a slow patch of the raid.
        /// </remarks>
        public const float ChestBonus = 6f;

        /// <summary>How long a chest's bonus lasts.</summary>
        public const float ChestBonusSeconds = 5f;

        /// <summary>
        /// How long before another chest pays full value again.
        /// </summary>
        /// <remarks>
        /// Without this, a room floored with chests would be a flat rate increase for the whole raid,
        /// and the shop already lets a player buy as many as they can afford. Chests opened closer
        /// together than this pay proportionally less, so the second one in quick succession is worth
        /// a fraction of the first and stacking them stops being the obvious answer.
        /// </remarks>
        public const float ChestCooldownSeconds = 15f;

        /// <summary>
        /// How much of a chest's bonus is paid, given how long since the last one.
        /// </summary>
        /// <param name="secondsSinceLastChest">Seconds since the previous chest was opened.</param>
        /// <returns>A scale from 0 to 1.</returns>
        public static float ChestValue(float secondsSinceLastChest)
        {
            if (secondsSinceLastChest >= ChestCooldownSeconds)
            {
                return 1f;
            }

            return Math.Clamp(secondsSinceLastChest / ChestCooldownSeconds, 0f, 1f);
        }

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
