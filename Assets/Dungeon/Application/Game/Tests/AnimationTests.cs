using Dungeon.Game;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Covers the procedural attack, cast, shoot and facing motion every sprite animates with.
    /// </summary>
    /// <remarks>
    /// The game ships no drawn animation frames, so a swing is only visible if these functions move
    /// the sprite. That makes them the animation -- if they return zero, combat renders as two health
    /// bars changing length with everyone standing perfectly still, which is what it did before.
    /// <para>
    /// Every one of these is a pure function of state, so a seeded raid photographed at a given
    /// moment reproduces exactly. That is a spec requirement, not a convenience: SPEC.md asks for a
    /// run to be reproducible from a seed in a bug report.
    /// </para>
    /// </remarks>
    public sealed class AnimationTests
    {
        /// <summary>
        /// A tank's swing throws it at what it is hitting rather than away from it.
        /// </summary>
        [Test]
        public void TankLungesTowardItsTarget()
        {
            (Vector2 shove, float _) = SpriteMotion.ForAttack(
                AdventurerRole.Tank, 0.2f, Vector2.right);

            Assert.Greater(shove.x, 0.05f, "a tank should lunge at the monster, not shy from it");
        }

        /// <summary>
        /// An archer recoils backwards from its own shot instead of charging the target.
        /// </summary>
        /// <remarks>
        /// The whole point of giving the roles different shapes: a bow pushes the shooter. An archer
        /// that lunged would read as a second melee fighter, which is exactly the confusion the
        /// distinct sprites are there to avoid.
        /// </remarks>
        [Test]
        public void ArcherRecoilsAwayFromItsShot()
        {
            (Vector2 shove, float _) = SpriteMotion.ForAttack(
                AdventurerRole.Ranged, 0.15f, Vector2.right);

            Assert.Less(shove.x, 0f, "an archer should be shoved back by the shot, not forward");
        }

        /// <summary>
        /// A mage rises as it casts rather than lunging, so a cast never reads as a melee blow.
        /// </summary>
        [Test]
        public void MageRisesWhenItCasts()
        {
            (Vector2 shove, float tilt) = SpriteMotion.ForAttack(
                AdventurerRole.Mage, 0.2f, Vector2.right);

            Assert.Greater(shove.y, 0.05f, "a mage should lift as it casts");
            Assert.Greater(tilt, 1f, "a mage should lean back, not into the blow");
        }

        /// <summary>
        /// Every role's attack starts at rest, peaks, and returns to rest.
        /// </summary>
        /// <remarks>
        /// A shape that ended anywhere but zero would leave the sprite permanently displaced from the
        /// position the simulation says it occupies -- the picture and the fight would disagree about
        /// where everyone is standing, and every later frame would inherit the error.
        /// </remarks>
        [Test]
        public void EveryAttackReturnsToRest()
        {
            foreach (AdventurerRole role in System.Enum.GetValues(typeof(AdventurerRole)))
            {
                (Vector2 start, float startTilt) = SpriteMotion.ForAttack(role, 0f, Vector2.right);
                (Vector2 end, float endTilt) = SpriteMotion.ForAttack(role, 1f, Vector2.right);
                (Vector2 peak, float _) = SpriteMotion.ForAttack(role, 0.2f, Vector2.right);

                Assert.Less(start.magnitude, 0.001f, $"{role} should begin at rest");
                Assert.Less(Mathf.Abs(startTilt), 0.001f, $"{role} should begin untilted");
                Assert.Less(end.magnitude, 0.001f, $"{role} should settle back to rest");
                Assert.Less(Mathf.Abs(endTilt), 0.001f, $"{role} should settle back untilted");
                Assert.Greater(peak.magnitude, 0.02f, $"{role}'s swing should be visible");
            }
        }

        /// <summary>
        /// A monster's lunge is heavier than an adventurer's and also returns to rest.
        /// </summary>
        [Test]
        public void MobLungeIsHeavyAndReturns()
        {
            Vector2 peak = SpriteMotion.ForMobAttack(0.2f, Vector2.right);
            Vector2 rest = SpriteMotion.ForMobAttack(1f, Vector2.right);
            (Vector2 tank, float _) = SpriteMotion.ForAttack(
                AdventurerRole.Tank, 0.25f, Vector2.right);

            Assert.Greater(peak.magnitude, tank.magnitude, "a monster should hit heavier than a tank");
            Assert.Less(rest.magnitude, 0.001f, "a monster should settle back to rest");
        }

        /// <summary>
        /// A walking sprite squashes and stretches, and does so without changing size.
        /// </summary>
        /// <remarks>
        /// Volume preservation is the assertion that matters. A squash that also shrinks the sprite
        /// reads as the character moving away from the camera, which in a top-down game is nonsense.
        /// </remarks>
        [Test]
        public void WalkingSquashesWithoutChangingSize()
        {
            float widest = 0f;
            float narrowest = 2f;

            for (int step = 0; step < 60; step++)
            {
                float t = step * 0.02f;
                Vector2 squash = SpriteMotion.WalkSquash(
                    PartyGoal.Advancing, WoundState.Healthy, t, 0f);

                widest = Mathf.Max(widest, squash.x);
                narrowest = Mathf.Min(narrowest, squash.x);
                Assert.AreEqual(2f, squash.x + squash.y, 0.0001f,
                    "squash must preserve volume or the sprite appears to change size");
            }

            Assert.Greater(widest, 1.01f, "a walker should widen on the footfall");
            Assert.Less(narrowest, 0.99f, "a walker should narrow between footfalls");
        }

        /// <summary>
        /// A standing sprite does not squash, and a badly wounded one barely does.
        /// </summary>
        [Test]
        public void SquashFadesWithWoundsAndStopsWhenStill()
        {
            Assert.AreEqual(Vector2.one,
                SpriteMotion.WalkSquash(PartyGoal.Fighting, WoundState.Healthy, 0.3f, 0f),
                "a sprite that is not walking should not bounce");

            float healthy = 0f;
            float critical = 0f;
            for (int step = 0; step < 60; step++)
            {
                float t = step * 0.02f;
                healthy = Mathf.Max(healthy, Mathf.Abs(
                    SpriteMotion.WalkSquash(PartyGoal.Advancing, WoundState.Healthy, t, 0f).x - 1f));
                critical = Mathf.Max(critical, Mathf.Abs(
                    SpriteMotion.WalkSquash(PartyGoal.Advancing, WoundState.Critical, t, 0f).x - 1f));
            }

            Assert.Less(critical, healthy * 0.6f,
                "a critical member should drag rather than bounce");
        }

        /// <summary>
        /// Facing follows sideways movement, so a retreating party visibly turns around.
        /// </summary>
        [Test]
        public void FacingFollowsMovement()
        {
            Assert.AreEqual(1f, SpriteMotion.Facing(-1f, 0.4f), "moving right should face right");
            Assert.AreEqual(-1f, SpriteMotion.Facing(1f, -0.4f), "moving left should face left");
        }

        /// <summary>
        /// A sprite jostling in place keeps its facing rather than strobing every frame.
        /// </summary>
        /// <remarks>
        /// Without the deadzone a member drifting a hundredth of a cell toward its formation slot
        /// flips left and right on alternate frames, which at 60fps is a flicker, not a turn.
        /// </remarks>
        [Test]
        public void FacingHoldsThroughTinyJostles()
        {
            float facing = 1f;
            for (int i = 0; i < 20; i++)
            {
                facing = SpriteMotion.Facing(facing, i % 2 == 0 ? 0.001f : -0.001f);
                Assert.AreEqual(1f, facing, "a jostling sprite should hold its facing");
            }
        }

        /// <summary>
        /// An adventurer's attack phase runs from 0 at the blow to 1 once recovered.
        /// </summary>
        /// <remarks>
        /// Derived from the same cooldown the combat code sets, so the animation cannot claim a swing
        /// that never landed or miss one that did.
        /// </remarks>
        [Test]
        public void AdventurerAttackPhaseTracksItsCooldown()
        {
            var tank = new Adventurer(AdventurerRole.Tank, Vector2Int.zero)
            {
                AttackCooldown = 1.2f
            };
            Assert.AreEqual(0f, tank.AttackPhase, 0.001f, "phase should be 0 at the blow");

            tank.AttackCooldown = 0f;
            Assert.AreEqual(1f, tank.AttackPhase, 0.001f, "phase should be 1 once recovered");
        }

        /// <summary>
        /// A monster's attack phase tracks its own cooldown the same way.
        /// </summary>
        [Test]
        public void MobAttackPhaseTracksItsCooldown()
        {
            var mob = new Mob(MobKind.Skeleton, Vector2Int.zero, 0)
            {
                AttackCooldown = 1.3f
            };
            Assert.AreEqual(0f, mob.AttackPhase, 0.001f, "phase should be 0 at the blow");

            mob.AttackCooldown = 0.65f;
            Assert.AreEqual(0.5f, mob.AttackPhase, 0.01f, "phase should be half way through");
        }
    }
}
