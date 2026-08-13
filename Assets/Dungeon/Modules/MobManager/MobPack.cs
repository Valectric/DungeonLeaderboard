using System.Collections.Generic;
using System.Linq;
using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.MobManager
{
    /// <summary>The monster types available in the demo.</summary>
    public enum MobKind
    {
        /// <summary>Cheap, weak, spawns in numbers.</summary>
        Slime = 0,

        /// <summary>Costlier, hits harder, survives longer.</summary>
        Skeleton = 1
    }

    /// <summary>
    /// One monster. Dumb on purpose: it has no orders and cannot be recalled.
    /// </summary>
    public sealed class Mob
    {
        private float _health;

        /// <summary>What kind of monster this is.</summary>
        public MobKind Kind { get; }

        /// <summary>Room this mob will never leave.</summary>
        public int HomeRoom { get; }

        /// <summary>Current cell.</summary>
        public Vector2Int Cell { get; set; }

        /// <summary>Damage dealt per second while in contact with the party.</summary>
        public float DamagePerSecond { get; }

        /// <summary>Whether this mob is still alive.</summary>
        public bool IsAlive => _health > 0f;

        /// <summary>Creates a mob bound to the room it spawned in.</summary>
        /// <param name="kind">Monster type.</param>
        /// <param name="cell">Spawn cell.</param>
        /// <param name="homeRoom">Room index the mob is confined to.</param>
        public Mob(MobKind kind, Vector2Int cell, int homeRoom)
        {
            Kind = kind;
            Cell = cell;
            HomeRoom = homeRoom;
            // Sized against the party's 20 dps so a slime holds them ~6s and a skeleton ~13s. Mobs
            // exist to keep the party standing still and bleeding, not to kill it, so health matters
            // far more than damage here.
            _health = kind == MobKind.Slime ? 120f : 260f;
            DamagePerSecond = kind == MobKind.Slime ? 8f : 15f;
        }

        /// <summary>Applies damage, floored at zero.</summary>
        /// <param name="amount">Damage to apply.</param>
        public void TakeDamage(float amount)
        {
            if (amount > 0f)
            {
                _health = Mathf.Max(0f, _health - amount);
            }
        }
    }

    /// <summary>
    /// Every monster in the dungeon, and the rule that keeps them in their rooms.
    /// </summary>
    /// <remarks>
    /// This is a Module.
    /// <para>
    /// <b>Mobs never leave their home room.</b> That single rule is what makes the design's only
    /// safety valve work: the player cannot call monsters off -- there is no such verb, deliberately
    /// -- so the sole way to rescue a losing party is to open a door behind it and let the party
    /// retreat somewhere the mobs cannot follow. Remove the room bound and the game loses its
    /// central regret and becomes an execution machine. This is load-bearing, not polish.
    /// </para>
    /// </remarks>
    public sealed class MobPack
    {
        private readonly List<Mob> _mobs = new();
        private readonly DungeonGrid _grid;
        private float _stepTimer;

        /// <summary>Cells a mob shuffles per second while closing on the party.</summary>
        public const float ChaseSpeed = 1.9f;

        /// <summary>Every mob ever spawned, including the dead.</summary>
        public IReadOnlyList<Mob> Mobs => _mobs;

        /// <summary>Living mobs.</summary>
        public IEnumerable<Mob> Living => _mobs.Where(m => m.IsAlive);

        /// <summary>Creates an empty pack bound to a dungeon.</summary>
        /// <param name="grid">Dungeon the mobs live in.</param>
        public MobPack(DungeonGrid grid)
        {
            _grid = grid;
        }

        /// <summary>
        /// Spawns a mob, binding it to whatever room the spawn cell belongs to.
        /// </summary>
        /// <param name="kind">Monster type to spawn.</param>
        /// <param name="cell">Cell to spawn at.</param>
        /// <returns>The new mob, or null if the cell belongs to no room.</returns>
        public Mob Spawn(MobKind kind, Vector2Int cell)
        {
            int room = _grid.RoomAt(cell);
            if (room == DungeonGrid.NoRoom)
            {
                return null;
            }

            var mob = new Mob(kind, cell, room);
            _mobs.Add(mob);
            return mob;
        }

        /// <summary>Counts living mobs whose home room matches.</summary>
        /// <param name="room">Room index to count in.</param>
        /// <returns>Number of living mobs in that room.</returns>
        public int CountInRoom(int room)
        {
            return Living.Count(m => _grid.RoomAt(m.Cell) == room);
        }

        /// <summary>
        /// Moves every mob one step toward the party, but only within its own room.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="partyCell">Where the party currently stands.</param>
        public void Tick(float deltaTime, Vector2Int partyCell)
        {
            _stepTimer += deltaTime * ChaseSpeed;
            if (_stepTimer < 1f)
            {
                return;
            }

            _stepTimer -= 1f;
            int partyRoom = _grid.RoomAt(partyCell);

            foreach (Mob mob in Living)
            {
                // The whole safety valve in one condition: a mob whose room the party has left
                // simply stops. It does not path to the threshold and wait, and it does not follow
                // through an open door.
                if (partyRoom != mob.HomeRoom)
                {
                    continue;
                }

                List<Vector2Int> path = _grid.FindPath(mob.Cell, partyCell);
                if (path.Count == 0)
                {
                    continue;
                }

                Vector2Int next = path[0];
                if (_grid.RoomAt(next) != mob.HomeRoom)
                {
                    continue;
                }

                mob.Cell = next;
            }
        }

        /// <summary>Total damage per second the mobs sharing a room with the party deal.</summary>
        /// <param name="partyCell">Where the party stands.</param>
        /// <returns>Damage per second.</returns>
        public float DamageOutputAgainst(Vector2Int partyCell)
        {
            int partyRoom = _grid.RoomAt(partyCell);
            if (partyRoom == DungeonGrid.NoRoom)
            {
                return 0f;
            }

            return Living.Where(m => _grid.RoomAt(m.Cell) == partyRoom).Sum(m => m.DamagePerSecond);
        }

        /// <summary>
        /// Applies the party's damage to the mobs it is fighting, focusing one at a time.
        /// </summary>
        /// <param name="amount">Total damage this tick.</param>
        /// <param name="partyCell">Where the party stands.</param>
        public void DistributeDamage(float amount, Vector2Int partyCell)
        {
            int partyRoom = _grid.RoomAt(partyCell);
            Mob target = Living.FirstOrDefault(m => _grid.RoomAt(m.Cell) == partyRoom);
            target?.TakeDamage(amount);
        }
    }
}
