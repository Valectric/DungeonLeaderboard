using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.DungeonManager;
using Dungeon.RaidManager;
using Dungeon.ShopManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Plays whole runs the way a competent player would, and reports how far they get.
    /// </summary>
    /// <remarks>
    /// Every other sweep in this suite asks whether a transition works. None of them asks the
    /// question the game is actually judged on: <b>can a player who understands the rules survive the
    /// league?</b> <c>TenWholeRounds_LeaveTheGameIntact</c> walks the same loop but never spawns a
    /// monster, so it measures a player who does nothing — which is a legitimate thing to be robust
    /// against and tells you nothing about whether the game can be won.
    /// <para>
    /// That gap matters more since the run started opening on a single room. The opening board earns
    /// 51 untouched and 342 played, so the difference between the two policies is now most of the
    /// score, and the league's rival calibration has not been revisited since D13.
    /// </para>
    /// <para>
    /// The policy below is deliberately simple and mechanical — spawn when you can afford it, shut
    /// the door the party is walking at, buy a hall when it is affordable and fittings otherwise.
    /// It is a floor on competence, not a good player. Anything a real player does better than this
    /// only helps them.
    /// </para>
    /// </remarks>
    public sealed class RunProgressionTests
    {
        /// <summary>The controller under test.</summary>
        private GameController _game;

        /// <summary>
        /// Health of the worst survivor below which this player stops pressing.
        /// </summary>
        /// <remarks>
        /// Set per run by the caller, because <b>which value works is the question</b> rather than a
        /// detail: this is the single dial a player has, and the sweep below reports what each
        /// setting is worth.
        /// </remarks>
        private float _ceaseFireBelow = 0.6f;

        /// <summary>What one round of a run did.</summary>
        private struct Round
        {
            /// <summary>Energy harvested.</summary>
            public float Harvested;

            /// <summary>How the raid ended.</summary>
            public RaidOutcome Outcome;

            /// <summary>League position after banking.</summary>
            public int Position;

            /// <summary>Dungeons still in the competition.</summary>
            public int Field;
        }

        /// <summary>Loads the play scene once for the fixture.</summary>
        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            await MooseRunnerFacade.InstanceQuiet.LoadSceneFromNameAsync("Raid");
        }

        /// <summary>Builds a fresh controller, which starts a fresh run.</summary>
        [SetUp]
        public void SetUp()
        {
            DoNotDestroyOnTeardown.CleanSceneImmediate();
            _game = new GameObject("game").AddComponent<GameController>();
        }

        /// <summary>UI scale the controller draws the shop at.</summary>
        private static float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);

        /// <summary>Centre of the Ready button, in input space.</summary>
        /// <returns>A screen point inside the Ready button.</returns>
        private static Vector2 ReadyPoint()
        {
            Rect ready = ShopScreen.ReadyRect(Scale, Screen.width, Screen.height);
            return new Vector2(ready.center.x, Screen.height - ready.center.y);
        }

        /// <summary>The same point in GUI space, which the shop lays its controls out from.</summary>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>The anchor for a popup or marker on that cell.</returns>
        private static Vector2 GuiPoint(Vector2Int cell)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(DungeonView.CellToWorld(cell));
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        /// <summary>Buys one item onto the first tile that will take it.</summary>
        /// <param name="item">What to buy.</param>
        /// <returns>True when something was bought.</returns>
        private bool BuySomething(ShopItem item)
        {
            DungeonLayout layout = _game.CurrentRaid.Layout;
            int before = _game.Loadout.Count(item);

            Vector2Int cell = DeepestBuildableCell(layout);
            if (cell.x < 0)
            {
                return false;
            }

            Vector2 anchor = GuiPoint(cell);
            _game.TapShop(new Vector2(anchor.x, Screen.height - anchor.y));

            Rect[] rows = ShopScreen.PopupRows(anchor, Scale, Screen.width, Screen.height);
            for (int i = 0; i < ShopScreen.Items.Length; i++)
            {
                if (ShopScreen.Items[i] == item)
                {
                    _game.TapShop(new Vector2(
                        rows[i].center.x, Screen.height - rows[i].center.y));
                    return _game.Loadout.Count(item) > before;
                }
            }

            return false;
        }

        /// <summary>
        /// The buildable tile furthest from the entrance, by walking distance.
        /// </summary>
        /// <remarks>
        /// Deep, not near, and it is the difference between a run and a rout. Buying onto the first
        /// buildable tile -- lowest row, lowest column, which is beside the door -- stacked every
        /// purchase in the room the party walks into, and runs ended on raids harvesting <b>exactly
        /// zero</b>: the party met four monsters at the threshold and died before the rate had
        /// accrued anything at all.
        /// <para>
        /// D29 says the same thing from the other end, and this is the sharpest evidence for it so
        /// far: placement is not a refinement in this game, it decides whether a dungeon earns or
        /// commits suicide.
        /// </para>
        /// </remarks>
        /// <param name="layout">Dungeon to search.</param>
        /// <returns>The cell, or a negative cell when there is nowhere to build.</returns>
        private static Vector2Int DeepestBuildableCell(DungeonLayout layout)
        {
            var best = new Vector2Int(-1, -1);
            int furthest = -1;

            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!layout.CanBuildOn(cell))
                    {
                        continue;
                    }

                    int steps = layout.Grid.FindPath(layout.EntranceCell, cell).Count;
                    if (steps > furthest)
                    {
                        furthest = steps;
                        best = cell;
                    }
                }
            }

            return best;
        }

        /// <summary>Buys another hall, if the shop is still offering one.</summary>
        /// <returns>True when the dungeon grew.</returns>
        private bool BuyAHall()
        {
            DungeonLayout layout = _game.CurrentRaid.Layout;
            List<Vector2Int> offered = layout.Plan.Expansions();
            if (offered.Count == 0)
            {
                return false;
            }

            int before = layout.RoomCentres.Count;
            Rect marker = ShopScreen.HallMarkerRect(
                GuiPoint(layout.CentreOfLattice(offered[0])), Scale, Screen.width, Screen.height);
            _game.TapShop(new Vector2(marker.center.x, Screen.height - marker.center.y));

            return _game.CurrentRaid.Layout.RoomCentres.Count > before;
        }

        /// <summary>
        /// Plays one raid to its end, spending energy the way a player who has read the rules does.
        /// </summary>
        /// <remarks>
        /// Driven through the raid rather than through screen coordinates, which is a white-box
        /// shortcut and deliberate: the click path is covered by <c>VerbClickTests</c>, and sixty
        /// seconds a round at real time would make a ten-round run take ten minutes.
        /// </remarks>
        private void PlayRaid()
        {
            Raid raid = _game.CurrentRaid;
            DungeonLayout layout = raid.Layout;

            int guard = 0;
            while (raid.IsRunning && guard++ < 4000)
            {
                // The one skill the design asks for: STOP once the party is nearly dead. A dead
                // party pays nothing, and the wound curve means the last sliver of a health bar is
                // where almost all the money is -- so the difference between a good player and a
                // greedy one is knowing when to stop pressing.
                //
                // Measured, this is not a nicety. Spawning whenever it was affordable earned 343,
                // 380 and 351 in the first three rounds and then wiped the party twice for nothing
                // at all, and the run was eliminated in fifth place of twelve.
                bool safeToPress = raid.Party.WoundFraction > _ceaseFireBelow;

                foreach (Vector2Int spawner in layout.SpawnerCells)
                {
                    int room = layout.Grid.RoomAt(spawner);
                    if (safeToPress && raid.TotalEnergy > Raid.SpawnCost * 2f &&
                        raid.Mobs.CountInRoom(room) < 2)
                    {
                        raid.SpawnMob(spawner);
                    }
                }

                foreach (Door door in layout.Grid.Doors)
                {
                    bool nearby = Vector2.Distance(raid.Party.Position, door.Cell) < 3f;
                    if (door.IsForced || !nearby)
                    {
                        continue;
                    }

                    // Shut what the party is about to walk through while they can take it, and open
                    // it again when they cannot: the safety valve, which costs no extra verb.
                    if (safeToPress == door.IsOpen)
                    {
                        raid.ToggleDoor(door.Cell);
                    }
                }

                raid.Tick(0.05f);
            }
        }

        /// <summary>Spends a shop the way a player building a dungeon would.</summary>
        private void PlayShop()
        {
            // A hall first while they are still cheap enough to matter, then fill it.
            if (!BuyAHall())
            {
                BuySomething(ShopItem.Slime);
            }

            BuySomething(ShopItem.Chest);
            BuySomething(ShopItem.Skeleton);
            _game.TapShop(ReadyPoint());
        }

        /// <summary>Plays a whole run and reports every round of it.</summary>
        /// <param name="maximumRounds">How many rounds to play before giving up.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>What each round did.</returns>
        private async UniTask<List<Round>> PlayARun(int maximumRounds, CancellationToken ct)
        {
            var rounds = new List<Round>();

            _game.Advance();
            await UniTask.Yield(ct);

            for (int round = 0; round < maximumRounds; round++)
            {
                for (int press = 0; press < 4 && !_game.IsRaiding && !_game.IsShopping; press++)
                {
                    _game.Advance();
                    await UniTask.Yield(ct);
                }

                if (_game.IsShopping)
                {
                    PlayShop();
                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                if (!_game.IsRaiding)
                {
                    break;
                }

                PlayRaid();
                await UniTask.Yield(ct);
                await UniTask.Yield(ct);

                float harvested = _game.CurrentRaid.EnergyHarvested;
                RaidOutcome outcome = _game.CurrentRaid.Outcome;

                await UniTask.WaitForSeconds(
                    GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);
                if (!_game.DismissReview())
                {
                    break;
                }

                await UniTask.Yield(ct);

                rounds.Add(new Round
                {
                    Harvested = harvested,
                    Outcome = outcome,
                    Position = _game.League.PlayerPosition,
                    Field = _game.League.Entries.Count
                });

                if (_game.League.PlayerRelegated || _game.HasWon)
                {
                    break;
                }
            }

            return rounds;
        }

        /// <summary>Plays a run at one cease-fire setting and writes down what it did.</summary>
        /// <param name="ceaseFire">Health of the worst survivor below which to stop pressing.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The rounds played.</returns>
        private async UniTask<List<Round>> Attempt(float ceaseFire, CancellationToken ct)
        {
            SetUp();
            _ceaseFireBelow = ceaseFire;

            List<Round> rounds = await PlayARun(12, ct);

            var trail = new List<string>();
            foreach (Round round in rounds)
            {
                // The outcome letter matters as much as the number: a low round is a different
                // problem depending on whether the party died, walked out, or simply earned badly.
                char how = round.Outcome switch
                {
                    RaidOutcome.PartyWiped => 'W',
                    RaidOutcome.PartyEscaped => 'E',
                    _ => 'T'
                };

                trail.Add($"{round.Harvested:F0}{how}@{round.Position}/{round.Field}");
            }

            MooseRunnerFacade.Log(
                $"cease-fire below {ceaseFire:P0}: {rounds.Count} rounds — "
                + $"{string.Join("  ", trail)}; won={_game.HasWon} "
                + $"relegated={_game.League.PlayerRelegated}");

            return rounds;
        }

        /// <summary>
        /// What the player's one judgement call — when to stop spawning — is actually worth.
        /// </summary>
        /// <remarks>
        /// The exploratory instrument, and it earns its place: the first version of this file
        /// spawned whenever it could afford to and the run collapsed at round four with two
        /// consecutive <b>wipes worth nothing at all</b>, after three rounds of 340+. Kills pay
        /// nothing and refund the spawn, so a player who keeps the rooms stocked is not stopped by
        /// the economy — only by learning.
        /// <para>
        /// That is the game working as designed, and it is also the sharpest edge in it, so the
        /// shape of the curve is worth having written down: too eager and the party dies, too timid
        /// and it walks out unharmed. The assertion only asks that <i>some</i> setting gets a player
        /// most of the way through a season. If none does, the league needs retuning rather than the
        /// test relaxing.
        /// </para>
        /// </remarks>
        [Test]
        public async UniTask WhenToStopSpawning_DecidesTheRun(CancellationToken ct)
        {
            var reached = new List<int>();
            var settings = new[] { 0.3f, 0.45f, 0.6f, 0.75f };

            int best = 0;
            bool won = false;
            foreach (float ceaseFire in settings)
            {
                List<Round> rounds = await Attempt(ceaseFire, ct);
                reached.Add(rounds.Count);
                best = Mathf.Max(best, rounds.Count);
                won |= _game.HasWon;
            }

            MooseRunnerFacade.Log(
                $"rounds survived by cease-fire setting: {string.Join(", ", reached)} "
                + $"(best {best}, won={won})");

            Assert.GreaterOrEqual(best, 6,
                $"the best of {settings.Length} plausible ways to play the game reached only round "
                + $"{best} of a ten-round season, so the league is not survivable by playing well");
        }

        /// <summary>
        /// The winning ending exists, is reachable, and draws without throwing.
        /// </summary>
        /// <remarks>
        /// Nothing had ever reached <c>Phase.Won</c>. The sweep above cannot get there — the best of
        /// four ways to play reaches round nine of ten and is eliminated — so the one screen the
        /// game is played <i>for</i> had never been rendered by anything, at any point in the
        /// project. That is precisely where a crash sits undisturbed, and one was already found
        /// next door: <c>LeagueScreen.DrawStrip</c> indexed the standings against the length the
        /// table <b>started</b> at, so it threw out of <c>OnGUI</c> once rivals began to be
        /// eliminated — a fault that can only happen late in a winning run.
        /// <para>
        /// The league is driven to the final through its own public API rather than by playing nine
        /// perfect seasons. That is a white-box shortcut and the right one: the subject here is the
        /// controller's ending and the screen it draws, not the league's arithmetic, which has its
        /// own suite. The last round is played through the controller's real handlers so the win is
        /// declared by the code that declares it in a shipped game.
        /// </para>
        /// </remarks>
        [Test]
        public async UniTask TheWinningEnding_IsReachedAndDrawn(CancellationToken ct)
        {
            _game.Advance();
            await UniTask.Yield(ct);

            // Down to the final: the player and one rival, with the player far ahead.
            int guard = 0;
            while (_game.League.Entries.Count > 2 && guard++ < 40)
            {
                _game.League.SubmitRaid(5000f);
                _game.League.CollapseRelegated();
            }

            Assert.AreEqual(2, _game.League.Entries.Count, "the league never reached a final");
            Assert.IsTrue(_game.League.IsFinal, "and it does not think it is one");

            bool sawError = false;
            void Watch(string condition, string trace, LogType type)
            {
                if (type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    sawError = true;
                    MooseRunnerFacade.Log($"error while the ending drew: {condition}");
                }
            }

            UnityEngine.Application.logMessageReceived += Watch;
            try
            {
                for (int press = 0; press < 4 && !_game.IsRaiding && !_game.IsShopping; press++)
                {
                    _game.Advance();
                    await UniTask.Yield(ct);
                }

                if (_game.IsShopping)
                {
                    _game.TapShop(ReadyPoint());
                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                Assert.IsTrue(_game.IsRaiding, "the final never started");
                PlayRaid();
                await UniTask.Yield(ct);
                await UniTask.Yield(ct);

                await UniTask.WaitForSeconds(
                    GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);
                Assert.IsTrue(_game.DismissReview(), "the final's review refused to be dismissed");

                // Several frames, because the fault this is looking for is in drawing rather than
                // in the transition, and OnGUI runs per frame per event.
                for (int frame = 0; frame < 12; frame++)
                {
                    await UniTask.NextFrame(ct);
                }

                // Photographed, because "it did not throw" is a weaker claim than it sounds and this
                // project has shipped three features that were green and invisible. Nobody has ever
                // seen this screen.
                await UniTask.WaitForEndOfFrame(ct);
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                string directory = Path.Combine(
                    UnityEngine.Application.dataPath, "..", "Screenshots");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "05-the-winning-ending.png");
                File.WriteAllBytes(path, shot.EncodeToPNG());
                Object.DestroyImmediate(shot);
                MooseRunnerFacade.Log($"captured {path}");
            }
            finally
            {
                UnityEngine.Application.logMessageReceived -= Watch;
            }

            MooseRunnerFacade.Log(
                $"after the final: won={_game.HasWon} relegated={_game.League.PlayerRelegated} "
                + $"field={_game.League.Entries.Count} errors={sawError}");

            Assert.IsTrue(_game.HasWon,
                "winning the final did not put the game on its winning ending, so the screen the "
                + "whole run is played for cannot be reached");
            Assert.IsFalse(sawError, "the winning ending logged an error while drawing");
        }

        /// <summary>
        /// A player who stops pressing in time survives the opening rounds.
        /// </summary>
        /// <remarks>
        /// The first round is the one at risk. The run opens on a single room holding one slime pit,
        /// and the rivals' round-one earnings are drawn across their whole range with no handicap
        /// yet — so a weak opening board could put a competent player in the relegation zone before
        /// they have had a chance to build anything. Measured, it does not: the opening board is
        /// worth well over 300 to a player who uses it.
        /// </remarks>
        [Test]
        public async UniTask ACompetentPlayer_SurvivesTheOpeningRounds(CancellationToken ct)
        {
            List<Round> rounds = await Attempt(0.6f, ct);

            Assert.Greater(rounds.Count, 0, "the run never played a round at all");
            Assert.Greater(rounds[0].Harvested, 150f,
                $"the opening raid harvested only {rounds[0].Harvested:F0} for a player using the "
                + "board, which is inside the range a rival can beat by doing nothing");
            Assert.GreaterOrEqual(rounds.Count, 3,
                $"a player using the board lasted {rounds.Count} round(s)");
        }
    }
}
