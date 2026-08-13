using System.Collections.Generic;
using System.Linq;
using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.PartyManager
{
    /// <summary>
    /// Everything an adventurer can perceive on a given tick.
    /// </summary>
    /// <remarks>
    /// Threats arrive as bare positions rather than mob objects. That is deliberate: PartyManager
    /// and MobManager are sibling modules that must never reference each other, so the raid above
    /// them flattens the mobs into coordinates before handing them down. The party can react to
    /// what it can see without knowing what a mob <i>is</i>.
    /// </remarks>
    public sealed class Perception
    {
        /// <summary>Positions of living threats the party can currently see.</summary>
        public IReadOnlyList<Vector2> Threats { get; set; } = new List<Vector2>();

        /// <summary>Living party members, including the one deciding.</summary>
        public IReadOnlyList<Adventurer> Allies { get; set; } = new List<Adventurer>();

        /// <summary>The dungeon, for walkability, pathing and line of sight.</summary>
        public DungeonGrid Grid { get; set; }

        /// <summary>Where the party is trying to get to -- the next door, or the boss room.</summary>
        public Vector2Int Objective { get; set; }

        /// <summary>Trap cells the party can see and would rather not walk over.</summary>
        public IReadOnlyCollection<Vector2Int> Traps { get; set; } = new List<Vector2Int>();

        /// <summary>Formation slot for this member, used whenever it has nothing better to do.</summary>
        public Vector2 FormationSlot { get; set; }

        /// <summary>What the tank has decided to attack, so the mage can focus the same enemy.</summary>
        public Vector2? TankTarget { get; set; }
    }

    /// <summary>
    /// Per-role behaviour: what each adventurer wants to do this tick.
    /// </summary>
    /// <remarks>
    /// Rudimentary by design. Each role answers one question -- where do I want to stand? -- and the
    /// party moves them there. Roles that have nothing to fight fall back to their formation slot,
    /// which keeps the group travelling as a column rather than four units wandering off.
    /// </remarks>
    public static class AdventurerAI
    {
        /// <summary>How close a healer lets an enemy get before it runs.</summary>
        public const float HealerFleeRange = 1f;

        /// <summary>Distance the ranged attacker tries to keep from its target.</summary>
        public const float RangedRange = 3f;

        /// <summary>Distance the mage tries to keep from the tank's target.</summary>
        public const float MageRange = 2.4f;

        /// <summary>How close the tank closes before it stops and swings.</summary>
        public const float TankReach = 0.85f;

        /// <summary>How far the rogue will detour to defuse a trap.</summary>
        public const float TrapDetourRange = 6f;

        /// <summary>How close the rogue must be to work on a trap.</summary>
        public const float DisarmReach = 0.6f;

        /// <summary>Health restored by one heal.</summary>
        public const float HealAmount = 45f;

        /// <summary>Mana one heal costs.</summary>
        public const float HealCost = 26f;

        /// <summary>
        /// Chooses where an adventurer wants to stand.
        /// </summary>
        /// <param name="self">The adventurer deciding.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>A desired position in grid units.</returns>
        public static Vector2 DesiredPosition(Adventurer self, Perception view)
        {
            return self.Role switch
            {
                AdventurerRole.Tank => TankGoal(self, view),
                AdventurerRole.Mage => MageGoal(self, view),
                AdventurerRole.Ranged => RangedGoal(self, view),
                _ => HealerGoal(self, view)
            };
        }

        /// <summary>
        /// The tank leads: it charges the nearest enemy it can actually see, and otherwise walks the
        /// party toward the next door, stepping around traps on the way.
        /// </summary>
        private static Vector2 TankGoal(Adventurer self, Perception view)
        {
            Vector2? target = NearestVisible(self.Position, view);
            view.TankTarget = target;

            if (target.HasValue)
            {
                return StandOff(self.Position, target.Value, TankReach);
            }

            List<Vector2Int> path = view.Grid.FindPath(self.Cell, view.Objective, view.Traps);
            if (path.Count == 0)
            {
                return self.Position;
            }

            // Aim a couple of cells ahead so the tank keeps moving smoothly instead of stopping on
            // every cell centre.
            Vector2Int waypoint = path[Mathf.Min(1, path.Count - 1)];
            return waypoint;
        }

        /// <summary>The mage focuses whatever the tank is fighting, from a comfortable distance.</summary>
        private static Vector2 MageGoal(Adventurer self, Perception view)
        {
            Vector2? target = view.TankTarget ?? NearestVisible(self.Position, view);
            return target.HasValue
                ? StandOff(self.Position, target.Value, MageRange)
                : view.FormationSlot;
        }

        /// <summary>
        /// The ranged attacker shoots the closest enemy, and defuses traps when nothing is shooting
        /// back.
        /// </summary>
        /// <remarks>
        /// Fighting always wins over disarming -- a rogue crouched over a pressure plate while a
        /// skeleton swings at the tank reads as broken, however sensible it looks in isolation.
        /// </remarks>
        private static Vector2 RangedGoal(Adventurer self, Perception view)
        {
            Vector2? target = NearestVisible(self.Position, view);
            if (target.HasValue)
            {
                return StandOff(self.Position, target.Value, RangedRange);
            }

            Vector2Int? trap = NearestArmedTrap(self.Position, view);
            return trap.HasValue ? trap.Value : view.FormationSlot;
        }

        /// <summary>
        /// The armed trap this adventurer would walk to, if any is close enough to be worth it.
        /// </summary>
        /// <param name="from">Where the adventurer is standing.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>The trap cell to head for, or null.</returns>
        public static Vector2Int? NearestArmedTrap(Vector2 from, Perception view)
        {
            Vector2Int? best = null;
            float bestDistance = TrapDetourRange;

            foreach (Vector2Int trap in view.Traps)
            {
                float distance = Vector2.Distance(from, trap);
                if (distance >= bestDistance || !view.Grid.HasLineOfSight(from, trap))
                {
                    continue;
                }

                bestDistance = distance;
                best = trap;
            }

            return best;
        }

        /// <summary>
        /// The healer keeps out of reach and stays with the group.
        /// </summary>
        /// <remarks>
        /// It is the party's whole survivability, so anything within a cell sends it backwards --
        /// away from the threat, but biased toward the rest of the party so it does not flee alone
        /// into an empty room.
        /// </remarks>
        private static Vector2 HealerGoal(Adventurer self, Perception view)
        {
            Vector2? nearest = Nearest(self.Position, view.Threats);
            if (!nearest.HasValue ||
                Vector2.Distance(self.Position, nearest.Value) > HealerFleeRange)
            {
                return view.FormationSlot;
            }

            Vector2 away = (self.Position - nearest.Value).normalized;
            Vector2 toward = (view.FormationSlot - self.Position).normalized;
            Vector2 escape = self.Position + ((away * 1.6f) + (toward * 0.6f));

            var cell = new Vector2Int(Mathf.RoundToInt(escape.x), Mathf.RoundToInt(escape.y));
            return view.Grid.IsWalkable(cell) ? escape : view.FormationSlot;
        }

        /// <summary>
        /// Picks who the healer should heal, and whether healing is worth doing at all.
        /// </summary>
        /// <remarks>
        /// Two rules. It will not cast unless the target is missing at least a full heal's worth of
        /// health, so none of a limited mana pool is wasted topping someone up; and among those it
        /// could usefully heal it takes the highest priority, weighted so the tank -- whose survival
        /// keeps everyone else alive -- outranks a squishier ally at the same fraction.
        /// </remarks>
        /// <param name="allies">Living party members.</param>
        /// <param name="mana">Mana currently available.</param>
        /// <returns>Who to heal, or null when no cast is worthwhile.</returns>
        public static Adventurer ChooseHealTarget(IReadOnlyList<Adventurer> allies, float mana)
        {
            if (mana < HealCost)
            {
                return null;
            }

            Adventurer best = null;
            float bestPriority = 0f;

            foreach (Adventurer ally in allies)
            {
                float missing = ally.MaxHealth - (ally.HealthFraction * ally.MaxHealth);
                if (missing < HealAmount)
                {
                    // A full heal would overflow, so the cast would waste mana. Wait.
                    continue;
                }

                float priority = (1f - ally.HealthFraction) * RoleWeight(ally.Role);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    best = ally;
                }
            }

            return best;
        }

        /// <summary>How much the healer values keeping each role alive.</summary>
        private static float RoleWeight(AdventurerRole role) => role switch
        {
            AdventurerRole.Tank => 1.35f,
            AdventurerRole.Healer => 1.1f,
            _ => 1f
        };

        /// <summary>Nearest threat with a clear line to it, or null.</summary>
        private static Vector2? NearestVisible(Vector2 from, Perception view)
        {
            Vector2? best = null;
            float bestDistance = float.MaxValue;

            foreach (Vector2 threat in view.Threats)
            {
                float distance = Vector2.Distance(from, threat);
                if (distance >= bestDistance || !view.Grid.HasLineOfSight(from, threat))
                {
                    continue;
                }

                bestDistance = distance;
                best = threat;
            }

            return best;
        }

        /// <summary>Nearest of a set of points, ignoring visibility.</summary>
        private static Vector2? Nearest(Vector2 from, IReadOnlyList<Vector2> points)
        {
            return points.Count == 0
                ? null
                : points.OrderBy(p => Vector2.Distance(from, p)).First();
        }

        /// <summary>A point the given distance from a target, on the side the mover is already on.</summary>
        private static Vector2 StandOff(Vector2 self, Vector2 target, float range)
        {
            Vector2 offset = self - target;
            if (offset.sqrMagnitude < 0.0001f)
            {
                offset = Vector2.left;
            }

            return target + (offset.normalized * range);
        }
    }
}
