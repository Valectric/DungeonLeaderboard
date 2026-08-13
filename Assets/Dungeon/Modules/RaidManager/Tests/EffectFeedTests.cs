using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Verifies that every player action and every monster death announces itself.
    /// </summary>
    /// <remarks>
    /// SPEC.md: <b>"Juice matters more than content."</b> All three verbs used to be actions with no
    /// acknowledgement at all -- the player pressed a thing and the game appeared not to notice,
    /// beyond a number changing elsewhere on screen. These assertions exist so a verb cannot quietly
    /// lose its feedback again during a refactor.
    /// </remarks>
    public sealed class EffectFeedTests
    {
        /// <summary>
        /// A fresh raid, before anything has happened.
        /// </summary>
        /// <remarks>
        /// Deliberately does <i>not</i> walk the party up the corridor first. An earlier version did,
        /// and the trap test failed for a completely legitimate reason: the party's rogue defuses
        /// traps on the way past, so by the time it arrived <c>FireTrap</c> was correctly refusing to
        /// fire a trap that no longer existed. The verbs need no walking to exercise.
        /// </remarks>
        /// <returns>The raid.</returns>
        private static Raid ReadyRaid()
        {
            var raid = new Raid(DungeonLayout.BuildCorridor());
            raid.Effects.Drain();
            return raid;
        }

        /// <summary>Every kind raised in a raid, for a single pass of assertions.</summary>
        private static HashSet<EffectKind> KindsIn(Raid raid)
        {
            var kinds = new HashSet<EffectKind>();
            foreach (Effect effect in raid.Effects.Pending)
            {
                kinds.Add(effect.Kind);
            }

            return kinds;
        }

        /// <summary>Spawning a monster announces itself, at the spawner.</summary>
        [Test]
        public void SpawningAMonster_RaisesAnEffect()
        {
            Raid raid = ReadyRaid();
            Vector2Int spawner = raid.Layout.SpawnerCells[0];

            Assert.IsTrue(raid.SpawnMob(spawner), "the spawn should have succeeded");

            Assert.Contains(EffectKind.MobSpawned, new List<EffectKind>(KindsIn(raid)),
                "spawning a monster must announce itself");
            Assert.AreEqual(spawner, new Vector2Int(
                    Mathf.RoundToInt(raid.Effects.Pending[0].Position.x),
                    Mathf.RoundToInt(raid.Effects.Pending[0].Position.y)),
                "the burst must appear at the spawner the player tapped");
        }

        /// <summary>Firing a trap announces itself, at the trap.</summary>
        [Test]
        public void FiringATrap_RaisesAnEffect()
        {
            Raid raid = ReadyRaid();
            Vector2Int trap = raid.Layout.TrapCells[0];

            Assert.IsTrue(raid.FireTrap(trap), "the trap should have fired");
            Assert.Contains(EffectKind.TrapFired, new List<EffectKind>(KindsIn(raid)),
                "firing a trap must announce itself");
        }

        /// <summary>Toggling a door announces itself -- the cheapest verb still gets an answer.</summary>
        [Test]
        public void TogglingADoor_RaisesAnEffect()
        {
            Raid raid = ReadyRaid();
            Vector2Int door = raid.Layout.Grid.Doors[0].Cell;

            Assert.IsTrue(raid.ToggleDoor(door), "the door should have toggled");
            Assert.Contains(EffectKind.DoorToggled, new List<EffectKind>(KindsIn(raid)),
                "toggling a door must announce itself");
        }

        /// <summary>A monster dying announces itself, because that is the stall ending.</summary>
        [Test]
        public void AMonsterDying_RaisesAnEffect()
        {
            Raid raid = ReadyRaid();
            Mob mob = raid.Mobs.Spawn(MobKind.Slime, raid.Layout.SpawnerCells[0]);
            Assert.IsNotNull(mob, "the test needs a monster");

            bool sawDeath = false;
            while (raid.IsRunning && !sawDeath)
            {
                raid.Tick(0.02f);
                foreach (Effect effect in raid.Effects.Pending)
                {
                    sawDeath |= effect.Kind == EffectKind.MobDied;
                }

                raid.Effects.Drain();
            }

            MooseRunnerFacade.Log($"saw a death effect: {sawDeath}");
            Assert.IsTrue(sawDeath, "a monster dying must announce itself");
        }

        /// <summary>
        /// A death is announced once, not once per tick for the rest of the raid.
        /// </summary>
        /// <remarks>
        /// The death is derived from a health transition rather than a flag, so the obvious mistake
        /// is re-raising it on every subsequent tick -- a corpse fountaining bone chips for forty
        /// seconds.
        /// </remarks>
        [Test]
        public void ADeath_IsAnnouncedExactlyOnce()
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor();
            var raid = new Raid(layout);
            raid.Mobs.Spawn(MobKind.Slime, layout.SpawnerCells[0]);

            int deaths = 0;
            while (raid.IsRunning)
            {
                raid.Tick(0.02f);
                foreach (Effect effect in raid.Effects.Pending)
                {
                    if (effect.Kind == EffectKind.MobDied)
                    {
                        deaths++;
                    }
                }

                raid.Effects.Drain();
            }

            MooseRunnerFacade.Log($"one slime produced {deaths} death effect(s)");
            Assert.AreEqual(1, deaths, "the death fired more than once");
        }

        /// <summary>Draining clears the feed, so a burst is never drawn twice.</summary>
        [Test]
        public void Draining_ClearsTheFeed()
        {
            Raid raid = ReadyRaid();
            raid.ToggleDoor(raid.Layout.Grid.Doors[0].Cell);

            Assert.IsNotEmpty(raid.Effects.Pending, "the toggle should have raised something");
            raid.Effects.Drain();
            Assert.IsEmpty(raid.Effects.Pending, "draining must empty the feed");
        }
    }
}
