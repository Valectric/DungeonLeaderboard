using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Guards the energy rate formula, which is the whole design.
    /// </summary>
    /// <remarks>
    /// These tests assert the <i>shape</i> of the curve at named points, and the earnings of a whole
    /// simulated raid. That is deliberate. A test that only asserted "energy went up" would pass
    /// with a flat curve and a ruined game -- which is exactly how a broken rate survived a fully
    /// green suite in the sister project. Every constant the spec names has an assertion here, so
    /// tuning the curve forces a conscious decision rather than a silent drift.
    /// </remarks>
    public sealed class EnergyCurveTests
    {
        /// <summary>Tolerance on the spec's approximate anchors ("around 4x", "around 8x+").</summary>
        private const float Tolerance = 0.25f;

        /// <summary>An untouched party pays the base rate and nothing more.</summary>
        [Test]
        public void WoundMultiplier_AtFullHealth_IsOne()
        {
            Assert.AreEqual(1f, EnergyCurve.WoundMultiplier(1f), 0.001f);
        }

        /// <summary>SPEC.md section 3: "Around 20% HP approximately 4x".</summary>
        [Test]
        public void WoundMultiplier_AtTwentyPercentHealth_IsAboutFour()
        {
            float actual = EnergyCurve.WoundMultiplier(0.2f);
            MooseRunnerFacade.Log($"wound(0.20) = {actual:F3}");
            Assert.AreEqual(4f, actual, Tolerance);
        }

        /// <summary>SPEC.md section 3: "Around 5% HP approximately 8x+".</summary>
        [Test]
        public void WoundMultiplier_AtFivePercentHealth_IsAboutEight()
        {
            float actual = EnergyCurve.WoundMultiplier(0.05f);
            MooseRunnerFacade.Log($"wound(0.05) = {actual:F3}");
            Assert.GreaterOrEqual(actual, 8f - Tolerance);
        }

        /// <summary>
        /// The curve must rise monotonically as health falls. A dip anywhere would create a band of
        /// health where hurting the party earns less, which inverts the core incentive.
        /// </summary>
        [Test]
        public void WoundMultiplier_RisesMonotonically_AsHealthFalls()
        {
            float previous = EnergyCurve.WoundMultiplier(1f);
            for (int step = 99; step >= 0; step--)
            {
                float current = EnergyCurve.WoundMultiplier(step / 100f);
                Assert.GreaterOrEqual(current, previous,
                    $"multiplier dipped at health {step / 100f:F2}");
                previous = current;
            }
        }

        /// <summary>
        /// Most of the money must sit in the last sliver of health. Concretely: the drop from 20%
        /// to 5% health must be worth more than the entire journey from full health down to 20%.
        /// </summary>
        [Test]
        public void WoundMultiplier_ConcentratesValue_InTheLastSliver()
        {
            float fullToTwenty = EnergyCurve.WoundMultiplier(0.2f) - EnergyCurve.WoundMultiplier(1f);
            float twentyToFive = EnergyCurve.WoundMultiplier(0.05f) - EnergyCurve.WoundMultiplier(0.2f);
            MooseRunnerFacade.Log($"gain 100%->20% = {fullToTwenty:F2}, gain 20%->5% = {twentyToFive:F2}");
            Assert.Greater(twentyToFive, fullToTwenty,
                "the last sliver of health must be where most of the money is");
        }

        /// <summary>Health outside 0..1 is clamped rather than producing a runaway multiplier.</summary>
        [Test]
        public void WoundMultiplier_ClampsHealthOutsideRange()
        {
            Assert.AreEqual(EnergyCurve.WoundMultiplier(0f), EnergyCurve.WoundMultiplier(-5f), 0.001f);
            Assert.AreEqual(EnergyCurve.WoundMultiplier(1f), EnergyCurve.WoundMultiplier(5f), 0.001f);
        }

        /// <summary>A party walking an empty corridor must earn almost nothing.</summary>
        [Test]
        public void Rate_ForUnengagedHealthyParty_IsNearlyZero()
        {
            float rate = EnergyCurve.Rate(engagedCount: 0, healthFraction: 1f);
            MooseRunnerFacade.Log($"idle rate = {rate:F4}/s");
            Assert.Less(rate, 0.1f, "an unengaged party must earn almost nothing");
        }

        /// <summary>Engagement rises with each additional member drawn into combat.</summary>
        [Test]
        public void EngagementMultiplier_RisesWithEachFighter()
        {
            for (int engaged = 1; engaged <= 4; engaged++)
            {
                Assert.Greater(EnergyCurve.EngagementMultiplier(engaged),
                    EnergyCurve.EngagementMultiplier(engaged - 1));
            }
        }

        /// <summary>
        /// The single assertion that captures the design: a fully engaged, nearly dead party is
        /// worth hundreds of times an untouched party strolling to the boss room.
        /// </summary>
        [Test]
        public void Rate_ForEngagedDyingParty_DwarfsIdleParty()
        {
            float idle = EnergyCurve.Rate(0, 1f);
            float perfect = EnergyCurve.Rate(4, 0.05f);
            MooseRunnerFacade.Log($"idle = {idle:F4}/s, perfect suffering = {perfect:F2}/s, ratio {perfect / idle:F0}x");
            Assert.Greater(perfect / idle, 500f);
        }

        /// <summary>
        /// A whole simulated 60-second raid, which is the unit the player actually experiences.
        /// Stalling a wounded party in combat must beat letting a healthy one walk to the boss.
        /// This is the test that a flat curve would fail and a per-instant assertion would not.
        /// </summary>
        [Test]
        public void SimulatedRaid_StallingWoundedParty_FarOutEarnsAWalkthrough()
        {
            const float step = 1f / 60f;

            // Played badly: the party walks the corridor untouched and leaves early.
            float walkthrough = 0f;
            for (float t = 0f; t < 20f; t += step)
            {
                walkthrough += EnergyCurve.Rate(0, 1f) * step;
            }

            // Played well: engaged the whole minute, ground down from full health to a sliver.
            float stalled = 0f;
            for (float t = 0f; t < 60f; t += step)
            {
                float health = Mathf01(1f - (t / 60f) * 0.95f);
                stalled += EnergyCurve.Rate(4, health) * step;
            }

            MooseRunnerFacade.Log($"walkthrough = {walkthrough:F1}, stalled = {stalled:F1}, " +
                                  $"ratio {stalled / walkthrough:F0}x");
            Assert.Greater(stalled / walkthrough, 100f,
                "a stalled, wounded raid must be worth vastly more than a walkthrough");
        }

        /// <summary>
        /// Killing the party early must earn less than keeping it alive and bleeding for the full
        /// minute, even though the dying party spends time at the richest part of the curve.
        /// </summary>
        [Test]
        public void SimulatedRaid_WipingTheParty_EarnsLessThanKeepingItAlive()
        {
            const float step = 1f / 60f;

            // Wiped at 20s: members die one by one, so engagement collapses as health falls.
            float wiped = 0f;
            for (float t = 0f; t < 20f; t += step)
            {
                float progress = t / 20f;
                int alive = 4 - (int)(progress * 4f);
                wiped += EnergyCurve.Rate(alive, Mathf01(1f - progress)) * step;
            }

            // Kept alive: four fighters the whole minute, hovering at a sliver of health.
            float sustained = 0f;
            for (float t = 0f; t < 60f; t += step)
            {
                sustained += EnergyCurve.Rate(4, 0.08f) * step;
            }

            MooseRunnerFacade.Log($"wiped = {wiped:F1}, sustained = {sustained:F1}");
            Assert.Greater(sustained, wiped,
                "killing the party must never out-earn keeping it alive");
        }

        /// <summary>Clamps a float into 0..1 without depending on UnityEngine.Mathf.</summary>
        private static float Mathf01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
