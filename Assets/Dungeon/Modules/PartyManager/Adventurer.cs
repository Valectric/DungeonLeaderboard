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
        /// <remarks>
        /// The balance anchor the stat block below is derived from, kept so the figure the game was
        /// tuned around is still stated in one place and still assertable.
        /// </remarks>
        public float DamagePerSecond { get; }

        /// <summary>Base damage of this role's weapon.</summary>
        public float WeaponDamage { get; }

        /// <summary>This adventurer's own strength, added to the weapon on every blow.</summary>
        public float Might { get; }

        /// <summary>Fraction of incoming damage this adventurer's armour turns aside, 0 to 1.</summary>
        public float Armour { get; }

        /// <summary>Seconds between blows.</summary>
        /// <remarks>
        /// The mage swings slowest and hits hardest, which is what SPEC.md means by burst -- and it
        /// is what makes a mage's damage number the big one on screen.
        /// </remarks>
        public float AttackInterval { get; }

        /// <summary>Seconds until this adventurer can swing again.</summary>
        public float AttackCooldown { get; set; }

        /// <summary>
        /// The mage's own spell pool, spent on bolts and on blinking, and slowly refilled.
        /// </summary>
        /// <remarks>
        /// Separate from the healer's pool on purpose. They are different resources belonging to
        /// different people, and the interesting moment -- a mage out of mana with a skeleton on it,
        /// unable to blink away -- only exists if the mage can run dry on its own.
        /// </remarks>
        public float Mana { get; private set; }

        /// <summary>The mage's full spell pool. Zero for everyone else.</summary>
        public float MaxMana { get; }

        /// <summary>Mana as a fraction from 1 down to 0, for the bar.</summary>
        public float ManaFraction => MaxMana <= 0f ? 0f : Mathf.Clamp01(Mana / MaxMana);

        /// <summary>Mana restored per second.</summary>
        /// <remarks>
        /// Sized so a bolt is affordable roughly as often as the mage can cast one anyway, and a
        /// blink costs a noticeable stretch of shooting. Spending is meant to be a real trade, not a
        /// formality.
        /// </remarks>
        public const float ManaRegenPerSecond = 6f;

        /// <summary>Mana one offensive bolt costs.</summary>
        public const float BoltManaCost = 8f;

        /// <summary>Mana one blink costs.</summary>
        public const float BlinkManaCost = 26f;

        /// <summary>Whether this adventurer can pay for a spell.</summary>
        /// <param name="cost">Mana required.</param>
        /// <returns>True when the pool covers it.</returns>
        public bool CanCast(float cost) => Mana >= cost;

        /// <summary>Spends mana, floored at zero.</summary>
        /// <param name="cost">Mana to spend.</param>
        /// <returns>True when the cast was paid for.</returns>
        public bool SpendMana(float cost)
        {
            if (!CanCast(cost))
            {
                return false;
            }

            Mana = Mathf.Max(0f, Mana - cost);
            return true;
        }

        /// <summary>Refills the pool over time, capped at maximum.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void RegenerateMana(float deltaTime)
        {
            if (MaxMana > 0f)
            {
                Mana = Mathf.Min(MaxMana, Mana + (ManaRegenPerSecond * deltaTime));
            }
        }

        /// <summary>
        /// Continuous position in grid units, so an adventurer can stand between cells.
        /// </summary>
        /// <remarks>
        /// The party used to store a bare cell and jump a whole cell at a time, which read as
        /// teleporting rather than walking and forced all four members onto one square. Position is
        /// the authority now; <see cref="Cell"/> is derived from it for the room and pathing lookups
        /// that genuinely want a discrete answer.
        /// </remarks>
        public Vector2 Position { get; set; }

        /// <summary>Grid cell this adventurer currently stands in.</summary>
        public Vector2Int Cell => new(Mathf.RoundToInt(Position.x), Mathf.RoundToInt(Position.y));

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
            Position = cell;
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

            // The stat block is a decomposition of the figure above, not a replacement for it. Each
            // (weapon + might) is chosen so the expected output against a skeleton's 0.15 armour
            // comes back to the same damage per second the game was balanced around; drift here
            // changes fight length, and fight length is the rate the whole design rests on.
            (WeaponDamage, Might, Armour, AttackInterval) = role switch
            {
                AdventurerRole.Tank => (3f, 1.2f, 0.25f, 1.2f),
                AdventurerRole.Healer => (2f, 1f, 0.10f, 2.5f),
                AdventurerRole.Ranged => (6f, 2.2f, 0.05f, 1.0f),
                AdventurerRole.Mage => (15f, 4f, 0.05f, 1.8f),
                _ => (4f, 1f, 0.1f, 1.5f)
            };

            // Staggered so a fresh party does not swing in one synchronised volley, which reads as a
            // single big hit rather than four people fighting.
            AttackCooldown = (int)role * 0.27f;

            MaxMana = role == AdventurerRole.Mage ? 100f : 0f;
            Mana = MaxMana;
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
