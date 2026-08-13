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

        /// <summary>Creates a number at a position.</summary>
        /// <param name="origin">Where it appeared.</param>
        /// <param name="amount">How much.</param>
        /// <param name="isHeal">True for healing.</param>
        public CombatNumber(Vector2 origin, int amount, bool isHeal)
        {
            Origin = origin;
            Amount = amount;
            IsHeal = isHeal;
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
        /// <summary>Damage that must accumulate before a number is shown.</summary>
        public const float DamageThreshold = 6f;

        /// <summary>Seconds a number stays on screen.</summary>
        public const float Lifetime = 1.1f;

        private readonly List<CombatNumber> _numbers = new();
        private readonly Dictionary<object, float> _pending = new();

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
            _numbers.Add(new CombatNumber(position, Mathf.RoundToInt(total), false));
        }

        /// <summary>Adds a heal, which is always a single discrete cast and shows immediately.</summary>
        /// <param name="position">Where to show it.</param>
        /// <param name="amount">Health restored.</param>
        public void Heal(Vector2 position, float amount)
        {
            if (amount > 0f)
            {
                _numbers.Add(new CombatNumber(position, Mathf.RoundToInt(amount), true));
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
