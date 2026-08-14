using System.Collections.Generic;
using System.Linq;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using UnityEngine;

namespace Dungeon.RaidManager
{
    /// <summary>Why a raid stopped earning.</summary>
    public enum RaidOutcome
    {
        /// <summary>Still running.</summary>
        InProgress = 0,

        /// <summary>The full sixty seconds elapsed. The best ending for the player.</summary>
        TimeExpired = 1,

        /// <summary>The party reached the boss room and left early.</summary>
        PartyEscaped = 2,

        /// <summary>The party died. The worst ending, and it is the player's fault.</summary>
        PartyWiped = 3
    }

    /// <summary>
    /// One sixty-second raid: the clock, the energy, and the three player verbs.
    /// </summary>
    /// <remarks>
    /// This is a Module. It is the coordinator that owns the dungeon, the party and the mobs, and
    /// resolves combat between the latter two. Party and mobs are siblings that never reference each
    /// other -- all traffic goes through here, per the One-Flow rule.
    /// <para>
    /// Ending early is not a special fail state and carries no extra penalty. The lost seconds are
    /// the punishment, because seconds are the only thing that earns.
    /// </para>
    /// </remarks>
    public sealed class Raid
    {
        /// <summary>Hard cap on a raid, in seconds.</summary>
        public const float RaidSeconds = 60f;

        /// <summary>Energy a spawn costs.</summary>
        public const float SpawnCost = 25f;

        /// <summary>Energy a trap firing costs.</summary>
        public const float TrapCost = 40f;

        /// <summary>Damage a trap deals to the party.</summary>
        public const float TrapDamage = 34f;

        /// <summary>Seconds a trap needs before it can fire again.</summary>
        public const float TrapCooldown = 6f;

        /// <summary>
        /// Energy the core starts a raid holding.
        /// </summary>
        /// <remarks>
        /// Without this the game cannot be played at all. An idle party earns 0.05/s by design, and
        /// the cheapest verb costs 25, so a player starting from zero would wait five hundred
        /// seconds before their first action was affordable -- inside a sixty-second raid. The core
        /// is meant to already be charged when the doors open; this is that charge, and it is sized
        /// to buy two spawns and a trap.
        /// </remarks>
        public const float StartingEnergy = 100f;

        private float _trapReadyAt;
        private readonly Dictionary<object, float> _lastHealth = new();

        /// <summary>
        /// Recent damage and healing, for the view to float off the fight.
        /// </summary>
        /// <remarks>
        /// Filled by diffing health each tick rather than by having every damage path report itself.
        /// One place to get right, and it cannot miss a source: a trap firing, a mob biting and a
        /// healer casting all show up the same way, including any source added later.
        /// </remarks>
        public CombatFeed Feed { get; } = new();

        /// <summary>Arrows and bolts currently in the air.</summary>
        public ProjectileFeed Shots { get; } = new();

        /// <summary>One-shot events -- the three verbs and monster deaths -- awaiting a burst.</summary>
        public EffectFeed Effects { get; } = new();

        private readonly System.Random _random;

        /// <summary>The dungeon being raided.</summary>
        public DungeonLayout Layout { get; }

        /// <summary>The party currently inside.</summary>
        public Party Party { get; }

        /// <summary>Every monster in the dungeon.</summary>
        public MobPack Mobs { get; }

        /// <summary>Seconds left on the clock.</summary>
        public float TimeRemaining { get; private set; } = RaidSeconds;

        /// <summary>Energy available to spend on verbs. Starts at <see cref="StartingEnergy"/>.</summary>
        public float TotalEnergy { get; private set; }

        /// <summary>
        /// Energy harvested from the party during this raid. <b>This is the score.</b>
        /// </summary>
        /// <remarks>
        /// Kept separate from the spendable balance on purpose. The starting charge is a float the
        /// player is lent so the first verb is affordable, and counting it as score would flatter
        /// every raid equally -- a walkthrough and a perfectly milked minute would open only 100
        /// apart in relative terms. The league ranks what was taken from the adventurers, nothing
        /// else.
        /// </remarks>
        public float EnergyHarvested { get; private set; }

        /// <summary>Energy per second at this instant, for the big pulsing number.</summary>
        public float CurrentRate { get; private set; }

        /// <summary>Why the raid stopped, or <see cref="RaidOutcome.InProgress"/>.</summary>
        public RaidOutcome Outcome { get; private set; } = RaidOutcome.InProgress;

        /// <summary>Whether the raid is still earning.</summary>
        public bool IsRunning => Outcome == RaidOutcome.InProgress;

        /// <summary>Whether a trap can fire right now.</summary>
        public bool IsTrapReady => _trapReadyAt <= 0f;

        /// <summary>Starts a raid on a freshly built dungeon.</summary>
        /// <param name="layout">Dungeon to raid.</param>
        /// <param name="bonusEnergy">
        /// Extra spendable energy carried in from the shop's Ready button. It is spendable but is
        /// not score -- <see cref="EnergyHarvested"/> still counts only what was taken from the
        /// adventurers, so a player cannot climb the league by skipping shops.
        /// </param>
        /// <param name="composition">
        /// Which party walks in. Null means the balanced one, which is what a new player should meet
        /// first and what every test that does not care about the roster gets.
        /// </param>
        /// <param name="seed">
        /// Seed for every combat roll in this raid. Fixed by default so a test, a screenshot and a
        /// replayed bug report all see the identical fight; the game passes a real seed per raid.
        /// </param>
        public Raid(DungeonLayout layout, float bonusEnergy = 0f,
            PartyComposition composition = null, int seed = 20260813)
        {
            _random = new System.Random(seed);
            // Start already at the idle rate rather than easing up from zero, so the HUD opens on
            // the true number instead of a fifth of a second of meaningless ramp.
            CurrentRate = EnergyCurve.Rate(0, 1f);
            TotalEnergy = StartingEnergy + Mathf.Max(0f, bonusEnergy);
            Layout = layout;
            Party = new Party(layout.Grid, layout.EntranceCell, layout.BossCell, composition,
                layout.RoomCentres);
            Mobs = new MobPack(layout.Grid);
        }

        /// <summary>
        /// Advances the raid by one step: combat, movement, energy, then end conditions.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            _trapReadyAt = Mathf.Max(0f, _trapReadyAt - deltaTime);

            int partyRoom = Layout.Grid.RoomAt(Party.Cell);
            int threats = Mobs.CountInRoom(partyRoom);

            ResolveCombat(deltaTime, threats);
            // Every living member's position, so a monster can pick the nearest rather than always
            // chasing whoever leads. Flattened to plain vectors here because MobManager and
            // PartyManager are siblings that must never reference each other.
            _partyPositions.Clear();
            foreach (PartyManager.Adventurer member in Party.Living)
            {
                _partyPositions.Add(member.Position);
            }

            Mobs.Tick(deltaTime, _partyPositions);

            // Flatten the mobs the party can see into bare coordinates. PartyManager and MobManager
            // are siblings that must never reference each other, so the raid is the only place that
            // knows about both, and it hands the party positions rather than monsters.
            int room = Layout.Grid.RoomAt(Party.Cell);
            var visible = Mobs.Living
                .Where(mob => Layout.Grid.RoomAt(mob.Cell) == room)
                .Select(mob => mob.Position)
                .ToList();

            Party.Tick(deltaTime, visible, Layout.ArmedTrapCells(), Layout.ChestCells);

            // A teleport with no visual reads as the sprite glitching across the room, so the blink
            // draws its own streak -- the same presentation an arrow gets, in the mage's colour.
            if (Party.BlinkedFrom.HasValue && Party.BlinkedTo.HasValue)
            {
                Shots.Fire(Party.BlinkedFrom.Value, Party.BlinkedTo.Value, ShotKind.Bolt);
            }

            // The party reports the work it did; the dungeon owns the trap and applies it. Keeps
            // PartyManager from having to know what a trap is beyond a cell to stand on.
            if (Party.DisarmingCell.HasValue)
            {
                Layout.TrapAt(Party.DisarmingCell.Value)?.Disarm(Party.DisarmSeconds);
            }

            ChargeForDeathsAndLoot(deltaTime);
            AccrueEnergy(deltaTime);
            RecordCombatNumbers();
            Feed.Tick(deltaTime);
            Shots.Tick(deltaTime);

            TimeRemaining = Mathf.Max(0f, TimeRemaining - deltaTime);
            UpdateOutcome();
        }

        /// <summary>
        /// Turns this tick's health changes into floating numbers.
        /// </summary>
        /// <remarks>
        /// Everything that can wound or mend goes through health, so watching health catches every
        /// source without each one having to remember to report itself -- and a source added later is
        /// caught for free.
        /// </remarks>
        private void RecordCombatNumbers()
        {
            foreach (PartyManager.Adventurer member in Party.Members)
            {
                Record(member, member.Position, member.HealthFraction * member.MaxHealth,
                    CombatTarget.Adventurer);
            }

            foreach (Mob mob in Mobs.Mobs)
            {
                // A monster dying is the moment the player's stall ends, so it is worth its own
                // burst. Caught here because the health diff already runs -- the transition to zero
                // is the same signal, read one step further.
                bool wasAlive = _lastHealth.TryGetValue(mob, out float previous) && previous > 0f;
                Record(mob, mob.Position, mob.HealthFraction * mob.MaxHealth, CombatTarget.Monster);

                if (wasAlive && !mob.IsAlive)
                {
                    Effects.Raise(EffectKind.MobDied, mob.Position);
                }
            }
        }

        /// <summary>Compares one combatant's health against last tick and feeds the difference.</summary>
        /// <param name="who">Combatant, used only as a key.</param>
        /// <param name="position">Where to show the number.</param>
        /// <param name="health">Health right now.</param>
        /// <param name="target">Whether this is an adventurer or a monster.</param>
        private void Record(object who, Vector2 position, float health, CombatTarget target)
        {
            if (!_lastHealth.TryGetValue(who, out float previous))
            {
                _lastHealth[who] = health;
                return;
            }

            _lastHealth[who] = health;
            float change = health - previous;

            if (change < 0f)
            {
                Feed.Damage(who, position, -change, target);
            }
            else if (change > 0f)
            {
                Feed.Heal(position, change);
            }
        }

        /// <summary>
        /// Toggles a door. The cheap, spammable verb -- and the only way to save a losing party.
        /// </summary>
        /// <param name="cell">Cell of the door to toggle.</param>
        /// <returns>True when a door was found and toggled.</returns>
        public bool ToggleDoor(Vector2Int cell)
        {
            Door door = Layout.Grid.DoorAt(cell);
            if (door == null)
            {
                return false;
            }

            // A door the party has picked or broken is jammed open for the rest of the raid. Without
            // this the whole mechanic is theatre: the player would simply shut it again the instant
            // the archer finished, and a single door would stall a party forever.
            if (door.IsForced)
            {
                return false;
            }

            door.IsOpen = !door.IsOpen;
            Effects.Raise(EffectKind.DoorToggled, cell);
            return true;
        }

        /// <summary>
        /// Spawns a monster at a spawner, if the player can afford it.
        /// </summary>
        /// <param name="cell">Spawner cell to fire.</param>
        /// <param name="kind">
        /// Monster to spawn. Left null the spawner decides: the ones the dungeon was built with
        /// produce skeletons, and a slime spawner bought in the shop produces slimes.
        /// </param>
        /// <returns>True when a monster was spawned.</returns>
        public bool SpawnMob(Vector2Int cell, MobKind? kind = null)
        {
            if (TotalEnergy < SpawnCost || !IsRunning)
            {
                return false;
            }

            MobKind spawning = kind ??
                (Layout.SpawnerTierAt(cell) == 0 ? MobKind.Slime : MobKind.Skeleton);

            if (Mobs.Spawn(spawning, cell) == null)
            {
                return false;
            }

            TotalEnergy -= SpawnCost;
            Effects.Raise(EffectKind.MobSpawned, cell);
            return true;
        }

        /// <summary>
        /// Fires a trap, wounding the party if it is standing on it.
        /// </summary>
        /// <param name="cell">Trap cell to fire.</param>
        /// <returns>True when the trap fired.</returns>
        public bool FireTrap(Vector2Int cell)
        {
            if (!IsRunning || !IsTrapReady || TotalEnergy < TrapCost)
            {
                return false;
            }

            // A trap the rogue already defused is spent. This is the pressure the disarm timer
            // creates: use it before they reach it, or lose it.
            Trap trap = Layout.TrapAt(cell);
            if (trap is not { IsArmed: true })
            {
                return false;
            }

            TotalEnergy -= TrapCost;
            _trapReadyAt = TrapCooldown;
            trap.Fire();
            Effects.Raise(EffectKind.TrapFired, cell);

            // Anyone standing on the plate takes it, not just whoever happens to be leading.
            foreach (PartyManager.Adventurer member in Party.Living)
            {
                if (member.Cell == cell)
                {
                    member.TakeDamage(TrapDamage);
                }
            }

            return true;
        }

        /// <summary>
        /// Trades blows between the party and whatever shares its room.
        /// </summary>
        /// <remarks>
        /// Discrete rolled attacks on individual cooldowns, rather than a continuous trickle of
        /// damage-per-second. The trickle was invisible: two health bars slowly shortened and the
        /// player could not see who was hitting whom, or when. Separate blows give every hit a
        /// number, an arrow and a moment.
        /// <para>
        /// Expected output is unchanged. Each stat block is derived from the damage-per-second the
        /// game was balanced around, and <c>CombatStatsTests</c> fails if a fight gets longer or
        /// shorter than it used to be.
        /// </para>
        /// </remarks>
        private void ResolveCombat(float deltaTime, int threats)
        {
            int room = Layout.Grid.RoomAt(Party.Cell);
            var engaged = new List<Mob>();
            foreach (Mob mob in Mobs.Living)
            {
                if (Layout.Grid.RoomAt(mob.Cell) == room)
                {
                    engaged.Add(mob);
                }
            }

            SwingParty(deltaTime, engaged);
            SwingMobs(deltaTime, engaged);
        }

        /// <summary>Lets every adventurer who can reach something take its swing.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="engaged">Living mobs sharing the party's room.</param>
        private void SwingParty(float deltaTime, List<Mob> engaged)
        {
            foreach (PartyManager.Adventurer member in Party.Living)
            {
                member.AttackCooldown -= deltaTime;
                if (member.AttackCooldown > 0f || engaged.Count == 0)
                {
                    continue;
                }

                // The ranged pair shoot across the room; everyone else has to be in reach.
                bool shoots = member.Role is PartyManager.AdventurerRole.Ranged
                    or PartyManager.AdventurerRole.Mage;

                Mob target = Nearest(engaged, member.Position);
                float distance = Vector2.Distance(member.Position, target.Position);
                if (!shoots && distance > PartyManager.Party.MeleeReach)
                {
                    continue;
                }

                // A mage's bolt is a spell and costs mana. A drained mage stops shooting, which is
                // what makes spending the pool on blinking a real decision rather than free safety.
                if (member.Role == PartyManager.AdventurerRole.Mage &&
                    !member.SpendMana(PartyManager.Adventurer.BoltManaCost))
                {
                    continue;
                }

                member.AttackCooldown = member.AttackInterval;
                member.LastAttackTarget = target.Position;
                target.TakeDamage(CombatMath.Roll(
                    member.WeaponDamage, member.Might, target.Armour, _random));

                if (shoots)
                {
                    Shots.Fire(member.Position, target.Position,
                        member.Role == PartyManager.AdventurerRole.Ranged
                            ? ShotKind.Arrow
                            : ShotKind.Bolt);
                }
            }
        }

        /// <summary>Lets every engaged monster take its swing at whoever it can reach.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="engaged">Living mobs sharing the party's room.</param>
        private void SwingMobs(float deltaTime, List<Mob> engaged)
        {
            foreach (Mob mob in engaged)
            {
                mob.AttackCooldown -= deltaTime;
                if (mob.AttackCooldown > 0f)
                {
                    continue;
                }

                // Mobs hit whoever is nearest, and the tank leads -- so the tank is almost always
                // who they hit. That falls out of the marching order rather than being a rule, and
                // it is why a party with no tank suffers so badly.
                PartyManager.Adventurer target = null;
                float best = PartyManager.Party.MeleeReach;
                foreach (PartyManager.Adventurer member in Party.Living)
                {
                    float distance = Vector2.Distance(member.Position, mob.Position);
                    if (distance <= best)
                    {
                        best = distance;
                        target = member;
                    }
                }

                if (target == null)
                {
                    continue;
                }

                mob.AttackCooldown = mob.AttackInterval;
                mob.LastAttackTarget = target.Position;
                target.TakeDamage(CombatMath.Roll(
                    mob.WeaponDamage, mob.Might, target.Armour, _random));
            }
        }

        /// <summary>The living mob nearest a point.</summary>
        /// <param name="mobs">Mobs to search.</param>
        /// <param name="from">Point to measure from.</param>
        /// <returns>The nearest mob.</returns>
        private static Mob Nearest(List<Mob> mobs, Vector2 from)
        {
            Mob best = mobs[0];
            float bestDistance = Vector2.Distance(from, best.Position);

            for (int i = 1; i < mobs.Count; i++)
            {
                float distance = Vector2.Distance(from, mobs[i].Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = mobs[i];
                }
            }

            return best;
        }

        /// <summary>
        /// Seconds the rate takes to cover most of the distance to a new value.
        /// </summary>
        /// <remarks>
        /// Engagement is binary -- a party is in combat or it is not -- so the raw curve steps
        /// straight from the idle 0.05/s to 4/s or beyond the instant a fight begins. Measured, that
        /// is a <b>4.03/s jump in a single frame</b>, and on screen the game's largest number simply
        /// teleports. Easing it over a fifth of a second reads as the core spinning up.
        /// <para>
        /// Energy accrues at the eased rate, not the raw one, so the number on screen and the money
        /// in the bank are the same number. A HUD that displayed one figure while paying another
        /// would be a worse bug than the flicker.
        /// </para>
        /// </remarks>
        public const float RateEaseSeconds = 0.22f;

        /// <summary>Scratch list of living member positions, reused so the tick allocates nothing.</summary>
        private readonly System.Collections.Generic.List<Vector2> _partyPositions = new();

        /// <summary>Seconds of chest bonus still owed to the team.</summary>
        private float _chestBonusLeft;

        /// <summary>How much of a full chest bonus the current one pays.</summary>
        private float _chestBonusScale = 1f;

        /// <summary>Seconds since a chest was last opened, for the diminishing return.</summary>
        private float _sinceLastChest = EnergyCurve.ChestCooldownSeconds;

        /// <summary>Living members last tick, so a death can be noticed and charged for.</summary>
        private int _livingLastTick = -1;

        /// <summary>
        /// Charges the player for anyone who died, and starts a chest bonus for anything looted.
        /// </summary>
        /// <remarks>
        /// A death costs banked score rather than spendable energy. It is a judgement on the raid,
        /// and taking it out of the purse would punish the player twice -- once in the score and
        /// again by removing the means to stop the next death.
        /// </remarks>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        private void ChargeForDeathsAndLoot(float deltaTime)
        {
            _sinceLastChest += deltaTime;

            int living = Party.LivingCount;
            if (_livingLastTick >= 0 && living < _livingLastTick)
            {
                float lost = (_livingLastTick - living) * EnergyCurve.DeathPenalty;
                EnergyHarvested = Mathf.Max(0f, EnergyHarvested - lost);

                // Shown where it happened, as a damage number against the dungeon rather than
                // against a monster. A penalty the player is charged silently is a penalty they
                // never learn from.
                Feed.Damage(this, Party.Position, lost, CombatTarget.Dungeon);
            }

            _livingLastTick = living;

            int looted = Party.LootedCount;
            if (looted > _lootedLastTick)
            {
                _chestBonusScale = EnergyCurve.ChestValue(_sinceLastChest);
                _chestBonusLeft = EnergyCurve.ChestBonusSeconds;
                _sinceLastChest = 0f;
            }

            _lootedLastTick = looted;
        }

        /// <summary>Chests looted as of last tick, so a new one can be noticed.</summary>
        private int _lootedLastTick;

        /// <summary>Applies the energy curve for this instant and banks the result.</summary>
        private void AccrueEnergy(float deltaTime)
        {
            // Summed per living member, each priced by what it is doing and by its own health.
            // The old curve asked one yes-or-no question of the whole party and multiplied by its
            // worst survivor, which made being wounded the only thing that paid: a party that could
            // not be hurt earned a ninth of one that could, and every attempt to let fragile parties
            // survive inverted the design's one rule. A corpse contributes nothing, which is most of
            // what keeps a wipe bad.
            float target = 0f;
            foreach (PartyManager.Adventurer member in Party.Living)
            {
                target += EnergyCurve.MemberRate(member.Action, member.HealthFraction);
            }

            // A chest pays the whole team for a few seconds, and pays less if another was opened
            // recently -- otherwise a floor of chests is a flat raise for the raid.
            if (_chestBonusLeft > 0f)
            {
                _chestBonusLeft = Mathf.Max(0f, _chestBonusLeft - deltaTime);
                target += EnergyCurve.ChestBonus * _chestBonusScale;
            }

            CurrentRate = Mathf.Lerp(
                CurrentRate, target, Mathf.Clamp01(deltaTime / RateEaseSeconds));

            float earned = CurrentRate * deltaTime;
            TotalEnergy += earned;
            EnergyHarvested += earned;
        }

        /// <summary>Checks the three ways a raid can stop, in priority order.</summary>
        private void UpdateOutcome()
        {
            if (Party.Goal == PartyGoal.Wiped || Party.LivingCount == 0)
            {
                Outcome = RaidOutcome.PartyWiped;
                CurrentRate = 0f;
                return;
            }

            if (Party.Goal == PartyGoal.Escaped)
            {
                Outcome = RaidOutcome.PartyEscaped;
                CurrentRate = 0f;
                return;
            }

            if (TimeRemaining <= 0f)
            {
                Outcome = RaidOutcome.TimeExpired;
                CurrentRate = 0f;
            }
        }
    }
}
