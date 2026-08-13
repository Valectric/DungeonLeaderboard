using UnityEngine;

namespace Dungeon.PartyManager
{
    /// <summary>The four adventurer archetypes. Party composition is the main source of variety.</summary>
    public enum AdventurerRole
    {
        /// <summary>High health, low damage, soaks the mobs' attention.</summary>
        Tank = 0,

        /// <summary>Heals wounded allies from a limited pool. The player's best customer.</summary>
        Healer = 1,

        /// <summary>Steady damage from range, fragile.</summary>
        Ranged = 2,

        /// <summary>Burst damage, fragile, limited resource.</summary>
        Mage = 3
    }

    /// <summary>How wounded an adventurer looks. Never a number -- the player reads posture and blood.</summary>
    /// <remarks>
    /// SPEC.md is explicit that exact HP must never be shown. The ambiguity between "nearly dead" and
    /// "dead in one hit" is where the tension lives, so this coarse three-state view is the *most*
    /// the presentation layer is ever allowed to know.
    /// </remarks>
    public enum WoundState
    {
        /// <summary>Above two thirds health.</summary>
        Healthy = 0,

        /// <summary>Between one third and two thirds.</summary>
        Hurt = 1,

        /// <summary>Below one third, and visibly limping.</summary>
        Critical = 2
    }

    /// <summary>
    /// One member of the raiding party.
    /// </summary>
    /// <remarks>
    /// Health is deliberately exposed only as a fraction and a <see cref="WoundState"/>. Nothing
    /// outside this module can read raw hit points, which makes the spec's "never show a number"
    /// rule structural rather than a habit the UI layer has to remember.
    /// </remarks>
    public sealed class Adventurer
    {
        private float _health;

        /// <summary>This adventurer's archetype.</summary>
        public AdventurerRole Role { get; }

        /// <summary>Maximum health for the role.</summary>
        public float MaxHealth { get; }

        /// <summary>Damage dealt per second while fighting.</summary>
        public float DamagePerSecond { get; }

        /// <summary>Current cell in the dungeon grid.</summary>
        public Vector2Int Cell { get; set; }

        /// <summary>Whether this adventurer is still alive.</summary>
        public bool IsAlive => _health > 0f;

        /// <summary>Health as a fraction from 1 down to 0. The only quantitative view exposed.</summary>
        public float HealthFraction => Mathf.Clamp01(_health / MaxHealth);

        /// <summary>Coarse wounded state for presentation. Never a precise number.</summary>
        public WoundState Wounds =>
            HealthFraction > 2f / 3f ? WoundState.Healthy :
            HealthFraction > 1f / 3f ? WoundState.Hurt :
            WoundState.Critical;

        /// <summary>Creates an adventurer at full health.</summary>
        /// <param name="role">Archetype to build.</param>
        /// <param name="cell">Starting cell.</param>
        public Adventurer(AdventurerRole role, Vector2Int cell)
        {
            Role = role;
            Cell = cell;
            MaxHealth = role switch
            {
                AdventurerRole.Tank => 220f,
                AdventurerRole.Healer => 110f,
                AdventurerRole.Ranged => 90f,
                AdventurerRole.Mage => 80f,
                _ => 100f
            };
            // Party damage is deliberately low -- 20 dps across the whole party. These numbers set
            // how long a fight lasts, and a fight is the only thing that earns. At the first pass
            // the party dealt 41 dps and killed a skeleton in 2.2 seconds, so a 60-second raid paid
            // out 11.7 energy in total and no verb was ever affordable. Fight length is a rate, and
            // rates are what this game is made of: raise these and the game quietly stops working.
            DamagePerSecond = role switch
            {
                AdventurerRole.Tank => 3f,
                AdventurerRole.Healer => 1f,
                AdventurerRole.Ranged => 7f,
                AdventurerRole.Mage => 9f,
                _ => 5f
            };
            _health = MaxHealth;
        }

        /// <summary>
        /// Applies damage, floored at zero.
        /// </summary>
        /// <param name="amount">Damage to apply; negative values are ignored.</param>
        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _health = Mathf.Max(0f, _health - amount);
        }

        /// <summary>
        /// Restores health, capped at maximum. Does not resurrect the dead.
        /// </summary>
        /// <param name="amount">Health to restore; negative values are ignored.</param>
        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return;
            }

            _health = Mathf.Min(MaxHealth, _health + amount);
        }
    }
}
