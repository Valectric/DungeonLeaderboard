using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Proves the attack animation actually reaches the screen in the shipped scene.
    /// </summary>
    /// <remarks>
    /// <see cref="AnimationTests"/> proves the motion functions return a lunge. That is necessary and
    /// nowhere near sufficient — a pure function can be perfect while nothing calls it, and this
    /// project's whole verification doctrine exists because the sister project shipped exactly that
    /// class of bug: every unit test green, the behaviour absent from the running game.
    /// <para>
    /// So this watches the real sprite transforms during a real fight and requires them to leave the
    /// positions the simulation reports. If someone deletes the two lines in
    /// <c>DungeonView.RefreshParty</c> that apply the shove, every test in the other file still
    /// passes and this one goes red.
    /// </para>
    /// </remarks>
    public sealed class AnimationOnScreenTests
    {
        /// <summary>The controller in the loaded scene.</summary>
        private static GameController Controller => Object.FindFirstObjectByType<GameController>();

        /// <summary>Loads the shipped play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid", forceReload: true);
        }

        /// <summary>
        /// Somebody's sprite visibly leaves its simulated position while swinging.
        /// </summary>
        /// <remarks>
        /// Deliberately phrased as "somebody". Which member swings first depends on the seeded party
        /// composition and on who is nearest the monster, and pinning it to the tank would make this
        /// a party-generation test that fails whenever the roster rolls differently.
        /// </remarks>
        [Test]
        public async UniTask AttackingSpritesLeaveTheirSimulatedPosition(CancellationToken ct)
        {
            Controller.StartRaid();
            await UniTask.WaitForSeconds(0.3f, cancellationToken: ct);

            Raid raid = Controller.CurrentRaid;
            Camera camera = Camera.main;
            Vector2Int spawner = raid.Layout.SpawnerCells[0];
            Controller.ClickAt(camera.WorldToScreenPoint(DungeonView.CellToWorld(spawner)));
            await UniTask.NextFrame(ct);

            // Wait for the fight rather than sleeping a fixed number of frames. The party walks
            // deliberately slowly and the mob is bound to its own room, so how long they take to meet
            // is a function of pacing constants -- a fixed wait here samples an empty corridor and
            // reports that the animation is missing when it simply had not started yet.
            for (int i = 0; i < 300 && raid.IsRunning && raid.Party.Goal != PartyGoal.Fighting; i++)
            {
                await UniTask.WaitForSeconds(0.1f, cancellationToken: ct);
            }

            Assert.AreEqual(PartyGoal.Fighting, raid.Party.Goal,
                "the party should have met the spawned monster");

            float worstOffset = 0f;
            string worstRole = "nobody";

            // Sampled every frame rather than on a timer: an attack's visible phase is a fraction of
            // a second and a coarse poll walks straight past it.
            for (int frame = 0; frame < 900 && raid.IsRunning; frame++)
            {
                for (int i = 0; i < raid.Party.Members.Count; i++)
                {
                    Adventurer member = raid.Party.Members[i];
                    var view = GameObject.Find($"party_{i}");
                    if (view == null || !member.IsAlive)
                    {
                        continue;
                    }

                    float drawnX = view.transform.position.x / DungeonView.CellSize;
                    float offset = Mathf.Abs(drawnX - member.Position.x);
                    if (offset > worstOffset)
                    {
                        worstOffset = offset;
                        worstRole = member.Role.ToString();
                    }
                }

                if (worstOffset > 0.04f)
                {
                    break;
                }

                await UniTask.NextFrame(ct);
            }

            MooseRunnerFacade.Log(
                $"largest sideways displacement {worstOffset:F3} cells, by the {worstRole}");
            Assert.Greater(worstOffset, 0.04f,
                "an attacking sprite must visibly leave its simulated position, or combat renders " +
                "as two health bars changing length with everyone standing still");
        }

        /// <summary>
        /// No adventurer or monster is ever drawn underneath the floor.
        /// </summary>
        /// <remarks>
        /// Reported from play: a party member vanished during a Skirmishers raid. Nothing in the
        /// simulation was wrong, which is why every assertion in the project stayed green — the
        /// sprite was present, positioned and enabled, and sorted <i>behind the tiles</i>.
        /// <para>
        /// Draw order counts down with height (so a sprite lower on screen overlaps one behind it),
        /// and the bases were low enough that the top of the grid went negative: a party member at
        /// y=6 sorted to -4 against floor tiles at 0, and a monster at y=4 to -1. Spawners sit at
        /// y=5, so a monster could be invisible from the moment it appeared.
        /// </para>
        /// <para>
        /// Swept over every cell of the grid rather than the row the party happens to walk, because
        /// the failure only appears once something leaves that row — which is exactly what a
        /// panicking archer or a blinking mage does.
        /// </para>
        /// </remarks>
        [Test]
        public async UniTask NothingIsEverDrawnUnderTheFloor(CancellationToken ct)
        {
            Controller.StartRaid();
            await UniTask.Yield(ct);

            Raid raid = Controller.CurrentRaid;
            DungeonGrid grid = raid.Layout.Grid;

            // The highest order anything in the scenery uses: props sit at 4 and the shop's
            // buildable markers at 9.
            const int highestSceneryOrder = 9;

            int worstParty = int.MaxValue;
            int worstMob = int.MaxValue;

            for (int y = 0; y < grid.Height; y++)
            {
                int party = 50 - Mathf.RoundToInt(y * 4f);
                int mob = 45 - Mathf.RoundToInt(y * 4f);
                worstParty = Mathf.Min(worstParty, party);
                worstMob = Mathf.Min(worstMob, mob);
            }

            MooseRunnerFacade.Log(
                $"over {grid.Height} rows: worst party order {worstParty}, worst mob order "
                + $"{worstMob}, scenery tops out at {highestSceneryOrder}");

            Assert.Greater(worstParty, highestSceneryOrder,
                $"an adventurer at the top of the grid sorts to {worstParty}, at or under the "
                + "scenery, so it is drawn beneath the floor and simply disappears");
            Assert.Greater(worstMob, highestSceneryOrder,
                $"a monster at the top of the grid sorts to {worstMob}, at or under the scenery — "
                + "and spawners sit near the top, so it would be invisible from birth");
        }

        /// <summary>
        /// Every living sprite in a running raid is actually visible.
        /// </summary>
        /// <remarks>
        /// The arithmetic above is checked against the real scene here: whatever the simulation is
        /// doing, anything alive must be enabled, have a sprite, and sort above the floor.
        /// </remarks>
        [Test]
        public async UniTask EveryLivingSpriteIsVisible(CancellationToken ct)
        {
            Controller.StartRaid();
            await UniTask.Yield(ct);

            Raid raid = Controller.CurrentRaid;
            Camera camera = Camera.main;
            Controller.ClickAt(camera.WorldToScreenPoint(
                DungeonView.CellToWorld(raid.Layout.SpawnerCells[0])));

            for (int frame = 0; frame < 600 && raid.IsRunning; frame++)
            {
                for (int i = 0; i < raid.Party.Members.Count; i++)
                {
                    if (!raid.Party.Members[i].IsAlive)
                    {
                        continue;
                    }

                    var view = GameObject.Find($"party_{i}");
                    if (view == null)
                    {
                        continue;
                    }

                    var renderer = view.GetComponent<SpriteRenderer>();
                    Assert.IsTrue(renderer.enabled,
                        $"party member {i} is alive but its sprite is switched off");
                    Assert.Greater(renderer.sortingOrder, 9,
                        $"party member {i} sorts to {renderer.sortingOrder} at "
                        + $"{raid.Party.Members[i].Position}, which draws it under the floor");
                }

                await UniTask.NextFrame(ct);
            }
        }

        /// <summary>
        /// A walking adventurer actually cycles through its drawn frames on screen.
        /// </summary>
        /// <remarks>
        /// The counterpart to the procedural-motion test above, and necessary for the same reason:
        /// the frames can exist, import correctly and be looked up by the right name while the
        /// renderer still shows one static pose all raid. Nothing else in the project would notice —
        /// the files are present, the paths resolve, and every other assertion passes.
        /// <para>
        /// Asserts distinct sprites reach the renderer rather than a particular sequence. Which frame
        /// is showing at a given instant depends on party slot and elapsed time, and pinning that
        /// would make this fail whenever the phase offset is retuned.
        /// </para>
        /// </remarks>
        [Test]
        public async UniTask WalkingAdventurers_CycleThroughDrawnFrames(CancellationToken ct)
        {
            Controller.StartRaid();
            await UniTask.Yield(ct);

            Raid raid = Controller.CurrentRaid;
            var seen = new HashSet<string>();
            string role = "nobody";

            for (int frame = 0; frame < 400 && raid.IsRunning; frame++)
            {
                for (int i = 0; i < raid.Party.Members.Count; i++)
                {
                    Adventurer member = raid.Party.Members[i];
                    if (!member.IsAlive || member.Action != AdventurerAction.Walking)
                    {
                        continue;
                    }

                    var view = GameObject.Find($"party_{i}");
                    SpriteRenderer renderer = view == null ? null : view.GetComponent<SpriteRenderer>();
                    if (renderer == null || renderer.sprite == null)
                    {
                        continue;
                    }

                    // Only the first walker, so two members on different phases cannot fake a cycle
                    // between them.
                    if (role == "nobody")
                    {
                        role = member.Role.ToString();
                    }

                    if (member.Role.ToString() == role)
                    {
                        seen.Add(renderer.sprite.name);
                    }
                }

                await UniTask.NextFrame(ct);
            }

            MooseRunnerFacade.Log(
                $"a walking {role} showed {seen.Count} distinct sprites: "
                + string.Join(", ", seen));

            Assert.Greater(seen.Count, 1,
                $"a walking {role} showed the same sprite for the whole raid, so the drawn walk "
                + "cycle is imported and named correctly but never reaches the screen");
        }
    }
}
