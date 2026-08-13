using Dungeon.PartyManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Procedural motion for sprites that have no drawn animation frames.
    /// </summary>
    /// <remarks>
    /// SPEC.md forbids showing adventurer hit points and requires wounded state to be read from
    /// movement instead -- limping, slowing, panicking. With only three static wound sprites per
    /// role, the art alone cannot carry that, and rigging twelve walk cycles is a large, uncertain,
    /// non-deterministic job.
    /// <para>
    /// This closes the gap cheaply and deterministically: a bob whose speed and height fall as
    /// health falls, plus a lean that grows as a member nears death. A healthy party strides, a
    /// critical one drags. It is a stopgap rather than a substitute for drawn frames -- but it makes
    /// the wound states legible in motion, which is the actual requirement, and it applies to every
    /// sprite at once rather than to whichever twelve got rigged.
    /// </para>
    /// <para>
    /// Everything here is a pure function of time and state, so a screenshot taken at a given moment
    /// is reproducible and the simulation is untouched.
    /// </para>
    /// </remarks>
    public static class SpriteMotion
    {
        /// <summary>Bob height in world units for a healthy walker.</summary>
        private const float WalkBob = 0.055f;

        /// <summary>Bob cycles per second for a healthy walker.</summary>
        private const float WalkRate = 5.2f;

        /// <summary>How far a dying adventurer leans, in degrees.</summary>
        private const float CriticalLean = 11f;

        /// <summary>
        /// Vertical offset and tilt for one party member.
        /// </summary>
        /// <param name="goal">What the party is doing.</param>
        /// <param name="wounds">How hurt this member is.</param>
        /// <param name="time">Seconds since the raid began.</param>
        /// <param name="phase">Per-member offset so the four do not bob in lockstep.</param>
        /// <returns>A vertical offset in world units and a roll angle in degrees.</returns>
        public static (float lift, float tilt) ForAdventurer(
            PartyGoal goal, WoundState wounds, float time, float phase)
        {
            // A wounded party moves less and slower. This is the readable signal that replaces a
            // health bar, so the falloff is steep enough to notice at a glance.
            float vigour = wounds switch
            {
                WoundState.Healthy => 1f,
                WoundState.Hurt => 0.62f,
                _ => 0.34f
            };

            float rate = WalkRate * vigour;
            float height = WalkBob * vigour;

            switch (goal)
            {
                case PartyGoal.Fighting:
                    // Short, urgent jabs rather than a stride.
                    return (Mathf.Abs(Mathf.Sin((time * rate * 1.6f) + phase)) * height * 0.8f,
                        Tilt(wounds, time, phase));

                case PartyGoal.Retreating:
                    // Faster and more frantic: the party is running for its life.
                    return (Mathf.Abs(Mathf.Sin((time * rate * 1.35f) + phase)) * height,
                        Tilt(wounds, time, phase));

                case PartyGoal.Advancing:
                    return (Mathf.Abs(Mathf.Sin((time * rate) + phase)) * height,
                        Tilt(wounds, time, phase));

                default:
                    return (0f, Tilt(wounds, time, phase));
            }
        }

        /// <summary>Roll angle for an adventurer, growing as they near death.</summary>
        private static float Tilt(WoundState wounds, float time, float phase)
        {
            if (wounds == WoundState.Healthy)
            {
                return 0f;
            }

            float amount = wounds == WoundState.Hurt ? 0.35f : 1f;

            // A limp is asymmetric: the sag dwells on one side rather than swinging evenly, so this
            // biases the wave rather than using a plain sine.
            float wave = Mathf.Sin((time * 3.1f) + phase);
            return (-CriticalLean * amount) * ((wave * 0.5f) + 0.5f);
        }

        /// <summary>
        /// Vertical offset for a monster, which breathes in place rather than walking.
        /// </summary>
        /// <param name="engaged">Whether it is in contact with the party.</param>
        /// <param name="time">Seconds since the raid began.</param>
        /// <param name="phase">Per-mob offset so a room of them does not pulse together.</param>
        /// <returns>A vertical offset in world units.</returns>
        public static float ForMob(bool engaged, float time, float phase)
        {
            float rate = engaged ? 8.5f : 2.4f;
            float height = engaged ? 0.075f : 0.03f;
            return Mathf.Abs(Mathf.Sin((time * rate) + phase)) * height;
        }
    }
}
