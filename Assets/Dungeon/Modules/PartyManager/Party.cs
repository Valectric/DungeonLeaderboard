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

        /// <summary>Distance in cells between one member and the next in the marching order.</summary>
        public const float FollowSpacing = 0.62f;

        /// <summary>
        /// Marching order, front to back.
        /// </summary>
        /// <remarks>
        /// The tank leads because it draws aggro and is built to soak it, and the healer walks last
        /// because it is the party's whole survivability -- and, per SPEC.md, the player's best
        /// customer. A healer that walks into the front rank dies early and takes the raid's earning
        /// potential with it.
        /// </remarks>
        private static readonly AdventurerRole[] MarchOrder =
        {
            AdventurerRole.Tank, AdventurerRole.Ranged, AdventurerRole.Mage, AdventurerRole.Healer
        };

        private readonly List<Adventurer> _members = new();
        private readonly List<Vector2> _trail = new();
        private readonly DungeonGrid _grid;
        private readonly Vector2Int _bossCell;
        private readonly Vector2Int _entranceCell;
        private float _mana = 100f;

        /// <summary>Every member, alive or dead, in spawn order.</summary>
        public IReadOnlyList<Adventurer> Members => _members;

        /// <summary>What the party is currently doing.</summary>
        public PartyGoal Goal { get; private set; } = PartyGoal.Advancing;

        /// <summary>Living members.</summary>
        public IEnumerable<Adventurer> Living => _members.Where(m => m.IsAlive);

        /// <summary>Count of living members.</summary>
        public int LivingCount => _members.Count(m => m.IsAlive);

        /// <summary>Cell the party as a whole occupies, taken from whoever is leading.</summary>
        public Vector2Int Cell => Living.FirstOrDefault()?.Cell ?? _entranceCell;

        /// <summary>Continuous position of the party's leader, for anything chasing it.</summary>
        public Vector2 Position => Living.FirstOrDefault()?.Position ?? _entranceCell;

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

            foreach (AdventurerRole role in MarchOrder)
            {
                _members.Add(new Adventurer(role, entranceCell));
            }

            // Seed the trail running back out of the entrance so the party starts strung out in
            // marching order rather than stacked on one square, and reads as walking in.
            for (int step = 8; step >= 0; step--)
            {
                _trail.Add(new Vector2(entranceCell.x - (step * 0.25f), entranceCell.y));
            }

            PlaceFollowers();
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

        /// <summary>
        /// Glides the leader along its path and drags the rest of the party behind it.
        /// </summary>
        /// <remarks>
        /// Movement is continuous rather than a cell-sized hop every 1/WalkSpeed seconds, which is
        /// what made the party look like it was teleporting between squares.
        /// </remarks>
        private void StepToward(Vector2Int target, float deltaTime)
        {
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
                PlaceFollowers();
                return;
            }

            // Aim at the second waypoint once the first is nearly reached, so the leader cuts the
            // corner smoothly instead of stopping dead on each cell centre.
            Vector2 waypoint = path[0];
            if (path.Count > 1 && Vector2.Distance(leader.Position, waypoint) < 0.25f)
            {
                waypoint = path[1];
            }

            Vector2 step = Vector2.MoveTowards(
                leader.Position, waypoint, WalkSpeed * deltaTime);
            leader.Position = step;

            if (_trail.Count == 0 || Vector2.Distance(_trail[^1], step) > 0.06f)
            {
                _trail.Add(step);
            }

            // The trail only needs to reach back past the last member. Trimming keeps this from
            // growing without bound across a sixty-second raid.
            int keep = Mathf.CeilToInt((_members.Count * FollowSpacing) / 0.06f) + 8;
            if (_trail.Count > keep)
            {
                _trail.RemoveRange(0, _trail.Count - keep);
            }

            PlaceFollowers();
        }

        /// <summary>
        /// Places each member the right distance back along the leader's trail.
        /// </summary>
        /// <remarks>
        /// Following a breadcrumb trail rather than holding a fixed offset means the party rounds
        /// corners in single file and threads doorways one at a time, instead of a rigid block
        /// sliding sideways through walls.
        /// </remarks>
        private void PlaceFollowers()
        {
            var living = Living.ToList();
            for (int rank = 1; rank < living.Count; rank++)
            {
                living[rank].Position = PositionBehind(rank * FollowSpacing);
            }
        }

        /// <summary>Walks back along the trail by a distance and returns the point reached.</summary>
        /// <param name="distance">How far behind the leader to sample, in cells.</param>
        /// <returns>A position on the trail, or its oldest point if the trail is too short.</returns>
        private Vector2 PositionBehind(float distance)
        {
            float remaining = distance;
            for (int i = _trail.Count - 1; i > 0; i--)
            {
                float segment = Vector2.Distance(_trail[i], _trail[i - 1]);
                if (segment >= remaining)
                {
                    return segment <= 0.0001f
                        ? _trail[i - 1]
                        : Vector2.Lerp(_trail[i], _trail[i - 1], remaining / segment);
                }

                remaining -= segment;
            }

            return _trail.Count > 0 ? _trail[0] : _entranceCell;
        }
    }
}
