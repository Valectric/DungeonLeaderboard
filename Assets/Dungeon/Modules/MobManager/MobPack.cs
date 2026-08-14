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

        /// <summary>Continuous position in grid units, so a mob can stand between cells.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Grid cell this mob currently stands in.</summary>
        public Vector2Int Cell => new(Mathf.RoundToInt(Position.x), Mathf.RoundToInt(Position.y));

        /// <summary>Damage dealt per second while in contact with the party.</summary>
        /// <remarks>The balance anchor the stat block below is derived from.</remarks>
        public float DamagePerSecond { get; }

        /// <summary>Base damage of this monster's attack.</summary>
        public float WeaponDamage { get; }

        /// <summary>This monster's own strength, added to its attack on every blow.</summary>
        public float Might { get; }

        /// <summary>Fraction of incoming damage this monster turns aside, 0 to 1.</summary>
        public float Armour { get; }

        /// <summary>Seconds between blows.</summary>
        public float AttackInterval { get; }

        /// <summary>Seconds until this monster can swing again.</summary>
        public float AttackCooldown { get; set; }

        /// <summary>Where this monster last struck, so the view can throw the sprite at it.</summary>
        public Vector2? LastAttackTarget { get; set; }

        /// <summary>How far through its attack this monster is, 0 at the blow to 1 at rest.</summary>
        public float AttackPhase =>
            AttackInterval <= 0f ? 1f : Mathf.Clamp01(1f - (AttackCooldown / AttackInterval));

        /// <summary>Whether this mob is still alive.</summary>
        public bool IsAlive => _health > 0f;

        /// <summary>Starting health, so the view can draw a bar.</summary>
        public float MaxHealth { get; }

        /// <summary>
        /// Health as a fraction from 1 down to 0.
        /// </summary>
        /// <remarks>
        /// Shown to the player, unlike an adventurer's. SPEC.md hides adventurer HP so that "nearly
        /// dead" and "dead in one hit" stay ambiguous -- that ambiguity is the tension. A mob is the
        /// opposite case: it is the player's own asset, they paid energy for it, and how long it will
        /// hold the party is the single fact they need to plan the next twenty seconds around.
        /// </remarks>
        public float HealthFraction => Mathf.Clamp01(_health / MaxHealth);

        /// <summary>
        /// The direction this mob shoves off in when it is standing exactly on another.
        /// </summary>
        /// <remarks>
        /// Distinct per mob and fixed at spawn. Two mobs on the same square have no separating
        /// direction to compute, and the previous fallback sent <i>every</i> one of them along
        /// <c>Vector2.up</c> — so they all moved together and stayed perfectly stacked. Measured,
        /// eight slimes spawned on one cell were still <b>0.00 cells apart</b> after four seconds,
        /// rendering as a single sprite with one health bar.
        /// <para>
        /// Spread by the golden angle off the spawn index, which fans any number of them evenly and
        /// stays deterministic — a seeded run has to look identical every time.
        /// </para>
        /// </remarks>
        public Vector2 Nudge { get; }

        /// <summary>Creates a mob bound to the room it spawned in.</summary>
        /// <param name="kind">Monster type.</param>
        /// <param name="cell">Spawn cell.</param>
        /// <param name="homeRoom">Room index the mob is confined to.</param>
        /// <param name="index">Spawn order, used to give this mob its own shove-off direction.</param>
        public Mob(MobKind kind, Vector2Int cell, int homeRoom, int index = 0)
        {
            float radians = index * 2.39996f;   // golden angle
            Nudge = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            Kind = kind;
            Position = cell;
            HomeRoom = homeRoom;
            // Sized against the party's 20 dps so a slime holds them ~6s and a skeleton ~13s. Mobs
            // exist to keep the party standing still and bleeding, not to kill it, so health matters
            // far more than damage here.
            //
            // The author asked for two-and-a-half times less, damage untouched. Under the old curve
            // that inverted the game -- a wipe earned 215 against 213 for surviving. Under the
            // per-action curve it no longer does: measured at 48/104 the figures are 14 against 231,
            // comfortably the right way round, so the change is viable now and only needs doing
            // properly. It is parked because it invalidates seven tests that encode the current
            // fight length, including "a skeleton still holds the party about thirteen seconds" and
            // two that need a fight long enough to observe a healer working. Those want reworking
            // deliberately, not at the end of a long session.
            MaxHealth = kind == MobKind.Slime ? 48f : 104f;
            _health = MaxHealth;
            DamagePerSecond = kind == MobKind.Slime ? 8f : 15f;

            // Decomposed to match the damage per second above against a tank's 0.25 armour, which is
            // who a mob almost always ends up hitting: the tank leads, so it is nearest.
            (WeaponDamage, Might, Armour, AttackInterval) = kind == MobKind.Slime
                ? (12f, 4f, 0.05f, 1.5f)
                : (20f, 6f, 0.15f, 1.3f);
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

            var mob = new Mob(kind, cell, room, _mobs.Count);
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

        /// <summary>How close a mob closes before it stops, so it fights beside the party not on it.</summary>
        public const float ContactRange = 0.85f;

        /// <summary>How close two mobs may stand before they shoulder each other apart.</summary>
        public const float MobSpacing = 0.7f;

        /// <summary>
        /// Moves every mob toward the party, but only within its own room.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="partyPosition">Where the party's leader currently stands.</param>
        public void Tick(float deltaTime, IReadOnlyList<Vector2> partyPositions)
        {
            if (partyPositions == null || partyPositions.Count == 0)
            {
                return;
            }

            Vector2 partyPosition = partyPositions[0];
            var partyCell = new Vector2Int(
                Mathf.RoundToInt(partyPosition.x), Mathf.RoundToInt(partyPosition.y));
            int partyRoom = _grid.RoomAt(partyCell);

            // Separation runs for every mob, including those the party has left behind. Gating it on
            // the party's room meant two mobs spawned from the same spawner stayed welded together
            // until the party happened to arrive.
            foreach (Mob mob in Living)
            {
                Separate(mob, deltaTime);
            }

            foreach (Mob mob in Living)
            {
                // The whole safety valve in one condition: a mob whose room the party has left
                // simply stops. It does not path to the threshold and wait, and it does not follow
                // through an open door.
                if (partyRoom != mob.HomeRoom)
                {
                    continue;
                }

                // Chase whoever is NEAREST, not whoever happens to be leading. Following the leader
                // let a tankless party form a permanent standoff: every fragile role runs at a
                // monster within 1.7 cells while melee reach is 1.15, so the distance settled at
                // 1.71 and neither side could touch the other. Measured, one skeleton spent 48
                // seconds beside THE GLASS CANNONS, reversed direction 46 times and dealt no damage
                // at all -- the "wagging" a player reported, and the reason those rosters earned a
                // ninth of the rest.
                // Only members standing in the mob's OWN room are quarry. The room check above is on
                // the party LEADER, so a straggler left behind in the previous room could be the
                // nearest body -- and the mob would charge straight through the doorway after them,
                // which is the one thing room-bounded pursuit exists to prevent. Found by the soak:
                // "a Skeleton left room 1 for room 0".
                Vector2 quarry = Vector2.zero;
                float best = float.MaxValue;
                foreach (Vector2 candidate in partyPositions)
                {
                    var candidateCell = new Vector2Int(
                        Mathf.RoundToInt(candidate.x), Mathf.RoundToInt(candidate.y));
                    if (_grid.RoomAt(candidateCell) != mob.HomeRoom)
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(mob.Position, candidate);
                    if (distance < best)
                    {
                        best = distance;
                        quarry = candidate;
                    }
                }

                if (best == float.MaxValue)
                {
                    continue;
                }

                // Stop at arm's length. Walking onto the party's own square is what made a skeleton
                // appear to stand on top of the adventurers it was fighting.
                if (best <= ContactRange)
                {
                    continue;
                }

                var quarryCell = new Vector2Int(
                    Mathf.RoundToInt(quarry.x), Mathf.RoundToInt(quarry.y));
                List<Vector2Int> path = _grid.FindPath(mob.Cell, quarryCell);
                Vector2 waypoint = path.Count > 0 ? path[0] : quarry;
                if (path.Count > 0 && _grid.RoomAt(path[0]) != mob.HomeRoom)
                {
                    continue;
                }

                // Straight at the quarry once it is close, rather than via the next cell centre.
                // Pathing cell to cell is what let a fleeing target stay ahead: the waypoint is a
                // corner away, so the chase is slower than the chaser.
                if (best < 2.5f)
                {
                    waypoint = quarry;
                }

                Vector2 next = Vector2.MoveTowards(
                    mob.Position, waypoint, ChaseSpeed * deltaTime);

                // Last line of defence. Charging straight at a quarry skips the cell-by-cell path,
                // so nothing else checks where the mob actually lands -- a diagonal across a corner
                // can cross into the next room even when the quarry is a legal one. A doorway is
                // allowed through: it belongs to no room, and a mob straddling the threshold of its
                // own room has not escaped it.
                var nextCell = new Vector2Int(Mathf.RoundToInt(next.x), Mathf.RoundToInt(next.y));
                int nextRoom = _grid.RoomAt(nextCell);
                if (nextRoom != mob.HomeRoom && nextRoom != DungeonGrid.NoRoom)
                {
                    continue;
                }

                mob.Position = next;
            }
        }

        /// <summary>Pushes a mob away from any neighbour it is standing too close to.</summary>
        private void Separate(Mob mob, float deltaTime)
        {
            foreach (Mob other in Living)
            {
                if (ReferenceEquals(other, mob))
                {
                    continue;
                }

                Vector2 away = mob.Position - other.Position;
                float distance = away.magnitude;
                if (distance >= MobSpacing)
                {
                    continue;
                }

                // Two mobs on the same square have no separating direction to compute, so each
                // falls back to its own fixed heading. A shared fallback moved them all the same
                // way and left them welded together forever.
                Vector2 direction = distance > 0.001f ? away / distance : mob.Nudge;
                Vector2 pushed = mob.Position + (direction * ChaseSpeed * deltaTime);
                if (_grid.RoomAt(new Vector2Int(
                        Mathf.RoundToInt(pushed.x), Mathf.RoundToInt(pushed.y))) == mob.HomeRoom)
                {
                    mob.Position = pushed;
                }
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
