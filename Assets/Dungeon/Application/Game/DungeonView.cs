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
        }

        /// <summary>Redraws everything that moves. Call once per frame.</summary>
        /// <param name="raid">Raid to read.</param>
        public void Refresh(RaidManager.Raid raid)
        {
            RefreshDoors(raid.Layout.Grid);
            RefreshParty(raid.Party);
            RefreshMobs(raid.Mobs);
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
                    continue;
                }

                view.enabled = true;
                // Fan the four members out inside the cell so they read as a party rather than one
                // sprite. Purely presentational -- the simulation keeps them on a single cell.
                var offset = new Vector3(((i % 2) - 0.5f) * 0.42f, ((i / 2) - 0.5f) * 0.34f, 0f);
                view.transform.position = CellToWorld(member.Cell, -1f) + offset;
                view.sprite = Load($"party/{RoleName(member.Role)}-{StateName(member.Wounds)}");
                view.sortingOrder = 20 + i;
            }
        }

        /// <summary>Positions mob sprites, creating views as mobs are spawned.</summary>
        private void RefreshMobs(MobPack pack)
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

                view.transform.position = CellToWorld(mob.Cell, -1f) + new Vector3(0f, 0.1f, 0f);
                view.sprite = Load(mob.Kind == MobKind.Slime ? "mobs/slime" : "mobs/skeleton");
            }
        }

        /// <summary>Chooses the tile art for a cell from its kind and its neighbours.</summary>
        private static string TileFor(DungeonGrid grid, Vector2Int cell)
        {
            CellKind kind = grid.KindAt(cell);
            if (kind == CellKind.Doorway)
            {
                return "floor-plain";
            }

            if (kind == CellKind.Floor)
            {
                // A cheap deterministic scatter of variants. Random would shimmer between frames and
                // differ between a test and the build; this stays put.
                int hash = (cell.x * 7) + (cell.y * 13);
                return hash % 5 == 0 ? "floor-cracked" : hash % 7 == 0 ? "floor-rubble" : "floor-plain";
            }

            bool floorBelow = grid.KindAt(cell + Vector2Int.down) != CellKind.Wall;
            bool floorAbove = grid.KindAt(cell + Vector2Int.up) != CellKind.Wall;
            bool floorLeft = grid.KindAt(cell + Vector2Int.left) != CellKind.Wall;
            bool floorRight = grid.KindAt(cell + Vector2Int.right) != CellKind.Wall;

            if (floorBelow && floorRight) return "corner-tl";
            if (floorBelow && floorLeft) return "corner-tr";
            if (floorAbove && floorRight) return "corner-bl";
            if (floorAbove && floorLeft) return "corner-br";
            if (floorBelow) return "wall-top";
            if (floorAbove) return "wall-bottom";
            if (floorRight) return "wall-left";
            if (floorLeft) return "wall-right";
            return null;
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
