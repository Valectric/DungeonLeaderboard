using System.Collections.Generic;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.PartyManager.Tests
{
    /// <summary>
    /// Checks that each role stands where its design says it should, asked directly.
    /// </summary>
    /// <remarks>
    /// <b><c>AdventurerAI</c> is 714 lines and had no test of its own.</b> Everything that reaches
    /// it drives a whole <c>Raid</c> and reads the answer off sixty seconds of party behaviour,
    /// which is the same indirection that let <c>CarveOpening</c>'s docstring stay false for the
    /// life of the project. It does not need that indirection: the class is static and takes a
    /// <c>Perception</c>, so <c>DesiredPosition</c> is a pure function of a described situation.
    /// <para>
    /// <b>Spacing is the mechanism behind the roles meaning anything.</b> Damage is only shared
    /// among members a melee attacker can actually reach — <c>Party.MeleeReach</c> is 1.15 — so the
    /// tank being the one that gets hit is not a rule written anywhere; it falls out of the tank
    /// closing while everyone else backs away. When that stopped being true the symptom was a healer
    /// bleeding from a skeleton three cells off, and, because the damage then arrived spread thin,
    /// nobody was ever wounded enough to be worth healing and the healer looked broken too. One bug,
    /// two complaints, and both of them a distance.
    /// </para>
    /// <para>
    /// <b>What is deliberately not asserted: an absolute distance.</b> <c>StandOff</c> backs off as
    /// far as the room allows <i>and no further</i>, because a mage that retreats five cells in a
    /// five-cell room leaves the fight — measured at not one bolt fired in a whole raid. So "the
    /// healer is 2.6 cells away" is false by design in a small room, and a test asserting it would
    /// be demanding a regression.
    /// </para>
    /// </remarks>
    public sealed class RoleSpacingTests
    {
        /// <summary>A room big enough that standing off is never clamped by the walls.</summary>
        /// <returns>A grid with one large room.</returns>
        private static DungeonGrid BigRoom()
        {
            var grid = new DungeonGrid(24, 24);
            grid.CarveRoom(new RectInt(1, 1, 22, 22), 0);
            return grid;
        }

        /// <summary>What an adventurer can see: one monster, in the middle of a big room.</summary>
        /// <param name="grid">The dungeon.</param>
        /// <param name="threat">Where the monster is.</param>
        /// <returns>The perception to reason from.</returns>
        private static Perception Facing(DungeonGrid grid, Vector2 threat)
        {
            return new Perception
            {
                Grid = grid,
                Threats = new List<Vector2> { threat },
                Objective = new Vector2Int(20, 12),
                FormationSlot = new Vector2(11f, 12f),
                TankTarget = threat
            };
        }

        /// <summary>
        /// Every role but the tank wants to be further from a monster than it currently is.
        /// </summary>
        /// <remarks>
        /// Stated as a direction rather than a distance, so it holds in a room of any size. Starting
        /// them inside melee reach is the case that matters: it is where the damage actually lands.
        /// </remarks>
        [Test]
        public void TheFragileRoles_BackAwayFromAMonster()
        {
            DungeonGrid grid = BigRoom();
            var threat = new Vector2(12f, 12f);
            Perception view = Facing(grid, threat);

            foreach (AdventurerRole role in new[]
                     {
                         AdventurerRole.Healer, AdventurerRole.Ranged, AdventurerRole.Mage
                     })
            {
                var member = new Adventurer(role, new Vector2Int(11, 12));
                float before = Vector2.Distance(member.Position, threat);
                float after = Vector2.Distance(
                    AdventurerAI.DesiredPosition(member, view), threat);

                MooseRunnerFacade.Log($"{role}: standing {before:F2} away, wants {after:F2}");

                Assert.Greater(after, before,
                    $"a {role} inside melee reach ({before:F2} cells) wants to stand {after:F2} "
                    + "away, which is no further -- damage is shared only among what a monster can "
                    + "reach, so a fragile role that does not back off is a fragile role being hit");
            }
        }

        /// <summary>
        /// The tank holds its ground against a monster instead of backing away from it.
        /// </summary>
        /// <remarks>
        /// <b>This asserted that the tank CHARGES, and that was wrong about the game.</b> A healthy
        /// tank's reach is 0.85, below <c>StandOff</c>'s search floor of 1.2, so it stands its
        /// ground and lets the monster walk over — which is defensible, and is what a tank is for.
        /// Making it genuinely charge was measured: fights resolve sooner and a stalled raid's
        /// harvest fell 2.6%, so it is a balance change and the author's, not mine to make while
        /// fixing something else.
        /// <para>
        /// What matters for the roles meaning anything is the <i>contrast</i> with the test above,
        /// and that survives intact: the fragile three move away from a monster and the tank does
        /// not, so the tank is what stays inside <c>Party.MeleeReach</c> and takes the damage.
        /// </para>
        /// </remarks>
        [Test]
        public void TheTank_HoldsItsGroundInstead()
        {
            DungeonGrid grid = BigRoom();
            var threat = new Vector2(12f, 12f);
            Perception view = Facing(grid, threat);

            var tank = new Adventurer(AdventurerRole.Tank, new Vector2Int(11, 12));
            float before = Vector2.Distance(tank.Position, threat);
            float after = Vector2.Distance(AdventurerAI.DesiredPosition(tank, view), threat);

            MooseRunnerFacade.Log($"Tank: standing {before:F2} away, wants {after:F2}");

            Assert.LessOrEqual(after, before + 0.01f,
                $"the tank is {before:F2} cells from the monster and wants to be {after:F2} -- it "
                + "is giving ground like the fragile roles do, so nothing in the party is left "
                + "inside melee reach to draw the damage");
            Assert.LessOrEqual(after, Party.MeleeReach,
                $"the tank sits {after:F2} away, outside the {Party.MeleeReach} a melee attacker "
                + "can reach, so it would draw nothing");
        }

        /// <summary>
        /// A badly wounded member keeps more distance than a healthy one.
        /// </summary>
        /// <remarks>
        /// <c>Spacing</c> adds <see cref="AdventurerAI.WoundedExtraSpace"/> below
        /// <see cref="AdventurerAI.WoundedBacksOffBelow"/>, and this is the rule that makes the wound
        /// curve something a player manages rather than watches. The money is in the last sliver of a
        /// health bar, so a party that keeps its wounded alive at the back is the party that pays —
        /// and if this stopped working, the wounded would drift back into reach and simply die, which
        /// costs 50 banked points each.
        /// </remarks>
        [Test]
        public void AWoundedMember_KeepsFurtherBack()
        {
            DungeonGrid grid = BigRoom();
            var threat = new Vector2(12f, 12f);
            Perception view = Facing(grid, threat);

            foreach (AdventurerRole role in new[]
                     {
                         AdventurerRole.Healer, AdventurerRole.Ranged, AdventurerRole.Mage
                     })
            {
                var healthy = new Adventurer(role, new Vector2Int(11, 12));
                var wounded = new Adventurer(role, new Vector2Int(11, 12));
                wounded.TakeDamage(wounded.MaxHealth * 0.8f);

                Assert.Less(wounded.HealthFraction, AdventurerAI.WoundedBacksOffBelow,
                    "the fixture failed to wound this member past the threshold being tested");

                float healthyRange = Vector2.Distance(
                    AdventurerAI.DesiredPosition(healthy, view), threat);
                float woundedRange = Vector2.Distance(
                    AdventurerAI.DesiredPosition(wounded, view), threat);

                MooseRunnerFacade.Log(
                    $"{role}: healthy stands {healthyRange:F2}, wounded at "
                    + $"{wounded.HealthFraction:P0} stands {woundedRange:F2}");

                Assert.Greater(woundedRange, healthyRange,
                    $"a {role} at {wounded.HealthFraction:P0} health stands {woundedRange:F2} away "
                    + $"against a healthy one's {healthyRange:F2} -- the wounded are not backing "
                    + "off, so the health the whole energy curve is paid on is being thrown away");
            }
        }

        /// <summary>
        /// A tank that can see a monster it cannot reach still makes progress.
        /// </summary>
        /// <remarks>
        /// <b>The case that turns a frozen tank into a lost raid.</b> Mobs are room-bounded by
        /// design — CLAUDE.md calls it load-bearing, because the retreat valve depends on it — so a
        /// monster standing in the next room will never walk to the party. If the tank stops the
        /// moment it can *see* one, and the one it sees cannot come, nobody moves for the rest of
        /// the minute and the party earns the idle floor.
        /// <para>
        /// Line of sight has no range limit here, so this is not an exotic setup: two rooms and one
        /// open door between them is the ordinary shape of this dungeon.
        /// </para>
        /// </remarks>
        [Test]
        public void ATank_SeeingAMonsterItCannotReach_StillMoves()
        {
            var grid = new DungeonGrid(24, 12);
            grid.CarveRoom(new RectInt(1, 1, 7, 9), 0);
            grid.CarveRoom(new RectInt(9, 1, 7, 9), 1);
            grid.AddDoor(new Vector2Int(8, 5), 0, 1, isOpen: true);

            // The monster is across the threshold, in the far room, in plain view down the row.
            var threat = new Vector2(13f, 5f);
            var tank = new Adventurer(AdventurerRole.Tank, new Vector2Int(3, 5));

            var view = new Perception
            {
                Grid = grid,
                Threats = new List<Vector2> { threat },
                Objective = new Vector2Int(14, 5),
                FormationSlot = new Vector2(2f, 5f),
                TankTarget = threat
            };

            Assert.IsTrue(grid.HasLineOfSight(tank.Position, threat),
                "the fixture is wrong: the tank cannot see the monster, so this proves nothing");

            Vector2 wants = AdventurerAI.DesiredPosition(tank, view);
            float moved = Vector2.Distance(wants, tank.Position);

            MooseRunnerFacade.Log(
                $"tank at {tank.Position} seeing a monster at {threat}: wants {wants}, "
                + $"which is {moved:F2} cells away");

            Assert.Greater(moved, 0.01f,
                $"the tank wants to stand exactly where it is ({moved:F2} cells of movement) while "
                + "looking at a monster in the next room that cannot come to it -- the party is "
                + "stalled here for the rest of the raid, earning the idle floor");
        }

        /// <summary>
        /// Given room to use, each role stands off at about the range its constant states.
        /// </summary>
        /// <remarks>
        /// The absolute check, made honest by giving it a room large enough that the clamp in
        /// <c>StandOff</c> never bites. Without this the directional tests above would still pass if
        /// every role backed off by a single pixel, and the three ranges — 3 for the archer, 2.6 for
        /// the healer, 2.4 for the mage — are a deliberate ordering rather than three numbers.
        /// </remarks>
        [Test]
        public void EachRole_ReachesItsStatedRangeWhenTheRoomAllows()
        {
            DungeonGrid grid = BigRoom();
            var threat = new Vector2(12f, 12f);
            Perception view = Facing(grid, threat);

            var expected = new Dictionary<AdventurerRole, float>
            {
                { AdventurerRole.Ranged, AdventurerAI.RangedRange },
                { AdventurerRole.Healer, AdventurerAI.HealerFleeRange },
                { AdventurerRole.Mage, AdventurerAI.MageRange }
            };

            foreach (KeyValuePair<AdventurerRole, float> pair in expected)
            {
                var member = new Adventurer(pair.Key, new Vector2Int(11, 12));
                float wants = Vector2.Distance(
                    AdventurerAI.DesiredPosition(member, view), threat);

                MooseRunnerFacade.Log(
                    $"{pair.Key}: wants {wants:F2}, its stated range is {pair.Value:F2}");

                Assert.GreaterOrEqual(wants, pair.Value - 0.35f,
                    $"a {pair.Key} in a 22x22 room only backs off to {wants:F2} against its stated "
                    + $"{pair.Value:F2}, and nothing here is clamping it");
            }
        }
    }
}
