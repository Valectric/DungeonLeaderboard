using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.PartyManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Guards the standings screen against the two ways it has actually broken.
    /// </summary>
    /// <remarks>
    /// Both failures below shipped, and neither was visible to any existing assertion: the standings
    /// still listed twenty dungeons in the right order with the right scores, and the model was
    /// correct throughout. IMGUI never appears in an editor camera screenshot either, so no `Look`
    /// test can catch a screen that has quietly grown past the bottom of the canvas.
    /// </remarks>
    public sealed class StandingsLayoutTests
    {
        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Clears the scene before each test.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
        }

        /// <summary>
        /// The prompt telling the player how to start stays on screen.
        /// </summary>
        /// <remarks>
        /// It did not. Adding the next-party announcement pushed "PRESS ANY KEY" past the bottom of a
        /// 960x600 canvas: the standings looked entirely healthy and the only line explaining how to
        /// begin was gone. Checked at several heights because the screen is centred and a layout can
        /// fit at one size and overflow at another.
        /// </remarks>
        [Test]
        public void ThePrompt_StaysOnScreenAtEveryHeight()
        {
            foreach (bool announcing in new[] { false, true })
            {
                Rect prompt = LeagueScreen.PromptRect(Screen.height / 720f, announcing);

                MooseRunnerFacade.Log(
                    $"screen {Screen.width}x{Screen.height} announcing={announcing} " +
                    $"prompt bottom={prompt.yMax:F0}");

                Assert.LessOrEqual(prompt.yMax, Screen.height,
                    $"the prompt runs {prompt.yMax - Screen.height:F0}px off the bottom");
                Assert.GreaterOrEqual(prompt.y, 0f, "the prompt is above the top of the screen");
            }
        }

        /// <summary>
        /// The first party a new player ever faces is the balanced one.
        /// </summary>
        /// <remarks>
        /// It was not. A raid is started behind the title screen so the standings have a dungeon to
        /// sit over, and that throwaway raid consumed the opening party on its way past -- so the
        /// title screen announced THE SKIRMISHERS, a roster with no tank at all, as the first thing a
        /// new player would meet. Wiping a party is the worst outcome in the design and it is the
        /// player's fault by construction; handing them the hardest roster before they know what a
        /// healer does makes that outcome feel like the game cheating.
        /// </remarks>
        [Test]
        public async UniTask TheFirstPartyOfARun_IsTheBalancedOne(CancellationToken ct)
        {
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            MooseRunnerFacade.Log($"opening announcement: {game.NextParty.Name}");
            Assert.AreSame(PartyComposition.Opening, game.NextParty,
                "the title screen should promise the balanced party, and then deliver it");

            Assert.AreEqual(1, game.NextParty.Count(AdventurerRole.Healer),
                "a new player's first party must have a healer, so early mistakes are survivable");
            Assert.AreEqual(1, game.NextParty.Count(AdventurerRole.Tank),
                "and a tank, so something soaks while they learn what the verbs do");
        }

        /// <summary>The party that walks in is the one the standings promised.</summary>
        /// <remarks>
        /// The announcement is only worth drawing if it is true. A screen that names one roster and
        /// then sends another is worse than saying nothing: the player plans against it.
        /// </remarks>
        [Test]
        public async UniTask TheAnnouncedParty_IsTheOneThatArrives(CancellationToken ct)
        {
            var game = new GameObject("game").AddComponent<GameController>();
            await UniTask.Yield(ct);

            for (int raid = 0; raid < 5; raid++)
            {
                PartyComposition promised = game.NextParty;
                game.StartRaid();
                await UniTask.Yield(ct);

                Assert.AreSame(promised, game.CurrentRaid.Party.Composition,
                    $"raid {raid}: the standings promised {promised.Name} and "
                    + $"{game.CurrentRaid.Party.Composition.Name} walked in");
            }
        }
    }
}
