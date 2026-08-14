using Dungeon.DungeonManager;
using Dungeon.MobManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Pins the spawn stake: energy leaves the core while a monster lives and comes back when it
    /// dies.
    /// </summary>
    /// <remarks>
    /// Spawning used to be a flat purchase, which made the arithmetic argue against the design. A
    /// monster that cost 25 and might be killed in four seconds had to earn its price back before it
    /// was worth pressing, so the optimal play was to hoard — in a game whose whole premise is a
    /// dungeon full of monsters the party is grinding through.
    /// <para>
    /// As a loan the cost becomes a <i>risk</i> rather than a fee: the player is only ever out of
    /// pocket for monsters still standing when the clock stops. That is a bet on the party being
    /// slow, which is exactly the bet the game wants them making.
    /// </para>
    /// </remarks>
    public sealed class SpawnRefundTests
    {
        /// <summary>A dungeon with a spawner the player can actually use.</summary>
        /// <returns>The layout.</returns>
        private static DungeonLayout Furnished()
        {
            return DungeonLayout.BuildCorridor();
        }

        /// <summary>
        /// Spawning takes the stake out of the purse straight away.
        /// </summary>
        [Test]
        public void Spawning_TakesTheStakeUpFront()
        {
            DungeonLayout layout = Furnished();
            var raid = new Raid(layout);
            float before = raid.TotalEnergy;

            Assert.IsTrue(raid.SpawnMob(layout.SpawnerCells[0]), "the spawner should fire");

            Assert.AreEqual(before - Raid.SpawnCost, raid.TotalEnergy, 0.01f,
                "a live monster should still be holding the player's energy");
        }

        /// <summary>
        /// Killing a spawned monster returns its stake to the purse.
        /// </summary>
        /// <remarks>
        /// The whole point of the change: a spawn the party fights and kills costs nothing, so the
        /// player can keep the dungeon stocked instead of rationing.
        /// </remarks>
        [Test]
        public void KillingASpawnedMonster_ReturnsTheStake()
        {
            DungeonLayout layout = Furnished();
            var raid = new Raid(layout);
            float before = raid.TotalEnergy;

            Assert.IsTrue(raid.SpawnMob(layout.SpawnerCells[0]), "the spawner should fire");
            Mob spawned = raid.Mobs.Mobs[raid.Mobs.Mobs.Count - 1];

            // A tick to register it alive, THEN kill it. Death is spotted by comparing health
            // against last tick, so a monster that is spawned and killed inside one frame was never
            // seen alive and never dies -- which cannot happen in play, but is easy to write here.
            raid.Tick(0.02f);
            spawned.TakeDamage(spawned.MaxHealth * 2f);
            raid.Tick(0.02f);

            MooseRunnerFacade.Log(
                $"purse {before:F0} -> {raid.TotalEnergy:F0} across a spawn and a kill");

            Assert.IsFalse(spawned.IsAlive, "the test needs the monster dead");
            Assert.AreEqual(before, raid.TotalEnergy, 0.01f,
                "a monster the party killed should have cost the player nothing");
        }

        /// <summary>
        /// A monster still alive at the end of the raid is never refunded.
        /// </summary>
        /// <remarks>
        /// The risk side of the loan. Without this the stake would be free money with a delay, and
        /// spawning would carry no decision at all.
        /// </remarks>
        [Test]
        public void AMonsterLeftStanding_IsNeverRefunded()
        {
            DungeonLayout layout = Furnished();
            var raid = new Raid(layout);
            float before = raid.TotalEnergy;

            Assert.IsTrue(raid.SpawnMob(layout.SpawnerCells[0]), "the spawner should fire");

            for (int step = 0; step < 200; step++)
            {
                raid.Tick(0.02f);
            }

            Mob spawned = raid.Mobs.Mobs[raid.Mobs.Mobs.Count - 1];
            if (!spawned.IsAlive)
            {
                Assert.Ignore("the party reached and killed it, which this case is not about");
            }

            Assert.Less(raid.TotalEnergy, before,
                "a monster still standing should still be holding the player's energy");
        }

        /// <summary>
        /// A monster nobody paid for is not refunded when it dies.
        /// </summary>
        /// <remarks>
        /// Tests and the dungeon itself can put monsters straight into the pack. Refunding those
        /// would mint energy from nothing, and every sweep that spawns freely would quietly report a
        /// richer economy than the game has.
        /// </remarks>
        [Test]
        public void AnUnpaidMonster_MintsNothing()
        {
            DungeonLayout layout = Furnished();
            var raid = new Raid(layout);
            float before = raid.TotalEnergy;

            Mob free = raid.Mobs.Spawn(MobKind.Slime, layout.SpawnerCells[0]);
            Assert.IsNotNull(free, "the test needs a monster");

            raid.Tick(0.02f);
            free.TakeDamage(free.MaxHealth * 2f);
            raid.Tick(0.02f);

            Assert.IsFalse(free.IsAlive, "the test needs the monster dead");
            Assert.LessOrEqual(raid.TotalEnergy, before + 0.5f,
                $"killing a monster nobody bought paid out {raid.TotalEnergy - before:F1} energy");
        }

        /// <summary>
        /// The refund is announced, so the player can see the loan come back.
        /// </summary>
        [Test]
        public void TheRefund_IsShownToThePlayer()
        {
            DungeonLayout layout = Furnished();
            var raid = new Raid(layout);

            Assert.IsTrue(raid.SpawnMob(layout.SpawnerCells[0]), "the spawner should fire");
            Mob spawned = raid.Mobs.Mobs[raid.Mobs.Mobs.Count - 1];
            raid.Tick(0.02f);
            spawned.TakeDamage(spawned.MaxHealth * 2f);

            raid.Effects.Drain();
            raid.Tick(0.02f);

            bool announced = false;
            foreach (Effect effect in raid.Effects.Pending)
            {
                announced |= effect.Kind == EffectKind.SpawnRefunded;
            }

            // The channel the player actually reads. The effect above carries no visual of its own
            // on purpose: it lands on the same tick and the same spot as the monster's death burst,
            // so a second burst there is noise -- and an effect kind with no case of its own falls
            // through to the DOOR visual and chime, which would say something opened.
            bool numbered = false;
            foreach (CombatNumber number in raid.Feed.Numbers)
            {
                numbered |= number.IsHeal && number.Amount == Mathf.RoundToInt(Raid.SpawnCost);
            }

            Assert.IsTrue(announced,
                "a refund the player never sees leaves them hoarding against a cost that is not "
                + "really there");
            Assert.IsTrue(numbered,
                $"no +{Raid.SpawnCost:F0} rose off the corpse, so the loan comes back invisibly");
        }
    }
}
