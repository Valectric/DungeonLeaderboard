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

        /// <summary>
        /// Whether this adventurer is the one at the front, and therefore responsible for advancing.
        /// </summary>
        /// <remarks>
        /// Load-bearing. A follower with nothing to do falls back to its formation slot, which is a
        /// point on the leader's trail -- but the <i>leader</i> has no slot, so that fallback left it
        /// walking at the origin. Only the tank ever pathed to the objective, so <b>any party not led
        /// by a tank never advanced at all</b>: THE GLASS CANNONS and THE SKIRMISHERS, and any party
        /// whose tank had died, simply drifted into a corner and stood there for the rest of the
        /// raid.
        /// </remarks>
        public bool IsLeader { get; set; }

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
        /// <remarks>
        /// Raised from 1.0 on the author's instruction that <i>"a healer should always back off"</i>.
        /// At 1.0 the healer only reacted once something was already swinging at it, which is not
        /// backing off, it is being caught -- and a dead healer costs the player fifty points and
        /// every heal the rest of the raid would have had.
        /// <para>
        /// 2.6 sits just outside a monster's melee reach plus its closing speed for a tick, so the
        /// healer keeps a working distance rather than a panic one. It is bounded by
        /// <c>StandOff</c>'s room clamp, so it cannot back out of the room and stop healing.
        /// </para>
        /// </remarks>
        public const float HealerFleeRange = 2.6f;

        /// <summary>Distance the ranged attacker tries to keep from its target.</summary>
        public const float RangedRange = 3f;

        /// <summary>Distance the mage tries to keep from the tank's target.</summary>
        public const float MageRange = 2.4f;

        /// <summary>How close the tank closes before it stops and swings.</summary>
        public const float TankReach = 0.85f;

        /// <summary>
        /// Health below which a tank stops holding the line and gives ground.
        /// </summary>
        /// <remarks>
        /// The author's rule: <i>"the tank should always move forward. If a tank gets low, it should
        /// move backwards as soon as it starts to get too much damage — so if there are two tanks
        /// and one takes all the damage, it should move back once it gets below thirty percent."</i>
        /// <para>
        /// Per <b>member</b>, not per party, which is what makes the two-tank case work: the one
        /// that has been soaking gives ground while its partner, still healthy, steps up and takes
        /// over. That falls out of reading each tank's own health rather than the party's.
        /// </para>
        /// </remarks>
        public const float TankHoldsUntil = 0.3f;

        /// <summary>
        /// Health below which anyone starts backing away from what is hitting them.
        /// </summary>
        /// <remarks>
        /// Applies to every role including the tank — a body at a sliver of health is worth more
        /// alive and bleeding than dead, and a corpse earns the player nothing at all while costing
        /// a fifty-point penalty.
        /// </remarks>
        public const float WoundedBacksOffBelow = 0.45f;

        /// <summary>
        /// How much further a badly wounded member wants to stand from a threat.
        /// </summary>
        /// <remarks>
        /// Added to whatever the role's usual distance is. Deliberately modest: combat is scoped per
        /// room, so a member that backs out of the room stops fighting, stops healing, and stops
        /// earning — and <see cref="StandOff"/> clamps to the room anyway, so this asks for space
        /// and takes whatever the walls allow.
        /// </remarks>
        public const float WoundedExtraSpace = 1.6f;

        /// <summary>How close a melee attacker has to be before a fragile role panics.</summary>
        /// <remarks>
        /// Wider than the reach a mob can actually strike from, so the scramble starts <i>before</i>
        /// the first blow lands rather than in response to it.
        /// </remarks>
        public const float PanicRange = 1.7f;

        /// <summary>
        /// How much faster a panicking adventurer moves.
        /// </summary>
        /// <remarks>
        /// A mob closes at 1.9 cells a second and the party walks at 0.6, so a mage backing away at
        /// walking pace was not retreating at all -- it was being escorted. The standoff behaviour
        /// was correct and completely ineffective, which looked from the outside like the fragile
        /// roles simply standing still and dying.
        /// <para>
        /// Deliberately still slower than a mob. Nobody outruns a monster in a straight line; a
        /// scrambling mage buys seconds and reaches its firing distance, which is all it needs.
        /// </para>
        /// </remarks>
        public const float PanicSpeed = 2.2f;

        /// <summary>
        /// Panic multiplier for an archer, which is fast enough to actually break away.
        /// </summary>
        /// <remarks>
        /// The author asked for an archer that can outrun a single monster. Everything here is a
        /// multiplier on <see cref="Party.WalkSpeed"/>, so the shared 2.2 gives 1.32 cells a second
        /// against a monster's 1.90 — the escape can only delay, never succeed. Clearing 3.17 fixes
        /// that, and 3.6 was measured to work.
        /// <para>
        /// It was held at 2.2 for a while because under the old curve it <b>inverted the game's one
        /// rule</b>: an archer that escapes is never wounded, a surviving party therefore earned
        /// less, and measured with <c>KillingTheParty_NeverPaysBest</c> the best wipe paid 185
        /// against 175 for the best raid the party survived.
        /// </para>
        /// <para>
        /// The per-action curve fixed the cause rather than the symptom. Firing at something now pays
        /// on its own, so an archer can kite all raid and still earn, and the same measurement reads
        /// <b>4 against 190</b> — the right way round by a wide margin. Raised to 3.6 on that
        /// evidence.
        /// </para>
        /// </remarks>
        public const float ArcherPanicSpeed = 3.6f;

        /// <summary>
        /// How fast this adventurer should move right now, as a multiple of walking pace.
        /// </summary>
        /// <param name="self">The adventurer moving.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>A speed multiplier.</returns>
        public static float SpeedMultiplier(Adventurer self, Perception view)
        {
            // The tank is meant to be in melee, so it never panics -- a tank that backed off would
            // stop soaking and the whole party would be exposed.
            if (self.Role == AdventurerRole.Tank)
            {
                return 1f;
            }

            Vector2? nearest = Nearest(self.Position, view.Threats);
            bool cornered = nearest.HasValue &&
                            Vector2.Distance(self.Position, nearest.Value) <= PanicRange;

            if (!cornered)
            {
                return 1f;
            }

            return self.Role == AdventurerRole.Ranged ? ArcherPanicSpeed : PanicSpeed;
        }

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
                // A tank holds the line and stands closer than anyone -- until it has taken enough
                // that dying becomes the likelier outcome, at which point it gives ground. Read off
                // THIS tank's health, so with two of them the one that has been soaking falls back
                // while its partner steps up.
                float reach = self.HealthFraction < TankHoldsUntil
                    ? RangedRange
                    : TankReach;

                return StandOff(self.Position, target.Value, Spacing(self, reach), view);
            }

            return Advance(self, view);
        }

        /// <summary>
        /// Walks toward the party's objective, stepping around traps.
        /// </summary>
        /// <remarks>
        /// Whoever is at the front uses this, not just the tank. It used to live inside the tank's
        /// behaviour alone, which meant a party led by anyone else had no way to make progress and
        /// simply stopped.
        /// </remarks>
        /// <param name="self">The adventurer deciding.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>The next point to walk to.</returns>
        private static Vector2 Advance(Adventurer self, Perception view)
        {
            // preferStraight: TRUE here and nowhere else, and that asymmetry is deliberate.
            //
            // The search breaks ties by push order, so a fixed neighbour list made every path lean
            // one way and come back as a staircase when the goal lay across that lean. Harmless
            // while the dungeon ran east; a 25% slowdown once it ran north.
            //
            // Fixing it for EVERYTHING was measured and rejected. Monsters path with the same
            // routine, so straightening it globally made the horde converge faster and no party
            // survived a maximal ambush -- on the HORIZONTAL layout as well, which is what proved
            // the pathfinder rather than the geometry was responsible. `KillingTheParty_NeverPaysBest`
            // went from a best survival of 434 to zero, which is the game's central rule inverted.
            //
            // So the party gets the straight route it always should have had, and the monsters keep
            // the pursuit they were tuned against. Making them cleverer is a balance decision and
            // belongs to the author, not to a bug fix.
            List<Vector2Int> path = view.Grid.FindPath(
                self.Cell, view.Objective, view.Traps, preferStraight: true);
            if (path.Count == 0)
            {
                return self.Position;
            }

            // Aim a couple of cells ahead so the leader keeps moving smoothly instead of stopping on
            // every cell centre.
            return path[Mathf.Min(1, path.Count - 1)];
        }

        /// <summary>
        /// Where an adventurer goes when it has nothing to fight.
        /// </summary>
        /// <remarks>
        /// The leader advances; everyone else falls in behind it. A follower's formation slot is a
        /// point on the leader's trail, so it is only meaningful for a follower -- the leader has no
        /// slot, and returning one left it walking toward <c>Vector2.zero</c>, the bottom-left corner
        /// of the grid. That is exactly where tankless parties were found standing.
        /// </remarks>
        /// <param name="self">The adventurer deciding.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>The next point to walk to.</returns>
        private static Vector2 Idle(Adventurer self, Perception view)
        {
            return view.IsLeader ? Advance(self, view) : view.FormationSlot;
        }

        /// <summary>The mage focuses whatever the tank is fighting, from a comfortable distance.</summary>
        private static Vector2 MageGoal(Adventurer self, Perception view)
        {
            // Whatever the tank is fighting, a monster in the mage's face comes first. Focusing the
            // tank's target while something else was closing meant the mage backed away from the
            // wrong monster -- sometimes directly into the one chasing it.
            Vector2? closing = Cornering(self, view);
            if (closing.HasValue)
            {
                return StandOff(self.Position, closing.Value, Spacing(self, MageRange), view);
            }

            Vector2? target = view.TankTarget ?? NearestVisible(self.Position, view);
            return target.HasValue
                ? StandOff(self.Position, target.Value, MageRange, view)
                : Idle(self, view);
        }

        /// <summary>How far a blink carries the mage, in cells.</summary>
        public const float BlinkDistance = 5f;

        /// <summary>How close a monster must be before the mage spends mana escaping it.</summary>
        /// <remarks>
        /// Tighter than <see cref="PanicRange"/>. Backing away on foot is free and happens early;
        /// blinking costs a quarter of the pool and is the answer when walking has failed.
        /// </remarks>
        public const float BlinkRange = 1.1f;

        /// <summary>
        /// Finds somewhere to blink to: five cells from the monster, still inside the dungeon.
        /// </summary>
        /// <remarks>
        /// Candidates are tried around a circle and scored on how far they end up from every visible
        /// threat, so the mage does not blink out of one monster's reach and into another's. A
        /// landing spot must be walkable and in a room -- a mage inside a wall would be unreachable,
        /// unkillable and would stall the raid.
        /// </remarks>
        /// <param name="self">The mage.</param>
        /// <param name="view">What it can perceive.</param>
        /// <param name="destination">Receives the landing spot.</param>
        /// <returns>True when somewhere safe was found.</returns>
        public static bool TryFindBlink(Adventurer self, Perception view, out Vector2 destination)
        {
            destination = self.Position;
            Vector2? threat = Nearest(self.Position, view.Threats);
            if (!threat.HasValue)
            {
                return false;
            }

            // Straight away from the monster first, then progressively wider angles, so the mage
            // prefers the obvious escape and only takes a sideways one when the wall is behind it.
            Vector2 away = (self.Position - threat.Value).normalized;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Vector2.left;
            }

            float bestScore = float.MinValue;
            bool found = false;

            for (int step = 0; step < 12; step++)
            {
                // 0, +30, -30, +60, -60 ... degrees off the escape line.
                float degrees = ((step + 1) / 2) * 30f * (step % 2 == 0 ? 1f : -1f);
                Vector2 direction = Quaternion.Euler(0f, 0f, degrees) * away;

                // Take the longest jump this direction allows INSIDE the room, rather than one
                // fixed length that the room usually cannot contain. BlinkDistance is 5 against 5x5
                // rooms, so demanding the full length and rejecting anything else left a cornered
                // mage with no escape at all -- clamped standoff on one side, refused blink on the
                // other. The escape valve has to survive the confinement, not be closed by it.
                Vector2 candidate = self.Position;
                var cell = self.Cell;
                bool reachable = false;

                for (float hop = BlinkDistance; hop >= 2f; hop -= 0.5f)
                {
                    Vector2 tried = self.Position + (direction * hop);
                    var triedCell = new Vector2Int(
                        Mathf.RoundToInt(tried.x), Mathf.RoundToInt(tried.y));

                    if (view.Grid.IsWalkable(triedCell) &&
                        view.Grid.RoomAt(triedCell) == view.Grid.RoomAt(self.Cell))
                    {
                        candidate = tried;
                        cell = triedCell;
                        reachable = true;
                        break;
                    }
                }

                if (!reachable)
                {
                    continue;
                }
                // The search above already guarantees walkable, in-room and non-doorway.
                //
                // A blink must stay inside the room the mage is already in. Only the LANDING cell
                // was ever checked, never the line travelled -- and BlinkDistance is 5 against 5x5
                // rooms, so a legal-looking landing routinely sat one or two rooms away with walls
                // in between. Every blink also draws a bolt from origin to destination
                // (Raid.cs, ShotKind.Bolt), which is the longest line the view ever draws: this is
                // the likeliest thing the author saw as "shooting through three or four walls".
                //
                // It also stopped the mage fighting. Landing outside the party's room dropped it
                // from the engaged set entirely, so it teleported out of the battle it was paid to
                // be in.


                float score = float.MaxValue;
                foreach (Vector2 other in view.Threats)
                {
                    score = Mathf.Min(score, Vector2.Distance(candidate, other));
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    destination = cell;
                    found = true;
                }
            }

            // Never blink somewhere no better than where it already stands.
            return found && bestScore > Vector2.Distance(self.Position, threat.Value) + 1f;
        }

        /// <summary>The nearest threat close enough to be a personal problem, if any.</summary>
        /// <param name="self">The adventurer deciding.</param>
        /// <param name="view">What it can perceive.</param>
        /// <returns>The threat's position, or null.</returns>
        private static Vector2? Cornering(Adventurer self, Perception view)
        {
            Vector2? nearest = Nearest(self.Position, view.Threats);
            return nearest.HasValue &&
                   Vector2.Distance(self.Position, nearest.Value) <= PanicRange
                ? nearest
                : null;
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
            // Something in its face outranks its firing solution. An archer that kept aiming at the
            // nearest visible target while a second monster closed on it never moved.
            Vector2? closing = Cornering(self, view);
            if (closing.HasValue)
            {
                return StandOff(self.Position, closing.Value, Spacing(self, RangedRange), view);
            }

            Vector2? target = NearestVisible(self.Position, view);
            if (target.HasValue)
            {
                return StandOff(self.Position, target.Value, Spacing(self, RangedRange), view);
            }

            // Only a follower detours to defuse a trap. A leading rogue would walk onto the plate and
            // stop there forever -- disarming is driven from the follower loop, so the leader would
            // never actually work on it, and the whole party would wait behind a trap that was never
            // going to be defused.
            if (view.IsLeader)
            {
                return Advance(self, view);
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
            if (!nearest.HasValue)
            {
                return Idle(self, view);
            }

            // HOLD a distance rather than switching between two opposite goals.
            //
            // This used to be binary: inside HealerFleeRange walk away, outside it walk back to
            // formation. Those are opposite directions either side of one line, so a healer sitting
            // near that line flipped between them every tick -- the author saw it as the sprite
            // "flipping ultra fast" mid-fight. Raising the range from 1.0 to 2.6 did not create the
            // oscillation, it just moved the healer onto the boundary and made it constant.
            //
            // StandOff has no such edge: it walks to a point at the requested distance, so a healer
            // already at that distance simply stays put. It also clamps to the room, so the healer
            // keeps its distance without backing out of the fight it is healing.
            return StandOff(
                self.Position, nearest.Value, Spacing(self, HealerFleeRange), view);
        }

        /// <summary>The healer's old bias-toward-the-party retreat, kept for reference.</summary>
        /// <remarks>
        /// Unused since the healer began holding a distance instead of fleeing across a threshold.
        /// </remarks>
        /// <param name="self">The healer.</param>
        /// <param name="view">What it can see.</param>
        /// <returns>Where it would have fled to.</returns>
        private static Vector2 HealerRetreat(Adventurer self, Perception view)
        {
            Vector2? nearest = Nearest(self.Position, view.Threats);
            if (!nearest.HasValue)
            {
                return Idle(self, view);
            }

            // Backing away is biased toward the rest of the party so the healer does not flee alone
            // into an empty room. A leading healer -- the last one standing, say -- has nobody to
            // fall back toward, so it retreats along its own line and keeps its objective in mind.
            Vector2 away = (self.Position - nearest.Value).normalized;
            Vector2 anchor = view.IsLeader ? Advance(self, view) : view.FormationSlot;
            Vector2 toward = (anchor - self.Position).normalized;
            Vector2 escape = self.Position + ((away * 1.6f) + (toward * 0.6f));

            var cell = new Vector2Int(Mathf.RoundToInt(escape.x), Mathf.RoundToInt(escape.y));
            return view.Grid.IsWalkable(cell) ? escape : anchor;
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
                if (missing < TopUpThreshold)
                {
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

        /// <summary>
        /// How much health someone must be missing before a healer will spend a cast on them.
        /// </summary>
        /// <remarks>
        /// This used to be a full heal's worth, on the reasoning that casting into an overflow wastes
        /// a limited pool. In play that made the healer look broken: once the party's wounds dropped
        /// below forty-five nobody qualified, so it stood there with a full bar doing nothing, and it
        /// appeared to <b>stop healing the moment a fight ended</b> -- exactly when it should be
        /// patching everyone up for the next room.
        /// <para>
        /// A modest overflow is the right trade now that mana regenerates: wasting a little of a
        /// refilling pool costs nothing, and a party that walks into the next ambush at full health
        /// has much further to fall, which is where the money is.
        /// </para>
        /// </remarks>
        public const float TopUpThreshold = 12f;

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

        /// <summary>The nearest visible threat to a point, or null.</summary>
        /// <param name="from">Where to measure from.</param>
        /// <param name="view">What can be perceived.</param>
        /// <returns>The nearest threat's position, or null.</returns>
        public static Vector2? NearestThreat(Vector2 from, Perception view)
        {
            return Nearest(from, view.Threats);
        }

        /// <summary>Nearest of a set of points, ignoring visibility.</summary>
        private static Vector2? Nearest(Vector2 from, IReadOnlyList<Vector2> points)
        {
            return points.Count == 0
                ? null
                : points.OrderBy(p => Vector2.Distance(from, p)).First();
        }

        /// <summary>A point the given distance from a target, on the side the mover is already on.</summary>
        /// <summary>
        /// How far a member wants to be from what is hitting it, given how hurt it is.
        /// </summary>
        /// <remarks>
        /// One rule for every role, so a wounded body backs off whatever its job is. The tank is
        /// not exempt: it simply starts from a much shorter distance, and it has its own earlier
        /// threshold in <see cref="TankGoal"/> for giving ground while still able to fight.
        /// </remarks>
        /// <param name="self">Who is standing off.</param>
        /// <param name="baseRange">The distance the role wants when healthy.</param>
        /// <returns>The distance to ask for.</returns>
        private static float Spacing(Adventurer self, float baseRange)
        {
            return self.HealthFraction < WoundedBacksOffBelow
                ? baseRange + WoundedExtraSpace
                : baseRange;
        }

        private static Vector2 StandOff(
            Vector2 self, Vector2 target, float range, Perception view)
        {
            Vector2 offset = self - target;
            if (offset.sqrMagnitude < 0.0001f)
            {
                offset = Vector2.left;
            }

            offset = offset.normalized;

            // Keeping your distance must not mean leaving the room. Combat is scoped per room, so a
            // mage that backs five cells away from a monster in a 5x5 room backs out of the fight
            // entirely -- measured, it fired NOT ONE BOLT in a whole raid, while the archer at a
            // shorter range kept shooting. Standing off into uselessness is worse than standing
            // close.
            //
            // So the ideal distance is an opening bid: back off as far as the room allows, and no
            // further. Walked in from the requested range rather than out from the target, so the
            // usual case costs one grid lookup.
            int targetRoom = view.Grid.RoomAt(
                new Vector2Int(Mathf.RoundToInt(target.x), Mathf.RoundToInt(target.y)));

            for (float distance = range; distance >= 1.2f; distance -= 0.4f)
            {
                Vector2 candidate = target + (offset * distance);
                var cell = new Vector2Int(
                    Mathf.RoundToInt(candidate.x), Mathf.RoundToInt(candidate.y));

                if (view.Grid.IsWalkable(cell) && view.Grid.RoomAt(cell) == targetRoom)
                {
                    return candidate;
                }
            }

            // Nowhere in the room is far enough away. Hold position rather than walk into rock.
            return self;
        }
    }
}
