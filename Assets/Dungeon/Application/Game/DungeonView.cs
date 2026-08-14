using System.Collections.Generic;
using Dungeon.DungeonManager;
using Dungeon.MobManager;
using Dungeon.AudioManager;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
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
        private readonly Dictionary<Vector2Int, SpriteRenderer> _chestViews = new();
        private readonly List<SpriteRenderer> _partyViews = new();
        private readonly List<SpriteRenderer> _mobViews = new();
        private readonly Dictionary<string, Sprite> _cache = new();

        // Facing is view state, not simulation state -- which way a sprite is turned changes nothing
        // about the fight, and a seeded run must replay identically whether or not anyone is looking.
        private readonly List<float> _partyFacing = new();
        private readonly List<float> _partyLastX = new();
        private readonly List<float> _mobFacing = new();
        private readonly List<float> _mobLastX = new();

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
            WorldBounds = new Bounds(
                new Vector3((grid.Width - 1) * 0.5f * CellSize, (grid.Height - 1) * 0.5f * CellSize, 0f),
                new Vector3(grid.Width * CellSize, grid.Height * CellSize, 1f));
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

            // Different art per tier, so a player can tell at a glance which spawner will give them a
            // slime and which will give them a skeleton. They cost different money and buy different
            // amounts of time; making them look identical would hide the only thing worth knowing.
            for (int i = 0; i < layout.SpawnerCells.Count; i++)
            {
                Vector2Int spawner = layout.SpawnerCells[i];
                string art = layout.SpawnerTiers[i] == 0
                    ? "dungeon/spawner-cauldron"
                    : "dungeon/spawner-skull";
                Make($"spawner_{spawner.x}_{spawner.y}", art, CellToWorld(spawner, 4f), 3);
            }

            // Alternating art for the same reason a dungeon is not one repeated tile: six identical
            // spike plates in a row read as a texture rather than as six separate things to fire.
            for (int i = 0; i < layout.TrapCells.Count; i++)
            {
                Vector2Int trap = layout.TrapCells[i];
                string art = (i % 2) == 0 ? "effects/trap-spikes" : "effects/trap-poison";
                Make($"trap_{trap.x}_{trap.y}", art, CellToWorld(trap, 6f), 1);
            }

            foreach (Vector2Int chest in layout.ChestCells)
            {
                _chestViews[chest] = Make(
                    $"chest_{chest.x}_{chest.y}", "dungeon/chest", CellToWorld(chest, 5f), 3);
            }

            DrawApproach(layout);
            Decorate(layout);
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
        private void DrawApproach(DungeonLayout layout)
        {
            Sprite scene = Load("scenes/entrance");
            if (scene == null)
            {
                return;
            }

            var go = new GameObject("approach");
            go.transform.SetParent(_root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = scene;
            renderer.sortingOrder = -5;

            // The sprite imports at 64 pixels per unit like everything else, so its world size
            // follows from its pixel size; place it by its own extents rather than a magic number.
            float halfWidth = scene.bounds.extents.x;
            float entranceY = layout.EntranceCell.y * CellSize;
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
        /// Everything drawn, in world units -- the dungeon plus its approach.
        /// </summary>
        /// <remarks>
        /// The camera frames and clamps panning to this rather than to the grid alone, so scenery
        /// outside the playable cells is reachable but the player cannot wander off into blackness.
        /// </remarks>
        public Bounds WorldBounds { get; private set; }

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

        private Sprite _glow;
        private Sprite _solid;
        private readonly List<SpriteRenderer> _healthBacks = new();
        private readonly List<SpriteRenderer> _healthFills = new();
        private readonly List<SpriteRenderer> _disarmBacks = new();
        private readonly List<SpriteRenderer> _disarmFills = new();
        private readonly List<SpriteRenderer> _mobHealthBacks = new();
        private readonly List<SpriteRenderer> _mobHealthFills = new();
        private readonly List<SpriteRenderer> _manaBacks = new();
        private readonly List<SpriteRenderer> _manaFills = new();

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
                    // About one tile of reach, matching the reference. The previous 4.6 covered a
                    // quarter of the room and measured 5.5x the target's radius.
                    light.transform.localScale = Vector3.one * (flame ? 2.1f : 1.7f);
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
            RefreshChests(raid.Party);
            RefreshShots(raid.Shots);
            RefreshDoorWork(raid.Party);
            SpawnImpacts(raid);
        }

        private SpriteRenderer _doorWorkBack;
        private SpriteRenderer _doorWorkFill;

        /// <summary>
        /// Shows the party working on a shut door.
        /// </summary>
        /// <remarks>
        /// Without this a closed door simply opens by itself after a while and the player never
        /// learns why -- or worse, assumes their verb failed. The colour says which of the two routes
        /// is happening: amber for an archer picking the lock, red for the party breaking it down,
        /// which is the slower and far more expensive answer.
        /// </remarks>
        /// <param name="party">Party to read.</param>
        private void RefreshDoorWork(Party party)
        {
            _doorWorkBack ??= MakeBar("doorworkback", 62);
            _doorWorkFill ??= MakeBar("doorworkfill", 63);

            Door door = party.WorkingOnDoor;
            bool show = door != null;
            _doorWorkBack.enabled = show;
            _doorWorkFill.enabled = show;
            if (!show)
            {
                return;
            }

            float progress = party.PickingLock ? door.PickFraction : door.DamageFraction;
            var origin = new Vector3(
                (door.Cell.x * CellSize) - (BarWidth * 0.5f),
                (door.Cell.y * CellSize) + 0.52f, -3f);

            _doorWorkBack.transform.position = origin;
            _doorWorkBack.transform.localScale = new Vector3(BarWidth, 0.10f, 1f);
            _doorWorkBack.color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

            _doorWorkFill.transform.position = origin + new Vector3(0f, 0f, -0.01f);
            _doorWorkFill.transform.localScale = new Vector3(BarWidth * progress, 0.10f, 1f);
            _doorWorkFill.color = party.PickingLock
                ? new Color(1f, 0.72f, 0.25f)
                : new Color(0.95f, 0.35f, 0.30f);
        }

        private int _numbersSeen;
        private int _shotsSeen;

        /// <summary>
        /// Fires a burst of particles wherever a blow just landed.
        /// </summary>
        /// <remarks>
        /// Driven off the same feeds the floating numbers use, rather than off a second set of combat
        /// events, so a spark and its number can never disagree about what happened.
        /// <para>
        /// Both feeds only ever append, so counting how many entries have been seen is enough to
        /// find the new ones. Comparing contents would mean allocating and diffing a list every
        /// frame to learn something the count already says.
        /// </para>
        /// </remarks>
        /// <param name="raid">Raid to read.</param>
        private void SpawnImpacts(RaidManager.Raid raid)
        {
            IReadOnlyList<CombatNumber> numbers = raid.Feed.Numbers;
            for (int i = _numbersSeen; i < numbers.Count; i++)
            {
                CombatNumber number = numbers[i];
                Burst(number.IsHeal ? "VfxHeal"
                    : number.Target == CombatTarget.Monster ? "VfxHitMonster"
                    : "VfxHitAdventurer", number.Origin);

                // Sound comes off the same feed as the spark and the number, so all three can never
                // disagree about what just happened.
                AudioFacade.Cue(number.IsHeal ? Sfx.Heal
                    : number.Target == CombatTarget.Monster ? Sfx.HitMonster
                    : Sfx.HitAdventurer, number.IsHeal ? 0.5f : 0.35f);
            }

            _numbersSeen = numbers.Count;

            // The three verbs and monster deaths. Drained rather than aged: each one is spawned the
            // instant it is read and then lives as its own particle system.
            foreach (Effect effect in raid.Effects.Pending)
            {
                Burst(effect.Kind switch
                {
                    EffectKind.TrapFired => "VfxTrapFire",
                    EffectKind.MobSpawned => "VfxSpawn",
                    EffectKind.MobDied => "VfxDeath",
                    _ => "VfxDoor"
                }, effect.Position);

                AudioFacade.Cue(effect.Kind switch
                {
                    EffectKind.TrapFired => Sfx.TrapFire,
                    EffectKind.MobSpawned => Sfx.MobSpawn,
                    EffectKind.MobDied => Sfx.MobDied,
                    _ => Sfx.DoorToggle
                }, effect.Kind == EffectKind.TrapFired ? 0.85f : 0.6f);
            }

            raid.Effects.Drain();

            IReadOnlyList<Shot> shots = raid.Shots.Shots;
            for (int i = _shotsSeen; i < shots.Count; i++)
            {
                // A bolt covers both the mage's attack and its blink, and the blink is the one worth
                // a bigger flash -- it is the only thing in the game that moves instantly.
                Shot shot = shots[i];
                float distance = Vector2.Distance(shot.From, shot.To);
                bool blink = shot.Kind == ShotKind.Bolt && distance > 3f;

                Burst(shot.Kind == ShotKind.Arrow ? "VfxArrowImpact"
                    : blink ? "VfxBlink" : "VfxHitMonster", shot.To);

                if (blink)
                {
                    AudioFacade.Cue(Sfx.Blink, 0.55f);
                }
            }

            _shotsSeen = shots.Count;
        }

        /// <summary>
        /// Orthographic size the impact prefabs were tuned at.
        /// </summary>
        /// <remarks>
        /// Particles are authored in world units, so a spark sized to read beside a 1-cell sprite is
        /// invisible once the player zooms out to see the corridor -- measured, a burst tuned at 1.9
        /// simply disappeared at the default play zoom of 5.63. Scaling by the camera keeps them the
        /// same size <i>on screen</i> across the whole zoom range, which is the same thing the
        /// floating damage numbers do with their font size.
        /// </remarks>
        private const float VfxReferenceOrtho = 1.9f;

        private Camera _camera;

        /// <summary>Instantiates one impact prefab, which destroys itself when it finishes.</summary>
        /// <param name="prefab">Prefab name under <c>Resources/vfx/</c>.</param>
        /// <param name="cell">Where it happened, in grid units.</param>
        private void Burst(string prefab, Vector2 cell)
        {
            GameObject art = Resources.Load<GameObject>($"vfx/{prefab}");
            if (art == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            var spawned = Object.Instantiate(art, _root);
            spawned.transform.position = new Vector3(
                cell.x * CellSize, (cell.y * CellSize) + 0.2f, -5f);

            float zoom = _camera == null
                ? 1f
                : Mathf.Clamp(_camera.orthographicSize / VfxReferenceOrtho, 0.5f, 4f);
            spawned.transform.localScale = Vector3.one * zoom;
        }

        private readonly List<SpriteRenderer> _shotViews = new();

        /// <summary>
        /// Draws arrows and mage bolts in flight.
        /// </summary>
        /// <remarks>
        /// Drawn as stretched solid quads rotated along their flight rather than as sprites, because
        /// no arrow art exists and a procedural streak is both cheaper and easier to read at this
        /// scale than a four-pixel arrow would be. An arrow is a pale tan streak; a bolt is a fat
        /// magenta one, matching the arcane glow the crystals cast.
        /// </remarks>
        /// <param name="feed">Shots to draw.</param>
        private void RefreshShots(ProjectileFeed feed)
        {
            IReadOnlyList<Shot> shots = feed.Shots;
            while (_shotViews.Count < shots.Count)
            {
                _shotViews.Add(MakeBar($"shot_{_shotViews.Count}", 40));
            }

            for (int i = 0; i < _shotViews.Count; i++)
            {
                if (i >= shots.Count)
                {
                    _shotViews[i].enabled = false;
                    continue;
                }

                Shot shot = shots[i];
                bool arrow = shot.Kind == ShotKind.Arrow;

                // The head leads, the tail trails behind it, so the streak reads as travelling
                // rather than as a line that appears and vanishes.
                float head = shot.Progress;
                float tail = Mathf.Max(0f, head - (arrow ? 0.22f : 0.16f));
                Vector2 from = Vector2.Lerp(shot.From, shot.To, tail);
                Vector2 to = Vector2.Lerp(shot.From, shot.To, head);

                Vector2 span = to - from;
                float length = Mathf.Max(0.08f, span.magnitude);

                SpriteRenderer view = _shotViews[i];
                view.enabled = true;
                view.transform.position = new Vector3(
                    from.x * CellSize, (from.y * CellSize) + 0.18f, -4f);
                view.transform.rotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
                view.transform.localScale = new Vector3(length, arrow ? 0.055f : 0.1f, 1f);
                view.color = arrow
                    ? new Color(0.95f, 0.86f, 0.62f)
                    : new Color(0.84f, 0.32f, 0.86f);
            }
        }

        /// <summary>
        /// Shows a chest being opened, then takes it away once it is empty.
        /// </summary>
        /// <remarks>
        /// A chest that stayed shut after being looted would have the party ignore it on the way back
        /// for no visible reason. Shrinking as it opens gives the detour a readable payoff.
        /// </remarks>
        /// <param name="party">Party to read.</param>
        private void RefreshChests(Party party)
        {
            foreach (KeyValuePair<Vector2Int, SpriteRenderer> pair in _chestViews)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.enabled = !party.HasLooted(pair.Key);

                float opening = party.LootingCell == pair.Key ? party.LootFraction : 0f;
                pair.Value.transform.localScale = Vector3.one * (1f - (opening * 0.4f));
                pair.Value.color = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), opening);
            }
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
                    party.Goal, member.Wounds, _time, i * 1.7f, member.IsPanicking);

                // An attack throws the sprite on top of whatever it was already doing, so a tank can
                // limp and lunge at once.
                Vector2 shove = Vector2.zero;
                if (member.LastAttackTarget.HasValue && member.AttackPhase < 1f)
                {
                    Vector2 toTarget =
                        (member.LastAttackTarget.Value - member.Position).normalized;
                    (Vector2 lunge, float swing) = SpriteMotion.ForAttack(
                        member.Role, member.AttackPhase, toTarget);
                    shove = lunge;
                    tilt += swing;
                }

                view.transform.position = new Vector3(
                    (member.Position.x + shove.x) * CellSize,
                    ((member.Position.y + shove.y) * CellSize) + lift, -1f);
                view.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
                view.sprite = Load($"party/{RoleName(member.Role)}-{StateName(member.Wounds)}");

                // Squash on the footfall so the walk has weight. Left at scale 1 the bob alone reads
                // as hovering.
                Vector2 squash = SpriteMotion.WalkSquash(
                    party.Goal, member.Wounds, _time, i * 1.7f);
                view.transform.localScale = new Vector3(squash.x, squash.y, 1f);

                // Face the way you are going -- or, while swinging, face what you are hitting, which
                // outranks it: an archer recoils backwards but must still be aiming at the monster.
                while (_partyFacing.Count <= i)
                {
                    _partyFacing.Add(1f);
                    _partyLastX.Add(member.Position.x);
                }

                float step = member.LastAttackTarget.HasValue && member.AttackPhase < 1f
                    ? member.LastAttackTarget.Value.x - member.Position.x
                    : member.Position.x - _partyLastX[i];
                _partyFacing[i] = SpriteMotion.Facing(_partyFacing[i], step);
                _partyLastX[i] = member.Position.x;
                view.flipX = _partyFacing[i] < 0f;

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

            DrawManaBar(index, member, origin);
        }

        /// <summary>
        /// Draws the mage's mana bar, tucked under its health bar.
        /// </summary>
        /// <remarks>
        /// Only the mage has one, so only the mage gets a bar -- three empty blue strips over the
        /// rest of the party would imply a resource they do not have. Blue reads as mana instantly
        /// and collides with nothing else on screen: green and amber and red are health, violet is a
        /// monster, gold is energy.
        /// <para>
        /// It is worth watching. A mage that has spent its pool on bolts cannot afford to blink, and
        /// the player can see that coming a few seconds before the skeleton does.
        /// </para>
        /// </remarks>
        private void DrawManaBar(int index, Adventurer member, Vector3 healthOrigin)
        {
            while (_manaBacks.Count <= index)
            {
                _manaBacks.Add(MakeBar($"manaback_{_manaBacks.Count}", 58));
                _manaFills.Add(MakeBar($"manafill_{_manaFills.Count}", 59));
            }

            bool show = member.MaxMana > 0f;
            _manaBacks[index].enabled = show;
            _manaFills[index].enabled = show;
            if (!show)
            {
                return;
            }

            Vector3 origin = healthOrigin + new Vector3(0f, -0.13f, 0f);
            _manaBacks[index].transform.position = origin;
            _manaBacks[index].transform.localScale = new Vector3(BarWidth, 0.07f, 1f);
            _manaBacks[index].color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

            _manaFills[index].transform.position = origin + new Vector3(0f, 0f, -0.01f);
            _manaFills[index].transform.localScale =
                new Vector3(BarWidth * member.ManaFraction, 0.07f, 1f);

            // Dims when there is not enough left to blink, so "cannot escape" is visible rather than
            // something the player only works out after the mage dies.
            _manaFills[index].color = member.CanCast(Adventurer.BlinkManaCost)
                ? new Color(0.35f, 0.65f, 1f)
                : new Color(0.25f, 0.35f, 0.6f);
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
                _mobHealthBacks.Add(MakeBar($"mobhpback_{_mobHealthBacks.Count}", 56));
                _mobHealthFills.Add(MakeBar($"mobhpfill_{_mobHealthFills.Count}", 57));
            }

            for (int i = 0; i < mobs.Count; i++)
            {
                Mob mob = mobs[i];
                SpriteRenderer view = _mobViews[i];
                view.enabled = mob.IsAlive;
                if (!mob.IsAlive)
                {
                    _mobHealthBacks[i].enabled = false;
                    _mobHealthFills[i].enabled = false;
                    continue;
                }

                bool engaged = grid.RoomAt(mob.Cell) == partyRoom;
                float lift = SpriteMotion.ForMob(engaged, _time, i * 0.9f);

                Vector2 shove = Vector2.zero;
                if (mob.LastAttackTarget.HasValue && mob.AttackPhase < 1f)
                {
                    shove = SpriteMotion.ForMobAttack(
                        mob.AttackPhase, (mob.LastAttackTarget.Value - mob.Position).normalized);
                }

                view.transform.position = new Vector3(
                    (mob.Position.x + shove.x) * CellSize,
                    ((mob.Position.y + shove.y) * CellSize) + 0.1f + lift,
                    -1f);
                view.sprite = Load(mob.Kind == MobKind.Slime ? "mobs/slime" : "mobs/skeleton");
                view.sortingOrder = 15 - Mathf.RoundToInt(mob.Position.y * 4f);

                while (_mobFacing.Count <= i)
                {
                    _mobFacing.Add(1f);
                    _mobLastX.Add(mob.Position.x);
                }

                float step = mob.LastAttackTarget.HasValue && mob.AttackPhase < 1f
                    ? mob.LastAttackTarget.Value.x - mob.Position.x
                    : mob.Position.x - _mobLastX[i];
                _mobFacing[i] = SpriteMotion.Facing(_mobFacing[i], step);
                _mobLastX[i] = mob.Position.x;
                view.flipX = _mobFacing[i] < 0f;

                // How long a mob will hold the party is the single fact the player needs to plan the
                // next twenty seconds around -- and it is their own asset, bought with energy, so
                // unlike an adventurer's health there is nothing to be gained by hiding it.
                SpriteRenderer back = _mobHealthBacks[i];
                SpriteRenderer fill = _mobHealthFills[i];
                back.enabled = true;
                fill.enabled = true;

                var origin = new Vector3(
                    view.transform.position.x - (BarWidth * 0.5f),
                    view.transform.position.y + 0.52f, -3f);
                back.transform.position = origin;
                back.transform.localScale = new Vector3(BarWidth, 0.10f, 1f);
                back.color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

                fill.transform.position = origin + new Vector3(0f, 0f, -0.01f);
                fill.transform.localScale = new Vector3(BarWidth * mob.HealthFraction, 0.10f, 1f);

                // Violet rather than the party's green-amber-red, so a glance never confuses whose
                // bar is whose. The player wants their monster's bar to stay long.
                fill.color = new Color(0.72f, 0.35f, 0.9f);
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
