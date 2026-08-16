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

        private readonly SpriteWorkshop _sprites;
        private readonly DungeonScenery _scenery;
        private readonly PartyBars _bars;
        private readonly List<SpriteRenderer> _partyViews = new();
        private readonly List<SpriteRenderer> _mobViews = new();

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
            _sprites = new SpriteWorkshop(root);
            _scenery = new DungeonScenery(_sprites);
            _bars = new PartyBars(_sprites);
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

        /// <summary>Everything drawn, in world units -- the dungeon plus its approach.</summary>
        /// <remarks>
        /// The camera frames and clamps panning to this rather than to the grid alone, so scenery
        /// outside the playable cells is reachable but the player cannot wander off into blackness.
        /// </remarks>
        public Bounds WorldBounds => _scenery.WorldBounds;

        /// <summary>Builds the static dungeon: floor, walls, doors, spawners, traps and scenery.</summary>
        /// <param name="layout">Dungeon to draw.</param>
        public void BuildStatic(DungeonLayout layout) => _scenery.Build(layout);

        /// <summary>
        /// Hides every overlay a raid draws, for screens that are not a raid.
        /// </summary>
        /// <remarks>
        /// The dungeon keeps being drawn behind the standings on purpose — it is the player's own
        /// dungeon and that is the joke — but these overlays are the part of it that survives the
        /// screen's darkening, because they are saturated where the masonry is not. See
        /// <see cref="PartyBars.HideAll"/> for the measured reason.
        /// <para>
        /// <b>This covers every overlay, and the first two attempts did not.</b> Hiding only the
        /// party's bars left the identical fault on the winning ending, where four monster health
        /// bars — <c>mobhpfill_25/26</c> and <c>mobhpback_25/26</c> — were still drawn as flat violet
        /// quads beside the tank. Fixing the instance in front of me rather than the class is a
        /// mistake this file has now made twice, so the loop below takes every collection there is.
        /// </para>
        /// </remarks>
        public void HideRaidOverlays()
        {
            _bars.HideAll();

            foreach (List<SpriteRenderer> quads in new[]
                     {
                         _disarmBacks, _disarmFills, _mobHealthBacks, _mobHealthFills, _shotViews
                     })
            {
                foreach (SpriteRenderer quad in quads)
                {
                    quad.enabled = false;
                }
            }

            if (_doorWorkBack != null)
            {
                _doorWorkBack.enabled = false;
            }

            if (_doorWorkFill != null)
            {
                _doorWorkFill.enabled = false;
            }
        }

        /// <summary>Marks every tile the player may build on, for the shop.</summary>
        /// <param name="layout">Dungeon to mark up.</param>
        public void MarkBuildableTiles(DungeonLayout layout) => _scenery.MarkBuildableTiles(layout);

        private readonly List<SpriteRenderer> _disarmBacks = new();
        private readonly List<SpriteRenderer> _disarmFills = new();
        private readonly List<SpriteRenderer> _mobHealthBacks = new();
        private readonly List<SpriteRenderer> _mobHealthFills = new();

        /// <summary>
        /// Draw order for the party, counted down by height so lower sprites overlap higher ones.
        /// </summary>
        /// <remarks>
        /// The base has to be high enough that the whole grid stays above the scenery. It used to be
        /// 20, which put a party member at y=5 on exactly the floor tiles' own order and anything at
        /// y=6 <b>behind the floor</b> -- the sprite was still there, still simulated, and simply not
        /// drawn. A mob was worse: base 15 meant y=4 already sorted under the tiles, and spawners sit
        /// at y=5, so a monster spawned at one could be invisible from birth.
        /// <para>
        /// Reported from play as a party member vanishing during a Skirmishers raid. Nothing in the
        /// simulation was wrong, which is why no test caught it and only a pair of eyes did.
        /// </para>
        /// <para>
        /// Scenery occupies 0 to 9 and the bars start at 56, so the sprites get the band between.
        /// The five-point gap between party and mobs is unchanged, so at equal height the party
        /// still draws in front exactly as before.
        /// </para>
        /// </remarks>
        private const int PartySortingBase = 50;

        /// <summary>Draw order for monsters, five below the party at the same height.</summary>
        private const int MobSortingBase = 45;

        /// <summary>Draw order for arrows and bolts, above every sprite and below the bars.</summary>
        private const int ShotSortingOrder = 54;

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
            _doorWorkBack ??= _sprites.MakeBar("doorworkback", 62);
            _doorWorkFill ??= _sprites.MakeBar("doorworkfill", 63);

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
                (door.Cell.x * CellSize) - (PartyBars.BarWidth * 0.5f),
                (door.Cell.y * CellSize) + 0.52f, -3f);

            _doorWorkBack.transform.position = origin;
            _doorWorkBack.transform.localScale = new Vector3(PartyBars.BarWidth, 0.10f, 1f);
            _doorWorkBack.color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

            _doorWorkFill.transform.position = origin + new Vector3(0f, 0f, -0.01f);
            _doorWorkFill.transform.localScale = new Vector3(PartyBars.BarWidth * progress, 0.10f, 1f);
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
                // The refund is a NUMBER, not a burst -- Raid raises it through the combat feed at
                // the corpse. It shares its tick and its position with the monster's death effect,
                // so drawing it here would stack a second burst on the one already playing, and the
                // fall-through below would make that burst a door.
                if (effect.Kind == EffectKind.SpawnRefunded)
                {
                    continue;
                }

                Burst(effect.Kind switch
                {
                    EffectKind.TrapFired => "VfxTrapFire",
                    EffectKind.MobSpawned => "VfxSpawn",
                    EffectKind.MobDied => "VfxDeath",
                    EffectKind.ChestOpened => "VfxLoot",
                    _ => "VfxDoor"
                }, effect.Position);

                AudioFacade.Cue(effect.Kind switch
                {
                    EffectKind.TrapFired => Sfx.TrapFire,
                    EffectKind.MobSpawned => Sfx.MobSpawn,
                    EffectKind.MobDied => Sfx.MobDied,

                    // The purchase chime, borrowed deliberately. The player already reads it as
                    // "you have been paid", which is exactly what opening a chest now means: five
                    // seconds of bonus rate for the whole team.
                    EffectKind.ChestOpened => Sfx.Purchase,
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

            var spawned = Object.Instantiate(art, _sprites.Root);
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
                _shotViews.Add(_sprites.MakeBar($"shot_{_shotViews.Count}", ShotSortingOrder));
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
            foreach (KeyValuePair<Vector2Int, SpriteRenderer> pair in _scenery.ChestViews)
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
                    _disarmBacks.Add(_sprites.MakeBar($"disarmback_{_disarmBacks.Count}", 58));
                    _disarmFills.Add(_sprites.MakeBar($"disarmfill_{_disarmFills.Count}", 59));
                }

                _disarmBacks[i].enabled = show;
                _disarmFills[i].enabled = show;
                if (!show)
                {
                    continue;
                }

                Vector3 origin = CellToWorld(trap.Cell, -3f)
                    + new Vector3(-PartyBars.BarWidth * 0.5f, 0.45f, 0f);
                _disarmBacks[i].transform.position = origin;
                _disarmBacks[i].transform.localScale = new Vector3(PartyBars.BarWidth, 0.09f, 1f);
                _disarmBacks[i].color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

                _disarmFills[i].transform.position = origin + new Vector3(0f, 0f, -0.01f);
                _disarmFills[i].transform.localScale =
                    new Vector3(PartyBars.BarWidth * trap.DisarmFraction, 0.09f, 1f);
                _disarmFills[i].color = new Color(1f, 0.62f, 0.2f);
            }
        }

        /// <summary>Swaps each door sprite for its open or closed art.</summary>
        private void RefreshDoors(DungeonGrid grid)
        {
            foreach (Door door in grid.Doors)
            {
                if (_scenery.DoorViews.TryGetValue(door.Cell, out SpriteRenderer view))
                {
                    // An open door reads as the barred gate you can see through; a closed one as
                    // solid timber. The player has to tell them apart at a glance while the clock
                    // runs, so the difference is silhouette, not a subtle tint.
                    view.sprite = _sprites.Load(door.IsOpen ? "dungeon/door-gate" : "dungeon/door-a");
                }
            }
        }

        /// <summary>Positions party sprites and picks art matching each member's wound state.</summary>
        private void RefreshParty(Party party)
        {
            IReadOnlyList<Adventurer> members = party.Members;
            while (_partyViews.Count < members.Count)
            {
                _partyViews.Add(_sprites.Make($"party_{_partyViews.Count}", "party/tank-healthy",
                    Vector3.zero, 20));
            }

            for (int i = 0; i < members.Count; i++)
            {
                Adventurer member = members[i];
                SpriteRenderer view = _partyViews[i];
                if (!member.IsAlive)
                {
                    view.enabled = false;
                    _bars.Hide(i);
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
                view.sprite = FrameFor(member, i);

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
                view.sortingOrder = PartySortingBase - Mathf.RoundToInt(member.Position.y * 4f);

                _bars.Draw(i, member, view.transform.position);
            }
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
                _mobViews.Add(_sprites.Make($"mob_{_mobViews.Count}", "mobs/slime", Vector3.zero, 15));
                _mobHealthBacks.Add(_sprites.MakeBar($"mobhpback_{_mobHealthBacks.Count}", 56));
                _mobHealthFills.Add(_sprites.MakeBar($"mobhpfill_{_mobHealthFills.Count}", 57));
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
                view.sprite = FrameForMob(mob, i);
                view.sortingOrder = MobSortingBase - Mathf.RoundToInt(mob.Position.y * 4f);

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
                    view.transform.position.x - (PartyBars.BarWidth * 0.5f),
                    view.transform.position.y + 0.52f, -3f);
                back.transform.position = origin;
                back.transform.localScale = new Vector3(PartyBars.BarWidth, 0.10f, 1f);
                back.color = new Color(0.05f, 0.04f, 0.08f, 0.92f);

                fill.transform.position = origin + new Vector3(0f, 0f, -0.01f);
                fill.transform.localScale = new Vector3(PartyBars.BarWidth * mob.HealthFraction, 0.10f, 1f);

                // Violet rather than the party's green-amber-red, so a glance never confuses whose
                // bar is whose. The player wants their monster's bar to stay long.
                fill.color = new Color(0.72f, 0.35f, 0.9f);
            }
        }

        /// <summary>
        /// The sprite a monster should be showing this instant.
        /// </summary>
        /// <remarks>
        /// The same shape as the party's: an attack takes precedence and plays once off its own
        /// phase, a walk cycle loops while it is closing, and everything falls back to the single
        /// static sprite. A monster with no drawn frames keeps working exactly as before.
        /// </remarks>
        /// <param name="mob">Monster being drawn.</param>
        /// <param name="index">Its spawn index, so a room of them does not move in lockstep.</param>
        /// <returns>The sprite to show.</returns>
        private Sprite FrameForMob(Mob mob, int index)
        {
            string kind = mob.Kind == MobKind.Slime ? "slime" : "skeleton";

            if (mob.LastAttackTarget.HasValue && mob.AttackPhase < 1f)
            {
                int swing = Mathf.Clamp(
                    Mathf.FloorToInt(mob.AttackPhase * WalkFrames), 0, WalkFrames - 1);
                Sprite struck = _sprites.Load($"mobs/attack/{kind}-attack-{swing + 1}", quiet: true);
                if (struck != null)
                {
                    return struck;
                }
            }

            int frame = Mathf.FloorToInt((_time / WalkFrameSeconds) + (index * 1.7f));
            Sprite drawn = _sprites.Load(
                $"mobs/walk/{kind}-walk-{(frame % WalkFrames) + 1}", quiet: true);

            return drawn != null ? drawn : _sprites.Load($"mobs/{kind}");
        }

        /// <summary>Frames a drawn walk cycle holds.</summary>
        private const int WalkFrames = 6;

        /// <summary>
        /// Phase offset for a party slot, in frames, so members do not march in lockstep.
        /// </summary>
        /// <remarks>
        /// Exposed so the number of DISTINCT phases a party gets can be asserted rather than
        /// eyeballed. "Looks like puppets" is not testable; how many of the six frames a party of
        /// nine actually occupies is.
        /// </remarks>
        /// <param name="slot">Place in the marching order.</param>
        /// <returns>Frames to offset this member's cycle by.</returns>
        public static float WalkPhaseOffset(int slot) => slot;

        /// <summary>
        /// Seconds one frame of a walk cycle is held, at twelve frames a second.
        /// </summary>
        /// <remarks>
        /// The rate the frames were generated at. Playing them faster or slower than they were drawn
        /// is what makes a hand-made gait look like a glitch.
        /// </remarks>
        private const float WalkFrameSeconds = 1f / 12f;

        /// <summary>
        /// The sprite an adventurer should be showing this instant.
        /// </summary>
        /// <remarks>
        /// Drawn frames where they exist, and the single static sprite everywhere else. Only the
        /// tank has a walk cycle so far, and the rest of the party has to keep working while the
        /// others are drawn — a view that demanded a full set would mean nothing renders until every
        /// animation is finished.
        /// <para>
        /// Phase is offset per party slot so four members do not march in lockstep, which reads as
        /// one animation playing on four puppets rather than four people walking.
        /// </para>
        /// </remarks>
        /// <para>
        /// An attack takes precedence and plays once rather than looping, driven off the same
        /// <c>AttackPhase</c> the procedural lunge uses — so the drawn swing and the sprite's throw
        /// toward its target are the same event and cannot disagree about when the blow landed.
        /// </para>
        /// <param name="member">Who is being drawn.</param>
        /// <param name="slot">Their place in the party, for the phase offset.</param>
        /// <returns>The sprite to show.</returns>
        private Sprite FrameFor(Adventurer member, int slot)
        {
            string role = RoleName(member.Role);

            // An attack outranks a walk cycle, and plays once through rather than looping: the
            // phase runs 0 to 1 between blows, so the frame is read straight off it and the last
            // frame holds while the weapon is recovering.
            if (member.LastAttackTarget.HasValue && member.AttackPhase < 1f)
            {
                int swing = Mathf.Clamp(
                    Mathf.FloorToInt(member.AttackPhase * WalkFrames), 0, WalkFrames - 1);
                Sprite struck = _sprites.Load(
                    $"party/attack/{role}-attack-{swing + 1}", quiet: true);
                if (struck != null)
                {
                    return struck;
                }
            }

            if (member.Action == AdventurerAction.Walking && member.Wounds == WoundState.Healthy)
            {
                // slot * 1, not slot * 2, and the arithmetic is the whole reason. The cycle is six
                // frames, so doubling the slot lands on 0, 2, 4, 0, 2, 4 -- THREE distinct phases
                // however many bodies there are. Four members read as three groups; nine read as
                // three groups of three, which is what "one animation playing on puppets" looks
                // like, and Gemini reported exactly that from a recording of nine.
                //
                // Stepping by one uses all six: four members get four phases, nine get six.
                int frame = Mathf.FloorToInt((_time / WalkFrameSeconds) + WalkPhaseOffset(slot));
                Sprite drawn = _sprites.Load(
                    $"party/walk/{role}-walk-{(frame % WalkFrames) + 1}", quiet: true);
                if (drawn != null)
                {
                    return drawn;
                }
            }

            return _sprites.Load($"party/{role}-{StateName(member.Wounds)}");
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

    }
}
