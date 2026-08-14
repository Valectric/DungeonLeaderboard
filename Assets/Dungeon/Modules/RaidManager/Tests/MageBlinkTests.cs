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

        /// <summary>
        /// The casters carry a mana pool each; the tank and the archer do not.
        /// </summary>
        /// <remarks>
        /// Per caster rather than per party. Two healers really do have twice the sustain, and a
        /// healer can run dry on its own while the mage still has plenty -- which is the whole point
        /// of showing each of them a bar.
        /// </remarks>
        [Test]
        public void OnlyTheCasters_HaveMana()
        {
            foreach (AdventurerRole role in System.Enum.GetValues(typeof(AdventurerRole)))
            {
                var member = new Adventurer(role, Vector2Int.zero);
                bool caster = role is AdventurerRole.Mage or AdventurerRole.Healer;

                if (caster)
                {
                    Assert.Greater(member.MaxMana, 0f, $"{role} should have a pool");
                    Assert.AreEqual(1f, member.ManaFraction, 0.001f, $"{role} should start full");
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
        /// A mage pressed by a monster blinks clear, and pays for it.
        /// </summary>
        /// <remarks>
        /// Observed rather than staged. This used to drop a skeleton on the mage and pin it there,
        /// which stopped working when monsters began chasing the <i>nearest</i> party member: the
        /// bully promptly left for the tank and the mage was never cornered, so it walked instead.
        /// Holding the bully in place by hand did not help either — a mage cornered where it happens
        /// to be standing often has nowhere legal to land, because a five-cell jump from near a wall
        /// leaves the dungeon and the "never blink somewhere no better" guard correctly refuses.
        /// <para>
        /// So this spawns pressure and watches for the blink the mage chooses on its own, which is
        /// the behaviour that matters and the one a player sees.
        /// </para>
        /// </remarks>
        [Test]
        public void APressedMage_BlinksClearAndPaysForIt()
        {
            Raid raid = AdvancedRaid();
            float manaBefore = MageIn(raid).ManaFraction;

            bool blinked = false;
            float furthestJump = 0f;
            float manaAfter = manaBefore;

            while (raid.IsRunning && !blinked)
            {
                Adventurer mage = MageIn(raid);
                if (mage == null)
                {
                    break;
                }

                // Press a monster onto the mage itself. Spawning at the spawners does not corner it:
                // monsters chase the NEAREST member, so they meet the tank and the mage never has to
                // blink -- which is the tank doing its job, not a bug.
                if (raid.Mobs.CountInRoom(raid.Layout.Grid.RoomAt(mage.Cell)) == 0)
                {
                    Mob bully = raid.Mobs.Spawn(MobKind.Skeleton, mage.Cell);
                    if (bully != null)
                    {
                        bully.Position = mage.Position;
                    }
                }

                Vector2 before = mage.Position;
                raid.Tick(0.02f);

                furthestJump = Mathf.Max(furthestJump, Vector2.Distance(before, mage.Position));
                blinked = raid.Party.BlinkedTo.HasValue;
                manaAfter = mage.ManaFraction;
            }

            MooseRunnerFacade.Log(
                $"mage blinked={blinked}, largest single step {furthestJump:F2} cells, "
                + $"mana {manaBefore:P0} -> {manaAfter:P0}");

            Assert.IsTrue(blinked, "the mage never blinked despite a monster standing on it");
            Assert.Greater(furthestJump, 2f, "a blink should move the mage further than a walk can");
            Assert.Less(manaAfter, manaBefore, "the blink should have cost mana");
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
