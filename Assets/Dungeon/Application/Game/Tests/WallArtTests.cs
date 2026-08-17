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
    
        /// <summary>
        /// The open door reads as a hole, not as a differently-coloured closed door.
        /// </summary>
        /// <remarks>
        /// <b>The author's complaint, turned into a number.</b> He reported that the open door "looks
        /// like it's closed", and the measurement said exactly why: the middle of the open sprite was
        /// luminance <b>56.7</b> against the shut one's <b>47.8</b>. It was BRIGHTER — a solid, lit
        /// surface either way, just a different colour. Nothing about it said "you can walk through
        /// this", which matters because the door is one of the three verbs and the only one whose
        /// state the player has to read at a glance.
        /// <para>
        /// So the rule is about the centre rather than the frame: whatever art either state uses, the
        /// open one has to be substantially darker where the doorway is, because that is what seeing
        /// through into an unlit room looks like. Stated as a ratio rather than an absolute so a
        /// future repaint of both doors cannot fail it for being generally darker.
        /// </para>
        /// </remarks>
        [Test]
        public void TheOpenDoor_ReadsAsAnOpening()
        {
            Assert.IsNotNull(Resources.Load<Sprite>("dungeon/door-a"), "the shut door is missing");
            Assert.IsNotNull(Resources.Load<Sprite>("dungeon/door-gate"), "the open door is missing");

            float shutCentre = CentreLuminance("door-a");
            float openCentre = CentreLuminance("door-gate");

            MooseRunnerFacade.Log(
                $"door centre luminance: shut {shutCentre:F1}, open {openCentre:F1}");

            Assert.Less(openCentre, shutCentre * 0.5f,
                $"the open door's middle reads at {openCentre:F1} against the shut door's "
                + $"{shutCentre:F1}, so it is a lit surface rather than a way through -- which is "
                + "exactly the complaint that produced this test");
        }

        /// <summary>Mean luminance of the middle third of a door sprite, read from the PNG.</summary>
        /// <remarks>
        /// From disk rather than from the loaded <c>Sprite</c>, because an imported texture is not
        /// CPU-readable unless Read/Write is ticked — and ticking it on art to satisfy a test would
        /// put a second copy of every door in the shipped build's memory. Decoding the file into a
        /// throwaway texture costs nothing anybody plays.
        /// </remarks>
        /// <param name="name">File stem under <c>Assets/Art/Resources/dungeon</c>.</param>
        /// <returns>Mean luminance, 0 to 255, with transparent pixels counted as black.</returns>
        private static float CentreLuminance(string name)
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "Art", "Resources", "dungeon", name + ".png");
            Assert.IsTrue(System.IO.File.Exists(path), $"{path} is not on disk");

            var texture = new Texture2D(2, 2);
            texture.LoadImage(System.IO.File.ReadAllBytes(path));

            int x0 = texture.width / 3;
            int y0 = texture.height / 3;
            Color[] pixels = texture.GetPixels(x0, y0, texture.width / 3, texture.height / 3);
            Object.DestroyImmediate(texture);

            float total = 0f;
            foreach (Color pixel in pixels)
            {
                total += ((0.2126f * pixel.r) + (0.7152f * pixel.g) + (0.0722f * pixel.b))
                         * pixel.a * 255f;
            }

            return total / pixels.Length;
        }
    }
}
