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
        /// <param name="panicking">
        /// Whether this member is scrambling away from something close. Per member rather than per
        /// party, so a healer bolting from a skeleton reads differently from the tank trading blows
        /// with it two feet away.
        /// </param>
        /// <returns>A vertical offset in world units and a roll angle in degrees.</returns>
        public static (float lift, float tilt) ForAdventurer(
            PartyGoal goal, WoundState wounds, float time, float phase, bool panicking = false)
        {
            if (panicking)
            {
                return Panic(wounds, time, phase);
            }

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

        /// <summary>
        /// Motion for an adventurer scrambling away from something.
        /// </summary>
        /// <remarks>
        /// SPEC.md section 9 puts "party members visibly panicking" first on its list of what polish
        /// time should buy, and until now the only sign was that they moved faster -- which nobody
        /// notices in a busy room and which is invisible in a still frame.
        /// <para>
        /// Fast, tall, off-balance, and deliberately <i>not</i> a clean sine. Two waves at
        /// incommensurable rates never repeat, so the sprite jitters rather than bouncing in a tidy
        /// rhythm; a regular bob reads as marching however fast you make it.
        /// </para>
        /// </remarks>
        /// <param name="wounds">How hurt this member is.</param>
        /// <param name="time">Seconds since the raid began.</param>
        /// <param name="phase">Per-member offset.</param>
        /// <returns>Lift and tilt.</returns>
        private static (float lift, float tilt) Panic(WoundState wounds, float time, float phase)
        {
            const float rate = 14f;
            float scramble = Mathf.Abs(Mathf.Sin((time * rate) + phase))
                             + (Mathf.Abs(Mathf.Sin((time * rate * 0.61f) + phase)) * 0.5f);

            // Rocking hard enough to read at a glance, and leaning further the worse off they are --
            // a critical member flailing is the picture the spec is asking for.
            float lean = wounds == WoundState.Critical ? 1.4f : 1f;
            float tilt = Mathf.Sin((time * rate * 0.47f) + phase) * 16f * lean;

            return (scramble * WalkBob * 1.35f, tilt);
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
        /// How an attack throws a sprite about, on top of whatever it was already doing.
        /// </summary>
        /// <remarks>
        /// A swing was previously invisible: combat was two health bars changing length and a number
        /// popping, with the sprites standing perfectly still throughout. This gives every role a
        /// distinct action, driven from the same cooldown that produced the damage, so the picture
        /// cannot disagree with the fight.
        /// <para>
        /// The shapes differ on purpose, because the roles do. A tank lunges bodily at what it is
        /// hitting and recovers slowly. An archer snaps <i>backwards</i> — a bow's recoil pushes the
        /// shooter, not the target. A mage rises and leans back as it casts, hands up rather than
        /// weight forward. A healer barely moves.
        /// </para>
        /// </remarks>
        /// <param name="role">Which role is attacking.</param>
        /// <param name="phase">0 at the instant of the blow, 1 once recovered.</param>
        /// <param name="toTarget">Direction of whatever is being struck, normalised.</param>
        /// <returns>A positional offset in world units and an extra roll in degrees.</returns>
        public static (Vector2 shove, float tilt) ForAttack(
            AdventurerRole role, float phase, Vector2 toTarget)
        {
            // A hard strike out and a soft settle back is the difference between a punch and a sway.
            float strike = phase < 0.25f
                ? phase / 0.25f
                : 1f - ((phase - 0.25f) / 0.75f);
            strike = Mathf.Clamp01(strike);

            switch (role)
            {
                case AdventurerRole.Tank:
                    return (toTarget * 0.26f * strike, -9f * strike);

                case AdventurerRole.Ranged:
                    // Recoil: the archer is shoved away from the shot, not toward it.
                    return (-toTarget * 0.13f * strike, 5f * strike);

                case AdventurerRole.Mage:
                    // Rises and leans back, hands up. Nothing lunges when it casts.
                    return (new Vector2(-toTarget.x * 0.05f, 0.16f) * strike, 11f * strike);

                default:
                    return (toTarget * 0.06f * strike, -3f * strike);
            }
        }

        /// <summary>
        /// How a monster's attack throws it about.
        /// </summary>
        /// <remarks>
        /// A single hard lunge. Monsters have no ranged options in this game, so there is only the
        /// one shape to express, and it should look heavier than an adventurer's.
        /// </remarks>
        /// <param name="phase">0 at the instant of the blow, 1 once recovered.</param>
        /// <param name="toTarget">Direction of the party, normalised.</param>
        /// <returns>A positional offset in world units.</returns>
        public static Vector2 ForMobAttack(float phase, Vector2 toTarget)
        {
            float strike = phase < 0.2f ? phase / 0.2f : 1f - ((phase - 0.2f) / 0.8f);
            return toTarget * 0.3f * Mathf.Clamp01(strike);
        }

        /// <summary>
        /// Squash and stretch for a walking sprite, so footfalls land instead of the figure floating.
        /// </summary>
        /// <remarks>
        /// A bob on its own reads as hovering: the sprite rises and falls with no weight behind it.
        /// Compressing on the way down and stretching on the way up is the oldest trick there is for
        /// making a static drawing walk, and it costs one multiply per sprite.
        /// <para>
        /// The scale is deliberately volume-preserving — wider exactly as much as it is shorter — so a
        /// squashing sprite never appears to change size, only shape.
        /// </para>
        /// </remarks>
        /// <param name="goal">What the party is doing.</param>
        /// <param name="wounds">How hurt this member is.</param>
        /// <param name="time">Seconds since the raid began.</param>
        /// <param name="phase">Per-member offset.</param>
        /// <returns>A non-uniform scale to multiply the sprite's own scale by.</returns>
        public static Vector2 WalkSquash(
            PartyGoal goal, WoundState wounds, float time, float phase)
        {
            if (goal is not (PartyGoal.Advancing or PartyGoal.Retreating))
            {
                return Vector2.one;
            }

            // A wounded walker drags rather than bounces, so the effect fades exactly as the bob does
            // -- the two have to agree or the sprite squashes without rising.
            float vigour = wounds switch
            {
                WoundState.Healthy => 1f,
                WoundState.Hurt => 0.62f,
                _ => 0.34f
            };

            float rate = WalkRate * vigour * (goal == PartyGoal.Retreating ? 1.35f : 1f);

            // Twice the bob's frequency: a stride has two footfalls, and squashing once per cycle
            // reads as a limp even on a healthy member.
            float beat = Mathf.Sin(((time * rate * 2f) + phase) - (Mathf.PI * 0.5f));
            float amount = 0.055f * vigour * beat;
            return new Vector2(1f + amount, 1f - amount);
        }

        /// <summary>
        /// Which way a sprite should face, given how far it just moved sideways.
        /// </summary>
        /// <remarks>
        /// Sprites previously faced one fixed direction for the whole raid, so a party that turned
        /// around and ran for a door still walked backwards the entire way — the retreat, which is
        /// the single most important thing the player can cause, looked identical to the advance.
        /// <para>
        /// The deadzone is why this needs to be a function rather than a comparison at the call site.
        /// A member drifting a hundredth of a cell as it jostles for a formation slot would otherwise
        /// flip every frame and strobe. Below the threshold the previous facing is kept, so a sprite
        /// standing still holds whichever way it was last going.
        /// </para>
        /// </remarks>
        /// <param name="previous">Facing from the last frame, 1 for right and -1 for left.</param>
        /// <param name="deltaX">Sideways movement since the last frame, in cells.</param>
        /// <returns>1 to face right, -1 to face left.</returns>
        public static float Facing(float previous, float deltaX)
        {
            const float deadzone = 0.004f;
            if (deltaX > deadzone)
            {
                return 1f;
            }

            if (deltaX < -deadzone)
            {
                return -1f;
            }

            return previous == 0f ? 1f : previous;
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
