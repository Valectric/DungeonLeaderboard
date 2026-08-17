using System.Collections.Generic;
using Dungeon.DungeonManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Builds everything in the dungeon that is placed once and never moves.
    /// </summary>
    /// <remarks>
    /// Floor, walls, doors, spawners, traps, chests, the forest approach, the atmosphere props and
    /// their pools of light — plus the markers the shop paints over buildable tiles. Split out of
    /// <see cref="DungeonView"/>, which had grown past the project's 400-line cap while doing two
    /// unrelated jobs: laying out a dungeon once, and moving sprites every frame.
    /// <para>
    /// Everything here reads the layout and never the simulation, so a scenery object cannot end up
    /// tracking something that moves.
    /// </para>
    /// </remarks>
    public sealed class DungeonScenery
    {
        /// <summary>Props that dress a room without ever affecting play.</summary>
        private static readonly string[] Decorations =
        {
            "props/lanterns", "props/crystals-small", "props/banner",
            "props/candle-skull", "props/books", "props/crystals-large"
        };

        /// <summary>Warm light for flame props; cold arcane light for crystals.</summary>
        /// <remarks>
        /// Deliberately weak. An art review measured the moodboard's rooms as essentially flat-lit:
        /// floor luminance varies by 3.5 across a whole room, against 16.1 in an earlier version of
        /// this view. Its torches are bright <i>sprites</i> that barely light the floor at all --
        /// beyond about a tile, the floor under a sconce is actually darker than the far side of the
        /// room. Strong pools looked atmospheric in isolation and wrong beside the reference.
        /// </remarks>
        private static readonly Color Candlelight = new(1f, 0.55f, 0.18f, 0.20f);

        /// <summary>Colour of the glow cast by crystal props.</summary>
        private static readonly Color ArcaneLight = new(0.84f, 0.32f, 0.86f, 0.17f);

        /// <summary>
        /// Resolution the glow sprite is generated at, in logical pixels.
        /// </summary>
        /// <remarks>
        /// The moodboard is a 120x120 image shown at 4x, so every one of its 4x4 screen cells is
        /// flat and its effective pixel is four screen pixels. A smooth 64px gradient shreds that
        /// grid: measured on a composed room, tiles that went in 100% flat on 4x4 came out at 8%,
        /// and the lighting pass alone was responsible. Generating the glow small and letting point
        /// filtering blow it up keeps the light on the same grid as the art it falls on.
        /// </remarks>
        private const int GlowResolution = 16;

        private readonly SpriteWorkshop _sprites;
        private readonly Dictionary<Vector2Int, SpriteRenderer> _doorViews = new();

        /// <summary>Upper halves of the doorframes, drawn over the party. Empty when the art is absent.</summary>
        private readonly Dictionary<Vector2Int, SpriteRenderer> _doorTops = new();

        /// <summary>Resources path of the upper half of a doorframe.</summary>
        private const string DoorTopSprite = "dungeon/door-top";

        /// <summary>
        /// Sorting order of the upper half of a doorframe.
        /// </summary>
        /// <remarks>
        /// <b>Must stay above the party's 20</b>, which is the whole point of the piece: it is what
        /// makes an adventurer pass behind the frame instead of over it. <c>DoorOcclusionTests</c>
        /// fails if this stops exceeding the party's own layer.
        /// </remarks>
        private const int DoorTopOrder = 25;
        private readonly Dictionary<Vector2Int, SpriteRenderer> _chestViews = new();
        private Sprite _glow;

        /// <summary>Creates scenery that builds through a shared sprite workshop.</summary>
        /// <param name="sprites">Workshop to make objects with.</param>
        public DungeonScenery(SpriteWorkshop sprites)
        {
            _sprites = sprites;
        }

        /// <summary>The door sprites, so the view can open and shut them.</summary>
        public IReadOnlyDictionary<Vector2Int, SpriteRenderer> DoorViews => _doorViews;

        /// <summary>Upper doorframe halves by cell. Empty when the two-part art is not installed.</summary>
        public IReadOnlyDictionary<Vector2Int, SpriteRenderer> DoorTops => _doorTops;

        /// <summary>Sorting order the upper doorframe halves are drawn at.</summary>
        public static int DoorTopSortingOrder => DoorTopOrder;

        /// <summary>The chest sprites, so the view can show them looted.</summary>
        public IReadOnlyDictionary<Vector2Int, SpriteRenderer> ChestViews => _chestViews;

        /// <summary>
        /// Everything drawn, in world units -- the dungeon plus its approach.
        /// </summary>
        /// <remarks>
        /// The camera frames and clamps panning to this rather than to the grid alone, so scenery
        /// outside the playable cells is reachable but the player cannot wander off into blackness.
        /// </remarks>
        public Bounds WorldBounds { get; private set; }

        /// <summary>
        /// Builds the static dungeon: floor, walls, doors, spawners and traps.
        /// </summary>
        /// <param name="layout">Dungeon to draw.</param>
        public void Build(DungeonLayout layout)
        {
            DungeonGrid grid = layout.Grid;
            WorldBounds = new Bounds(
                new Vector3((grid.Width - 1) * 0.5f * DungeonView.CellSize,
                    (grid.Height - 1) * 0.5f * DungeonView.CellSize, 0f),
                new Vector3(grid.Width * DungeonView.CellSize,
                    grid.Height * DungeonView.CellSize, 1f));

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    string tile = TileFor(grid, cell);

                    // A wall that knows which sides face open floor, when the art can draw one.
                    // Falls through to the flat variants when it cannot, so this is inert until a
                    // tileset with the sixteen pieces is imported and correct the moment one is.
                    if (grid.KindAt(cell) == CellKind.Wall)
                    {
                        string shaped = $"wall-{WallMask(grid, cell)}";
                        if (_sprites.Load($"tiles/{shaped}", quiet: true) != null)
                        {
                            tile = shaped;
                        }
                    }

                    if (tile != null)
                    {
                        _sprites.Make($"tile_{x}_{y}", $"tiles/{tile}",
                            DungeonView.CellToWorld(cell, 10f), 0);
                    }
                }
            }

            foreach (Door door in grid.Doors)
            {
                _doorViews[door.Cell] = _sprites.Make($"door_{door.Cell.x}_{door.Cell.y}",
                    "dungeon/door-a", DungeonView.CellToWorld(door.Cell, 5f), 2);

                // The UPPER half of the frame, drawn above the party so adventurers pass BEHIND it.
                // A door drawn as one sprite can only ever be behind them, which reads as the party
                // walking over the doorway rather than through it.
                //
                // The lintel is CUT FROM door-a's own top rows rather than imported from anywhere:
                // seventeen solid rows of the stone arch and a five-row alpha ramp under them. That
                // is what makes it free of the licence problem that held this feature up -- the
                // two-part art originally wanted was Pipoya's, which cannot be committed to a public
                // repo -- and it is also simply better, because a band cut from the door it covers
                // matches it pixel for pixel in both states, door-a and door-gate alike. There is no
                // seam to hide because the pixels underneath are the same pixels.
                //
                // Still loaded quietly and skipped when absent: the sprite is optional, and a clone
                // that has not imported it should lose the occlusion, not the doors.
                if (_sprites.Load(DoorTopSprite, quiet: true) != null)
                {
                    _doorTops[door.Cell] = _sprites.Make(
                        $"doortop_{door.Cell.x}_{door.Cell.y}", DoorTopSprite,
                        DungeonView.CellToWorld(door.Cell, 4f), DoorTopOrder);
                }
            }

            // The way in, stood open. It is a Doorway cell with no Door behind it, so it is drawn
            // here rather than in the loop above -- there is nothing to toggle and nothing to tap,
            // and door-b is the open leaf. Without this the first room was a sealed box that the
            // party appeared inside, which is what the author asked to fix.
            var opening = new Vector2Int(layout.EntranceCell.x - 1, layout.EntranceCell.y);
            if (grid.InBounds(opening) && grid.KindAt(opening) == CellKind.Doorway
                && grid.DoorAt(opening) == null)
            {
                _sprites.Make($"entrance_{opening.x}_{opening.y}", "dungeon/door-open",
                    DungeonView.CellToWorld(opening, 5f), 2);
            }

            // Different art per tier, so a player can tell at a glance which spawner will give them a
            // slime and which will give them a skeleton. They cost different money and buy different
            // amounts of time; making them look identical would hide the only thing worth knowing.
            for (int i = 0; i < layout.SpawnerCells.Count; i++)
            {
                Vector2Int spawner = layout.SpawnerCells[i];
                string art = layout.SpawnerTiers[i] == 0
                    ? "dungeon/spawner-cauldron"
                    : "dungeon/spawner-skull";
                _sprites.Make($"spawner_{spawner.x}_{spawner.y}", art,
                    DungeonView.CellToWorld(spawner, 4f), 3);
            }

            // Alternating art for the same reason a dungeon is not one repeated tile: six identical
            // spike plates in a row read as a texture rather than as six separate things to fire.
            for (int i = 0; i < layout.TrapCells.Count; i++)
            {
                Vector2Int trap = layout.TrapCells[i];
                string art = (i % 2) == 0 ? "effects/trap-spikes" : "effects/trap-poison";
                _sprites.Make($"trap_{trap.x}_{trap.y}", art,
                    DungeonView.CellToWorld(trap, 6f), 1);
            }

            foreach (Vector2Int chest in layout.ChestCells)
            {
                _chestViews[chest] = _sprites.Make($"chest_{chest.x}_{chest.y}", "dungeon/chest",
                    DungeonView.CellToWorld(chest, 5f), 3);
            }

            DrawApproach(layout);
            Decorate(layout);
        }

        /// <summary>
        /// Marks every tile the player may build on, for the shop.
        /// </summary>
        /// <remarks>
        /// Without this the shop tells the player to "tap any empty tile" and then leaves them to
        /// work out which ones those are by tapping walls and getting nothing. The rule is not
        /// guessable from the picture — a doorway looks like floor, and the cell under a candle looks
        /// empty — so it has to be drawn.
        /// <para>
        /// Read straight from <see cref="DungeonLayout.CanBuildOn"/>, the same predicate the tap is
        /// tested against, so the marks cannot promise a tile the purchase would refuse.
        /// </para>
        /// </remarks>
        /// <param name="layout">Dungeon to mark up.</param>
        public void MarkBuildableTiles(DungeonLayout layout)
        {
            for (int y = 0; y < layout.Grid.Height; y++)
            {
                for (int x = 0; x < layout.Grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!layout.CanBuildOn(cell))
                    {
                        continue;
                    }

                    SpriteRenderer marker = _sprites.MakeBar($"buildable_{x}_{y}", 9);
                    marker.color = new Color(0.66f, 0.40f, 0.92f, 0.085f);
                    marker.transform.position = new Vector3(
                        (x * DungeonView.CellSize) - (DungeonView.CellSize * 0.42f),
                        y * DungeonView.CellSize, -0.5f);
                    marker.transform.localScale = new Vector3(
                        DungeonView.CellSize * 0.78f, DungeonView.CellSize * 0.78f, 1f);
                }
            }
        }

        /// <summary>
        /// Places the forest approach to the left of the entrance.
        /// </summary>
        /// <remarks>
        /// The party used to simply appear at the left-hand wall, which read as them walking out of
        /// nothing. This puts the world they came from on screen: forest, a path winding out of it,
        /// a forecourt, and the archway they are about to enter. Purely scenery -- it sits outside
        /// the grid entirely and nothing pathfinds through it.
        /// <para>
        /// Anchored by its right edge to the dungeon's left wall so the archway lines up with the
        /// entrance whatever size the corridor is.
        /// </para>
        /// </remarks>
        /// <param name="layout">Dungeon the approach leads into.</param>
        private void DrawApproach(DungeonLayout layout)
        {
            Sprite scene = _sprites.Load("scenes/entrance");
            if (scene == null)
            {
                return;
            }

            var go = new GameObject("approach");
            go.transform.SetParent(_sprites.Root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = scene;
            renderer.sortingOrder = -5;

            // The sprite imports at 64 pixels per unit like everything else, so its world size
            // follows from its pixel size; place it by its own extents rather than a magic number.
            float halfWidth = scene.bounds.extents.x;
            float entranceY = layout.EntranceCell.y * DungeonView.CellSize;
            // Tucked slightly under the dungeon's left wall rather than butted against it. A gap
            // here shows the empty camera background as a black seam between the two, which reads
            // as a rendering fault rather than as scenery meeting architecture.
            go.transform.position = new Vector3(-halfWidth + 0.6f, entranceY, 12f);

            // Read into a local, grow it, assign back. Bounds is a struct, so calling Encapsulate
            // straight on the property mutates a temporary copy and silently does nothing -- which
            // is exactly what happened: the camera never widened to include the approach.
            Bounds bounds = WorldBounds;
            bounds.Encapsulate(new Bounds(
                go.transform.position, new Vector3(halfWidth * 2f, scene.bounds.size.y, 1f)));
            WorldBounds = bounds;
        }

        /// <summary>
        /// Scatters atmosphere props around the edges of each room.
        /// </summary>
        /// <remarks>
        /// Purely cosmetic, and deliberately placed against the walls rather than in the middle so
        /// they never sit under the party or a mob and confuse what is happening. Selection is by
        /// room index rather than randomly, so the dungeon looks identical in a screenshot, a test
        /// and the shipped build.
        /// </remarks>
        /// <param name="layout">Dungeon to dress.</param>
        private void Decorate(DungeonLayout layout)
        {
            // Nothing decorative may land on something the player taps. Props draw above spawners
            // and traps, so a prop sharing a cell hides it completely -- a bought slime pit went in
            // at exactly a decoration spot and was invisible under a banner in the shipped build.
            var occupied = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in layout.SpawnerCells) { occupied.Add(cell); }
            foreach (Vector2Int cell in layout.TrapCells) { occupied.Add(cell); }
            foreach (Vector2Int cell in layout.ChestCells) { occupied.Add(cell); }
            foreach (Door door in layout.Grid.Doors) { occupied.Add(door.Cell); }

            for (int room = 0; room < layout.RoomCentres.Count; room++)
            {
                Vector2Int centre = layout.RoomCentres[room];
                var spots = new[]
                {
                    new Vector2Int(centre.x - 1, centre.y + 2),
                    new Vector2Int(centre.x + 1, centre.y - 2)
                };

                for (int i = 0; i < spots.Length; i++)
                {
                    Vector2Int cell = spots[i];
                    if (layout.Grid.RoomAt(cell) != room || occupied.Contains(cell))
                    {
                        continue;
                    }

                    string prop = Decorations[((room * 2) + i) % Decorations.Length];
                    _sprites.Make($"prop_{room}_{i}", prop, DungeonView.CellToWorld(cell, 7f), 4);
                    LightUnder(prop, room, i, cell);
                }
            }
        }

        /// <summary>Puts a pool of light under a prop that burns or glows.</summary>
        /// <remarks>
        /// This is most of what makes the moodboard's rooms read: its torchlit panels spike to
        /// 212-252 luminance against a floor sitting near 11, and flat tiles alone cannot produce
        /// that range.
        /// </remarks>
        /// <param name="prop">Resources path of the prop, which decides the colour.</param>
        /// <param name="room">Room index, for the object name.</param>
        /// <param name="index">Spot index within the room, for the object name.</param>
        /// <param name="cell">Cell the prop stands on.</param>
        private void LightUnder(string prop, int room, int index, Vector2Int cell)
        {
            bool flame = prop.Contains("lantern") || prop.Contains("candle");
            bool arcane = prop.Contains("crystal");
            if (!flame && !arcane)
            {
                return;
            }

            var light = new GameObject($"glow_{room}_{index}");
            light.transform.SetParent(_sprites.Root, false);
            light.transform.position = DungeonView.CellToWorld(cell, 8f);
            // About one tile of reach, matching the reference. The previous 4.6 covered a quarter of
            // the room and measured 5.5x the target's radius.
            light.transform.localScale = Vector3.one * (flame ? 2.1f : 1.7f);
            var renderer = light.AddComponent<SpriteRenderer>();
            renderer.sprite = Glow();
            renderer.color = flame ? Candlelight : ArcaneLight;
            renderer.sortingOrder = 3;
        }

        /// <summary>
        /// A soft radial sprite, generated rather than imported.
        /// </summary>
        /// <remarks>
        /// Built in code so the game ships no lighting asset and nothing can go missing in a build.
        /// Reaching for <c>Shader.Find</c> here would be the obvious move and the wrong one: shaders
        /// found that way are stripped from a player build unless registered in Graphics Settings,
        /// and the game then renders magenta.
        /// </remarks>
        /// <returns>The shared glow sprite, created on first use.</returns>
        private Sprite Glow()
        {
            if (_glow != null)
            {
                return _glow;
            }

            const int size = GlowResolution;

            // Point, not bilinear. Bilinear would smooth the falloff back into a continuous gradient
            // and undo the whole reason for generating it small.
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                    // Cubed falloff: a tight core that fades fast, rather than a wide wash. The
                    // reference's light reaches roughly one tile, not the four an earlier version
                    // covered.
                    float fade = Mathf.Clamp01(1f - (distance / centre));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, fade * fade * fade));
                }
            }

            texture.Apply();
            _glow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _glow;
        }

        /// <summary>
        /// Chooses floor or wall art for a cell.
        /// </summary>
        /// <remarks>
        /// Varied by a cheap spatial hash rather than randomly, so the same dungeon looks identical
        /// in a screenshot, a test and the shipped build.
        /// </remarks>
        /// <param name="grid">Dungeon being drawn.</param>
        /// <param name="cell">Cell to choose art for.</param>
        /// <returns>A sprite name under <c>tiles/</c>, or null to leave the cell empty.</returns>
        private static string TileFor(DungeonGrid grid, Vector2Int cell)
        {
            CellKind kind = grid.KindAt(cell);
            int spread = (cell.x * 7) + (cell.y * 13);

            if (kind == CellKind.Doorway)
            {
                return "floor-plain";
            }

            if (kind == CellKind.Floor)
            {
                // The drain is a strong, dark, readable shape, so it has to stay rare -- at every
                // 23rd cell a corridor sprouted six of them and they read as holes in the floor.
                return spread % 41 == 0 ? "floor-drain"
                    : spread % 5 == 0 ? "floor-cracked"
                    : spread % 7 == 0 ? "floor-rubble"
                    : "floor-plain";
            }

            // Every wall cell is drawn, with no "does it border floor" shortcut. That optimisation
            // left unpainted black columns between rooms -- the masonry either side of a doorway
            // read as a hole straight through the dungeon. The whole grid is around 130 cells, so
            // the saving was never worth the failure mode.
            return spread % 11 == 0 ? "wall-moss" : "wall";
        }

        /// <summary>
        /// Which sides of a wall cell are backed by more wall, as a four-bit mask.
        /// </summary>
        /// <remarks>
        /// North is 1, east 2, south 4, west 8, so a wall buried in rock is 15 and a wall with open
        /// floor to the south is 11. Sixteen cases, which is the standard blob-tile numbering a
        /// tileset that ships edges and corners is drawn against.
        /// <para>
        /// This is the hole in the current art, and it is a code fact as much as an art one: every
        /// wall cell has always been given the same sprite regardless of its neighbours, so no
        /// redraw of a single tile could ever make a wall turn a corner. The masonry reads as a
        /// repeated band because that is exactly what it is.
        /// </para>
        /// <para>
        /// Anything off the grid counts as wall. The dungeon is surrounded by rock, and treating the
        /// void as open floor would draw a lit edge along the outside of the map facing nothing.
        /// </para>
        /// </remarks>
        /// <param name="grid">Dungeon being drawn.</param>
        /// <param name="cell">Wall cell to classify.</param>
        /// <returns>A mask from 0 to 15.</returns>
        public static int WallMask(DungeonGrid grid, Vector2Int cell)
        {
            int mask = 0;
            if (IsSolid(grid, cell + Vector2Int.up)) { mask |= 1; }
            if (IsSolid(grid, cell + Vector2Int.right)) { mask |= 2; }
            if (IsSolid(grid, cell + Vector2Int.down)) { mask |= 4; }
            if (IsSolid(grid, cell + Vector2Int.left)) { mask |= 8; }
            return mask;
        }

        /// <summary>Whether a cell is wall, or off the map and therefore rock.</summary>
        /// <param name="grid">Dungeon being drawn.</param>
        /// <param name="cell">Cell to test.</param>
        /// <returns>True when nothing can stand there.</returns>
        private static bool IsSolid(DungeonGrid grid, Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= grid.Width || cell.y >= grid.Height)
            {
                return true;
            }

            return grid.KindAt(cell) == CellKind.Wall;
        }
    }
}
