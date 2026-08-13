using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.RaidManager
{
    /// <summary>One number that should float up off something.</summary>
    public sealed class CombatNumber
    {
        /// <summary>Where it started, in grid units.</summary>
        public Vector2 Origin { get; }

        /// <summary>How much, already rounded for display.</summary>
        public int Amount { get; }

        /// <summary>Whether this was healing rather than damage.</summary>
        public bool IsHeal { get; }

        /// <summary>Seconds since it appeared.</summary>
        public float Age { get; private set; }

        /// <summary>
        /// Sideways offset in cells, so consecutive numbers do not stack into one column.
        /// </summary>
        /// <remarks>
        /// Everything a monster takes originates from the same point, so without this the numbers
        /// pile straight up on top of each other and the result is an unreadable clump rather than
        /// legible hits. Assigned in rotating lanes rather than randomly, so a replayed seed shows
        /// the identical picture.
        /// </remarks>
        public float Spread { get; }

        /// <summary>Creates a number at a position.</summary>
        /// <param name="origin">Where it appeared.</param>
        /// <param name="amount">How much.</param>
        /// <param name="isHeal">True for healing.</param>
        /// <param name="spread">Sideways offset in cells.</param>
        public CombatNumber(Vector2 origin, int amount, bool isHeal, float spread)
        {
            Origin = origin;
            Amount = amount;
            IsHeal = isHeal;
            Spread = spread;
        }

        /// <summary>Ages the number.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(float deltaTime) => Age += deltaTime;
    }

    /// <summary>
    /// The recent damage and healing, for the view to float up off the fight.
    /// </summary>
    /// <remarks>
    /// Damage arrives continuously -- fractions of a point every physics tick -- and a number that
    /// changed sixty times a second would be unreadable, the same mistake the energy rate made. So
    /// hits are accumulated per source and only released once they are worth showing, which is what
    /// turns a stream into the punchy red and green pops the author asked for.
    /// <para>
    /// This deliberately shows a <i>delta</i>, never a total. SPEC.md's rule is that adventurer HP is
    /// never shown as a number, and "you just took 12" says nothing about how much is left -- which
    /// is the ambiguity the design wants to keep.
    /// </para>
    /// </remarks>
    public sealed class CombatFeed
    {
        /// <summary>
        /// Damage that must accumulate before a number is shown.
        /// </summary>
        /// <remarks>
        /// Sized against the party's 20 dps, which at a threshold of 6 produced a number three times
        /// a second and a solid unreadable column of them. At 14 a fight pops about twice a second,
        /// which reads as blows landing.
        /// </remarks>
        public const float DamageThreshold = 14f;

        /// <summary>Seconds a number stays on screen.</summary>
        public const float Lifetime = 1.1f;

        /// <summary>
        /// How many lanes numbers are spread across, and how far apart they sit.
        /// </summary>
        /// <remarks>
        /// Deliberately narrow: the whole spread is about one number's width either side of the
        /// source, which is enough to stop consecutive hits overlapping without scattering them so
        /// far that it stops being obvious what they came off. At 0.26 they drifted a half-cell out
        /// and read as belonging to whatever they had wandered over.
        /// </remarks>
        private const int Lanes = 5;
        private const float LaneWidth = 0.16f;

        private readonly List<CombatNumber> _numbers = new();
        private readonly Dictionary<object, float> _pending = new();
        private int _released;

        /// <summary>Next lane offset, rotating so consecutive numbers never overlap.</summary>
        private float NextSpread()
        {
            float lane = ((_released % Lanes) - ((Lanes - 1) * 0.5f)) * LaneWidth;
            _released++;
            return lane;
        }

        /// <summary>Numbers currently floating.</summary>
        public IReadOnlyList<CombatNumber> Numbers => _numbers;

        /// <summary>
        /// Adds damage against a source, releasing a number once enough has built up.
        /// </summary>
        /// <param name="source">Whatever is taking the damage, used to keep tallies apart.</param>
        /// <param name="position">Where to show it.</param>
        /// <param name="amount">Damage this tick.</param>
        public void Damage(object source, Vector2 position, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float total = _pending.GetValueOrDefault(source, 0f) + amount;
            if (total < DamageThreshold)
            {
                _pending[source] = total;
                return;
            }

            _pending[source] = 0f;
            _numbers.Add(new CombatNumber(position, Mathf.RoundToInt(total), false, NextSpread()));
        }

        /// <summary>Adds a heal, which is always a single discrete cast and shows immediately.</summary>
        /// <param name="position">Where to show it.</param>
        /// <param name="amount">Health restored.</param>
        public void Heal(Vector2 position, float amount)
        {
            if (amount > 0f)
            {
                _numbers.Add(new CombatNumber(
                    position, Mathf.RoundToInt(amount), true, NextSpread()));
            }
        }

        /// <summary>Ages every number and drops the expired ones.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(float deltaTime)
        {
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                _numbers[i].Tick(deltaTime);
                if (_numbers[i].Age >= Lifetime)
                {
                    _numbers.RemoveAt(i);
                }
            }
        }
    }
}
