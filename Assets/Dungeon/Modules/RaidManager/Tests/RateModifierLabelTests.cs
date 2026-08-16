using MooseRunner;
using NUnit.Framework;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Pins the one line that tells the player why their rate just moved.
    /// </summary>
    /// <remarks>
    /// <c>RateModifiers.Summary()</c> had no test of any kind, which is a poor place for a gap: it
    /// is the entire explanation the game offers for a number that jumps around on its own, and the
    /// module's own doctrine is that a bonus the player cannot see is decoration. A wrong or empty
    /// label breaks nothing, throws nothing, and fails no other assertion — it just quietly turns
    /// the variation system back into noise.
    /// <para>
    /// These check causes are <i>named</i> and that they expire, not the exact wording, except for
    /// the new-room seconds — that number is load-bearing and is asserted for real. It is the only
    /// cause that moves the clock as well as the rate, so it is the only one where a missing figure
    /// leaves the player watching a countdown count up.
    /// </para>
    /// </remarks>
    public sealed class RateModifierLabelTests
    {
        /// <summary>Nothing active reads as nothing, rather than as an empty decoration.</summary>
        [Test]
        public void WithNothingActive_TheLineIsEmpty()
        {
            var mods = new RateModifiers();

            Assert.IsEmpty(mods.Summary(),
                "the modifier line is showing something with no modifier active, so the player is "
                + "being given a reason for a rate that has not moved");
        }

        /// <summary>Walking into a room names the cause and the seconds it paid.</summary>
        /// <remarks>
        /// The seconds matter here in a way they do not for the other causes. The clock visibly
        /// counts up when a room is entered, and an unexplained countdown running backwards reads as
        /// a bug — so "+ NEW ROOM" alone was true and still taught the wrong thing.
        /// </remarks>
        [Test]
        public void EnteringARoom_NamesTheCauseAndTheSeconds()
        {
            var mods = new RateModifiers();
            mods.RecordNewRoom();

            string line = mods.Summary();
            MooseRunnerFacade.Log($"new-room line: '{line}'");

            Assert.IsNotEmpty(line, "walking into a room said nothing at all");
            StringAssert.Contains("NEW ROOM", line,
                "the new-room bonus is not named, so the player cannot learn that reaching somewhere "
                + "new is worth doing");
            StringAssert.Contains($"+{Raid.NewRoomSeconds:0}s", line,
                "the seconds the room paid are not shown, so the clock counts UP with nothing to "
                + "explain it -- which reads as a glitch, not as a reward");
        }

        /// <summary>
        /// The arrival notice expires, but the bonus it announced does not.
        /// </summary>
        /// <remarks>
        /// The author's rule is that a room pays "+2/s for the rest of the run", so the thing that
        /// must expire here is only the <i>notice</i>. If the running total expired with it the
        /// bonus would be a three-second flourish again, which is what was replaced.
        /// </remarks>
        [Test]
        public void TheArrivalNoticeExpires_ButTheBonusDoesNot()
        {
            var mods = new RateModifiers();
            mods.RecordNewRoom();

            StringAssert.Contains("NEW ROOM", mods.Summary(), "the arrival never showed at all");

            // Past the notice window, with no monsters, so nothing else can light the line up.
            float ticked = 0f;
            while (ticked < RateModifiers.NewRoomNoticeSeconds + 1f)
            {
                mods.Tick(0.1f, enemiesFacing: 0);
                ticked += 0.1f;
            }

            MooseRunnerFacade.Log(
                $"after {ticked:F1}s (notice lasts {RateModifiers.NewRoomNoticeSeconds:F0}s): "
                + $"'{mods.Summary()}', raw total {mods.RawTotal():F1}/s");

            StringAssert.DoesNotContain("NEW ROOM", mods.Summary(),
                "the arrival notice is still up long after the party arrived");
            StringAssert.Contains("ROOMS x1", mods.Summary(),
                "the running room total is not shown once the arrival notice clears, so a bonus the "
                + "player is still being paid has become invisible");
            Assert.AreEqual(RateModifiers.RoomBonus, mods.RawTotal(), 0.01f,
                "the room bonus expired with its notice, so a room is a three-second flourish again "
                + "rather than the lasting gain it was changed to be");
        }

        /// <summary>Each room stacks another <c>Bonus</c> on, and none of them wear off.</summary>
        /// <remarks>
        /// The whole substance of the author's change. A party three rooms deep earns +6/s from
        /// depth alone, which is what makes pushing them onward worth doing — every other modifier
        /// pays for what is happening at this instant.
        /// </remarks>
        [Test]
        public void EachRoomStacks_AndNoneOfThemWearOff()
        {
            var mods = new RateModifiers();

            for (int rooms = 1; rooms <= 4; rooms++)
            {
                mods.RecordNewRoom();

                // A long quiet spell between rooms: nothing here may erode what depth has paid.
                for (int i = 0; i < 100; i++)
                {
                    mods.Tick(0.1f, enemiesFacing: 0);
                }

                MooseRunnerFacade.Log(
                    $"{rooms} room(s) entered, 10s quiet after each: raw {mods.RawTotal():F1}/s, "
                    + $"line '{mods.Summary()}'");

                Assert.AreEqual(rooms, mods.RoomsEntered, "the room count did not keep up");
                Assert.AreEqual(RateModifiers.RoomBonus * rooms, mods.RawTotal(), 0.01f,
                    $"after {rooms} rooms and ten quiet seconds each, depth is not paying "
                    + $"{RateModifiers.RoomBonus * rooms:F0}/s -- the bonus is decaying, or not stacking");
            }
        }

        /// <summary>Several causes at once are all named, not just the first.</summary>
        /// <remarks>
        /// Written because a summary that returns early would pass every single-cause test above
        /// while hiding the crowd bonus behind the room bonus for the whole three seconds a party
        /// spends walking into a room full of monsters — which is exactly when both apply.
        /// </remarks>
        [Test]
        public void SeveralCausesAtOnce_AreAllNamed()
        {
            var mods = new RateModifiers();
            mods.RecordNewRoom();
            mods.RecordDisarm();
            mods.Tick(0.1f, enemiesFacing: 3);

            string line = mods.Summary();
            MooseRunnerFacade.Log($"three causes at once: '{line}'");

            StringAssert.Contains("NEW ROOM", line, "the room bonus vanished when other causes ran");
            StringAssert.Contains("DISARM", line, "the disarm bonus vanished when other causes ran");
            StringAssert.Contains("CROWD", line, "the crowd bonus vanished when other causes ran");
        }
    }
}
