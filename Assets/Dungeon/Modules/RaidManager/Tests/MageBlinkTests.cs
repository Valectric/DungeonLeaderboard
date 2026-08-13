using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Verifies the mage's mana pool and its blink escape.
    /// </summary>
    /// <remarks>
    /// The interesting state these create is a mage that has spent its pool on bolts and cannot buy
    /// its way out of a skeleton's reach. That only exists if spending genuinely runs the pool dry
    /// and blinking genuinely costs, so both are asserted rather than assumed.
    /// </remarks>
    public sealed class MageBlinkTests
    {
        /// <summary>Runs a raid to the point the party is inside the second room.</summary>
        /// <returns>The raid.</returns>
        private static Raid AdvancedRaid()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);

            while (raid.IsRunning &&
                   Vector2.Distance(raid.Party.Position, layout.SpawnerCells[0]) > 2.5f)
            {
                raid.Tick(0.02f);
            }

            return raid;
        }

        /// <summary>The mage in a raid.</summary>
        private static Adventurer MageIn(Raid raid)
        {
            foreach (Adventurer member in raid.Party.Living)
            {
                if (member.Role == AdventurerRole.Mage)
                {
                    return member;
                }
            }

            return null;
        }

        /// <summary>Only the mage carries a mana pool.</summary>
        [Test]
        public void OnlyTheMage_HasMana()
        {
            foreach (AdventurerRole role in System.Enum.GetValues(typeof(AdventurerRole)))
            {
                var member = new Adventurer(role, Vector2Int.zero);
                if (role == AdventurerRole.Mage)
                {
                    Assert.Greater(member.MaxMana, 0f, "the mage should have a pool");
                    Assert.AreEqual(1f, member.ManaFraction, 0.001f, "and start full");
                }
                else
                {
                    Assert.AreEqual(0f, member.MaxMana, 0.001f, $"{role} should have no mana bar");
                }
            }
        }

        /// <summary>Casting spends mana and it refills over time.</summary>
        [Test]
        public void Mana_IsSpentByCastingAndRefillsOverTime()
        {
            var mage = new Adventurer(AdventurerRole.Mage, Vector2Int.zero);

            Assert.IsTrue(mage.SpendMana(Adventurer.BlinkManaCost), "a full pool should cover a blink");
            float afterCast = mage.ManaFraction;
            Assert.Less(afterCast, 1f, "casting should have cost something");

            mage.RegenerateMana(2f);
            MooseRunnerFacade.Log(
                $"mana {afterCast:P0} after a blink, {mage.ManaFraction:P0} two seconds later");
            Assert.Greater(mage.ManaFraction, afterCast, "mana should refill over time");
        }

        /// <summary>An empty pool refuses the cast rather than going negative.</summary>
        [Test]
        public void AnEmptyPool_RefusesToCast()
        {
            var mage = new Adventurer(AdventurerRole.Mage, Vector2Int.zero);

            int casts = 0;
            while (mage.SpendMana(Adventurer.BoltManaCost) && casts < 100)
            {
                casts++;
            }

            MooseRunnerFacade.Log($"a full pool paid for {casts} bolts");
            Assert.Greater(casts, 5, "the pool should be worth a meaningful number of bolts");
            Assert.IsFalse(mage.CanCast(Adventurer.BoltManaCost), "the pool should now be dry");
            Assert.GreaterOrEqual(mage.ManaFraction, 0f, "mana must never go negative");
        }

        /// <summary>
        /// A mage with a monster on top of it blinks clear, and pays for it.
        /// </summary>
        [Test]
        public void ACorneredMage_BlinksClearAndPaysForIt()
        {
            Raid raid = AdvancedRaid();
            Adventurer mage = MageIn(raid);

            Mob bully = raid.Mobs.Spawn(MobKind.Skeleton, mage.Cell);
            Assert.IsNotNull(bully, "the test needs a monster beside the mage");
            bully.Position = mage.Position;

            float manaBefore = mage.ManaFraction;
            Vector2 startedAt = mage.Position;

            for (int step = 0; step < 40 && raid.IsRunning; step++)
            {
                raid.Tick(0.02f);
            }

            float jumped = Vector2.Distance(startedAt, mage.Position);
            MooseRunnerFacade.Log(
                $"mage moved {jumped:F1} cells, mana {manaBefore:P0} -> {mage.ManaFraction:P0}");

            Assert.Greater(jumped, 2f, "the mage should have blinked, not walked");
            Assert.Less(mage.ManaFraction, manaBefore, "the blink should have cost mana");
        }

        /// <summary>A blink always lands somewhere walkable inside the dungeon.</summary>
        /// <remarks>
        /// A mage inside a wall would be unreachable, unkillable and would stall the raid until the
        /// clock ran out.
        /// </remarks>
        [Test]
        public void ABlink_AlwaysLandsOnWalkableFloor()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            int blinks = 0;

            // Keep a monster on the mage for the whole raid so it blinks repeatedly.
            while (raid.IsRunning)
            {
                Adventurer mage = MageIn(raid);
                if (mage != null && raid.Mobs.CountInRoom(layout.Grid.RoomAt(mage.Cell)) == 0)
                {
                    Mob spawned = raid.Mobs.Spawn(MobKind.Slime, mage.Cell);
                    if (spawned != null)
                    {
                        spawned.Position = mage.Position;
                    }
                }

                raid.Tick(0.02f);

                if (raid.Party.BlinkedTo.HasValue)
                {
                    blinks++;
                    Vector2 landed = raid.Party.BlinkedTo.Value;
                    var cell = new Vector2Int(
                        Mathf.RoundToInt(landed.x), Mathf.RoundToInt(landed.y));

                    Assert.IsTrue(layout.Grid.IsWalkable(cell), $"blinked into a wall at {cell}");
                    Assert.AreNotEqual(DungeonGrid.NoRoom, layout.Grid.RoomAt(cell),
                        $"blinked outside the dungeon to {cell}");
                }
            }

            MooseRunnerFacade.Log($"the mage blinked {blinks} times, always onto floor");
            Assert.Greater(blinks, 0, "the mage never blinked, so nothing was actually checked");
        }

        /// <summary>A drained mage stops shooting, which is the cost of blinking too often.</summary>
        [Test]
        public void ADrainedMage_StopsCastingBolts()
        {
            var mage = new Adventurer(AdventurerRole.Mage, Vector2Int.zero);
            while (mage.SpendMana(Adventurer.BoltManaCost))
            {
            }

            Assert.IsFalse(mage.CanCast(Adventurer.BoltManaCost),
                "a dry mage should not be able to pay for a bolt");
            Assert.IsFalse(mage.CanCast(Adventurer.BlinkManaCost),
                "and certainly not for a blink, which is the trade the pool creates");
        }
    }
}
