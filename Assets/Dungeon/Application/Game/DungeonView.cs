using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws a raid: the tiled dungeon, the doors, the party and the mobs.
    /// </summary>
    /// <remarks>
    /// The whole view is built from code and loads its art through <c>Resources</c>, so there is no
    /// hand-wired scene to drift out of step with the simulation. Sprites live under
    /// <c>Assets/Art/Resources</c>, which is both a Unity resources root and inside the folder the
    /// pixel-art importer claims.
    /// <para>
    /// This class only ever reads simulation state. It never moves an adventurer or kills a mob --
    /// if it did, the tests that assert the rules would no longer be asserting the shipped game.
    /// </para>
    /// </remarks>
    public sealed class DungeonView
    {
        /// <summary>World units per grid cell. One cell is one unit at 64 pixels per unit.</summary>
        public const float CellSize = 1f;

        private readonly Transform _root;
        private readonly Dictionary<Vector2Int, SpriteRenderer> _doorViews = new();
        private readonly List<SpriteRenderer> _partyViews = new();
        private readonly List<SpriteRenderer> _mobViews = new();
        private readonly Dictionary<string, Sprite> _cache = new();

        /// <summary>Creates a view and parents everything it makes under one object.</summary>
        /// <param name="root">Transform to build under.</param>
        public DungeonView(Transform root)
        {
            _root = root;
        }

        /// <summary>Converts a grid cell to the world position of its centre.</summary>
        /// <param name="cell">Cell to convert.</param>
        /// <param name="z">Sorting depth.</param>
        /// <returns>World position.</returns>
        public static Vector3 CellToWorld(Vector2Int cell, float z = 0f)
        {
            return new Vector3(cell.x * CellSize, cell.y * CellSize, z);
        }

        /// <summary>Converts a world position back to the grid cell containing it.</summary>
        /// <param name="world">World position.</param>
        /// <returns>The cell.</returns>
        public static Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(
                Mathf.RoundToInt(world.x / CellSize),
                Mathf.RoundToInt(world.y / CellSize));
        }

        /// <summary>
        /// Builds the static dungeon: floor, walls, doors, spawners and traps.
        /// </summary>
        /// <param name="layout">Dungeon to draw.</param>
        public void BuildStatic(DungeonLayout layout)
        {
            DungeonGrid grid = layout.Grid;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    string tile = TileFor(grid, cell);
                    if (tile != null)
                    {
                        Make($"tile_{x}_{y}", $"tiles/{tile}", CellToWorld(cell, 10f), 0);
                    }
                }
            }

            foreach (Door door in grid.Doors)
            {
                SpriteRenderer view = Make($"door_{door.Cell.x}_{door.Cell.y}",
                    "dungeon/door-a", CellToWorld(door.Cell, 5f), 2);
                _doorViews[door.Cell] = view;
            }

            foreach (Vector2Int spawner in layout.SpawnerCells)
            {
                Make($"spawner_{spawner.x}", "dungeon/spawner-skull", CellToWorld(spawner, 4f), 3);
            }

            foreach (Vector2Int trap in layout.TrapCells)
            {
                Make($"trap_{trap.x}", "effects/trap-spikes", CellToWorld(trap, 6f), 1);
            }

            Decorate(layout);
        }

        /// <summary>Props that dress a room without ever affecting play.</summary>
        private static readonly string[] Decorations =
        {
            "props/lanterns", "props/crystals-small", "props/banner",
            "props/candle-skull", "props/books", "props/crystals-large"
        };

        /// <summary>Warm light for flame props; cold arcane light for crystals.</summary>
        private static readonly Color Candlelight = new(1f, 0.55f, 0.18f, 0.30f);

        /// <summary>Colour of the glow cast by crystal props.</summary>
        private static readonly Color ArcaneLight = new(0.84f, 0.32f, 0.86f, 0.26f);

        private Sprite _glow;
        private Sprite _solid;
        private readonly List<SpriteRenderer> _healthBacks = new();
        private readonly List<SpriteRenderer> _healthFills = new();
        private readonly List<SpriteRenderer> _disarmBacks = new();
        private readonly List<SpriteRenderer> _disarmFills = new();

        /// <summary>A single opaque pixel, stretched to draw bars.</summary>
        private Sprite Solid()
        {
            if (_solid != null)
            {
                return _solid;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            _solid = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            return _solid;
        }

        /// <summary>
        /// Builds the soft radial sprite used for light pools, once.
        /// </summary>
        /// <remarks>
        /// Generated in code rather than imported, and drawn with the SpriteRenderer's own default
        /// material. Reaching for <c>Shader.Find</c> here would be the obvious move and the wrong
        /// one: shaders found that way are stripped from a player build unless registered in
        /// Graphics Settings, and the game then renders magenta.
        /// </remarks>
        private Sprite Glow()
        {
            if (_glow != null)
            {
                return _glow;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
                    // Squared falloff reads as light rather than as a flat disc with a hard rim.
                    float fade = Mathf.Clamp01(1f - (distance / centre));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, fade * fade));
                }
            }

            texture.Apply();
            _glow = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            return _glow;
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
        private void Decorate(DungeonLayout layout)
        {
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
                    if (layout.Grid.RoomAt(cell) != room)
                    {
                        continue;
                    }

                    string prop = Decorations[((room * 2) + i) % Decorations.Length];
                    Make($"prop_{room}_{i}", prop, CellToWorld(cell, 7f), 4);

                    // A pool of light under anything that burns or glows. This is most of what
                    // makes the moodboard's rooms read: its torchlit panels spike to 212-252
                    // luminance against a floor sitting near 11, and flat tiles alone cannot
                    // produce that range.
                    bool flame = prop.Contains("lantern") || prop.Contains("candle");
                    bool arcane = prop.Contains("crystal");
                    if (!flame && !arcane)
                    {
                        continue;
                    }

                    var light = new GameObject($"glow_{room}_{i}");
                    light.transform.SetParent(_root, false);
                    light.transform.position = CellToWorld(cell, 8f);
                    light.transform.localScale = Vector3.one * (flame ? 4.6f : 3.8f);
                    var renderer = light.AddComponent<SpriteRenderer>();
                    renderer.sprite = Glow();
                    renderer.color = flame ? Candlelight : ArcaneLight;
                    renderer.sortingOrder = 3;
                }
            }
        }

        /// <summary>Seconds since the raid began, driving all procedural motion.</summary>
        private float _time;

        /// <summary>Redraws everything that moves. Call once per frame.</summary>
        /// <param name="raid">Raid to read.</param>
        /// <param name="deltaTime">Seconds since the last redraw.</param>
        public void Refresh(RaidManager.Raid raid, float deltaTime = 0f)
        {
            _time += deltaTime;
            RefreshDoors(raid.Layout.Grid);
            RefreshParty(raid.Party);
            RefreshMobs(raid.Mobs, raid.Layout.Grid, raid.Layout.Grid.RoomAt(raid.Party.Cell));
            RefreshTraps(raid.Layout);
        }

        /// <summary>
        /// Shows how far the rogue has got with each trap it is defusing.
        /// </summary>
        /// <remarks>
        /// Without this a trap simply stops working and the player never learns why. The bar is the
        /// warning that a purchase is about to be taken away, and the whole point of the mechanic is
        /// the decision it forces: spend the trap now, or lose it.
        /// </remarks>
        private void RefreshTraps(DungeonLayout layout)
        {
            for (int i = 0; i < layout.Traps.Count; i++)
            {
                Trap trap = layout.Traps[i];
                bool show = trap.IsArmed && trap.DisarmProgress > 0f;

                while (_disarmBacks.Count <= i)
                {
                    _disarmBacks.Add(MakeBar($"disarmback_{_disarmBacks.Count}", 58));
                    _disarmFills.Add(MakeBar($"disarmfill_{_disarmFills.Count}", 59));
                }

                _disarmBacks[i].enabled = show;
                _disarmFills[i].enabled = show;
                if (!show)
                {
                    continue;
                }

                Vector3 origin = CellToWorld(trap.Cell, -3f)
                    + new Vector3(-BarWidth * 0.5f, 0.45f, 0f);
                _disarmBacks[i].transform.position = origin;
                _disarmBacks[i].transform.localScale = new Vector3(BarWidth, 0.09f, 1f);
                _disarmBacks[i].color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

                _disarmFills[i].transform.position = origin + new Vector3(0f, 0f, -0.01f);
                _disarmFills[i].transform.localScale =
                    new Vector3(BarWidth * trap.DisarmFraction, 0.09f, 1f);
                _disarmFills[i].color = new Color(1f, 0.62f, 0.2f);
            }
        }

        /// <summary>Swaps each door sprite for its open or closed art.</summary>
        private void RefreshDoors(DungeonGrid grid)
        {
            foreach (Door door in grid.Doors)
            {
                if (_doorViews.TryGetValue(door.Cell, out SpriteRenderer view))
                {
                    // An open door reads as the barred gate you can see through; a closed one as
                    // solid timber. The player has to tell them apart at a glance while the clock
                    // runs, so the difference is silhouette, not a subtle tint.
                    view.sprite = Load(door.IsOpen ? "dungeon/door-gate" : "dungeon/door-a");
                }
            }
        }

        /// <summary>Positions party sprites and picks art matching each member's wound state.</summary>
        private void RefreshParty(Party party)
        {
            IReadOnlyList<Adventurer> members = party.Members;
            while (_partyViews.Count < members.Count)
            {
                _partyViews.Add(Make($"party_{_partyViews.Count}", "party/tank-healthy",
                    Vector3.zero, 20));
            }

            for (int i = 0; i < members.Count; i++)
            {
                Adventurer member = members[i];
                SpriteRenderer view = _partyViews[i];
                if (!member.IsAlive)
                {
                    view.enabled = false;
                    if (i < _healthBacks.Count)
                    {
                        _healthBacks[i].enabled = false;
                        _healthFills[i].enabled = false;
                    }

                    continue;
                }

                view.enabled = true;

                // Drawn straight from the simulation's continuous position -- the party genuinely
                // occupies a column of the corridor in marching order, so no cosmetic fan-out is
                // needed to make four sprites look like four people.
                (float lift, float tilt) = SpriteMotion.ForAdventurer(
                    party.Goal, member.Wounds, _time, i * 1.7f);
                view.transform.position =
                    new Vector3(member.Position.x * CellSize, (member.Position.y * CellSize) + lift, -1f);
                view.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
                view.sprite = Load($"party/{RoleName(member.Role)}-{StateName(member.Wounds)}");

                // Whoever is lower on screen draws in front, so the party overlaps believably as it
                // rounds a corner instead of the back rank punching through the front.
                view.sortingOrder = 20 - Mathf.RoundToInt(member.Position.y * 4f);

                DrawHealthBar(i, member, view.transform.position);
            }
        }

        /// <summary>Width of an adventurer's health bar, in world units.</summary>
        private const float BarWidth = 0.62f;

        /// <summary>
        /// Draws one adventurer's health bar.
        /// </summary>
        /// <remarks>
        /// SPEC.md originally banned any HP readout, on the grounds that ambiguity between "nearly
        /// dead" and "dead in one hit" is where the tension lives. In play that ambiguity produced
        /// deaths the player could not see coming, and a party wipe is the one outcome the whole
        /// design is built to avoid -- so the player needs data they can act on. Superseded
        /// deliberately; see DECISIONS.md D8.
        /// <para>
        /// Colour carries the same information as length, so the state is readable at a glance and
        /// in the corner of the eye, not only by measuring a bar.
        /// </para>
        /// </remarks>
        private void DrawHealthBar(int index, Adventurer member, Vector3 spritePosition)
        {
            while (_healthBacks.Count <= index)
            {
                _healthBacks.Add(MakeBar($"hpback_{_healthBacks.Count}", 60));
                _healthFills.Add(MakeBar($"hpfill_{_healthFills.Count}", 61));
            }

            SpriteRenderer back = _healthBacks[index];
            SpriteRenderer fill = _healthFills[index];
            back.enabled = true;
            fill.enabled = true;

            var origin = new Vector3(
                spritePosition.x - (BarWidth * 0.5f), spritePosition.y + 0.52f, -3f);
            back.transform.position = origin;
            back.transform.localScale = new Vector3(BarWidth, 0.10f, 1f);
            back.color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

            float health = member.HealthFraction;
            fill.transform.position = origin + new Vector3(0f, 0f, -0.01f);
            fill.transform.localScale = new Vector3(BarWidth * health, 0.10f, 1f);
            fill.color = health > 0.6f ? new Color(0.45f, 0.9f, 0.4f)
                : health > 0.3f ? new Color(0.95f, 0.78f, 0.25f)
                : new Color(0.95f, 0.28f, 0.28f);
        }

        /// <summary>Creates one bar quad under the view root.</summary>
        private SpriteRenderer MakeBar(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Solid();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Positions mob sprites, creating views as mobs are spawned.</summary>
        /// <param name="pack">Mobs to draw.</param>
        /// <param name="grid">Dungeon, for resolving which room a mob stands in.</param>
        /// <param name="partyRoom">Room the party occupies, so engaged mobs animate harder.</param>
        private void RefreshMobs(MobPack pack, DungeonGrid grid, int partyRoom)
        {
            IReadOnlyList<Mob> mobs = pack.Mobs;
            while (_mobViews.Count < mobs.Count)
            {
                _mobViews.Add(Make($"mob_{_mobViews.Count}", "mobs/slime", Vector3.zero, 15));
            }

            for (int i = 0; i < mobs.Count; i++)
            {
                Mob mob = mobs[i];
                SpriteRenderer view = _mobViews[i];
                view.enabled = mob.IsAlive;
                if (!mob.IsAlive)
                {
                    continue;
                }

                bool engaged = grid.RoomAt(mob.Cell) == partyRoom;
                float lift = SpriteMotion.ForMob(engaged, _time, i * 0.9f);
                view.transform.position = new Vector3(
                    mob.Position.x * CellSize,
                    (mob.Position.y * CellSize) + 0.1f + lift,
                    -1f);
                view.sprite = Load(mob.Kind == MobKind.Slime ? "mobs/slime" : "mobs/skeleton");
                view.sortingOrder = 15 - Mathf.RoundToInt(mob.Position.y * 4f);
            }
        }

        /// <summary>
        /// Chooses the tile art for a cell.
        /// </summary>
        /// <remarks>
        /// There is deliberately no edge or corner logic. Viewed from directly overhead a masonry
        /// wall looks the same on every side, so one wall tile covers every orientation -- which is
        /// also why this set cannot suffer the corner mismatches the previous, atlas-sliced set had.
        /// <para>
        /// Variant choice is a hash of the cell, never <see cref="Random"/>: a random pick would
        /// shimmer between frames and differ between a test, a screenshot and the shipped build.
        /// </para>
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
                return spread % 23 == 0 ? "floor-drain"
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

        /// <summary>Lowercase role name used in sprite paths.</summary>
        private static string RoleName(AdventurerRole role) => role switch
        {
            AdventurerRole.Tank => "tank",
            AdventurerRole.Healer => "healer",
            AdventurerRole.Ranged => "ranged",
            _ => "mage"
        };

        /// <summary>Lowercase wound name used in sprite paths.</summary>
        private static string StateName(WoundState state) => state switch
        {
            WoundState.Healthy => "healthy",
            WoundState.Hurt => "hurt",
            _ => "critical"
        };

        /// <summary>Creates one sprite object under the view root.</summary>
        private SpriteRenderer Make(string name, string sprite, Vector3 position, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Load(sprite);
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>Loads a sprite from Resources, caching it.</summary>
        private Sprite Load(string path)
        {
            if (_cache.TryGetValue(path, out Sprite cached))
            {
                return cached;
            }

            Sprite loaded = Resources.Load<Sprite>(path);
            if (loaded == null)
            {
                Debug.LogError($"[Dungeon] missing sprite at Resources/{path}");
            }

            _cache[path] = loaded;
            return loaded;
        }
    }
}
