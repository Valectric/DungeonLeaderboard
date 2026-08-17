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
        /// plays banks around 25 and a strong one around 620 — see <see cref="GoodRun"/> for the
        /// distribution that figure comes from.
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
        /// <b>Re-measured 2026-08-17 and raised from 430, because the dungeon's earning power did
        /// change.</b> Parties now grow from four to nine over a season and every room entered adds a
        /// permanent <c>RateModifiers.RoomBonus</c>, so a late raid harvests far more than a 4-strong
        /// opener ever did. Over 1659 raids in 169 simulated seasons the harvest runs: median 349,
        /// mean 370, p90 516, p99 650, best 694. <b>430 had become the 75th percentile</b> — three
        /// raids in four already beat the whole rival field — and the sweep recorded
        /// <b>wins 12 of 12</b>, every play-style winning every seed. The league had stopped being a
        /// contest, which is the same defect as the 500 above with the sign flipped.
        /// </para>
        /// <para>
        /// <b>Taking "the best raid measured" literally was tried first, at 690, and it is wrong —
        /// because that phrase is not sample-size independent.</b> The old 434 was the maximum of a
        /// few dozen raids, which on this distribution is about a p95; the new 694 is the maximum of
        /// 1659, which is a genuine extreme. Reading both as "the max" silently moved the goalposts
        /// two percentiles up the tail. Measured at 690, <c>CompetitivenessTests</c> put the turning
        /// point of the whole competition at a harvest of <b>550 a round</b> against a median raid of
        /// 349 — only the top few percent of raids win anything, which is the same failure as a
        /// walkover with the sign flipped.
        /// <para>
        /// So the figure is the <b>same percentile</b> as the original, not the same word: p95 of the
        /// distribution, re-measured whenever the distribution moves. The competition then turns just above what
        /// a competent bot averages across a season — so average play loses, good play wins, and the
        /// result is in doubt while the season is being played. The rival ceiling lands at 504.
        /// </para>
        /// <para>
        /// It is deliberately <i>not</i> the 837 that <c>EarningCeilingTests</c> finds. That figure is
        /// a different quantity — the most a raid can produce when the search is allowed to play it
        /// perfectly — and pricing rivals against perfect play would put the final out of reach of
        /// anyone playing it as a game.
        /// </para>
        /// <para>
        /// If the dungeon's earning power changes, re-measure and change this. It is a fact about the
        /// game, and the two dials below are the design opinions applied on top of it.
        /// </para>
        /// <para>
        /// <b>Re-measure it on p90, not p95 — the tail is not resolvable here.</b> The percentile
        /// rule above was adopted from a distribution measured once. Sampled twice on an unchanged
        /// build it reads: median 373 and 374, p90 604 and 616, <b>p95 709 and 776</b>. The median is
        /// exact and p90 holds within two percent, but p95 swings nine, because the tail is a handful
        /// of unusually long seasons and those are precisely what the harness's wall-clock leak moves
        /// (D49). Calibrating a constant on a statistic the instrument cannot resolve is how it ends
        /// up chased from 560 to 620 to 710 without the game changing.
        /// </para>
        /// <para>
        /// So 620 stands after the D48 retreat fix rather than being raised again: it sits on the
        /// stable p90 of the current distribution, and the deterministic instrument that matters —
        /// <c>CompetitivenessTests</c>, pure league arithmetic with no frames in it — puts the
        /// competition's turning point at 500 a round against the 410 a competent bot averages.
        /// </para>
        /// </remarks>
        public const float GoodRun = 620f;

        /// <summary>
        /// How far short of the player's own range a rival is held.
        /// </summary>
        /// <remarks>
        /// A rival rolls somewhere between a bad run and a good one, then loses a tenth. That tenth
        /// is the whole design of the contest: <b>play a genuinely good raid and no rival can have
        /// beaten it</b>, because the best roll available to them is 558 against the player's 620.
        /// Play badly and the floor is still above them, but almost every rival clears it.
        /// <para>
        /// Those two numbers read 450 against 500 until 2026-08-17 — figures from the era when
        /// <see cref="GoodRun"/> itself was 500, left behind by the correction to 430 and stale
        /// through it. A doc that quotes a number the code stopped using is worse than one that
        /// quotes none, so they are now derived in the text from the constants above.
        /// </para>
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
        /// round and the final would be an arithmetic check rather than a race. At 0.9, against the
        /// re-measured <see cref="GoodRun"/> of 620, they roll roughly 505 to 558 in the final.
        /// <para>
        /// <b>Tried at 0.55 and reverted, because it bought nothing.</b> The reasoning looked sound —
        /// the player's harvest is flat across a season, 341 in round one against 359 in round seven,
        /// while this climb lifts the rivals' floor, so it reads like the dial that ends runs.
        /// Measured, it is not: correcting <see cref="GoodRun"/> alone took the best of four
        /// play-styles from round seven to round nine, and lowering this as well left it at nine.
        /// The stale ceiling was doing the damage on its own.
        /// </para>
        /// <para>
        /// So D25 stands unchanged — only the floor rises, and late on a rival never has an off day.
        /// Reach for this dial only after a change that makes it earn its keep, and measure it alone,
        /// because a plausible story about a dial is not evidence that moving it does anything.
        /// </para>
        /// </remarks>
        public const float FinalistPressure = 0.9f;

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
