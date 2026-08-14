using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
    }
}
