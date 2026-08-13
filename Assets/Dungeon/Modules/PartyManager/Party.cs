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

        /// <summary>Seconds between the healer's casts.</summary>
        public const float HealInterval = 2.2f;

        /// <summary>
        /// Seconds the party spends prising a chest open.
        /// </summary>
        /// <remarks>
        /// This, plus the walk to reach it, is the whole value of a chest to the player: it is time
        /// the party spends not advancing, in a room the player has had a moment to fill. A chest
        /// that opened instantly would be scenery.
        /// </remarks>
        public const float LootSeconds = 3f;

        /// <summary>
        /// How close the leader must be to a chest to start opening it.
        /// </summary>
        /// <remarks>
        /// Generous on purpose, and it has to be. The tank stops as soon as its <i>cell</i> equals
        /// its objective, which on a diagonal approach can leave it two thirds of a cell from the
        /// centre. A tighter reach than that deadlocks the raid outright: the chest stays unlooted,
        /// so it stays the objective, so the tank stands next to it doing nothing until the clock
        /// runs out. That is exactly what a 0.45 reach did.
        /// </remarks>
        public const float LootReach = 0.8f;

        /// <summary>Distance in cells between one member and the next in the marching order.</summary>
        public const float FollowSpacing = 0.62f;

        /// <summary>
        /// Mana each healer brings.
        /// </summary>
        /// <remarks>
        /// Per healer, not per party. A party with two healers genuinely has twice the sustain, which
        /// is what makes THE PILGRIMAGE the best raid on the board and THE UNSHRIVEN the most
        /// dangerous -- a pool that stayed fixed would have made the roster cosmetic.
        /// </remarks>
        public const float ManaPerHealer = 100f;

        private readonly List<Adventurer> _members = new();
        private readonly List<Vector2> _trail = new();
        private readonly DungeonGrid _grid;
        private readonly Vector2Int _bossCell;
        private readonly Vector2Int _entranceCell;
        private readonly HashSet<Vector2Int> _looted = new();
        private IReadOnlyCollection<Vector2Int> _chests = System.Array.Empty<Vector2Int>();
        private readonly float _maxMana;
        private float _mana;
        private float _healCooldown;
        private float _lootProgress;

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

        /// <summary>Mana the healers have left, from 1 down to 0.</summary>
        public float ManaFraction => _maxMana <= 0f ? 0f : Mathf.Clamp01(_mana / _maxMana);

        /// <summary>Which party walked in, for the HUD and the pre-raid warning.</summary>
        public PartyComposition Composition { get; }

        /// <summary>Trap the rogue is working on this tick, or null.</summary>
        public Vector2Int? DisarmingCell { get; private set; }

        /// <summary>Seconds of disarming work done this tick, for the raid to apply.</summary>
        public float DisarmSeconds { get; private set; }

        /// <summary>Chest the party is currently prising open, or null.</summary>
        public Vector2Int? LootingCell { get; private set; }

        /// <summary>How far through opening the current chest, 0 to 1.</summary>
        public float LootFraction => Mathf.Clamp01(_lootProgress / LootSeconds);

        /// <summary>Chests the party has already emptied, so the view can stop drawing them.</summary>
        public IReadOnlyCollection<Vector2Int> LootedChests => _looted;

        /// <summary>Whether a chest has already been emptied.</summary>
        /// <param name="cell">Chest cell to test.</param>
        /// <returns>True when the party has looted it.</returns>
        public bool HasLooted(Vector2Int cell) => _looted.Contains(cell);

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
        /// <param name="composition">
        /// Who walks in. Defaults to the balanced party, so every existing caller and test keeps the
        /// roster it was written against.
        /// </param>
        public Party(DungeonGrid grid, Vector2Int entranceCell, Vector2Int bossCell,
            PartyComposition composition = null)
        {
            _grid = grid;
            _entranceCell = entranceCell;
            _bossCell = bossCell;
            Composition = composition ?? PartyComposition.Opening;

            foreach (AdventurerRole role in Composition.Roles)
            {
                _members.Add(new Adventurer(role, entranceCell));
            }

            _maxMana = Composition.Count(AdventurerRole.Healer) * ManaPerHealer;
            _mana = _maxMana;

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
        /// <param name="threats">
        /// Positions of living mobs the party can see. Bare coordinates rather than mob objects,
        /// because PartyManager and MobManager are siblings that must not reference each other.
        /// </param>
        /// <param name="traps">Trap cells the party would rather walk around.</param>
        /// <param name="chests">
        /// Chest cells. The party detours to any it has not yet emptied in the room it is standing
        /// in, which is the entire point of a chest: it buys the player seconds.
        /// </param>
        public void Tick(
            float deltaTime,
            IReadOnlyList<Vector2> threats,
            IReadOnlyCollection<Vector2Int> traps,
            IReadOnlyCollection<Vector2Int> chests = null)
        {
            _chests = chests ?? System.Array.Empty<Vector2Int>();

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
            ChooseGoal(threats.Count);

            var living = Living.ToList();
            Adventurer leader = living[0];

            // Retreating overrides every individual decision: the whole party runs for the entrance
            // together. This is the player's safety valve, and it must not be second-guessed by a
            // tank that still fancies its chances.
            if (Goal == PartyGoal.Retreating)
            {
                MoveAlongPath(leader, _entranceCell, deltaTime, traps);
                RecordTrail(leader.Position);
                for (int rank = 1; rank < living.Count; rank++)
                {
                    Glide(living[rank], PositionBehind(rank * FollowSpacing), deltaTime);
                }

                return;
            }

            var view = new Perception
            {
                Threats = threats,
                Allies = living,
                Grid = _grid,
                Objective = NextObjective(leader),
                Traps = traps
            };

            // The tank decides first and publishes its target, so the mage focuses the same enemy.
            Glide(leader, AdventurerAI.DesiredPosition(leader, view), deltaTime);
            RecordTrail(leader.Position);

            DisarmingCell = null;
            DisarmSeconds = 0f;

            for (int rank = 1; rank < living.Count; rank++)
            {
                Adventurer member = living[rank];
                var slot = new Perception
                {
                    Threats = threats,
                    Allies = living,
                    Grid = _grid,
                    Objective = view.Objective,
                    Traps = traps,
                    FormationSlot = PositionBehind(rank * FollowSpacing),
                    TankTarget = view.TankTarget
                };

                Vector2 desired = AdventurerAI.DesiredPosition(member, slot);
                Glide(member, desired, deltaTime);

                if (member.Role != AdventurerRole.Ranged || threats.Count > 0)
                {
                    continue;
                }

                // Standing on the plate is the work. Reported outward so the view can show a
                // countdown, because a trap quietly vanishing would be the worst kind of surprise.
                Vector2Int? trap = AdventurerAI.NearestArmedTrap(member.Position, slot);
                if (trap.HasValue &&
                    Vector2.Distance(member.Position, trap.Value) <= AdventurerAI.DisarmReach)
                {
                    DisarmingCell = trap.Value;
                    DisarmSeconds = deltaTime;
                }
            }

            OpenChests(leader, deltaTime);

            if (Goal != PartyGoal.Fighting && Cell == _bossCell)
            {
                Goal = PartyGoal.Escaped;
            }
        }

        /// <summary>
        /// Prises open whatever chest the leader is standing over.
        /// </summary>
        /// <remarks>
        /// Only while advancing. A party in a fight has better things to do, and a party running for
        /// the entrance stopping to loot would look like a bug.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        private void OpenChests(Adventurer leader, float deltaTime)
        {
            Vector2Int? chest = NearestUnlootedChest(leader);
            if (Goal != PartyGoal.Advancing || !chest.HasValue ||
                Vector2.Distance(leader.Position, chest.Value) > LootReach)
            {
                LootingCell = null;
                _lootProgress = 0f;
                return;
            }

            // Restart the timer if they moved to a different chest, so progress cannot be banked on
            // one chest and spent on another.
            if (LootingCell != chest)
            {
                _lootProgress = 0f;
            }

            LootingCell = chest;
            _lootProgress += deltaTime;

            if (_lootProgress >= LootSeconds)
            {
                _looted.Add(chest.Value);
                LootingCell = null;
                _lootProgress = 0f;
            }
        }

        /// <summary>The nearest chest in the leader's room that has not been emptied yet.</summary>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>The chest cell, or null.</returns>
        private Vector2Int? NearestUnlootedChest(Adventurer leader)
        {
            int room = _grid.RoomAt(leader.Cell);
            if (room == DungeonGrid.NoRoom)
            {
                return null;
            }

            Vector2Int? best = null;
            float bestDistance = float.MaxValue;

            foreach (Vector2Int chest in _chests)
            {
                if (_looted.Contains(chest) || _grid.RoomAt(chest) != room)
                {
                    continue;
                }

                float distance = Vector2.Distance(leader.Position, chest);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = chest;
                }
            }

            return best;
        }

        /// <summary>
        /// The next door on the way to the boss room, or the boss room itself.
        /// </summary>
        /// <remarks>
        /// Steering door to door rather than straight at the boss is what makes the tank commit to a
        /// threshold, which is where the player's door-closing actually bites.
        /// </remarks>
        private Vector2Int NextObjective(Adventurer leader)
        {
            // A chest in this room comes first. Greed beats progress, which is what makes a chest
            // worth its price -- the party walks off the shortest route and the clock keeps running.
            Vector2Int? chest = NearestUnlootedChest(leader);
            if (chest.HasValue)
            {
                return chest.Value;
            }

            List<Vector2Int> path = _grid.FindPath(leader.Cell, _bossCell);
            foreach (Vector2Int cell in path)
            {
                if (_grid.KindAt(cell) == CellKind.Doorway)
                {
                    return cell;
                }
            }

            return _bossCell;
        }

        /// <summary>Moves one adventurer toward a point at walking pace.</summary>
        private static void Glide(Adventurer member, Vector2 desired, float deltaTime)
        {
            member.Position = Vector2.MoveTowards(member.Position, desired, WalkSpeed * deltaTime);
        }

        /// <summary>Moves an adventurer one step along a path toward a cell.</summary>
        private void MoveAlongPath(
            Adventurer member, Vector2Int target, float deltaTime,
            IReadOnlyCollection<Vector2Int> traps)
        {
            List<Vector2Int> path = _grid.FindPath(member.Cell, target, traps);
            if (path.Count == 0)
            {
                return;
            }

            Vector2 waypoint = path[0];
            if (path.Count > 1 && Vector2.Distance(member.Position, waypoint) < 0.25f)
            {
                waypoint = path[1];
            }

            Glide(member, waypoint, deltaTime);
        }

        /// <summary>Appends to the breadcrumb trail the rest of the party follows.</summary>
        private void RecordTrail(Vector2 position)
        {
            if (_trail.Count == 0 || Vector2.Distance(_trail[^1], position) > 0.06f)
            {
                _trail.Add(position);
            }

            int keep = Mathf.CeilToInt((_members.Count * FollowSpacing) / 0.06f) + 8;
            if (_trail.Count > keep)
            {
                _trail.RemoveRange(0, _trail.Count - keep);
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

            var tanks = living.Where(m => m.Role == AdventurerRole.Tank).ToList();
            if (tanks.Count > 0)
            {
                // Tanks draw aggro, so they eat the bulk between them. This is what keeps a party
                // alive long enough to be milked; spreading damage evenly would kill the fragile
                // roles fast and end the raid early -- which is exactly what happens to a party that
                // brought no tank at all, and is why THE SKIRMISHERS are so easy to kill by mistake.
                float perTank = amount * 0.6f / tanks.Count;
                foreach (Adventurer tank in tanks)
                {
                    tank.TakeDamage(perTank);
                }

                var others = living.Where(m => m.Role != AdventurerRole.Tank).ToList();
                if (others.Count == 0)
                {
                    return;
                }

                float rest = amount * 0.4f / others.Count;
                foreach (Adventurer member in others)
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

        /// <summary>
        /// Runs the healer, who spends a limited pool keeping the worst-off alive.
        /// </summary>
        /// <remarks>
        /// Discrete casts rather than a continuous trickle, so the healer's mana buys a known number
        /// of heals and the player can watch it run dry. Target choice and the decision to cast at
        /// all live in <see cref="AdventurerAI.ChooseHealTarget"/>: it refuses to cast unless a full
        /// heal would land without overflowing, which is what stops a limited pool being frittered
        /// away topping people up.
        /// </remarks>
        private void HealWounded(float deltaTime)
        {
            // Every living healer casts on its own schedule, so two healers really do heal twice as
            // fast and a party that has lost its last healer stops healing entirely. Counting the
            // living rather than the roster is the point: killing the healer is how a player ruins
            // their own raid, and it has to be felt immediately.
            int healers = Living.Count(m => m.Role == AdventurerRole.Healer);
            if (healers == 0)
            {
                return;
            }

            _healCooldown = Mathf.Max(0f, _healCooldown - (deltaTime * healers));
            if (_healCooldown > 0f)
            {
                return;
            }

            Adventurer target = AdventurerAI.ChooseHealTarget(Living.ToList(), _mana);
            if (target == null)
            {
                return;
            }

            target.Heal(AdventurerAI.HealAmount);
            _mana -= AdventurerAI.HealCost;
            _healCooldown = HealInterval;
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
