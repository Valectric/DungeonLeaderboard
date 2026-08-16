using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.RaidManager.Tests
{
    /// <summary>
    /// Measures the most a single raid can produce, which is what <c>GoodRun</c> is supposed to be.
    /// </summary>
    /// <remarks>
    /// <c>LeagueTable.GoodRun</c> documents itself as <b>measured, not chosen</b> — "if the dungeon's
    /// earning power changes, re-measure and change this". It has changed twice: parties now grow to
    /// nine through the season, and the room bonus became permanent. 430 was measured against
    /// four-strong parties on the old curve.
    /// <para>
    /// The ceiling is a raid played WELL on the biggest board the shop can build: doors shut so the
    /// party cannot leave, monsters fed in so they stay wounded and fighting. That is the state the
    /// whole design points at — hurt, alive, in combat and still inside.
    /// </para>
    /// </remarks>
    public sealed class EarningCeilingTests
    {
        /// <summary>Plays one worked raid and returns the harvest.</summary>
        /// <param name="composition">Roster to send in.</param>
        /// <param name="rooms">How many rooms the dungeon has.</param>
        /// <param name="seed">Combat seed.</param>
        /// <returns>Energy harvested.</returns>
        private static float Worked(PartyComposition composition, int rooms, int seed)
        {
            DungeonLayout layout = DungeonLayout.BuildCorridor(roomCount: rooms);
            var raid = new Raid(layout, 0f, composition, seed);

            foreach (Door door in layout.Grid.Doors)
            {
                door.IsOpen = false;
            }

            int guard = 0;
            while (raid.IsRunning && guard++ < 8000)
            {
                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    if (raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) < 3)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                raid.Tick(0.02f);
            }

            return raid.EnergyHarvested;
        }

        /// <summary>Reports the most a raid produces, across rosters, sizes and seeds.</summary>
        [Test]
        public void TheMostARaidCanProduce()
        {
            const int maxRooms = 5;
            float best = 0f;
            string bestWhere = "nothing";
            var bySize = new List<string>();

            foreach (int size in new[] { 4, 6, 9 })
            {
                float sizeBest = 0f;
                foreach (PartyComposition roster in PartyComposition.All)
                {
                    for (int seed = 0; seed < 3; seed++)
                    {
                        float got = Worked(roster.Grown(size), maxRooms, seed);
                        sizeBest = Mathf.Max(sizeBest, got);
                        if (got > best)
                        {
                            best = got;
                            bestWhere = $"{roster.Name} at {size}, seed {seed}";
                        }
                    }
                }

                bySize.Add($"{size}:{sizeBest:F0}");
                MooseRunnerFacade.Log($"party of {size}: best worked raid {sizeBest:F0}");
            }

            MooseRunnerFacade.Log(
                $"CEILING {best:F0} by {bestWhere}  |  by size {string.Join("  ", bySize)}");

            Assert.Greater(best, 0f, "no raid produced anything, so this measures nothing");
        }
    }
}
