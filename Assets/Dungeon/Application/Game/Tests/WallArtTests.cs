using System.Collections.Generic;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Checks that every wall shape the autotiler can ask for has art behind it.
    /// </summary>
    /// <remarks>
    /// <b>Written because the failure here is silent by design.</b> <c>DungeonScenery</c> asks for
    /// <c>tiles/wall-{mask}</c> with the loader's <c>quiet</c> flag set and falls back to a flat wall
    /// when nothing comes back — deliberately, so the feature could be landed before the tileset
    /// existed. The consequence now that the tileset does exist is that a deleted or misnamed piece
    /// produces no error, no warning, and a tile that looks plain rather than broken.
    /// <para>
    /// <c>WallShapeTests</c> measures which masks the dungeons actually produce and found ten of
    /// sixteen across one-, three- and five-room corridors. That is a fact about those layouts
    /// rather than about the mask: halls the player buys make shapes a fixed corridor never does, so
    /// the other six are unexercised rather than unreachable, and every one of the sixteen has to be
    /// there.
    /// </para>
    /// </remarks>
    public sealed class WallArtTests
    {
        /// <summary>
        /// All sixteen shaped wall sprites load.
        /// </summary>
        /// <remarks>
        /// Loaded through <c>Resources</c> exactly as the scenery does, so this fails for the same
        /// reasons the game would: a missing file, a rename, or a folder that did not ship.
        /// </remarks>
        [Test]
        public void EveryWallShape_HasASprite()
        {
            var missing = new List<string>();

            for (int mask = 0; mask < 16; mask++)
            {
                string path = $"tiles/wall-{mask}";
                if (Resources.Load<Sprite>(path) == null)
                {
                    missing.Add(path);
                }
            }

            MooseRunnerFacade.Log(
                missing.Count == 0
                    ? "all sixteen shaped wall sprites load"
                    : $"missing: {string.Join(", ", missing)}");

            Assert.IsEmpty(missing,
                $"{missing.Count} of the sixteen wall shapes have no sprite -- the scenery loads "
                + "these quietly and falls back to a flat wall, so the dungeon would draw those "
                + "pieces plain with nothing logged and nothing looking obviously wrong");
        }

        /// <summary>
        /// The floor variants the scenery scatters are present too.
        /// </summary>
        /// <remarks>
        /// Same silent-fallback reasoning, and these carry the room's texture: lose them and every
        /// floor becomes one flat tile, which reads as a rendering setting rather than as missing
        /// files.
        /// </remarks>
        [Test]
        public void TheFloorVariants_HaveSprites()
        {
            var missing = new List<string>();

            foreach (string name in new[] { "floor-plain", "floor-cracked", "floor-drain", "floor-rubble" })
            {
                if (Resources.Load<Sprite>($"tiles/{name}") == null)
                {
                    missing.Add(name);
                }
            }

            MooseRunnerFacade.Log(
                missing.Count == 0
                    ? "all four floor variants load"
                    : $"missing: {string.Join(", ", missing)}");

            Assert.IsEmpty(missing, $"floor art missing: {string.Join(", ", missing)}");
        }

        /// <summary>
        /// The loader really can report a missing sprite.
        /// </summary>
        /// <remarks>
        /// The control. Both checks above are satisfied by a <c>Resources.Load</c> that has stopped
        /// returning null for anything, and this repository has produced that shape of vacuous pass
        /// twice this week — a sealed fixture whose route assertions all passed against an empty
        /// list, and a <c>strings</c> command that did not exist reporting zero matches.
        /// </remarks>
        [Test]
        public void AMissingSprite_ReadsAsMissing()
        {
            Assert.IsNull(Resources.Load<Sprite>("tiles/wall-16"),
                "a wall shape that cannot exist loaded anyway, so the two checks above cannot fail "
                + "and prove nothing");
        }
    }
}
