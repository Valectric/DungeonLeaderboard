using System;
using System.Collections.Generic;
using System.Linq;

namespace Dungeon.LeagueManager
{
    /// <summary>One dungeon's standing in the league.</summary>
    public sealed class LeagueEntry
    {
        /// <summary>The dungeon's name.</summary>
        public string Name { get; set; }

        /// <summary>Energy harvested so far this season. This is the ranking key.</summary>
        public float Score { get; set; }

        /// <summary>Whether this row is the player.</summary>
        public bool IsPlayer { get; }

        /// <summary>Position last time the table was shown, for the shift animation.</summary>
        public int PreviousPosition { get; set; }

        /// <summary>Creates an entry.</summary>
        /// <param name="name">Dungeon name.</param>
        /// <param name="score">Starting score.</param>
        /// <param name="isPlayer">Whether this is the player's dungeon.</param>
        public LeagueEntry(string name, float score, bool isPlayer)
        {
            Name = name;
            Score = score;
            IsPlayer = isPlayer;
        }
    }

    /// <summary>
    /// The league standings: twenty dungeons, ranked by energy, with the bottom two relegated.
    /// </summary>
    /// <remarks>
    /// This is a Module.
    /// <para>
    /// SPEC.md section 6 makes the standings the title screen -- no menu, no logo -- because they
    /// are the 10-second hook: a new player reads the board and immediately understands "I am 14th,
    /// 16th is death, I need to climb". Everything here exists to make that sentence legible.
    /// </para>
    /// <para>
    /// Rival scores move every round by a seeded amount, so the table is never static, and a run is
    /// reproducible from its seed for a bug report.
    /// </para>
    /// </remarks>
    public sealed class LeagueTable
    {
        /// <summary>How many dungeons compete.</summary>
        public const int Size = 20;

        /// <summary>How many are relegated -- the bottom 10%.</summary>
        public const int RelegationCount = 2;

        /// <summary>Where the player starts, one-based, per the spec's "around 14th".</summary>
        public const int PlayerStartPosition = 14;

        private readonly List<LeagueEntry> _entries = new();
        private readonly Random _random;
        private readonly List<string> _spareNames;
        private int _spareIndex;

        /// <summary>Standings, best first.</summary>
        public IReadOnlyList<LeagueEntry> Entries => _entries;

        /// <summary>The player's row.</summary>
        public LeagueEntry Player => _entries.First(e => e.IsPlayer);

        /// <summary>The player's current position, one-based.</summary>
        public int PlayerPosition => _entries.FindIndex(e => e.IsPlayer) + 1;

        /// <summary>Whether the player is currently inside the relegation zone.</summary>
        public bool PlayerRelegated => PlayerPosition > Size - RelegationCount;

        /// <summary>How many rounds have been played.</summary>
        public int Round { get; private set; }

        /// <summary>Builds a league with the player sitting around 14th.</summary>
        /// <param name="seed">Seed for names and score movement.</param>
        public LeagueTable(int seed)
        {
            _random = new Random(seed);
            List<string> names = DungeonNames.Generate(Size + 8, seed);
            _spareNames = names.Skip(Size).ToList();

            // Scores descend from the top so the player lands on the spec's fourteenth place with
            // rivals plausibly spread either side, rather than everyone bunched on one number.
            for (int i = 0; i < Size; i++)
            {
                bool isPlayer = i == PlayerStartPosition - 1;
                float score = 16000f - (i * 620f) + (float)((_random.NextDouble() - 0.5) * 260.0);
                _entries.Add(new LeagueEntry(
                    isPlayer ? "Your Dungeon" : names[i], MathF.Round(score), isPlayer)
                {
                    PreviousPosition = i + 1
                });
            }

            Sort();
        }

        /// <summary>
        /// Banks the player's raid and moves every rival, then re-ranks.
        /// </summary>
        /// <param name="harvested">Energy the player harvested this raid.</param>
        public void SubmitRaid(float harvested)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                _entries[i].PreviousPosition = i + 1;
            }

            Player.Score += MathF.Round(harvested);

            foreach (LeagueEntry rival in _entries.Where(e => !e.IsPlayer))
            {
                // Rivals earn on the same scale the player does, so the table stays a real contest
                // rather than a backdrop that drifts while the player climbs past it.
                float earned = 380f + (float)(_random.NextDouble() * 900.0);
                rival.Score += MathF.Round(earned);
            }

            Round++;
            Sort();
        }

        /// <summary>
        /// Replaces the relegated dungeons with new ones, as happens after the player survives.
        /// </summary>
        /// <remarks>
        /// The bottom two collapse and fresh names take their slots, so the league keeps its shape
        /// and the relegation line never becomes a comfortable place to sit.
        /// </remarks>
        public void CollapseRelegated()
        {
            for (int i = Size - RelegationCount; i < Size; i++)
            {
                if (_entries[i].IsPlayer)
                {
                    continue;
                }

                _entries[i].Name = NextSpareName();
                _entries[i].Score = MathF.Round(_entries[Size - RelegationCount - 1].Score * 0.82f);
            }

            Sort();
        }

        /// <summary>Takes the next unused name, generating more when the pool runs out.</summary>
        private string NextSpareName()
        {
            if (_spareIndex >= _spareNames.Count)
            {
                _spareNames.AddRange(DungeonNames.Generate(8, _random.Next()));
            }

            return _spareNames[_spareIndex++];
        }

        /// <summary>Re-ranks by score, best first.</summary>
        private void Sort()
        {
            _entries.Sort((a, b) => b.Score.CompareTo(a.Score));
        }
    }
}
