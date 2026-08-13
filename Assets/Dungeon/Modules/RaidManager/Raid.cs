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
        public Raid(DungeonLayout layout, float bonusEnergy = 0f,
            PartyComposition composition = null)
        {
            TotalEnergy = StartingEnergy + Mathf.Max(0f, bonusEnergy);
            Layout = layout;
            Party = new Party(layout.Grid, layout.EntranceCell, layout.BossCell, composition);
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
            Mobs.Tick(deltaTime, Party.Position);

            // Flatten the mobs the party can see into bare coordinates. PartyManager and MobManager
            // are siblings that must never reference each other, so the raid is the only place that
            // knows about both, and it hands the party positions rather than monsters.
            int room = Layout.Grid.RoomAt(Party.Cell);
            var visible = Mobs.Living
                .Where(mob => Layout.Grid.RoomAt(mob.Cell) == room)
                .Select(mob => mob.Position)
                .ToList();

            Party.Tick(deltaTime, visible, Layout.ArmedTrapCells(), Layout.ChestCells);

            // The party reports the work it did; the dungeon owns the trap and applies it. Keeps
            // PartyManager from having to know what a trap is beyond a cell to stand on.
            if (Party.DisarmingCell.HasValue)
            {
                Layout.TrapAt(Party.DisarmingCell.Value)?.Disarm(Party.DisarmSeconds);
            }

            AccrueEnergy(deltaTime);

            TimeRemaining = Mathf.Max(0f, TimeRemaining - deltaTime);
            UpdateOutcome();
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

            door.IsOpen = !door.IsOpen;
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

        /// <summary>Trades damage between the party and whatever shares its room.</summary>
        private void ResolveCombat(float deltaTime, int threats)
        {
            if (threats <= 0)
            {
                return;
            }

            Mobs.DistributeDamage(Party.DamageOutput() * deltaTime, Party.Cell);
            Party.DistributeDamage(Mobs.DamageOutputAgainst(Party.Cell) * deltaTime);
        }

        /// <summary>Applies the energy curve for this instant and banks the result.</summary>
        private void AccrueEnergy(float deltaTime)
        {
            int engaged = Party.Goal == PartyGoal.Fighting ? Party.LivingCount : 0;
            CurrentRate = EnergyCurve.Rate(engaged, Party.HealthFraction);

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
