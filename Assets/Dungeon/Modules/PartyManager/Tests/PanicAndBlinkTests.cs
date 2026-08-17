using System.Collections.Generic;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.PartyManager.Tests
{
    /// <summary>
    /// Checks the other two things <c>AdventurerAI</c> decides: how fast a frightened member moves,
    /// and where a cornered mage blinks to.
    /// </summary>
    /// <remarks>
    /// The remaining public entry points on a class that had no direct tests until today, and where
    /// asking the first one directly turned up a tank that froze on sight of anything. Both are pure
    /// functions of a described situation, so neither needs a raid to interrogate.
    /// <para>
    /// Both are escape valves, and an escape valve is worth pinning precisely because it only runs
    /// when things are already going wrong: a mage that blinks <i>toward</i> the monster, or a healer
    /// that ambles away from one, would show up as "they died and I do not know why" rather than as
    /// anything a player could describe.
    /// </para>
    /// </remarks>
    public sealed class PanicAndBlinkTests
    {
        /// <summary>A room big enough to have somewhere to run to.</summary>
        /// <returns>A grid with one large room.</returns>
        private static DungeonGrid BigRoom()
        {
            var grid = new DungeonGrid(24, 24);
            grid.CarveRoom(new RectInt(1, 1, 22, 22), 0);
            return grid;
        }

        /// <summary>What a member can see: one monster.</summary>
        /// <param name="grid">The dungeon.</param>
        /// <param name="threat">Where the monster is, or null for an empty room.</param>
        /// <returns>The perception to reason from.</returns>
        private static Perception Facing(DungeonGrid grid, Vector2? threat)
        {
            return new Perception
            {
                Grid = grid,
                Threats = threat.HasValue
                    ? new List<Vector2> { threat.Value }
                    : new List<Vector2>(),
                Objective = new Vector2Int(20, 12),
                FormationSlot = new Vector2(11f, 12f)
            };
        }

        /// <summary>
        /// Nobody hurries when there is nothing to hurry from, and the tank never hurries at all.
        /// </summary>
        /// <remarks>
        /// The tank's exemption is the load-bearing half: a tank that broke off and ran would stop
        /// soaking, and the roles standing behind it would be the ones in reach.
        /// </remarks>
        [Test]
        public void NobodyPanics_WithoutSomethingCloseEnough()
        {
            DungeonGrid grid = BigRoom();

            foreach (AdventurerRole role in new[]
                     {
                         AdventurerRole.Tank, AdventurerRole.Healer,
                         AdventurerRole.Ranged, AdventurerRole.Mage
                     })
            {
                var member = new Adventurer(role, new Vector2Int(11, 12));

                float empty = AdventurerAI.SpeedMultiplier(member, Facing(grid, null));
                float distant = AdventurerAI.SpeedMultiplier(
                    member, Facing(grid, new Vector2(11f + AdventurerAI.PanicRange + 1f, 12f)));

                Assert.AreEqual(1f, empty, 0.001f,
                    $"a {role} with an empty room to itself is moving at {empty:F2} of walking pace");
                Assert.AreEqual(1f, distant, 0.001f,
                    $"a {role} is panicking at {distant:F2} over a monster further away than "
                    + $"{AdventurerAI.PanicRange}, which is not close enough to be a threat");
            }

            var tank = new Adventurer(AdventurerRole.Tank, new Vector2Int(11, 12));
            float cornered = AdventurerAI.SpeedMultiplier(
                tank, Facing(grid, new Vector2(11.5f, 12f)));

            Assert.AreEqual(1f, cornered, 0.001f,
                $"the tank broke into a run at {cornered:F2} with a monster on top of it -- a tank "
                + "that flees stops soaking and leaves the fragile roles in reach");
        }

        /// <summary>
        /// A cornered fragile role scrambles, and the archer scrambles hardest.
        /// </summary>
        /// <remarks>
        /// The ordering is the design rather than the numbers: the author asked for an archer that
        /// can actually break away from a single monster, where everyone else only buys seconds. A
        /// mob closes at 1.9 cells a second against a party walking at 0.9, so a mage retreating at
        /// walking pace is not retreating — it is being escorted.
        /// </remarks>
        [Test]
        public void ACorneredRole_Scrambles_AndTheArcherScramblesHardest()
        {
            DungeonGrid grid = BigRoom();
            Perception view = Facing(grid, new Vector2(11.5f, 12f));

            var speeds = new Dictionary<AdventurerRole, float>();
            foreach (AdventurerRole role in new[]
                     {
                         AdventurerRole.Healer, AdventurerRole.Ranged, AdventurerRole.Mage
                     })
            {
                var member = new Adventurer(role, new Vector2Int(11, 12));
                speeds[role] = AdventurerAI.SpeedMultiplier(member, view);
                MooseRunnerFacade.Log($"{role} cornered: {speeds[role]:F2}x");
            }

            foreach (KeyValuePair<AdventurerRole, float> pair in speeds)
            {
                Assert.Greater(pair.Value, 1f,
                    $"a cornered {pair.Key} moves at {pair.Value:F2}x, which is walking pace -- it "
                    + "is being escorted to its death rather than escaping");
            }

            Assert.Greater(speeds[AdventurerRole.Ranged], speeds[AdventurerRole.Mage],
                $"the archer scrambles at {speeds[AdventurerRole.Ranged]:F2}x against the mage's "
                + $"{speeds[AdventurerRole.Mage]:F2}x, so it cannot break away as asked");
        }

        /// <summary>
        /// A blink puts the mage somewhere it can stand, in the same room, further from the monster.
        /// </summary>
        /// <remarks>
        /// All three clauses matter and the last is the point. A blink that landed the mage no
        /// further away would spend the cooldown and the mana to achieve nothing, and the player
        /// would see a mage vanish and reappear next to the thing killing it.
        /// <para>
        /// Same room is not fussiness either: combat is scoped per room, so blinking out of one is
        /// blinking out of the fight, which stops the mage earning at the same time as it saves it.
        /// </para>
        /// </remarks>
        [Test]
        public void ABlink_LandsSomewhereSaferInTheSameRoom()
        {
            DungeonGrid grid = BigRoom();
            var threat = new Vector2(11f, 12f);
            var mage = new Adventurer(AdventurerRole.Mage, new Vector2Int(12, 12));
            Perception view = Facing(grid, threat);

            bool found = AdventurerAI.TryFindBlink(mage, view, out Vector2 destination);

            Assert.IsTrue(found, "a mage with a monster beside it and a whole room behind it "
                + "should be able to find somewhere to blink");

            var cell = new Vector2Int(
                Mathf.RoundToInt(destination.x), Mathf.RoundToInt(destination.y));
            float before = Vector2.Distance(mage.Position, threat);
            float after = Vector2.Distance(destination, threat);

            MooseRunnerFacade.Log(
                $"mage at {mage.Position} blinks to {destination}: {before:F2} -> {after:F2} "
                + $"from the monster, landing in room {grid.RoomAt(cell)}");

            Assert.IsTrue(grid.IsWalkable(cell),
                $"the blink lands on {cell}, which is not somewhere a body can stand");
            Assert.AreEqual(grid.RoomAt(mage.Cell), grid.RoomAt(cell),
                "the blink left the room, so the mage has left the fight it was trying to survive");
            Assert.Greater(after, before,
                $"the mage blinked from {before:F2} to {after:F2} of the monster, spending its "
                + "cooldown and mana to end up no safer");
        }

        /// <summary>
        /// There is nothing to blink away from in an empty room, and the mage does not try.
        /// </summary>
        /// <remarks>
        /// The negative case, worth having because the method reports its answer through a
        /// <c>bool</c> and an <c>out</c>: a caller that trusted the destination without checking the
        /// return would teleport the mage on every tick of a quiet raid.
        /// </remarks>
        [Test]
        public void ABlink_IsNotAttemptedWithNothingToEscape()
        {
            DungeonGrid grid = BigRoom();
            var mage = new Adventurer(AdventurerRole.Mage, new Vector2Int(12, 12));

            bool found = AdventurerAI.TryFindBlink(mage, Facing(grid, null), out Vector2 destination);

            Assert.IsFalse(found, "the mage found somewhere to blink with no monster in the room");
            Assert.AreEqual(mage.Position, destination,
                "the destination should be left where the mage stands when there is no blink to "
                + "make, so a caller that ignores the return value does not teleport it");
        }
    }
}
