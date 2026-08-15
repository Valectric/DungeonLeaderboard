using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// A party running for the exit forces the door that is barring it.
    /// </summary>
    /// <remarks>
    /// The author's report was one line -- <i>"make sure a team attacks a closed door"</i> -- and it
    /// pointed at the door the party has just walked through, not the one ahead of them. Everything
    /// in <c>ForceDoors</c> was written for an <b>advancing</b> party: it returned immediately while
    /// the goal was <see cref="PartyGoal.Retreating"/>, and the door it looked for was the one on the
    /// route to the boss room. Shut the door behind a losing party and both of those are the wrong
    /// answer -- the route home does not exist, so the retreat pathfind returned nothing and the
    /// party stood against the door until the clock ran out.
    /// <para>
    /// That matters more than a stalled animation. SPEC.md makes the retreat the player's only
    /// safety valve and their central regret: <i>open a door behind a losing party and let them
    /// retreat and heal</i>. A party that will not use a door it can open itself turns the valve
    /// into a trap, and turns the game's most interesting decision into a farm.
    /// </para>
    /// </remarks>
    public sealed class TrappedPartyTests
    {
        /// <summary>
        /// Walks a party into the second room, then seals the door behind them and wounds them.
        /// </summary>
        /// <param name="raid">Receives the raid in progress.</param>
        /// <param name="minimumGap">
        /// How far past the door to let them get first, in cells. Zero seals them the moment they
        /// step through, which is where the player's finger usually is; a larger gap is for asking
        /// whether they walk back to it.
        /// </param>
        /// <returns>The door that was shut behind them.</returns>
        private static Door SealThemIn(out Raid raid, float minimumGap = 0f)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: 3);
            raid = new Raid(layout, 0f, PartyComposition.Opening, 4242);
            Door door = layout.Grid.Doors[0];

            // Let them walk through the first door under their own steam.
            for (float t = 0f; t < 30f && raid.IsRunning; t += 0.02f)
            {
                raid.Tick(0.02f);
                if (layout.Grid.RoomAt(raid.Party.Cell) >= 1 &&
                    Vector2.Distance(raid.Party.Position, door.Cell) >= minimumGap)
                {
                    break;
                }
            }

            Assert.GreaterOrEqual(layout.Grid.RoomAt(raid.Party.Cell), 1,
                "the party never got past the first door, so nothing can be sealed behind them");
            Assert.GreaterOrEqual(Vector2.Distance(raid.Party.Position, door.Cell), minimumGap,
                "the party never got far enough past the door for this to measure anything");

            door.IsOpen = false;

            // Hurt enough to send them home. The retreat threshold is a health fraction, and this is
            // the only way to reach it deterministically -- waiting for monsters to do it would make
            // the test a measurement of combat rolls.
            //
            // Each member taken DOWN TO a share of its own bar rather than hit for a share of it.
            // Two earlier attempts each failed in their own direction and both looked like a
            // production bug: eighty percent each left the party reading 29% against a threshold of
            // 28%, so it advanced; hitting repeatedly until the pooled figure dropped killed them
            // instead, because the pool is measured over the LIVING and each death lifts it.
            const float target = 0.15f;
            foreach (Adventurer member in raid.Party.Living)
            {
                member.TakeDamage(member.MaxHealth * (member.HealthFraction - target));
            }

            Assert.Less(raid.Party.HealthFraction, Party.RetreatThreshold,
                "the party has to be hurt enough to actually break off, or this measures nothing");
            Assert.Greater(raid.Party.LivingCount, 0, "and hurt is not the same as dead");

            return door;
        }

        /// <summary>
        /// A wounded party sealed in works on the door instead of standing at it.
        /// </summary>
        /// <remarks>
        /// Asserted on progress rather than on the door opening, because the two failure modes are
        /// different: no progress at all is the stall this fixes, while slow progress is a tuning
        /// question about how long a door is worth.
        /// </remarks>
        [Test]
        public void APartySealedIn_WorksOnTheDoorBehindThem()
        {
            Door door = SealThemIn(out Raid raid);

            for (float t = 0f; t < 12f && raid.IsRunning && !door.IsOpen; t += 0.02f)
            {
                raid.Tick(0.02f);
            }

            MooseRunnerFacade.Log(
                $"sealed-in party: goal {raid.Party.Goal}, door picked {door.PickFraction:P0}, "
                + $"battered {door.DamageFraction:P0}, open {door.IsOpen}");

            Assert.IsTrue(door.PickFraction > 0f || door.DamageFraction > 0f || door.IsOpen,
                "a party running for an exit it cannot reach did nothing at all to the door in "
                + "its way -- which is the standoff that makes the retreat valve worthless");
        }

        /// <summary>
        /// They get through it, rather than merely scratching at it.
        /// </summary>
        /// <remarks>
        /// The door has to be a delay, exactly as it is for an advancing party. The generous window
        /// is on purpose: an archer picks a lock in a few seconds and a party without one batters
        /// through twice a skeleton's health, and this must hold for either.
        /// </remarks>
        [Test]
        public void APartySealedIn_EventuallyGetsOut()
        {
            Door door = SealThemIn(out Raid raid);

            float elapsed = 0f;
            for (; elapsed < 40f && raid.IsRunning && !door.IsOpen; elapsed += 0.02f)
            {
                raid.Tick(0.02f);
            }

            MooseRunnerFacade.Log(
                $"the sealed-in party opened the door after {elapsed:F1}s (open={door.IsOpen})");

            Assert.IsTrue(door.IsOpen,
                $"after {elapsed:F1}s a wounded party still could not open the door between them "
                + "and the way out");
        }

        /// <summary>
        /// A retreating party moves toward the shut door rather than standing where it stopped.
        /// </summary>
        /// <remarks>
        /// The movement half of the same defect, and the one that is visible on screen: pathing to an
        /// entrance behind a shut door returns an empty route, and an empty route means the leader
        /// simply does not move. A party frozen mid-room while its health bar flashes reads as the
        /// game having crashed.
        /// </remarks>
        [Test]
        public void ARetreatingParty_ClosesOnTheDoorItMustOpen()
        {
            // Sealed in only once they are well past it, or there is no walking to measure: a party
            // caught on the threshold is already standing in reach of the door and correctly does
            // not move at all.
            Door door = SealThemIn(out Raid raid, minimumGap: 4f);
            float before = Vector2.Distance(raid.Party.Position, door.Cell);

            float closest = before;
            for (float t = 0f; t < 6f && raid.IsRunning && !door.IsOpen; t += 0.02f)
            {
                raid.Tick(0.02f);
                closest = Mathf.Min(closest, Vector2.Distance(raid.Party.Position, door.Cell));
            }

            MooseRunnerFacade.Log(
                $"distance to the sealed door {before:F2} -> {closest:F2} cells");

            Assert.Less(closest, before - 0.5f,
                $"the party stayed {closest:F2} cells from the door it has to open, having started "
                + $"{before:F2} away -- it is not moving at all");
        }
    }
}
