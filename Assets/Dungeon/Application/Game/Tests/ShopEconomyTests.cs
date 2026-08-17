using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Dungeon.ShopManager;
using MooseRunner;
using MooseRunner.helper;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Measures whether the shop is still a choice late in a season.
    /// </summary>
    /// <remarks>
    /// A rate nobody had measured, and this project's doctrine is explicit that it is made of rates
    /// and that green tests hide broken ones. The shop's whole job is to make the player choose: five
    /// placeable items, 75 to 125 each, <b>500 for one of everything</b>. If the purse outgrows that,
    /// the player stops choosing and starts buying the lot, and thirty seconds of decision becomes
    /// thirty seconds of clicking.
    /// <para>
    /// It became a live question on 2026-08-17. The growth curve was fixed so parties reach nine, the
    /// room bonus went permanent, and the best measured raid went from 694 to <b>1120</b> — more than
    /// twice the price of the whole shop. What the purse actually is, though, is what the player did
    /// <i>not</i> spend during the raid, so it does not follow the harvest directly, and guessing
    /// either way is what the doctrine warns against.
    /// </para>
    /// </remarks>
    public sealed class ShopEconomyTests
    {
        /// <summary>Cost of one of every placeable item, from <c>Shop</c>'s price table.</summary>
        private const float WholeShop = 500f;

        /// <summary>The controller under test.</summary>
        private GameController _game;

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

        /// <summary>
        /// The purse never grows past the point where everything is affordable every round.
        /// </summary>
        /// <remarks>
        /// Played by a bot that spends the way the game asks — spawning while the party can take it
        /// and stopping when they cannot — because the purse is the residue of that decision. A
        /// player who never spends would bank everything and prove nothing about the design.
        /// <para>
        /// The assertion is deliberately generous: it fails only if the purse reaches twice the
        /// price of the entire shop, which is the point at which no purchase can be a trade-off. The
        /// interesting output is the printed curve.
        /// </para>
        /// </remarks>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable check.</returns>
        [Test]
        public async UniTask ThePurse_NeverOutgrowsTheWholeShop(CancellationToken ct)
        {
            var purses = new List<string>();
            float worst = 0f;
            int worstRound = 0;

            _game.Advance();
            await UniTask.Yield(ct);

            // Iterations, NOT rounds -- each round takes several phase transitions, so ten
            // iterations reached only the first three shops. The first version of this labelled
            // those "r3, r6, r9" from the loop counter and would have reported an early-season
            // sample as a late-season one, which is the instrument error this project keeps finding.
            for (int step = 0; step < 60 && _game.League.Round < 9; step++)
            {
                // Play the raid the way the game asks: press while they can take it, stop when they
                // cannot. Ticked directly so a season does not take ten real minutes.
                if (_game.IsRaiding)
                {
                    RaidManager.Raid raid = _game.CurrentRaid;
                    DungeonManager.DungeonLayout layout = raid.Layout;
                    int spin = 0;

                    while (raid.IsRunning && spin++ < 4000)
                    {
                        bool safe = raid.Party.WoundFraction > 0.45f;
                        foreach (Vector2Int spawner in layout.SpawnerCells)
                        {
                            if (safe && raid.TotalEnergy > RaidManager.Raid.SpawnCost * 2f &&
                                raid.Mobs.CountInRoom(layout.Grid.RoomAt(spawner)) < 2)
                            {
                                raid.SpawnMob(spawner);
                            }
                        }

                        raid.Tick(0.05f);
                    }

                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                if (_game.IsReviewing)
                {
                    await UniTask.WaitForSeconds(
                        GameController.ReviewLockoutSeconds + 0.2f, cancellationToken: ct);
                    _game.DismissReview();
                    await UniTask.Yield(ct);
                }

                if (_game.IsShopping)
                {
                    float purse = _game.CurrentShop.Purse;
                    purses.Add($"r{_game.League.Round}:{purse:F0}");
                    if (purse > worst)
                    {
                        worst = purse;
                        worstRound = _game.League.Round;
                    }

                    _game.CurrentShop.Ready();
                    await UniTask.Yield(ct);
                    await UniTask.Yield(ct);
                }

                // Keep the run alive so the season reaches its later rounds; the subject here is the
                // purse, not whether this bot can survive the league.
                if (_game.League.Entries.Count > 1)
                {
                    float leader = 0f;
                    foreach (LeagueManager.LeagueEntry entry in _game.League.Entries)
                    {
                        leader = Mathf.Max(leader, entry.Score);
                    }

                    _game.League.Player.Score = leader + 1000f;
                }

                if (!_game.IsRaiding && !_game.IsReviewing && !_game.IsShopping)
                {
                    _game.Advance();
                }

                await UniTask.Yield(ct);
            }

            MooseRunnerFacade.Log(
                $"purse at each shop -- {string.Join("  ", purses)} "
                + $"(reached league round {_game.League.Round}, party of "
                + $"{PartyManager.PartyComposition.SizeForRound(_game.League.Round)})");

            Assert.GreaterOrEqual(_game.League.Round, 6,
                $"only reached round {_game.League.Round}, so this never saw a grown party and says "
                + "nothing about the late-season economy it was written to measure");
            MooseRunnerFacade.Log(
                $"biggest purse {worst:F0} at round {worstRound}; one of everything costs {WholeShop:F0}");

            Assert.Less(worst, WholeShop * 2f,
                $"the purse reached {worst:F0} by round {worstRound}, more than twice the "
                + $"{WholeShop:F0} it costs to buy one of everything -- past that point no purchase "
                + "is a trade-off and the shop stops being a decision");
        }
    }
}
