using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RaidManager
{
    /// <summary>Something that happened once, worth a burst of particles.</summary>
    public enum EffectKind
    {
        /// <summary>A trap fired. One of the player's three verbs.</summary>
        TrapFired = 0,

        /// <summary>A monster was spawned. One of the player's three verbs.</summary>
        MobSpawned = 1,

        /// <summary>A monster died, ending whatever stalling it was doing.</summary>
        MobDied = 2,

        /// <summary>A door was opened or shut. The third verb, and the cheapest.</summary>
        DoorToggled = 3,

        /// <summary>
        /// A chest was prised open.
        /// </summary>
        /// <remarks>
        /// Worth its own burst because it is the one moment the player is <i>paid</i> for something
        /// the party chose to do. A chest costs the party seconds and now pays the whole team a
        /// bonus for five of them, and a reward nobody sees arrive is a reward nobody learns to buy.
        /// </remarks>
        ChestOpened = 4,

        /// <summary>
        /// The stake on a spawned monster came back because the party killed it.
        /// </summary>
        /// <remarks>
        /// Shown so the loan is legible. A player who never sees the twenty-five return will go on
        /// treating spawning as a purchase and hoard against a cost that is not really there.
        /// </remarks>
        SpawnRefunded = 5
    }

    /// <summary>One effect waiting to be drawn.</summary>
    public readonly struct Effect
    {
        /// <summary>What happened.</summary>
        public EffectKind Kind { get; }

        /// <summary>Where it happened, in grid units.</summary>
        public Vector2 Position { get; }

        /// <summary>Creates an effect.</summary>
        /// <param name="kind">What happened.</param>
        /// <param name="position">Where.</param>
        public Effect(EffectKind kind, Vector2 position)
        {
            Kind = kind;
            Position = position;
        }
    }

    /// <summary>
    /// One-shot events the view turns into particle bursts.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="CombatFeed"/>. That one carries a running stream of
    /// damage, where the interesting question is "how much"; these are discrete moments where the
    /// only question is "what just happened, and where".
    /// <para>
    /// <b>All three of the player's verbs report here.</b> Firing a trap, spawning a monster and
    /// swinging a door were, until now, actions with no acknowledgement at all beyond a number
    /// moving somewhere else on screen -- the player pressed a thing and the game appeared not to
    /// notice. SPEC.md is explicit that juice matters more than content.
    /// </para>
    /// </remarks>
    public sealed class EffectFeed
    {
        private readonly List<Effect> _pending = new();

        /// <summary>Effects raised since the view last drained them.</summary>
        public IReadOnlyList<Effect> Pending => _pending;

        /// <summary>Raises an effect.</summary>
        /// <param name="kind">What happened.</param>
        /// <param name="position">Where it happened.</param>
        public void Raise(EffectKind kind, Vector2 position)
        {
            _pending.Add(new Effect(kind, position));
        }

        /// <summary>
        /// Clears everything raised so far.
        /// </summary>
        /// <remarks>
        /// Drained rather than aged, unlike the combat numbers. A burst is spawned the instant it is
        /// read and then lives its own life as a particle system, so the feed only has to survive
        /// from the tick that raised it to the frame that draws it.
        /// </remarks>
        public void Drain()
        {
            _pending.Clear();
        }
    }
}
