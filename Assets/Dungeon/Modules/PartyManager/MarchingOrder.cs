using System.Collections.Generic;
using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.PartyManager
{
    /// <summary>
    /// Where each member of the party walks: the leader's breadcrumb trail, and the shape the rest
    /// hold behind it.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="Party"/> on 2026-08-17, which had reached 1474 lines against this
    /// project's 400-line cap. This is its most self-contained piece — it needs the grid to know
    /// where the floor is, and nothing else about the party beyond how many of them there are.
    /// <para>
    /// Following a trail rather than holding a fixed offset is what makes the party round corners in
    /// single file and thread doorways one at a time, instead of a rigid block sliding sideways
    /// through walls. The trail is also the reason a party bunches up when its leader doubles back:
    /// the followers are walking a path that now folds over itself, which is a property of the
    /// trail and not of the formation.
    /// </para>
    /// </remarks>
    public sealed class MarchingOrder
    {
        private readonly DungeonGrid _grid;
        private readonly List<Vector2> _trail = new();
        private int _size;

        /// <summary>Creates a marching order that walks on a given grid.</summary>
        /// <param name="grid">Grid the party walks on, for testing where the floor is.</param>
        public MarchingOrder(DungeonGrid grid)
        {
            _grid = grid;
        }

        /// <summary>
        /// Seeds the trail running back out of the entrance.
        /// </summary>
        /// <remarks>
        /// So the party starts strung out in marching order rather than stacked on one square, and
        /// reads as walking in.
        /// </remarks>
        /// <param name="entranceCell">Where the party comes in.</param>
        public void Seed(Vector2Int entranceCell)
        {
            for (int step = 8; step >= 0; step--)
            {
                _trail.Add(new Vector2(entranceCell.x - (step * 0.25f), entranceCell.y));
            }
        }

        /// <summary>Distance in cells between one rank and the next in the marching order.</summary>
        public const float FollowSpacing = 0.62f;
        /// <summary>Distance in cells between two members standing abreast in the same rank.</summary>
        /// <remarks>
        /// Tighter than <see cref="FollowSpacing"/> because sideways room is the scarce one: a
        /// corridor is a single cell wide, so anything wider than this puts the flanks in rock and
        /// falls back to single file every time it matters.
        /// </remarks>
        public const float AbreastSpacing = 0.5f;
        /// <summary>
        /// How many members walk abreast, for a party of the given size.
        /// </summary>
        /// <remarks>
        /// <b>One up to four, so nothing about the opening party changes.</b> Parties only grow past
        /// four from raid six onwards, and single file is the shape the whole game was tuned against.
        /// <para>
        /// Above that it is a question of how far back the tail sits. Single file puts member nine at
        /// eight times <see cref="FollowSpacing"/> — <b>4.96 cells</b>, a whole room — behind the
        /// tank, so a player filling the room the party is in is spending mobs on a fight half the
        /// party has not arrived at, and the wound curve that is meant to pay them is being applied
        /// to four people out of nine. Three abreast puts the last rank 1.86 cells back instead.
        /// </para>
        /// </remarks>
        /// <param name="size">Living members in the party — a decimated party tightens up again.</param>
        /// <returns>Members per rank, at least one.</returns>
        public static int AbreastFor(int size)
        {
            if (size <= 4)
            {
                return 1;
            }

            return size <= 6 ? 2 : 3;
        }
        /// <summary>Appends to the breadcrumb trail the rest of the party follows.</summary>
        public void Record(Vector2 position, int size)
        {
            // How far back the trail has to reach is set by how many people are following it, so the
            // caller states the party size on every record rather than this class caching a count
            // that would go stale the moment somebody died.
            _size = size;

            if (_trail.Count == 0 || Vector2.Distance(_trail[^1], position) > 0.06f)
            {
                _trail.Add(position);
            }

            int keep = Mathf.CeilToInt((_size * FollowSpacing) / 0.06f) + 8;
            if (_trail.Count > keep)
            {
                _trail.RemoveRange(0, _trail.Count - keep);
            }
        }
        /// <summary>
        /// Places each member the right distance back along the leader's trail.
        /// </summary>
        /// <remarks>
        /// Following a breadcrumb trail rather than holding a fixed offset means the party rounds
        /// corners in single file and threads doorways one at a time, instead of a rigid block
        /// sliding sideways through walls.
        /// </remarks>
        public void Place(IReadOnlyList<Adventurer> living)
        {
            for (int rank = 1; rank < living.Count; rank++)
            {
                living[rank].Position = SlotFor(rank, living.Count);
            }
        }
        /// <summary>Walks back along the trail by a distance and returns the point reached.</summary>
        /// <param name="distance">How far behind the leader to sample, in cells.</param>
        /// <returns>A position on the trail, or its oldest point if the trail is too short.</returns>
        private Vector2 PositionBehind(float distance)
        {
            return PositionBehind(distance, out _);
        }
        /// <summary>
        /// Walks back along the leader's trail, reporting the direction of travel there too.
        /// </summary>
        /// <param name="distance">How far back along the trail to stand, in cells.</param>
        /// <param name="heading">
        /// Unit direction the party is travelling at that point, pointing forwards. Zero when the
        /// trail is too short to have a direction, which is the caller's cue not to fan out.
        /// </param>
        /// <returns>The point that far back along the trail.</returns>
        private Vector2 PositionBehind(float distance, out Vector2 heading)
        {
            heading = Vector2.zero;
            float remaining = distance;
            for (int i = _trail.Count - 1; i > 0; i--)
            {
                float segment = Vector2.Distance(_trail[i], _trail[i - 1]);
                if (segment >= remaining)
                {
                    if (segment > 0.0001f)
                    {
                        heading = (_trail[i] - _trail[i - 1]) / segment;
                        return Vector2.Lerp(_trail[i], _trail[i - 1], remaining / segment);
                    }

                    return _trail[i - 1];
                }

                remaining -= segment;
            }

            // The trail is seeded from the entrance in Seed, so its oldest point is the
            // entrance until the party has walked far enough to push it off the end. Party.cs
            // returned _entranceCell here for the empty case; this class has no reason to know
            // about the entrance beyond that seed, and the trail is never empty once seeded.
            return _trail.Count > 0 ? _trail[0] : Vector2.zero;
        }
        /// <summary>
        /// Where the member at this place in the marching order should stand.
        /// </summary>
        /// <remarks>
        /// Ranks of <see cref="AbreastFor"/> members walk abreast, so the party gets wider rather
        /// than longer as it grows. <b>The lateral offset is given up rather than forced</b>: a
        /// flanker whose cell is rock falls back onto the centre line, so a party three abreast in a
        /// room becomes single file in a corridor without anyone standing inside a wall. That check
        /// is why this is worth having as a method rather than a formula at the call sites.
        /// </remarks>
        /// <param name="rank">Place in the order, where zero is the leader.</param>
        /// <returns>The point that member should be moving towards.</returns>
        public Vector2 SlotFor(int rank, int livingCount)
        {
            int abreast = AbreastFor(livingCount);
            if (abreast <= 1)
            {
                return PositionBehind(rank * FollowSpacing);
            }

            int row = (rank - 1) / abreast;
            int column = (rank - 1) % abreast;
            Vector2 centre = PositionBehind((row + 1) * FollowSpacing, out Vector2 heading);

            if (heading == Vector2.zero)
            {
                return centre;
            }

            float offset = (column - ((abreast - 1) * 0.5f)) * AbreastSpacing;
            if (Mathf.Abs(offset) < 0.0001f)
            {
                return centre;
            }

            // Falling back to the CENTRE looks wrong and is right, and this was tried the other way
            // round first. The objection is that every member of a rank then wants one point, so
            // three sprites stand on one square in a corridor -- measured at 0.000 cells apart.
            //
            // But the CONTROL says the fan does not cause it: four members on the untouched
            // single-file path close to 0.002 cells in the same walk. A party whose leader turns
            // back along its own trail bunches up whatever shape it is in, because the followers are
            // walking a path that now doubles back on itself.
            //
            // Two attempts to "fix" it made the game worse on both figures that actually matter,
            // because deciding the fallback per rank, or from the front of the column, stops the
            // party fanning at all: depth at nine went 2.44 -> 4.13 -> 5.01 cells against single
            // file's 4.96, and rock went 3.1 % -> 5.4 % -> 7.4 %. The simple fallback is the best of
            // the three, and the bunching belongs to the trail, not to this.
            var sideways = new Vector2(-heading.y, heading.x);
            Vector2 flank = centre + (sideways * offset);
            var flankCell = new Vector2Int(Mathf.RoundToInt(flank.x), Mathf.RoundToInt(flank.y));
            return _grid.IsWalkable(flankCell) ? flank : centre;
        }
    }
}
