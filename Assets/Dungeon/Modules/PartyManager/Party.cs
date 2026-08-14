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
        /// Seconds a party stays in combat after the last threat leaves its room.
        /// </summary>
        /// <remarks>
        /// Engagement decides the energy rate, and without this it is a knife edge: a mob shuffling
        /// across a room threshold, or a moment's gap between one dying and the next arriving, flips
        /// the party out of combat for a frame and drops the rate from about 4/s to 0.05/s and back.
        /// The player sees the game's most important number snapping through its whole range.
        /// <para>
        /// It is also honest about what is happening -- a party that has just killed something is
        /// still braced, weapons out, and not walking on.
        /// </para>
        /// </remarks>
        public const float CombatGrace = 1.4f;

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
        /// At 0.6 an unopposed crossing took about twenty-seven seconds. Raised half again to 0.9 at
        /// the author's direction: the game is called CHARGE! and the party was strolling. A crossing
        /// is now about eighteen seconds, which leaves less of the clock to spend and makes the
        /// player's opening move matter more.
        /// Guarded by <c>UnopposedParty_TakesMostOfTheClockToCross</c>.
        /// </para>
        /// </remarks>
        public const float WalkSpeed = 0.9f;

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
        private readonly IReadOnlyList<Vector2Int> _roomCentres;
        private readonly Vector2Int _entranceCell;
        private readonly HashSet<Vector2Int> _looted = new();
        private IReadOnlyCollection<Vector2Int> _chests = System.Array.Empty<Vector2Int>();
        private float _healCooldown;
        private float _lootProgress;
        private float _combatGraceLeft;

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
        public bool HasMana => ManaFraction > 0f;

        /// <summary>
        /// Mana the living healers have left between them, from 1 down to 0.
        /// </summary>
        /// <remarks>
        /// Derived from the healers themselves rather than from a party-level pool. Each caster owns
        /// its mana now, so a total kept alongside them would be a second copy of the truth and would
        /// drift the first time one of them died.
        /// </remarks>
        public float ManaFraction
        {
            get
            {
                float have = 0f;
                float most = 0f;
                foreach (Adventurer member in Living)
                {
                    if (member.Role != AdventurerRole.Healer)
                    {
                        continue;
                    }

                    have += member.Mana;
                    most += member.MaxMana;
                }

                return most <= 0f ? 0f : Mathf.Clamp01(have / most);
            }
        }

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

        /// <summary>How many chests the party has opened, so the raid can pay for a new one.</summary>
        public int LootedCount => _looted.Count;

        /// <summary>
        /// The chest opened this tick, if one was, so the view can throw sparks off it.
        /// </summary>
        /// <remarks>
        /// Cleared at the start of every tick, so it names a moment rather than a state. The raid
        /// already notices the count changing to start the team bonus; this says <i>where</i>, which
        /// a count cannot.
        /// </remarks>
        public Vector2Int? JustLooted { get; private set; }

        /// <summary>
        /// Aggregate health of the living party, 1 down to 0.
        /// </summary>
        /// <remarks>
        /// Over the <i>living</i> only. Counting corpses as zero would make a half-dead party look
        /// desperately wounded and pay out accordingly, which would reward killing -- the exact
        /// inversion the design forbids.
        /// <para>
        /// <b>Pooled health, not the mean of fractions.</b> The mean treated a wounded tank exactly
        /// like a wounded mage, despite the tank carrying 220 of the party's 500 hit points -- so
        /// once damage began landing only on whoever was in melee reach, a tank at death's door
        /// beside three untouched allies read as a comfortable 77%, and the wound curve, which is a
        /// fifth power, barely stirred. Measured across every roster, the rate never once passed
        /// 4.1/s in a game whose curve is built to reach 32.
        /// </para>
        /// </remarks>
        public float HealthFraction
        {
            get
            {
                float have = 0f;
                float most = 0f;

                foreach (Adventurer member in Living)
                {
                    have += member.HealthFraction * member.MaxHealth;
                    most += member.MaxHealth;
                }

                return most <= 0f ? 0f : Mathf.Clamp01(have / most);
            }
        }

        /// <summary>
        /// How close the party's worst-off living member is to death, 1 down to 0.
        /// </summary>
        /// <remarks>
        /// <b>This is what the energy curve reads</b>, and it is deliberately the single most wounded
        /// survivor rather than any kind of average. CLAUDE.md states the intent in one line: "most
        /// of the money is in the last sliver of a health bar" -- one bar, not the mean of four.
        /// <para>
        /// Measured, no aggregate could work. The tank carries 220 of the party's 500 hit points and
        /// soaks nearly everything, so it reaches death's door while its allies are untouched: the
        /// mean read 77%, pooled health read 63%, and a fifth-power curve ignores both. The rate
        /// never passed <b>4.1/s in a game built to reach 32</b>, and a wipe out-earned every raid
        /// where the party lived -- the exact inversion SPEC.md forbids.
        /// </para>
        /// <para>
        /// Reading the worst survivor also punishes killing precisely as the design wants. Let the
        /// nearly-dead tank die and the reading leaps back up to whoever is next worst, usually
        /// somebody healthy, and the rate collapses. The player has to hold one bar on the edge
        /// without tipping it over, which is exactly the tension the spec asks for.
        /// </para>
        /// </remarks>
        public float WoundFraction
        {
            get
            {
                float worst = 1f;
                bool any = false;

                foreach (Adventurer member in Living)
                {
                    worst = Mathf.Min(worst, member.HealthFraction);
                    any = true;
                }

                return any ? worst : 0f;
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
        /// <param name="roomCentres">
        /// Centre of every room, so the party explores rather than walking a line to a fixed cell.
        /// Optional: without it the party heads for the boss cell, which is what every test written
        /// before exploration existed expects.
        /// </param>
        public Party(DungeonGrid grid, Vector2Int entranceCell, Vector2Int bossCell,
            PartyComposition composition = null, IReadOnlyList<Vector2Int> roomCentres = null)
        {
            _grid = grid;
            _entranceCell = entranceCell;
            _bossCell = bossCell;
            _roomCentres = roomCentres ?? new List<Vector2Int> { bossCell };
            Composition = composition ?? PartyComposition.Opening;

            foreach (AdventurerRole role in Composition.Roles)
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
            JustLooted = null;

            if (Goal is PartyGoal.Escaped or PartyGoal.Wiped)
            {
                return;
            }

            if (LivingCount == 0)
            {
                Goal = PartyGoal.Wiped;
                return;
            }

            foreach (Adventurer member in Living)
            {
                member.RegenerateMana(deltaTime);
            }

            HealWounded(deltaTime);
            ChooseGoal(threats.Count, deltaTime);

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

                AssignActions(threats);
                return;
            }

            var view = new Perception
            {
                Threats = threats,
                Allies = living,
                Grid = _grid,
                Objective = NextObjective(leader),
                Traps = traps,
                IsLeader = true
            };

            // The tank decides first and publishes its target, so the mage focuses the same enemy.
            float leaderSpeed = AdventurerAI.SpeedMultiplier(leader, view);
            leader.IsPanicking = leaderSpeed > 1f;
            Glide(leader, AdventurerAI.DesiredPosition(leader, view), deltaTime, leaderSpeed);
            RecordTrail(leader.Position);

            DisarmingCell = null;
            DisarmSeconds = 0f;
            BlinkedFrom = null;
            BlinkedTo = null;

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

                if (TryBlink(member, slot))
                {
                    continue;
                }

                Vector2 desired = AdventurerAI.DesiredPosition(member, slot);
                float speed = AdventurerAI.SpeedMultiplier(member, slot);
                member.IsPanicking = speed > 1f;
                Glide(member, desired, deltaTime, speed);

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

            RecordVisit(leader);
            ForceDoors(leader, deltaTime, threats.Count);
            OpenChests(leader, deltaTime);

            // Last, because it reads the door and loot state the two calls above have just settled.
            AssignActions(threats);

            // The raid ends when they leave, not when they touch one particular room. A party that
            // has seen everything turns round and walks out the way it came in. Dungeons built
            // without room centres keep the old boss-cell ending, which is every test written before
            // exploration existed.
            bool left = _roomCentres.Count > 1
                ? HasExploredEverything && Cell == _entranceCell
                : Cell == _bossCell;

            if (Goal != PartyGoal.Fighting && left)
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
                JustLooted = chest.Value;
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

            // A shut door blocks pathfinding entirely, so the route to the boss room comes back
            // empty and the party would mill about with nowhere to go. Head for the door instead:
            // it is the obstacle, and they have two ways of dealing with it.
            // Walk to the square in front of the door, not to the door itself. A shut door is not
            // walkable, so pathing to its own cell returns no route at all and the party simply
            // stands where it is -- which is exactly what happened: nobody ever reached a door to
            // work on it, and every roster made precisely zero progress.
            Door blocking = BlockingDoor(leader);
            if (blocking != null)
            {
                return ApproachCell(blocking, leader);
            }

            // Explore rather than walk a line to a fixed boss room: head for the nearest room not
            // yet seen, and once they have all been seen, leave by the way in. That is what makes
            // the player's placements steer anything -- a party choosing where to go can be tempted
            // by a chest or held up by a monster; a party walking to one fixed cell cannot.
            Vector2Int destination = NearestUnvisitedRoomCentre(leader) ?? _entranceCell;

            List<Vector2Int> path = _grid.FindPath(leader.Cell, destination);
            foreach (Vector2Int cell in path)
            {
                if (_grid.KindAt(cell) == CellKind.Doorway)
                {
                    return cell;
                }
            }

            return destination;
        }

        /// <summary>
        /// The shut door standing between the party and the boss room, if any.
        /// </summary>
        /// <remarks>
        /// Found by asking which door the party would path through if every door were open, then
        /// checking whether that one is actually shut. Pathfinding cannot answer this directly --
        /// a closed door is simply not walkable, so the route it would have been on does not exist.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>The door to deal with, or null when the way is clear.</returns>
        private Door BlockingDoor(Adventurer leader)
        {
            if (_grid.FindPath(leader.Cell, _bossCell).Count > 0)
            {
                return null;
            }

            int room = _grid.RoomAt(leader.Cell);
            Door nearest = null;
            float best = float.MaxValue;

            foreach (Door door in _grid.Doors)
            {
                // Only a door on this room's threshold, and only one that still bars the way.
                if (door.IsOpen || (door.RoomA != room && door.RoomB != room))
                {
                    continue;
                }

                float distance = Vector2.Distance(leader.Position, door.Cell);
                if (distance < best)
                {
                    best = distance;
                    nearest = door;
                }
            }

            // Nothing shut on this room's threshold, yet the boss room is still unreachable: the
            // party has already opened its own way out and the next door along is somebody else's
            // threshold. Without this the objective fell through to a boss cell no path could reach,
            // MoveAlongPath had nowhere to go, and the party STOOD STILL FOR THE REST OF THE RAID.
            //
            // Measured with every door shut: all six rosters forced the first door at six or seven
            // seconds and then sat at cell (5,3) in room zero for the remaining fifty-three,
            // earning the idle floor. Two tests had encoded that as correct -- one asserting the
            // party is still in the first room after twenty seconds, one expecting the clock to run
            // out -- so the freeze was protected rather than caught.
            return nearest ?? NearestReachableShutDoor(leader);
        }

        /// <summary>
        /// The nearest shut door the party can actually walk to, wherever it is.
        /// </summary>
        /// <remarks>
        /// The fallback for a party that has opened its own room and now needs to cross another to
        /// reach the next obstacle. Reachability is the whole point: a door behind two more shut
        /// doors is no use as an objective, and pathing to it would strand the party exactly as
        /// before.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>A shut door with a walkable route to it, or null.</returns>
        private Door NearestReachableShutDoor(Adventurer leader)
        {
            Door nearest = null;
            int shortest = int.MaxValue;

            foreach (Door door in _grid.Doors)
            {
                if (door.IsOpen)
                {
                    continue;
                }

                Vector2Int approach = ApproachCell(door, leader);
                if (approach == leader.Cell)
                {
                    return door;
                }

                List<Vector2Int> route = _grid.FindPath(leader.Cell, approach);
                if (route.Count > 0 && route.Count < shortest)
                {
                    shortest = route.Count;
                    nearest = door;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The walkable square beside a door, on the party's side of it.
        /// </summary>
        /// <param name="door">Door to approach.</param>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>A cell to stand on, or the door's own cell if none is walkable.</returns>
        private Vector2Int ApproachCell(Door door, Adventurer leader)
        {
            Vector2Int best = door.Cell;
            float bestDistance = float.MaxValue;

            foreach (Vector2Int step in new[]
                     {
                         Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down
                     })
            {
                Vector2Int candidate = door.Cell + step;
                if (!_grid.IsWalkable(candidate))
                {
                    continue;
                }

                float distance = Vector2.Distance(leader.Position, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>Moves one adventurer toward a point.</summary>
        /// <param name="member">Who is moving.</param>
        /// <param name="desired">Where they want to be.</param>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="speed">Multiple of walking pace, so a panicking role can scramble.</param>
        private static void Glide(
            Adventurer member, Vector2 desired, float deltaTime, float speed = 1f)
        {
            member.Position = Vector2.MoveTowards(
                member.Position, desired, WalkSpeed * speed * deltaTime);
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

        /// <summary>How close a melee attacker must be to land a hit.</summary>
        /// <remarks>
        /// Slightly beyond the range mobs stop at, so a mob that has closed properly is always in
        /// reach and one that has been shouldered aside by another is not.
        /// </remarks>
        public const float MeleeReach = 1.15f;

        /// <summary>
        /// Spreads incoming damage across the party members a melee attacker can actually reach.
        /// </summary>
        /// <remarks>
        /// The reach test is the important part and it was missing. Damage used to be shared out
        /// across the whole party wherever it stood, so <b>a healer that had correctly fled to the
        /// back of the room still bled from a skeleton three cells away</b> -- and because that
        /// damage arrived spread thin, nobody was ever wounded by a full heal's worth, so the healer
        /// never found a target worth casting on and appeared to do nothing at all. Both complaints
        /// were the same bug.
        /// <para>
        /// It also makes the roles mean what SPEC.md says they mean: the tank draws the mobs and
        /// stands in reach, so the tank is what gets hit, and positioning the fragile roles out of
        /// reach is now genuinely worth doing.
        /// </para>
        /// </remarks>
        /// <param name="amount">Total damage this tick.</param>
        /// <param name="threats">
        /// Where the attackers are. Empty or null falls back to hitting everyone, which is what a
        /// trap or any other position-less source should do.
        /// </param>
        public void DistributeDamage(float amount, IReadOnlyList<Vector2> threats = null)
        {
            var living = Living.ToList();
            if (living.Count == 0 || amount <= 0f)
            {
                return;
            }

            if (threats is { Count: > 0 })
            {
                var reachable = living.Where(m => InReachOf(m, threats)).ToList();

                // Nobody in reach means the mobs are still closing, so nothing lands yet. Without
                // this the damage would simply fall back onto the whole party and undo the fix.
                if (reachable.Count == 0)
                {
                    return;
                }

                living = reachable;
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

        /// <summary>
        /// How close someone must be to work on a door.
        /// </summary>
        /// <remarks>
        /// Sized against the marching order, not against arm's length. The leader stops one cell
        /// short of a shut door and the rest trail at <see cref="FollowSpacing"/> behind it, so the
        /// archer -- second in the column -- stands about 1.6 cells away and a tighter reach left it
        /// permanently unable to touch the lock. Every roster made exactly zero progress on a door
        /// and the party simply stood there.
        /// </remarks>
        public const float DoorReach = 2.4f;

        /// <summary>The door the party is currently working on, for the view to show a bar.</summary>
        public Door WorkingOnDoor { get; private set; }

        /// <summary>Whether the party is picking the lock rather than battering it.</summary>
        public bool PickingLock { get; private set; }

        /// <summary>
        /// Deals with a shut door: the archer picks the lock, or the party breaks it down.
        /// </summary>
        /// <remarks>
        /// This is what stops a closed door being an unanswerable stall. An archer opens it in a few
        /// seconds and it is <b>jammed open for good</b>; a party with no archer has to batter
        /// through twice a skeleton's health, which costs them most of a minute -- and every one of
        /// those seconds is the player's income, so a tankless, archerless party being forced to
        /// smash is a good outcome for the dungeon rather than a punishment.
        /// <para>
        /// Fighting comes first. A party sawing at a lock while a skeleton bites them would look
        /// broken however sensible the priority list.
        /// </para>
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="threats">How many monsters the party can see.</param>
        private void ForceDoors(Adventurer leader, float deltaTime, int threats)
        {
            WorkingOnDoor = null;
            PickingLock = false;

            if (threats > 0 || Goal == PartyGoal.Retreating)
            {
                return;
            }

            Door door = BlockingDoor(leader);
            if (door == null)
            {
                return;
            }

            // Whoever is nearest the door and can reach it does the work.
            Adventurer picker = null;
            foreach (Adventurer member in Living)
            {
                if (member.Role == AdventurerRole.Ranged &&
                    Vector2.Distance(member.Position, door.Cell) <= DoorReach)
                {
                    picker = member;
                    break;
                }
            }

            if (picker != null)
            {
                WorkingOnDoor = door;
                PickingLock = true;
                door.Pick(deltaTime);
                return;
            }

            // No archer in reach. Anyone who is close enough swings at it instead.
            float force = 0f;
            foreach (Adventurer member in Living)
            {
                if (member.Role != AdventurerRole.Ranged &&
                    Vector2.Distance(member.Position, door.Cell) <= DoorReach)
                {
                    force += member.DamagePerSecond;
                }
            }

            if (force > 0f)
            {
                WorkingOnDoor = door;
                door.Batter(force * deltaTime);
            }
        }

        /// <summary>Where the mage blinked from this tick, for the view to draw the flash.</summary>
        public Vector2? BlinkedFrom { get; private set; }

        /// <summary>Where the mage blinked to this tick.</summary>
        public Vector2? BlinkedTo { get; private set; }

        /// <summary>
        /// Blinks the mage clear of a monster standing on it, if it can pay.
        /// </summary>
        /// <remarks>
        /// The escape of last resort, and the reason the mage's mana is worth watching: a mage that
        /// has spent its pool on bolts cannot buy its way out of a skeleton's reach, which is a
        /// mistake the player can watch happening.
        /// </remarks>
        /// <param name="member">Adventurer to consider.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>True when the mage blinked and should not also walk this tick.</returns>
        private bool TryBlink(Adventurer member, Perception view)
        {
            if (member.Role != AdventurerRole.Mage ||
                !member.CanCast(Adventurer.BlinkManaCost))
            {
                return false;
            }

            Vector2? threat = AdventurerAI.NearestThreat(member.Position, view);
            if (!threat.HasValue ||
                Vector2.Distance(member.Position, threat.Value) > AdventurerAI.BlinkRange)
            {
                return false;
            }

            if (!AdventurerAI.TryFindBlink(member, view, out Vector2 destination) ||
                !member.SpendMana(Adventurer.BlinkManaCost))
            {
                return false;
            }

            BlinkedFrom = member.Position;
            BlinkedTo = destination;
            member.Position = destination;
            return true;
        }

        /// <summary>Whether any attacker is close enough to hit this member.</summary>
        /// <param name="member">Member to test.</param>
        /// <param name="threats">Attacker positions.</param>
        /// <returns>True when one of them is within <see cref="MeleeReach"/>.</returns>
        private static bool InReachOf(Adventurer member, IReadOnlyList<Vector2> threats)
        {
            foreach (Vector2 threat in threats)
            {
                if (Vector2.Distance(member.Position, threat) <= MeleeReach)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Total damage per second the living party deals.</summary>
        /// <returns>Damage per second.</returns>
        public float DamageOutput()
        {
            return Living.Sum(m => m.DamagePerSecond);
        }


        /// <summary>
        /// Records what each living member just did, for the energy curve to price.
        /// </summary>
        /// <remarks>
        /// Read from what actually happened this tick rather than from each member's role, because
        /// the two disagree exactly when it matters: a healer with a skeleton on it is fleeing, not
        /// healing, and a tank walking toward a fight across the room is not yet fighting.
        /// <para>
        /// Called from both paths through <see cref="Tick"/>. A retreating party is running, whatever
        /// its members would otherwise be doing -- the retreat overrides every individual decision,
        /// so the actions have to agree with it or the rate would pay for a fight nobody is having.
        /// </para>
        /// </remarks>
        /// <param name="threats">Living monsters the party can see.</param>
        private void AssignActions(IReadOnlyList<Vector2> threats)
        {
            bool retreating = Goal == PartyGoal.Retreating;
            bool working = WorkingOnDoor != null || _lootProgress > 0f;

            foreach (Adventurer member in Living)
            {
                if (retreating)
                {
                    member.Action = AdventurerAction.Fleeing;
                    continue;
                }

                if (member.IsPanicking)
                {
                    member.Action = AdventurerAction.Fleeing;
                    continue;
                }

                if (working)
                {
                    member.Action = AdventurerAction.Working;
                    continue;
                }

                if (threats.Count == 0)
                {
                    member.Action = AdventurerAction.Walking;
                    continue;
                }

                float nearest = float.MaxValue;
                foreach (Vector2 threat in threats)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(member.Position, threat));
                }

                if (nearest <= MeleeReach)
                {
                    member.Action = AdventurerAction.Fighting;
                    continue;
                }

                // Everyone but the tank fights at range, so a monster in the room and none in reach
                // means they are shooting or casting at it. A tank in that position is closing.
                member.Action = member.Role == AdventurerRole.Tank
                    ? AdventurerAction.Walking
                    : AdventurerAction.Shooting;
            }
        }


        /// <summary>Rooms the party has set foot in.</summary>
        private readonly HashSet<int> _visited = new();

        /// <summary>How many distinct rooms the party has entered.</summary>
        public int VisitedRooms => _visited.Count;

        /// <summary>Whether every room in the dungeon has been walked into.</summary>
        public bool HasExploredEverything { get; private set; }

        /// <summary>
        /// The centre of the nearest room the party has not been in, if any remain.
        /// </summary>
        /// <remarks>
        /// Nearest by walking distance rather than by lattice, because a room one wall away can be a
        /// long way round if the door between is shut. Rooms with no route at all are skipped, so a
        /// sealed-off wing never becomes an objective the party cannot reach.
        /// </remarks>
        /// <param name="leader">Whoever is at the front.</param>
        /// <returns>A room centre, or null once everything reachable has been seen.</returns>
        private Vector2Int? NearestUnvisitedRoomCentre(Adventurer leader)
        {
            Vector2Int? best = null;
            int shortest = int.MaxValue;

            for (int room = 0; room < _roomCentres.Count; room++)
            {
                if (_visited.Contains(room))
                {
                    continue;
                }

                List<Vector2Int> route = _grid.FindPath(leader.Cell, _roomCentres[room]);
                if (route.Count > 0 && route.Count < shortest)
                {
                    shortest = route.Count;
                    best = _roomCentres[room];
                }
            }

            return best;
        }

        /// <summary>Notes which room the party is standing in, for the exploration objective.</summary>
        /// <param name="leader">Whoever is at the front.</param>
        private void RecordVisit(Adventurer leader)
        {
            int room = _grid.RoomAt(leader.Cell);
            if (room != DungeonGrid.NoRoom)
            {
                _visited.Add(room);
            }

            HasExploredEverything = _visited.Count >= _roomCentres.Count;
        }

        /// <summary>Picks a goal from the party's health and what is in the room with it.</summary>
        /// <param name="threatsInRoom">Living mobs the party can see.</param>
        /// <param name="deltaTime">Seconds since the last tick, for the combat grace timer.</param>
        private void ChooseGoal(int threatsInRoom, float deltaTime)
        {
            float health = HealthFraction;

            // Reset on sight, decay when nothing is there. Everything below asks whether the party is
            // still braced rather than whether a monster exists this exact frame.
            _combatGraceLeft = threatsInRoom > 0
                ? CombatGrace
                : Mathf.Max(0f, _combatGraceLeft - deltaTime);

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

            Goal = _combatGraceLeft > 0f ? PartyGoal.Fighting : PartyGoal.Advancing;
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
            var living = Living.ToList();

            // Each healer casts from its own pool on its own cooldown, so two healers genuinely have
            // twice the sustain and a party that has lost its last healer stops healing at once --
            // killing the healer is how a player ruins their own raid, and it has to be felt.
            foreach (Adventurer healer in living)
            {
                if (healer.Role != AdventurerRole.Healer)
                {
                    continue;
                }

                healer.HealCooldown = Mathf.Max(0f, healer.HealCooldown - deltaTime);
                if (healer.HealCooldown > 0f || !healer.CanCast(AdventurerAI.HealCost))
                {
                    continue;
                }

                // Note this runs whether or not a fight is happening. Healers patch the party up
                // between rooms as well as during a brawl, which is exactly what makes them the
                // player's best customer: the party they keep alive walks into the next ambush and
                // bleeds all over again.
                Adventurer target = AdventurerAI.ChooseHealTarget(living, healer.Mana);
                if (target == null)
                {
                    continue;
                }

                target.Heal(AdventurerAI.HealAmount);
                healer.SpendMana(AdventurerAI.HealCost);
                healer.HealCooldown = HealInterval;
            }
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
