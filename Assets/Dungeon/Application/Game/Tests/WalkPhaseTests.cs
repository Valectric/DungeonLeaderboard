using System.Collections.Generic;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Checks a party occupies as many walk-cycle phases as it can.
    /// </summary>
    /// <remarks>
    /// Gemini, reading a recording of nine adventurers, described them as merging into a mass. Part
    /// of that is genuinely the bunching, and part was arithmetic: the offset stepped by two frames
    /// against a six-frame cycle, so it landed on 0, 2, 4 and repeated — <b>three distinct phases
    /// however many bodies walked in</b>. Nine members were three groups of three moving as one.
    /// <para>
    /// "Looks like puppets" cannot be asserted. How many phases a party occupies can, which is why
    /// the offset is exposed rather than inlined.
    /// </para>
    /// </remarks>
    public sealed class WalkPhaseTests
    {
        /// <summary>Frames in the walk cycle, from <c>DungeonView</c>.</summary>
        private const int WalkFrames = 6;

        /// <summary>A full party spreads across every frame of the cycle.</summary>
        [Test]
        public void AFullParty_UsesTheWholeWalkCycle()
        {
            var phases = new HashSet<int>();
            for (int slot = 0; slot < PartyComposition.MaxSize; slot++)
            {
                phases.Add(Mathf.FloorToInt(DungeonView.WalkPhaseOffset(slot)) % WalkFrames);
            }

            MooseRunnerFacade.Log(
                $"{PartyComposition.MaxSize} members occupy {phases.Count} of {WalkFrames} phases");

            Assert.AreEqual(WalkFrames, phases.Count,
                $"a party of {PartyComposition.MaxSize} occupies only {phases.Count} of the "
                + $"{WalkFrames} walk frames, so the members march in lockstep groups -- which reads "
                + "as one animation playing on several puppets rather than several people walking");
        }

        /// <summary>The opening four also spread, rather than pairing up.</summary>
        [Test]
        public void TheOpeningFour_DoNotMarchInLockstep()
        {
            var phases = new HashSet<int>();
            for (int slot = 0; slot < 4; slot++)
            {
                phases.Add(Mathf.FloorToInt(DungeonView.WalkPhaseOffset(slot)) % WalkFrames);
            }

            MooseRunnerFacade.Log($"four members occupy {phases.Count} phases");

            Assert.AreEqual(4, phases.Count,
                $"four members occupy only {phases.Count} phases, so some of them step together");
        }
    }
}
