using System.Collections.Generic;
using System.Linq;
using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.PartyManager
{
    /// <summary>What the party is currently trying to do.</summary>
    public enum PartyGoal
    {
        /// <summary>Walking toward the boss room.</summary>
        Advancing = 0,

        /// <summary>Standing and fighting whatever is in this room.</summary>
        Fighting = 1,

        /// <summary>Falling back toward the entrance to heal.</summary>
        Retreating = 2,

        /// <summary>Reached the boss room and left. The earning window is over.</summary>
        Escaped = 3,

        /// <summary>Everyone is dead. The earning window is over.</summary>
        Wiped = 4
    }

    /// <summary>
    /// A party of four adventurers and the AI that drives them.
    /// </summary>
    /// <remarks>
    /// This is a Module. The party pathfinds toward the boss room, stops to fight what it meets,
    /// and falls back to heal when it is losing.
    /// <para>
    /// The party has no knowledge of mobs beyond a count of what is threatening it, and it never
    /// asks a mob to do anything. Combat is resolved a level up, by the raid, so that this module
    /// and the mob module stay siblings that cannot reach each other -- the One-Flow rule.
    /// </para>
    /// </remarks>
    public sealed class Party
    {
        /// <summary>Health fraction below which the party breaks off and runs.</summary>
        public const float RetreatThreshold = 0.28f;

        /// <summary>Health fraction at which a retreating party turns around and pushes on again.</summary>
        public const float RecoverThreshold = 0.62f;

        /// <summary>
        /// Cells walked per second while advancing.
        /// </summary>
        /// <remarks>
        /// This is a rate, and it sets the pace of the entire game. At the first pass it was 2.4,
        /// which walked the party across the whole sixteen-cell corridor in <b>under seven seconds</b>
        /// of a sixty-second raid: the run ended before the player could click anything and harvested
        /// nothing. Every test passed, because they asserted that the party escapes and never asked
        /// how quickly.
        /// <para>
        /// At 0.6 an unopposed crossing takes about twenty-seven seconds — long enough to read the
        /// board and act, short enough that doing nothing still throws away half the earning window.
        /// Guarded by <c>UnopposedParty_TakesMostOfTheClockToCross</c>.
        /// </para>
        /// </remarks>
        public const float WalkSpeed = 0.6f;

        /// <summary>Healing per second the healer restores while it has mana.</summary>
        public const float HealPerSecond = 14f;

        private readonly List<Adventurer> _members = new();
        private readonly DungeonGrid _grid;
        private readonly Vector2Int _bossCell;
        private readonly Vector2Int _entranceCell;
        private float _stepTimer;
        private float _mana = 100f;

        /// <summary>Every member, alive or dead, in spawn order.</summary>
        public IReadOnlyList<Adventurer> Members => _members;

        /// <summary>What the party is currently doing.</summary>
        public PartyGoal Goal { get; private set; } = PartyGoal.Advancing;

        /// <summary>Living members.</summary>
        public IEnumerable<Adventurer> Living => _members.Where(m => m.IsAlive);

        /// <summary>Count of living members.</summary>
        public int LivingCount => _members.Count(m => m.IsAlive);

        /// <summary>Cell the party as a whole occupies, taken from its first living member.</summary>
        public Vector2Int Cell => Living.FirstOrDefault()?.Cell ?? _entranceCell;

        /// <summary>Whether the healer still has mana to spend.</summary>
        public bool HasMana => _mana > 0f;

        /// <summary>
        /// Aggregate health of the living party, 1 down to 0.
        /// </summary>
        /// <remarks>
        /// Averaged over the <i>living</i> only. Counting corpses as zero would make a half-dead
        /// party look desperately wounded and pay out accordingly, which would reward killing --
        /// the exact inversion the design forbids.
        /// </remarks>
        public float HealthFraction
        {
            get
            {
                var living = Living.ToList();
                return living.Count == 0 ? 0f : living.Average(m => m.HealthFraction);
            }
        }

        /// <summary>Creates a party of four at the entrance.</summary>
        /// <param name="grid">Dungeon to walk.</param>
        /// <param name="entranceCell">Where the party enters.</param>
        /// <param name="bossCell">Cell that ends the raid when reached.</param>
        public Party(DungeonGrid grid, Vector2Int entranceCell, Vector2Int bossCell)
        {
            _grid = grid;
            _entranceCell = entranceCell;
            _bossCell = bossCell;
            foreach (AdventurerRole role in new[]
                     {
                         AdventurerRole.Tank, AdventurerRole.Healer,
                         AdventurerRole.Ranged, AdventurerRole.Mage
                     })
            {
                _members.Add(new Adventurer(role, entranceCell));
            }
        }

        /// <summary>
        /// Advances the party by one step of simulation.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="threatsInRoom">How many mobs are alive in the party's current room.</param>
        public void Tick(float deltaTime, int threatsInRoom)
        {
            if (Goal is PartyGoal.Escaped or PartyGoal.Wiped)
            {
                return;
            }

            if (LivingCount == 0)
            {
                Goal = PartyGoal.Wiped;
                return;
            }

            HealWounded(deltaTime);
            ChooseGoal(threatsInRoom);

            if (Goal == PartyGoal.Fighting)
            {
                return;
            }

            Vector2Int target = Goal == PartyGoal.Retreating ? _entranceCell : _bossCell;
            StepToward(target, deltaTime);

            if (Goal == PartyGoal.Advancing && Cell == _bossCell)
            {
                Goal = PartyGoal.Escaped;
            }
        }

        /// <summary>
        /// Spreads incoming damage across the party, with the tank soaking most of it.
        /// </summary>
        /// <param name="amount">Total damage this tick.</param>
        public void DistributeDamage(float amount)
        {
            var living = Living.ToList();
            if (living.Count == 0 || amount <= 0f)
            {
                return;
            }

            Adventurer tank = living.FirstOrDefault(m => m.Role == AdventurerRole.Tank);
            if (tank != null)
            {
                // The tank draws aggro, so it eats the bulk. This is what keeps a party alive long
                // enough to be milked; spreading damage evenly would kill the fragile roles fast and
                // end the raid early.
                tank.TakeDamage(amount * 0.6f);
                float rest = amount * 0.4f / Mathf.Max(1, living.Count - 1);
                foreach (Adventurer member in living.Where(m => m != tank))
                {
                    member.TakeDamage(rest);
                }

                return;
            }

            float share = amount / living.Count;
            foreach (Adventurer member in living)
            {
                member.TakeDamage(share);
            }
        }

        /// <summary>Total damage per second the living party deals.</summary>
        /// <returns>Damage per second.</returns>
        public float DamageOutput()
        {
            return Living.Sum(m => m.DamagePerSecond);
        }

        /// <summary>Picks a goal from the party's health and what is in the room with it.</summary>
        private void ChooseGoal(int threatsInRoom)
        {
            float health = HealthFraction;

            if (Goal == PartyGoal.Retreating)
            {
                // Keep running until properly patched up, otherwise the party yo-yos on the
                // threshold and never actually escapes the fight.
                if (health >= RecoverThreshold)
                {
                    Goal = PartyGoal.Advancing;
                }

                return;
            }

            if (health < RetreatThreshold)
            {
                Goal = PartyGoal.Retreating;
                return;
            }

            Goal = threatsInRoom > 0 ? PartyGoal.Fighting : PartyGoal.Advancing;
        }

        /// <summary>Runs the healer, who spends a limited pool keeping the worst-off alive.</summary>
        private void HealWounded(float deltaTime)
        {
            Adventurer healer = Living.FirstOrDefault(m => m.Role == AdventurerRole.Healer);
            if (healer == null || _mana <= 0f)
            {
                return;
            }

            Adventurer worst = Living.OrderBy(m => m.HealthFraction).FirstOrDefault();
            if (worst == null || worst.HealthFraction >= 0.999f)
            {
                return;
            }

            float healed = HealPerSecond * deltaTime;
            worst.Heal(healed);
            _mana = Mathf.Max(0f, _mana - (healed * 0.5f));
        }

        /// <summary>Walks every living member one step along the path toward a target cell.</summary>
        private void StepToward(Vector2Int target, float deltaTime)
        {
            _stepTimer += deltaTime * WalkSpeed;
            if (_stepTimer < 1f)
            {
                return;
            }

            _stepTimer -= 1f;
            Adventurer leader = Living.FirstOrDefault();
            if (leader == null)
            {
                return;
            }

            List<Vector2Int> path = _grid.FindPath(leader.Cell, target);
            if (path.Count == 0)
            {
                // No route: the player has shut a door. Standing still is the correct behaviour and
                // is precisely the stall the whole game is built around.
                return;
            }

            Vector2Int next = path[0];
            foreach (Adventurer member in Living)
            {
                member.Cell = next;
            }
        }
    }
}
