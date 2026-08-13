using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RaidManager
{
    /// <summary>What kind of shot is in the air.</summary>
    public enum ShotKind
    {
        /// <summary>An archer's arrow.</summary>
        Arrow = 0,

        /// <summary>A mage's bolt.</summary>
        Bolt = 1
    }

    /// <summary>One shot travelling from an attacker to its target.</summary>
    /// <remarks>
    /// Purely presentational: the damage has already been applied when the shot is created. Making
    /// the projectile carry the damage would mean a mage's bolt could miss a target that died in
    /// flight, and the number the player saw would stop matching the health bar that moved.
    /// </remarks>
    public sealed class Shot
    {
        /// <summary>Where it was loosed from, in grid units.</summary>
        public Vector2 From { get; }

        /// <summary>Where it lands, in grid units.</summary>
        public Vector2 To { get; }

        /// <summary>Arrow or bolt.</summary>
        public ShotKind Kind { get; }

        /// <summary>Seconds since it was loosed.</summary>
        public float Age { get; private set; }

        /// <summary>How far along its flight, 0 to 1.</summary>
        public float Progress => Mathf.Clamp01(Age / ProjectileFeed.FlightSeconds);

        /// <summary>Creates a shot.</summary>
        /// <param name="from">Origin.</param>
        /// <param name="to">Target.</param>
        /// <param name="kind">Arrow or bolt.</param>
        public Shot(Vector2 from, Vector2 to, ShotKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        /// <summary>Ages the shot.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(float deltaTime) => Age += deltaTime;
    }

    /// <summary>
    /// Shots currently in the air, for the view to draw.
    /// </summary>
    /// <remarks>
    /// Without these the ranged pair simply stood at a distance while a monster's health silently
    /// fell, and there was no way to tell who was actually contributing. An arrow leaving a bow is
    /// the cheapest possible answer to "who did that?".
    /// </remarks>
    public sealed class ProjectileFeed
    {
        /// <summary>How long a shot takes to cross, whatever the distance.</summary>
        /// <remarks>
        /// Fixed rather than speed-based, so a shot always lands about a fifth of a second after the
        /// number it belongs to appears. Constant speed would make a long shot arrive well after its
        /// own damage, which reads as the arrow having missed.
        /// </remarks>
        public const float FlightSeconds = 0.22f;

        private readonly List<Shot> _shots = new();

        /// <summary>Shots currently in the air.</summary>
        public IReadOnlyList<Shot> Shots => _shots;

        /// <summary>Looses a shot.</summary>
        /// <param name="from">Origin.</param>
        /// <param name="to">Target.</param>
        /// <param name="kind">Arrow or bolt.</param>
        public void Fire(Vector2 from, Vector2 to, ShotKind kind)
        {
            _shots.Add(new Shot(from, to, kind));
        }

        /// <summary>Ages every shot and drops the landed ones.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(float deltaTime)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                _shots[i].Tick(deltaTime);
                if (_shots[i].Age >= FlightSeconds)
                {
                    _shots.RemoveAt(i);
                }
            }
        }
    }
}
