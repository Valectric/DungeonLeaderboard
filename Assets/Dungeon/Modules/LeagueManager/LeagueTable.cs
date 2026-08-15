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

        /// <summary>
        /// How many leave at the end of a round while the field is still large.
        /// </summary>
        /// <remarks>
        /// Two, which is what makes the competition a sitting rather than a campaign. Twenty
        /// dungeons losing two a round reach the last pair in nine rounds, and a tenth round decides
        /// it — against nineteen rounds at one a time, which is nineteen minutes plus shops and far
        /// too long for somebody voting on a jam entry.
        /// </remarks>
        public const int RelegationCount = 2;

        /// <summary>
        /// How many dungeons leave at the end of the round about to be played.
        /// </summary>
        /// <remarks>
        /// Two until only two are left, and then one — because taking two from a field of two would
        /// leave nobody holding the trophy. That last round is the final.
        /// </remarks>
        public int EliminationsThisRound => _entries.Count > 2 ? RelegationCount : 1;

        /// <summary>Whether this is the last round, with the competition decided at the end of it.</summary>
        public bool IsFinal => _entries.Count == 2;

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

        /// <summary>
        /// Whether the player is bottom of the table and therefore out.
        /// </summary>
        /// <remarks>
        /// One dungeon leaves each round, so last place is the only dangerous one — and with the
        /// field shrinking, last place gets easier to reach every round.
        /// </remarks>
        public bool PlayerRelegated =>
            _entries.Count > 1 && PlayerPosition > _entries.Count - EliminationsThisRound;

        /// <summary>How many dungeons are still in the competition.</summary>
        public int Remaining => _entries.Count;

        /// <summary>
        /// Whether the player is the last dungeon standing, which is how the game is won.
        /// </summary>
        /// <remarks>
        /// SPEC.md gives the game a losing ending and never a winning one. This is it: survive every
        /// elimination and the leaderboard has one name left on it.
        /// </remarks>
        public bool PlayerWon => _entries.Count == 1 && _entries[0].IsPlayer;

        /// <summary>How many rounds have been played.</summary>
        public int Round { get; private set; }

        /// <summary>Builds a league with the player sitting around 14th.</summary>
        /// <param name="seed">Seed for names and score movement.</param>
        public LeagueTable(int seed)
        {
            _random = new Random(seed);
            List<string> names = DungeonNames.Generate(Size + 8, seed);
            _spareNames = names.Skip(Size).ToList();

            // Everyone starts on nothing. The old table opened with scores descending from 16,000 so
            // the player sat fourteenth on arrival, which made the first raid a gesture -- no single
            // round could move them through a field already spread over 12,000 points. From zero,
            // every round is the whole story.
            for (int i = 0; i < Size; i++)
            {
                bool isPlayer = i == PlayerStartPosition - 1;
                _entries.Add(new LeagueEntry(
                    isPlayer ? "Your Dungeon" : names[i], 0f, isPlayer)
                {
                    PreviousPosition = i + 1
                });
            }

            Sort();
        }

        /// <summary>
        /// What a really bad raid harvests, and the bottom of a rival's range.
        /// </summary>
        /// <remarks>
        /// Rivals are priced against what the player can actually do, because that is the only scale
        /// on which the table is a contest. Measured over the season sweeps, a raid the player barely
        /// plays banks around 25 and a strong one around 430.
        /// </remarks>
        public const float BadRun = 25f;

        /// <summary>What a really good raid harvests, and the top of a rival's range.</summary>
        /// <remarks>
        /// <b>Measured, not chosen.</b> This read 500 for most of the project and no raid has ever
        /// harvested it: the four play-styles in <c>RunProgressionTests</c> bank 226 to 434 across a
        /// season, mean 308. A rival ceiling derived from 500 therefore sat <i>above</i> anything the
        /// game can produce, which quietly inverted the promise on <see cref="RivalHandicap"/> —
        /// there was no raid good enough that a rival could not have beaten it.
        /// <para>
        /// If the dungeon's earning power changes, re-measure and change this. It is a fact about the
        /// game, and the two dials below are the design opinions applied on top of it.
        /// </para>
        /// </remarks>
        public const float GoodRun = 430f;

        /// <summary>
        /// How far short of the player's own range a rival is held.
        /// </summary>
        /// <remarks>
        /// A rival rolls somewhere between a bad run and a good one, then loses a tenth. That tenth
        /// is the whole design of the contest: <b>play a genuinely good raid and no rival can have
        /// beaten it</b>, because the best roll available to them is 450 against the player's 500.
        /// Play badly and the floor is still above them, but almost every rival clears it.
        /// <para>
        /// So the league answers skill directly rather than statistically. The player is never
        /// eliminated by an unlucky round they played well.
        /// </para>
        /// </remarks>
        public const float RivalHandicap = 0.9f;

        /// <summary>Least a rival earns in the opening round, when the whole field is still in.</summary>
        public const float RivalFloor = BadRun * RivalHandicap;

        /// <summary>Most a rival can ever earn in a round, in any round of the competition.</summary>
        public const float RivalCeiling = GoodRun * RivalHandicap;

        /// <summary>How much a rival's earnings vary in the opening round.</summary>
        public const float RivalSpread = RivalCeiling - RivalFloor;

        /// <summary>
        /// How far the surviving rivals close on their own ceiling by the final.
        /// </summary>
        /// <remarks>
        /// Short of 1 on purpose: at 1 the last rival would score exactly the same number every
        /// round and the final would be an arithmetic check rather than a race.
        /// <para>
        /// <b>This is the dial that was ending runs, and the ceiling was not.</b> The player's
        /// harvest is flat across a season — 341 in round one and 359 in round seven, measured — while
        /// this climb lifted the rivals' floor from 22 to 407. At 0.9 the final field rolled a mean of
        /// 428 against a player mean of 308, so the last third of every season was arithmetically
        /// lost however well it was played, and the four play-styles in <c>RunProgressionTests</c>
        /// died in rounds five, six, seven and seven.
        /// </para>
        /// <para>
        /// At 0.55 the final field rolls roughly 223 to 387 for a mean of 305, against that same
        /// player mean of 308 — dead level, which is what "winnable but only with a genuinely good
        /// raid" has to mean when written as arithmetic. Raise it again only alongside a dungeon that
        /// earns more late than it does early; while the harvest is flat, this dial is a countdown.
        /// </para>
        /// </remarks>
        public const float FinalistPressure = 0.55f;

        /// <summary>
        /// How strong the surviving rivals are right now, from 0 in the opening round to
        /// <see cref="FinalistPressure"/> in the final.
        /// </summary>
        /// <remarks>
        /// The dungeons knocked out each round are the ones that earned least, so the field that
        /// remains is the field that was already doing well — a competition where the survivors go
        /// on rolling from the same range as the twenty that started is a competition that gets
        /// <i>easier</i> as it goes, which is backwards.
        /// <para>
        /// Only the floor moves. The ceiling stays at <see cref="RivalCeiling"/> in every round, so
        /// the handicap promise survives intact: play a genuinely good raid and no rival can have
        /// beaten it, in the first round or the last. What a shrinking field takes away is their bad
        /// rounds — late on, a rival never has an off day, so the player cannot coast in on one good
        /// raid and a rival's stumble.
        /// </para>
        /// </remarks>
        public float FieldStrength
        {
            get
            {
                const int finalists = 2;
                int startingRivals = Size - finalists;
                if (startingRivals <= 0)
                {
                    return 0f;
                }

                // System.MathF, not UnityEngine.Mathf: this module has no engine reference and is
                // worth keeping that way -- the league is arithmetic and a table, not a scene.
                float knockedOut = Math.Clamp(
                    (Size - _entries.Count) / (float)startingRivals, 0f, 1f);
                return knockedOut * FinalistPressure;
            }
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
                float floor = RivalFloor + ((RivalCeiling - RivalFloor) * FieldStrength);
                float earned = floor + (float)(_random.NextDouble() * (RivalCeiling - floor));
                rival.Score += MathF.Round(earned);
            }

            Round++;
            Sort();
        }

        /// <summary>
        /// Knocks the bottom dungeons out of the competition.
        /// </summary>
        /// <remarks>
        /// Called after the player survives a round. Two leave each time until only two are left,
        /// and then one, so twenty dungeons reach a winner in ten rounds. Nothing refills the gaps --
        /// that is the difference between a league that runs forever and a competition that ends.
        /// <para>
        /// Refuses to remove the player. Being in the drop zone is what <see cref="PlayerRelegated"/>
        /// reports, and the run ends there rather than here; this only ever clears rivals away.
        /// </para>
        /// </remarks>
        public void CollapseRelegated()
        {
            int leaving = EliminationsThisRound;
            for (int i = 0; i < leaving && _entries.Count > 1; i++)
            {
                LeagueEntry doomed = _entries[_entries.Count - 1];
                if (doomed.IsPlayer)
                {
                    return;
                }

                _entries.Remove(doomed);
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
            // Ties break on the previous standing, which matters now that every dungeon starts on
            // zero: without it List.Sort's unstable ordering scattered a table of twenty identical
            // scores arbitrarily, and the player -- who is meant to open around fourteenth, per
            // SPEC.md -- landed anywhere. It also means a round nobody scored in leaves the table
            // exactly as it was, rather than reshuffling for no reason the player can see.
            _entries.Sort((a, b) =>
            {
                int byScore = b.Score.CompareTo(a.Score);
                return byScore != 0 ? byScore : a.PreviousPosition.CompareTo(b.PreviousPosition);
            });
        }
    }
}
