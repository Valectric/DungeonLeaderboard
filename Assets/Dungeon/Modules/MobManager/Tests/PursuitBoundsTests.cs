using System.Collections.Generic;
using Dungeon.DungeonManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.MobManager.Tests
{
    /// <summary>
    /// Checks room-bounded pursuit by asking the mobs directly, with no raid around them.
    /// </summary>
    /// <remarks>
    /// <b>This is the rule the player's only safety valve rests on.</b> There is no verb for calling
    /// monsters off — CLAUDE.md forbids one — so the single way to save a losing party is to open a
    /// door behind it and let it retreat. That works only because a mob whose room the party has left
    /// stops rather than following, which is why the constraint is called load-bearing rather than
    /// polish.
    /// <para>
    /// It was covered only through whole raids until now, and D58 concluded that nothing here could
    /// be a pure MobManager test because every existing one built a <c>Raid</c>. That was true of the
    /// tests as written rather than of the module: <c>MobPack</c> takes a grid and a list of party
    /// <i>positions</i>, so the rule can be stated in a fixture of two rooms and one monster. Asked
    /// this way a failure names the rule, instead of arriving as a party that mysteriously could not
    /// escape.
    /// </para>
    /// </remarks>
    public sealed class PursuitBoundsTests
    {
        /// <summary>Two rooms joined by one open door — the shape the retreat valve is used in.</summary>
        /// <returns>The grid.</returns>
        private static DungeonGrid TwoRooms()
        {
            var grid = new DungeonGrid(20, 10);
            grid.CarveRoom(new RectInt(1, 1, 7, 7), 0);
            grid.CarveRoom(new RectInt(9, 1, 7, 7), 1);
            grid.AddDoor(new Vector2Int(8, 4), 0, 1, isOpen: true);
            return grid;
        }

        /// <summary>Ticks a pack for a while, so movement has time to show.</summary>
        /// <param name="mobs">The pack.</param>
        /// <param name="party">Where the party is standing, leader first.</param>
        /// <param name="seconds">How long to run.</param>
        private static void Run(MobPack mobs, IReadOnlyList<Vector2> party, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.02f)
            {
                mobs.Tick(0.02f, party);
            }
        }

        /// <summary>
        /// A monster does not follow the party out of its room, even through an open door.
        /// </summary>
        /// <remarks>
        /// The valve itself. If this fails the player has no way to rescue a party at all, and the
        /// central regret the design is built on — that you may not call your monsters off — becomes
        /// a dead end rather than a decision.
        /// </remarks>
        [Test]
        public void AMonster_DoesNotFollowThePartyOutOfItsRoom()
        {
            DungeonGrid grid = TwoRooms();
            var mobs = new MobPack(grid);
            Mob skeleton = mobs.Spawn(MobKind.Skeleton, new Vector2Int(4, 4));

            Assert.IsNotNull(skeleton, "the fixture failed to spawn a monster");
            Assert.AreEqual(0, skeleton.HomeRoom, "the monster should belong to the room it spawned in");

            Vector2 start = skeleton.Position;

            // The party has retreated next door and shut nothing behind it -- the door is open, so
            // only the rule stops the chase.
            Run(mobs, new List<Vector2> { new(13f, 4f) }, 3f);

            int endedIn = grid.RoomAt(skeleton.Cell);
            MooseRunnerFacade.Log(
                $"party next door: skeleton moved {Vector2.Distance(start, skeleton.Position):F2} "
                + $"cells and ended in room {endedIn}");

            Assert.AreEqual(0, endedIn,
                "the monster left its own room to chase a party that had retreated through an open "
                + "door -- the retreat valve is the player's only way to save a party, and it is "
                + "gone");
        }

        /// <summary>
        /// A monster chases a body that is in its room.
        /// </summary>
        /// <remarks>
        /// The control for the test above, and it is the reason that one means anything. "The
        /// monster stayed in its room" is equally true of a monster that never moves at all, so
        /// without this a pack that had stopped working entirely would pass.
        /// </remarks>
        [Test]
        public void AMonster_ChasesABodyInItsOwnRoom()
        {
            DungeonGrid grid = TwoRooms();
            var mobs = new MobPack(grid);
            Mob skeleton = mobs.Spawn(MobKind.Skeleton, new Vector2Int(2, 2));

            var prey = new Vector2(6f, 6f);
            float before = Vector2.Distance(skeleton.Position, prey);
            Run(mobs, new List<Vector2> { prey }, 2f);
            float after = Vector2.Distance(skeleton.Position, prey);

            MooseRunnerFacade.Log($"same room: skeleton closed {before:F2} -> {after:F2}");

            Assert.Less(after, before,
                $"the monster sat at {after:F2} cells from a body in its own room, having started "
                + $"at {before:F2} -- it is not chasing anything, so the test above proves nothing");
        }

        /// <summary>
        /// A straggler left behind in the previous room is not chased.
        /// </summary>
        /// <remarks>
        /// The subtle half, and one the soak test caught in the wild as <i>"a Skeleton left room 1
        /// for room 0"</i>. The room check is on the party <b>leader</b>, so a body left behind can
        /// be the nearest thing to a monster while standing on the far side of a threshold — and
        /// chasing the nearest body without a second check sends it straight through the door.
        /// </remarks>
        [Test]
        public void AStragglerInAnotherRoom_IsNotChased()
        {
            DungeonGrid grid = TwoRooms();
            var mobs = new MobPack(grid);
            Mob skeleton = mobs.Spawn(MobKind.Skeleton, new Vector2Int(12, 4));

            Assert.AreEqual(1, skeleton.HomeRoom, "this fixture needs the monster in the far room");

            // Leader in the monster's room at the far side; a straggler left behind next door, much
            // closer to the monster than the leader is.
            var leader = new Vector2(14f, 6f);
            var straggler = new Vector2(6f, 4f);

            Run(mobs, new List<Vector2> { leader, straggler }, 3f);

            int endedIn = grid.RoomAt(skeleton.Cell);
            MooseRunnerFacade.Log(
                $"straggler next door: skeleton ended at {skeleton.Position} in room {endedIn}");

            Assert.AreEqual(1, endedIn,
                "the monster crossed the threshold after a straggler in the next room, which is the "
                + "exact escape room-bounded pursuit exists to prevent");
        }

        /// <summary>
        /// Pull makes a body more attractive than distance alone would.
        /// </summary>
        /// <remarks>
        /// How a tank draws aggro without this module ever learning what a role is: the caller hands
        /// down a number per body and <c>MobPack</c> subtracts it from the distance. That keeps
        /// MobManager and PartyManager strangers, which is the One-Flow rule, and it is a score
        /// rather than an override on purpose — a hard "always the tank" rule flips target the
        /// instant the tank drifts a fraction further away, which is the oscillation that produced
        /// a skeleton reversing direction forty-six times beside a party and dealing no damage.
        /// </remarks>
        [Test]
        public void Pull_DrawsAMonsterToTheFurtherBody()
        {
            DungeonGrid grid = TwoRooms();
            var mobs = new MobPack(grid);
            Mob skeleton = mobs.Spawn(MobKind.Skeleton, new Vector2Int(2, 4));

            // The tank stands FURTHER away than the squishy body, so distance alone picks the wrong
            // one and only the pull can correct it.
            var squishy = new Vector2(4f, 4f);
            var tank = new Vector2(6f, 4f);

            Run(mobs, new List<Vector2> { squishy, tank }, 2.5f);

            float toSquishy = Vector2.Distance(skeleton.Position, squishy);
            float toTank = Vector2.Distance(skeleton.Position, tank);

            MooseRunnerFacade.Log(
                $"no pull: skeleton is {toSquishy:F2} from the near body, {toTank:F2} from the far one");

            Assert.Less(toSquishy, toTank,
                "without any pull the monster should simply take the nearest body, so this fixture "
                + "is not measuring what it claims");

            var pulled = new MobPack(grid);
            Mob second = pulled.Spawn(MobKind.Skeleton, new Vector2Int(2, 4));
            for (float t = 0f; t < 2.5f; t += 0.02f)
            {
                pulled.Tick(0.02f, new List<Vector2> { squishy, tank }, new List<float> { 0f, 6f });
            }

            float pulledToSquishy = Vector2.Distance(second.Position, squishy);
            float pulledToTank = Vector2.Distance(second.Position, tank);

            MooseRunnerFacade.Log(
                $"tank pulled: skeleton is {pulledToSquishy:F2} from the near body, "
                + $"{pulledToTank:F2} from the tank");

            Assert.Less(pulledToTank, pulledToSquishy,
                $"with the tank pulling hard the monster still ended up {pulledToTank:F2} from it "
                + $"and {pulledToSquishy:F2} from the body it was meant to ignore -- the tank draws "
                + "no aggro, so the fragile roles are what get hit");
        }
    }
}
