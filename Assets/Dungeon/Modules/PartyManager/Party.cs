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
    /// A party of four to nine adventurers and the AI that drives them.
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

        /// <summary>
        /// How fast the party is currently moving, as a fraction of its normal pace.
        /// </summary>
        /// <remarks>
        /// One for a fresh party; less for one that has been grinding. The author asked for a party
        /// in a long fight to visibly tire — <i>"it's not much, like eighty percent of normal speed,
        /// but you'll see the speed up after a while"</i> — which is the same idea as the rate decay
        /// expressed in movement instead of money, and the better half of it: the player <b>sees</b>
        /// the boredom being priced rather than only watching a number shrink.
        /// </remarks>
        public float Pace { get; private set; } = 1f;

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
        private readonly MarchingOrder _order;

        /// <summary>Finds the shut door in the party's way, and where to stand to force it.</summary>
        private readonly DoorSearch _doors;
        private readonly DungeonGrid _grid;
        private readonly Vector2Int _bossCell;
        private readonly IReadOnlyList<Vector2Int> _roomCentres;

        /// <summary>Whether the caller supplied real room centres rather than leaving them null.</summary>
        private readonly bool _hasRoomPlan;
        private readonly Vector2Int _entranceCell;
        private readonly HashSet<Vector2Int> _looted = new();
        private IReadOnlyCollection<Vector2Int> _chests = System.Array.Empty<Vector2Int>();
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
        /// Whether the party stepped into a room it had not seen this raid, this tick.
        /// </summary>
        /// <remarks>
        /// True for the single tick of arrival. New <b>this raid</b> rather than new ever: purchases
        /// are permanent for a season and the party explores toward the nearest unseen room, so
        /// "new ever" would pay nothing from the third round onward, while the author's intent is
        /// plainly to reward traversal every raid.
        /// </remarks>
        public bool JustEnteredNewRoom { get; private set; }

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

                // The WHOLE party, dead included, and that is the fix rather than an oversight.
                // Iterating Living meant a corpse left the denominator with it, so the fraction
                // JUMPED UP every time somebody died -- a party being killed one member at a time
                // read as a party getting healthier, and ChooseGoal reads this to decide whether to
                // break off. Measured before the fix, under heavy pressure: a nine-strong party lost
                // EIGHT OF NINE while this never fell below 53%, and the retreat -- which SPEC.md
                // calls the player's only mercy -- did not fire once in four raids.
                //
                // Masked at four, which is why it survived the whole project: a four-strong party
                // has too few members for the recovery to outrun the damage, so it still bottomed
                // out and still ran. The defect scaled in with the party.
                foreach (Adventurer member in _members)
                {
                    have += member.IsAlive ? member.HealthFraction * member.MaxHealth : 0f;
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

        /// <summary>Creates a party at the entrance, sized by its composition.</summary>
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
            _order = new MarchingOrder(grid);
            _doors = new DoorSearch(grid, entranceCell, bossCell);
            _entranceCell = entranceCell;
            _bossCell = bossCell;
            // Whether the caller described a real dungeon or left it to be inferred. Tests written
            // before exploration existed pass nothing and keep the old boss-cell ending; anything
            // built from a room plan explores and walks back out, however few rooms it has.
            _hasRoomPlan = roomCentres is { Count: > 0 };
            _roomCentres = roomCentres ?? new List<Vector2Int> { bossCell };
            Composition = composition ?? PartyComposition.Opening;

            foreach (AdventurerRole role in Composition.Roles)
            {
                _members.Add(new Adventurer(role, entranceCell));
            }


            _order.Seed(entranceCell);

            _order.Place(Living.ToList());
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
            IReadOnlyCollection<Vector2Int> chests = null,
            float paceMultiplier = 1f)
        {
            // A tired party moves at a fraction of its pace. Passed in rather than computed here
            // because the fight timer that decides it lives with the rate modifiers, and the party
            // has no business knowing how long it has been earning less.
            Pace = Mathf.Clamp(paceMultiplier, 0.1f, 1f);
            _chests = chests ?? System.Array.Empty<Vector2Int>();
            JustLooted = null;
            JustEnteredNewRoom = false;

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
                // Run for the door rather than for the entrance when a shut one is in the way. The
                // entrance is unreachable through it, so pathing there returns nothing and the party
                // simply stands still -- which is what a player who shut the door behind them saw.
                Door barred = _doors.TowardExit(leader);
                Vector2Int refuge = barred != null
                    ? _doors.ApproachCell(barred, leader)
                    : _entranceCell;

                MoveAlongPath(leader, refuge, deltaTime, traps);
                _order.Record(leader.Position, _members.Count);
                for (int rank = 1; rank < living.Count; rank++)
                {
                    Glide(living[rank], _order.SlotFor(rank, living.Count), deltaTime);
                }

                ForceDoors(leader, deltaTime, threats.Count);
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
            _order.Record(leader.Position, _members.Count);

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
                    FormationSlot = _order.SlotFor(rank, living.Count),
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
            // has seen everything AND has been as deep as the dungeon goes turns round and walks out
            // the way it came in. Dungeons built without room centres keep the old boss-cell ending,
            // which is every test written before exploration existed.
            //
            // The depth clause is what makes a ONE-ROOM dungeon a raid rather than a formality.
            // Entrance and deepest cell sit in the same room there, so "explored everything and
            // standing on the entrance" is true on the very first tick -- the party would have
            // escaped before taking a step and the raid would end at zero seconds. In a corridor the
            // clause costs a couple of cells of walking at the far end and nothing else.
            bool left = _hasRoomPlan
                ? HasExploredEverything && ReachedDepth && Cell == _entranceCell
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
            Door blocking = _doors.TowardBoss(leader);
            if (blocking != null)
            {
                return _doors.ApproachCell(blocking, leader);
            }

            // Explore rather than walk a line to a fixed boss room: head for the nearest room not
            // yet seen, then to the deepest cell, and only then leave by the way in. That is what
            // makes the player's placements steer anything -- a party choosing where to go can be
            // tempted by a chest or held up by a monster; a party walking to one fixed cell cannot.
            //
            // The middle step exists for the one-room dungeon the game now opens with. There is
            // nothing unvisited there from the first tick, so without it the objective is the
            // entrance the party is already standing on, and they walk in and straight back out --
            // or, more precisely, never walk in at all.
            Vector2Int destination = NearestUnvisitedRoomCentre(leader)
                                     ?? (ReachedDepth ? _entranceCell : _bossCell);

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


        /// <summary>Moves one adventurer toward a point.</summary>
        /// <param name="member">Who is moving.</param>
        /// <param name="desired">Where they want to be.</param>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="speed">Multiple of walking pace, so a panicking role can scramble.</param>
        private void Glide(
            Adventurer member, Vector2 desired, float deltaTime, float speed = 1f)
        {
            Vector2 from = member.Position;
            Vector2 to = Vector2.MoveTowards(
                from, desired, WalkSpeed * speed * Pace * deltaTime);

            member.Position = Constrained(from, to);
        }

        /// <summary>
        /// Keeps a step out of the rock, sliding along a wall rather than stopping dead against it.
        /// </summary>
        /// <remarks>
        /// <b>The author reported adventurers walking through walls, and this is where it happened.</b>
        /// Every movement in the game funnels through <see cref="Glide"/>, which was an unchecked
        /// <c>Vector2.MoveTowards</c>. The destination was almost always fine — the leader walks a
        /// path of walkable cells, and the formation already gives up a flank whose cell is rock —
        /// but a straight line between two good points cuts the corner between them, so a member
        /// rounding a doorway clipped through the jamb.
        /// <para>
        /// Measured before the fix: <b>1616 samples inside a wall, 1.43% of 113150</b>, across nine
        /// rosters, worst for THE PHALANX at 592. That is after <c>WallViolationTests</c> discounts
        /// the procession, so it is the real thing rather than the party queuing on the forecourt.
        /// </para>
        /// <para>
        /// <b>Slide, not stop.</b> A blocked step retries along each axis alone before giving up,
        /// which is the ordinary way a 2D body moves along a wall. Stopping dead instead would be a
        /// worse bug than the one being fixed: a party pinned on a corner earns the idle rate, and
        /// this game charges the player for every second the party is not in trouble.
        /// </para>
        /// <para>
        /// <b>A member already outside the grid is left alone</b>, and that exemption is
        /// load-bearing. The party is deliberately strung out along the approach at tick zero so it
        /// reads as marching in through the archway; the approach is scenery rather than grid, so
        /// every follower still on it stands on an unwalkable cell by construction. Constraining
        /// them there would pin the party on the forecourt for the whole raid.
        /// </para>
        /// </remarks>
        /// <param name="from">Where the member is now.</param>
        /// <param name="to">Where the step would put them.</param>
        /// <returns>The furthest part of that step that stays out of the rock.</returns>
        private Vector2 Constrained(Vector2 from, Vector2 to)
        {
            if (!StandableAt(from) || StandableAt(to))
            {
                return to;
            }

            var alongX = new Vector2(to.x, from.y);
            if (StandableAt(alongX))
            {
                return alongX;
            }

            var alongY = new Vector2(from.x, to.y);
            return StandableAt(alongY) ? alongY : from;
        }

        /// <summary>Whether a body can stand at a world point.</summary>
        /// <param name="point">Point to test.</param>
        /// <returns>True when the cell under it is walkable.</returns>
        private bool StandableAt(Vector2 point)
        {
            return _grid.IsWalkable(
                new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y)));
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
        /// short of a shut door and the rest trail at <see cref="MarchingOrder.FollowSpacing"/> behind it, so the
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

            bool fleeing = Goal == PartyGoal.Retreating;

            // Fighting comes first -- unless the party is running, in which case the door IS the
            // escape. A wounded party pinned against a shut door it will not touch is the one stall
            // with no answer in the game: the player's safety valve is opening a door behind them,
            // and it is worth nothing if the party will not use a door it can open itself.
            if (threats > 0 && !fleeing)
            {
                return;
            }

            Door door = fleeing ? _doors.TowardExit(leader) : _doors.TowardBoss(leader);
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
                // Add returns false when the room was already known, which makes this the exact
                // moment the party reaches somewhere new -- what the author's room bonus pays for.
                JustEnteredNewRoom = _visited.Add(room);
            }

            HasExploredEverything = _visited.Count >= _roomCentres.Count;

            // Latched, not sampled: they went in once and it stays true, so a party walking back
            // out does not lose the fact the moment it steps off the cell.
            ReachedDepth |= _visited.Count > 1 || leader.Cell == _bossCell;
        }

        /// <summary>
        /// Whether the party has actually gone into the dungeon.
        /// </summary>
        /// <remarks>
        /// The other half of "they are done here", alongside <see cref="HasExploredEverything"/>,
        /// and it exists entirely for the <b>single-room</b> dungeon the game now opens with. There,
        /// the entrance and the deepest cell share a room: every room has been visited on the first
        /// tick, the party is standing on the entrance, and "explored everything and back at the
        /// door" is therefore true before anybody has moved. The raid would end at zero seconds.
        /// <para>
        /// Crossing into a second room settles it in any larger dungeon, which is why this costs a
        /// corridor nothing: it is already true long before exploration finishes. Only a one-room
        /// dungeon has to earn it by walking to the far wall.
        /// </para>
        /// </remarks>
        public bool ReachedDepth { get; private set; }

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




    }
}
