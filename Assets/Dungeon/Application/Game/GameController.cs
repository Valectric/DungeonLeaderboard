using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dungeon.AudioManager;
using Dungeon.DungeonManager;
using Dungeon.LeagueManager;
using Dungeon.MobManager;
using Dungeon.PartyManager;
using Dungeon.RaidManager;
using Dungeon.ShopManager;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Dungeon.Game
{
    /// <summary>
    /// Runs a raid: builds the dungeon, drives the clock, and turns clicks into the three verbs.
    /// </summary>
    /// <remarks>
    /// Bootstraps itself, so the play scene needs nothing but this component and a camera.
    /// <para>
    /// <b>There are exactly three verbs</b> -- toggle a door, spawn a mob, fire a trap -- and there
    /// must be no fourth until those are proven fun. In particular there is deliberately no way to
    /// recall a monster: the only way to save a losing party is to open a door and let it run.
    /// </para>
    /// </remarks>
    public sealed class GameController : MonoBehaviour
    {
        /// <summary>What the game is currently showing.</summary>
        private enum Phase
        {
            /// <summary>The standings, which are also the title screen.</summary>
            Standings = 0,

            /// <summary>A raid in progress.</summary>
            Raiding = 1,

            /// <summary>The run is over: the player finished in the relegation zone.</summary>
            Destroyed = 2,

            /// <summary>The thirty seconds between raids, spending what the last one left over.</summary>
            Shopping = 3,

            /// <summary>The adventurers' verdict on the raid that just finished.</summary>
            Reviewing = 4,

            /// <summary>
            /// Every rival has been eliminated and the player's dungeon is the last one standing.
            /// </summary>
            /// <remarks>
            /// The game had only a losing ending until now. This is the one it is played for.
            /// </remarks>
            Won = 5
        }

        /// <summary>Seconds the standings take to slide into their new order.</summary>
        private const float ShiftSeconds = 0.9f;

        private Raid _raid;
        private DungeonView _view;
        private Camera _camera;
        private float _ratePulse;
        private LeagueTable _league;
        private Phase _phase = Phase.Standings;
        private float _shift = 1f;
        private int _finalPosition;
        private Shop _shop;
        private Loadout _loadout = new();
        private float _bonusEnergy;
        private float _carriedEnergy;
        private PartyComposition _nextParty = PartyComposition.Opening;
        private int _partySeed;
        private RaidReview _review;
        private float _reviewAge;

        /// <summary>The last raid's review, or null before any raid has finished.</summary>
        public RaidReview LastReview => _review;

        /// <summary>
        /// Who walks in next, so the player can read the door before it opens.
        /// </summary>
        /// <remarks>
        /// Announcing it is the whole point. SPEC.md calls composition the primary source of variety,
        /// and variety the player cannot see before they have to act on it is just noise -- they
        /// would learn only afterwards that the party they killed had no healer.
        /// </remarks>
        public PartyComposition NextParty => _nextParty;

        /// <summary>The raid in progress. Read-only; tests observe, they do not drive.</summary>
        public Raid CurrentRaid => _raid;

        /// <summary>The shop between raids, or null outside it. Read-only; tests observe.</summary>
        public Shop CurrentShop => _shop;

        /// <summary>Everything bought so far this run. Purchases are permanent for the season.</summary>
        public Loadout Loadout => _loadout;

        /// <summary>Whether the shop is currently on screen.</summary>
        public bool IsShopping => _phase == Phase.Shopping;

        /// <summary>
        /// Whether a raid is actually being played.
        /// </summary>
        /// <remarks>
        /// Not the same question as <c>CurrentRaid.IsRunning</c>, and the difference has already
        /// caught one test out. A raid is built and left running <i>behind the title screen</i> so
        /// the standings have a dungeon to sit over, so a running raid says nothing about whether
        /// the player is in one.
        /// </remarks>
        public bool IsRaiding => _phase == Phase.Raiding;

        /// <summary>Whether the adventurers' review of the last raid is on screen.</summary>
        public bool IsReviewing => _phase == Phase.Reviewing;

        /// <summary>Builds the dungeon and starts the first raid.</summary>
        private void Awake()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = cameraObject.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.backgroundColor = new Color32(0x15, 0x10, 0x1D, 0xFF);
            _camera.clearFlags = CameraClearFlags.SolidColor;

            NewRun();
        }

        /// <summary>The league in progress. Read-only; tests observe rather than drive.</summary>
        public LeagueTable League => _league;

        /// <summary>Starts a fresh season and opens on the standings.</summary>
        /// <remarks>
        /// The seed comes from the clock so each run is a different league, but every table is built
        /// from that one number -- so a run can be reproduced exactly from a bug report.
        /// </remarks>
        public void NewRun()
        {
            _league = new LeagueTable(System.Environment.TickCount);
            _phase = Phase.Standings;
            _shift = 1f;
            _loadout = new Loadout();
            _shop = null;
            _bonusEnergy = 0f;
            _carriedEnergy = 0f;

            // The first party of a run is always the balanced one. A new player who meets THE
            // UNSHRIVEN before they know what a healer does will wipe them and conclude the game is
            // unfair -- when a wipe is the one outcome the design most wants them to avoid.
            _partySeed = System.Environment.TickCount;
            _nextParty = PartyComposition.Opening;

            // A raid exists even on the title screen, so the dungeon is drawn behind the standings
            // rather than the player opening on an empty void.
            StartRaid();
            _phase = Phase.Standings;

            // That raid is scenery and is thrown away the moment the player presses a key -- but it
            // consumed the opening party on its way past, so the first party the player actually
            // faced was a random one. The title screen announced THE SKIRMISHERS, a roster with no
            // tank, as the first thing a new player would ever meet. Put the balanced party back.
            _nextParty = PartyComposition.Opening;
        }

        /// <summary>
        /// Tears down any previous view and starts a fresh sixty seconds.
        /// </summary>
        /// <remarks>
        /// Enters the raiding phase, because starting a raid is what raiding means. Callers that
        /// want the dungeon built but not yet running -- the title screen -- set the phase back
        /// afterwards.
        /// </remarks>
        public void StartRaid()
        {
            _phase = Phase.Raiding;
            // The combat seed comes from the same chain as the party roster, so one number at the
            // start of a run determines both who walks in and every blow they trade -- which is what
            // makes a run reproducible from a bug report.
            _raid = new Raid(BuildFromLoadout(), _bonusEnergy, _nextParty, _partySeed);
            _bonusEnergy = 0f;
            RollNextParty();
            RebuildView();
        }

        /// <summary>Throws away the drawn dungeon and draws the current raid from scratch.</summary>
        /// <remarks>
        /// Shared by the raid and by the shop's live preview, so the dungeon the player buys onto and
        /// the dungeon the party walks into are built by the same code. Two paths would eventually
        /// disagree, and the disagreement would only show up as a purchase that vanished.
        /// </remarks>
        private void RebuildView()
        {
            foreach (Transform child in transform.Cast<Transform>().ToList())
            {
                Destroy(child.gameObject);
            }

            _view = new DungeonView(transform);
            _view.BuildStatic(_raid.Layout);
            FrameCamera();
            _view.Refresh(_raid);
        }

        /// <summary>
        /// Draws the party that will walk in after this one.
        /// </summary>
        /// <remarks>
        /// Seeded and advanced one step at a time, so a whole run's sequence of parties follows from
        /// the single number stamped at the start of it -- the project's reproduce-from-a-bug-report
        /// constraint applies to who shows up as much as to the league table.
        /// </remarks>
        private void RollNextParty()
        {
            // Called just after the current party has been sent in, so _nextParty still holds who
            // just walked through the door -- exactly the one that must not come back immediately.
            PartyComposition justRaided = _nextParty;
            _partySeed = unchecked((_partySeed * 1103515245) + 12345);
            _nextParty = PartyComposition.ForSeed(_partySeed, justRaided);
        }

        /// <summary>Deepest the corridor is allowed to get, however many halls are bought.</summary>
        /// <remarks>
        /// A corridor that keeps growing eventually cannot be crossed in sixty seconds, at which
        /// point buying another hall stops being a purchase and starts being a guarantee -- the party
        /// can no longer reach the boss room whatever the player does, and the game's one losing
        /// ending quietly stops existing.
        /// </remarks>
        private const int MaxRooms = 5;

        /// <summary>
        /// Rooms the dungeon opens with, and the only ones that come furnished.
        /// </summary>
        /// <remarks>
        /// Everything beyond this is a hall the player bought, and a bought hall is bare floor until
        /// they put something in it.
        /// </remarks>
        private const int StartingRooms = 3;

        /// <summary>Builds the dungeon the player has paid for.</summary>
        /// <returns>The layout for the next raid.</returns>
        private DungeonLayout BuildFromLoadout()
        {
            // furnishedRooms: the opening corridor comes stocked so round one is playable at all.
            // Halls bought after that arrive EMPTY -- the player picks a floor tile and fits them
            // out, which is what buying a room is now for.
            return DungeonLayout.Build(
                PlannedRooms(), placed: PlacedFurniture(), furnishedRooms: StartingRooms);
        }

        /// <summary>Lattice cells the player has bought a hall on, in the order they bought them.</summary>
        private readonly List<Vector2Int> _boughtHalls = new();

        /// <summary>
        /// The shape of the dungeon the player has paid for.
        /// </summary>
        /// <remarks>
        /// Three rooms in a line to start with, as the game has always opened, plus whatever
        /// directions have been bought since. Capped at <see cref="MaxRooms"/>: a corridor that keeps
        /// growing eventually cannot be crossed in sixty seconds.
        /// </remarks>
        /// <returns>The plan to build.</returns>
        private RoomPlan PlannedRooms()
        {
            RoomPlan plan = RoomPlan.Corridor(3);
            foreach (Vector2Int lattice in _boughtHalls)
            {
                if (plan.Count >= MaxRooms)
                {
                    break;
                }

                plan.Add(lattice);
            }

            return plan;
        }

        /// <summary>
        /// Turns the player's purchases into the cells the dungeon should be furnished with.
        /// </summary>
        /// <remarks>
        /// The translation lives here because this is the one layer that knows both modules. The
        /// dungeon must not learn what a shop item is, and the shop must not learn what a grid is —
        /// each would make the module graph cyclic.
        /// </remarks>
        /// <returns>Furniture positioned exactly where the player put it.</returns>
        private Furnishings PlacedFurniture()
        {
            var furniture = new Furnishings();
            foreach (Placement placement in _loadout.Placements)
            {
                switch (placement.Item)
                {
                    case ShopItem.Slime:
                        furniture.SlimeSpawners.Add(placement.Cell);
                        break;
                    case ShopItem.Skeleton:
                        furniture.SkeletonSpawners.Add(placement.Cell);
                        break;
                    case ShopItem.SpikeTrap:
                    case ShopItem.PoisonDart:
                        furniture.Traps.Add(placement.Cell);
                        break;
                    case ShopItem.Chest:
                        furniture.Chests.Add(placement.Cell);
                        break;
                }
            }

            return furniture;
        }

        /// <summary>
        /// How far in the player may zoom, as a fraction of the fitted view.
        /// </summary>
        /// <remarks>
        /// The fitted view now covers the whole world -- dungeon plus forest approach -- so a limit
        /// tuned when it covered only the dungeon left everything too small. Four times more range
        /// than that, which brings a single room up to fill the screen.
        /// </remarks>
        private const float MaxZoomIn = 0.09f;

        /// <summary>How far out the player may zoom, as a fraction of the fitted view.</summary>
        private const float MaxZoomOut = 1.15f;

        /// <summary>
        /// Zoom change per scroll notch.
        /// </summary>
        /// <remarks>
        /// Sized so about four notches -- a normal flick of a wheel -- crosses the whole range from
        /// fully out to fully in. Zooming is a glance, not a journey; anything finer feels broken
        /// long before the player works out that it is merely slow.
        /// </remarks>
        private const float ZoomStep = 0.2f;

        private float _fittedSize;
        private float _zoom = 1f;
        private float _pinchDistance;
        private Vector3 _worldCentre;
        private Vector2 _pan;
        private Vector2 _dragAnchor;
        private bool _dragging;

        /// <summary>Points the camera at the whole dungeon so nothing sits off screen.</summary>
        private void FrameCamera()
        {
            // Frame everything drawn -- dungeon and forest approach together. The result is small,
            // which is fine: the player zooms and pans. Cropping the scenery to keep the dungeon
            // large would hide the world the party walks out of, which is the point of drawing it.
            Bounds world = _view.WorldBounds;
            _worldCentre = new Vector3(world.center.x, world.center.y, -10f);

            float halfHeight = (world.extents.y) + 1.6f;   // room at the bottom for the HUD strip
            float halfWidth = world.extents.x + 0.5f;
            _fittedSize = Mathf.Max(halfHeight, halfWidth / _camera.aspect);

            // Open framed on the dungeon, not on the whole world. Zoom 1 means "everything", which
            // is the right thing to be able to reach but the wrong thing to start on -- the game
            // happens in the corridor, and the forest is scenery the player can go and look at.
            DungeonGrid grid = _raid.Layout.Grid;
            float dungeonHalfHeight = (grid.Height * 0.5f) + 1.6f;
            float dungeonHalfWidth = (grid.Width * 0.5f) + 0.5f;
            float dungeonFit = Mathf.Max(dungeonHalfHeight, dungeonHalfWidth / _camera.aspect);

            _zoom = Mathf.Clamp(dungeonFit / _fittedSize, MaxZoomIn, MaxZoomOut);
            _pan = new Vector2(
                ((grid.Width - 1) * 0.5f * DungeonView.CellSize) - _worldCentre.x,
                ((grid.Height - 1) * 0.5f * DungeonView.CellSize) - _worldCentre.y);

            ApplyCamera();
        }

        /// <summary>Moves the camera to the current pan and zoom, clamped to the world.</summary>
        private void ApplyCamera()
        {
            _camera.orthographicSize = _fittedSize * _zoom;

            // Keep the view over the world. Once zoomed out far enough to see everything there is
            // nothing left to pan to, so the allowance collapses to zero rather than going negative.
            Bounds world = _view.WorldBounds;
            float halfViewY = _camera.orthographicSize;
            float halfViewX = halfViewY * _camera.aspect;
            float slackX = Mathf.Max(0f, world.extents.x - halfViewX);
            float slackY = Mathf.Max(0f, world.extents.y - halfViewY);

            _pan.x = Mathf.Clamp(_pan.x, -slackX, slackX);
            _pan.y = Mathf.Clamp(_pan.y, -slackY, slackY);
            _camera.transform.position = _worldCentre + new Vector3(_pan.x, _pan.y, 0f);
        }

        /// <summary>
        /// Applies scroll-wheel and pinch zoom.
        /// </summary>
        /// <remarks>
        /// Zoom is expressed as a fraction of the fitted view rather than an absolute orthographic
        /// size, so it means the same thing on a phone and on a desktop monitor, and survives the
        /// camera being re-fitted when a raid restarts.
        /// <para>
        /// Zooming out is capped just past the fitted view. Letting the player pull back further
        /// would only reveal the black nothing outside the dungeon, which reads as a bug.
        /// </para>
        /// </remarks>
        private void HandleZoom()
        {
            float previous = _zoom;

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    // Sign, not magnitude. Platforms disagree wildly about what a scroll delta
                    // means -- Windows reports 120 per notch, browsers often report 1 or a
                    // fractional pixel delta -- so scaling by the raw value made one notch move the
                    // zoom by about a thousandth in a WebGL build. It read as completely broken.
                    _zoom -= Mathf.Sign(scroll) * ZoomStep;
                }
            }

            Touchscreen touch = Touchscreen.current;
            if (touch != null && ActiveTouchCount() >= 2)
            {
                Vector2 first = touch.touches[0].position.ReadValue();
                Vector2 second = touch.touches[1].position.ReadValue();
                float distance = Vector2.Distance(first, second);

                if (_pinchDistance > 0f && distance > 0f)
                {
                    // Scale by ratio rather than raw pixel delta, so the gesture feels identical on
                    // a dense phone screen and a low-density tablet.
                    _zoom *= _pinchDistance / distance;
                }

                _pinchDistance = distance;
            }
            else
            {
                _pinchDistance = 0f;
            }

            _zoom = Mathf.Clamp(_zoom, MaxZoomIn, MaxZoomOut);
            HandlePan();

            if (_fittedSize > 0f)
            {
                ApplyCamera();
            }
        }

        /// <summary>
        /// Drags the view: right mouse button on desktop, two fingers on a touchscreen.
        /// </summary>
        /// <remarks>
        /// Right button rather than left, because left is the whole game -- clicking doors, spawners
        /// and traps. Two fingers on mobile for the same reason: one finger is a verb.
        /// <para>
        /// The drag is computed in world units from the screen delta, so the point under the cursor
        /// stays under the cursor at any zoom. Panning in fixed world units per pixel would feel
        /// slow zoomed out and frantic zoomed in.
        /// </para>
        /// </remarks>
        private void HandlePan()
        {
            Vector2 pointer;
            bool held;

            Touchscreen touch = Touchscreen.current;
            if (touch != null && ActiveTouchCount() >= 2)
            {
                pointer = (touch.touches[0].position.ReadValue()
                           + touch.touches[1].position.ReadValue()) * 0.5f;
                held = true;
            }
            else
            {
                Mouse mouse = Mouse.current;
                held = mouse != null && mouse.rightButton.isPressed;
                pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }

            if (!held)
            {
                _dragging = false;
                return;
            }

            if (!_dragging)
            {
                _dragging = true;
                _dragAnchor = pointer;
                return;
            }

            float unitsPerPixel = (_camera.orthographicSize * 2f) / Mathf.Max(1, Screen.height);
            _pan -= (pointer - _dragAnchor) * unitsPerPixel;
            _dragAnchor = pointer;
        }

        /// <summary>
        /// Advances the simulation on the physics clock, per the project's Unity practice.
        /// </summary>
        /// <remarks>
        /// Only while actually raiding. A raid is built behind the title screen so the standings have
        /// a dungeon to sit over rather than a void -- but it used to <b>tick</b> there too, so the
        /// first party walked in, fought and burned clock while the screen was still telling the
        /// player to press a key to begin. On a fresh load the game had started without them.
        /// </remarks>
        private void FixedUpdate()
        {
            if (_raid == null || _phase != Phase.Raiding)
            {
                return;
            }

            _raid.Tick(Time.fixedDeltaTime);
        }

        /// <summary>Reads input and redraws.</summary>
        private void Update()
        {
            if (_raid == null)
            {
                return;
            }

            _ratePulse += Time.deltaTime;
            _shift = Mathf.Min(1f, _shift + (Time.deltaTime / ShiftSeconds));
            _view.Refresh(_raid, Time.deltaTime);

            if (_phase == Phase.Shopping)
            {
                TickShop(Time.deltaTime);
                return;
            }

            if (_phase == Phase.Reviewing)
            {
                _reviewAge += Time.deltaTime;
                if (TryReadTap(out _) || AnyKeyPressed())
                {
                    DismissReview();
                }

                return;
            }

            if (_phase != Phase.Raiding)
            {
                HandleZoom();
                if (TryReadTap(out _) || AnyKeyPressed())
                {
                    Advance();
                }

                return;
            }

            // A raid that has just finished is judged by the party that survived it, and only then
            // banked. The review comes first because it is what makes the number mean something.
            if (!_raid.IsRunning)
            {
                _review = RaidReview.For(
                    _raid.Outcome, _raid.EnergyHarvested, _raid.Party.LivingCount);
                _reviewAge = 0f;
                _phase = Phase.Reviewing;
                return;
            }

            // The project runs the Input System package (activeInputHandler: 1), so the legacy
            // UnityEngine.Input class throws on every call. It did, silently, on every frame -- all
            // three verbs were dead in the shipped scene while the suite stayed green, because the
            // tests drove the simulation directly instead of clicking.
            HandleZoom();

            if (TryReadTap(out Vector2 tapPosition))
            {
                ClickAt(tapPosition);
            }
        }

        /// <summary>
        /// How long the review holds before it will accept a dismissal.
        /// </summary>
        /// <remarks>
        /// The stars land over about a second. Without a lockout a player already tapping to spawn
        /// mobs skips straight past the one screen that explains why the score is what it is.
        /// </remarks>
        public const float ReviewLockoutSeconds = 1.4f;

        /// <summary>
        /// Dismisses the review and banks the raid, exactly as tapping the screen does.
        /// </summary>
        /// <remarks>
        /// Public for the same reason <see cref="StartRaid"/>, <see cref="TapShop"/> and
        /// <see cref="ClickAt"/> are: every other transition in this game has an entry point a test
        /// can press, and this one did not — so a sweep that walked the whole loop silently skipped
        /// the review, never banked a raid, and reported ten rounds in which the league stayed on
        /// round zero. It looked like a passing test of the player's loop and was a slow way of
        /// restarting the same raid.
        /// <para>
        /// Dismissing the review is a real player action, so this is the handler rather than a test
        /// seam: the frame loop calls it when it reads a tap, and nothing here is reachable that a
        /// player could not do.
        /// </para>
        /// </remarks>
        /// <returns>True when the review was showing, had held long enough, and was banked.</returns>
        public bool DismissReview()
        {
            if (_phase != Phase.Reviewing || _reviewAge <= ReviewLockoutSeconds)
            {
                return false;
            }

            BankRaid();
            return true;
        }

        /// <summary>Whether any key was pressed this frame.</summary>
        private static bool AnyKeyPressed()
        {
            return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        }

        /// <summary>
        /// Banks a finished raid into the league and returns to the standings.
        /// </summary>
        /// <remarks>
        /// Relegation is judged only after the score lands, per SPEC.md: finishing in the bottom 10%
        /// after a raid ends the run.
        /// </remarks>
        private void BankRaid()
        {
            _league.SubmitRaid(_raid.EnergyHarvested);
            _shift = 0f;

            // Whatever the player did not spend during the raid is what they take to the shop. It
            // gives restraint a use -- a player who hoards energy buys a permanent spawner with it --
            // without letting hoarding score points, since the league only counts harvest.
            _carriedEnergy = _raid.TotalEnergy;

            if (_league.PlayerRelegated)
            {
                _finalPosition = _league.PlayerPosition;
                _phase = Phase.Destroyed;
                return;
            }

            // One rival leaves every round, so the field shrinks toward a single winner.
            _league.CollapseRelegated();

            if (_league.PlayerWon)
            {
                _finalPosition = 1;
                _phase = Phase.Won;
                return;
            }

            _phase = Phase.Standings;
        }

        /// <summary>
        /// Moves on from the standings: into the shop, the next raid, or a new run.
        /// </summary>
        /// <remarks>
        /// Public so a test can press the button rather than reach past it. Every transition in this
        /// game is guarded by something -- a keypress, a clock, a lockout, a shift animation -- and a
        /// guard that never releases strands the player on a screen that has stopped responding.
        /// </remarks>
        public void Advance()
        {
            if (_shift < 1f)
            {
                // Let the shift finish first, so a keen player cannot skip past the one moment the
                // whole raid was played for.
                _shift = 1f;
                return;
            }

            if (_phase is Phase.Destroyed or Phase.Won)
            {
                NewRun();
                return;
            }

            // No shop before the first party. The player has earned nothing yet, so it would be
            // thirty seconds of looking at prices they cannot pay -- and the first thing a new player
            // should see after the standings is a raid, not a menu.
            if (_league.Round > 0)
            {
                OpenShop();
                return;
            }

            StartRaid();
            _phase = Phase.Raiding;
        }

        /// <summary>Opens the shop with whatever the last raid left unspent.</summary>
        public void OpenShop()
        {
            OpenShopWith(_carriedEnergy);
        }

        /// <summary>
        /// Opens the shop with a given purse.
        /// </summary>
        /// <remarks>
        /// Public so a test can put a known amount of money on the table and then press real cards at
        /// real screen coordinates, instead of asserting against the shop model and never finding out
        /// whether the cards are clickable at all.
        /// </remarks>
        /// <param name="purse">Energy the player has to spend.</param>
        public void OpenShopWith(float purse)
        {
            _shop = new Shop(purse);
            _carriedEnergy = 0f;
            _phase = Phase.Shopping;
            _popupCell = null;
            ShowPreview();
        }

        /// <summary>The tile whose build menu is open, if any.</summary>
        private Vector2Int? _popupCell;

        /// <summary>
        /// Replaces what is on screen with the dungeon the player is currently buying.
        /// </summary>
        /// <remarks>
        /// The shop is spatial, so it has to be shown the dungeon it is spending money on — and the
        /// previous raid's dungeon is the wrong one the instant anything is bought. Rebuilt after
        /// every purchase so a new hall or a new spawner appears where the player put it, not on the
        /// next loading screen.
        /// <para>
        /// The preview raid is constructed but never ticked: <c>FixedUpdate</c> only advances a raid
        /// during <see cref="Phase.Raiding"/>. It is scenery with a party standing at the door.
        /// </para>
        /// </remarks>
        private void ShowPreview()
        {
            _raid = new Raid(BuildFromLoadout(), 0f, _nextParty, _partySeed);
            RebuildView();
            _view.MarkBuildableTiles(_raid.Layout);
        }

        /// <summary>
        /// Runs the shop clock and reads taps on the cards and the Ready button.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last frame.</param>
        private void TickShop(float deltaTime)
        {
            _shop.Tick(deltaTime);

            // The shop is spatial now, so the player has to be able to reach the far end of a
            // five-room corridor and the tiles at the top of a room. Without this the controls the
            // shop draws on the dungeon are only usable on whichever part of it happens to be framed.
            HandleZoom();

            if (TryReadTap(out Vector2 tap))
            {
                TapShop(tap);
            }

            if (!_shop.IsOpen)
            {
                StartRaid();
            }
        }

        /// <summary>
        /// Resolves a tap on the shop: buy an item, or press Ready.
        /// </summary>
        /// <remarks>
        /// Public so a test can press a real card from a real screen coordinate, the same way
        /// <see cref="ClickAt"/> exists for the raid verbs. A shipped build once had every verb dead
        /// while the suite stayed green, because the tests called the model instead of clicking.
        /// </remarks>
        /// <param name="screenPosition">Screen-space point that was tapped.</param>
        public void TapShop(Vector2 screenPosition)
        {
            if (_shop is not { IsOpen: true })
            {
                return;
            }

            float scale = Screen.height / 720f;

            if (ShopScreen.HitReady(screenPosition, scale))
            {
                // The bonus is carried into the next raid rather than paid into the purse being
                // closed, so pressing Ready buys starting energy instead of buying nothing.
                _bonusEnergy += _shop.Ready();
                return;
            }

            // An open menu swallows the tap first, so a row can never be missed because the tile
            // underneath it also matched.
            if (_popupCell.HasValue)
            {
                Vector2 anchor = GuiPointOf(_popupCell.Value);
                if (ShopScreen.TryHitPopup(screenPosition, anchor, scale, out ShopItem picked))
                {
                    BuyOnto(picked, _popupCell.Value);
                    return;
                }

                // A tap anywhere else dismisses the menu rather than buying something. Backing out
                // has to be as easy as opening it, or every mis-tap costs energy.
                _popupCell = null;
                return;
            }

            if (CanBuyHall)
            {
                foreach (Vector2Int lattice in ExpansionCells())
                {
                    if (!ShopScreen.HitHallMarker(
                            screenPosition, GuiPointOf(_raid.Layout.CentreOfLattice(lattice)),
                            scale))
                    {
                        continue;
                    }

                    if (_shop.Buy(ShopItem.Door))
                    {
                        BuyHallAt(lattice);
                    }

                    return;
                }
            }

            Vector2Int cell = DungeonView.WorldToCell(_camera.ScreenToWorldPoint(screenPosition));
            if (_raid.Layout.CanBuildOn(cell))
            {
                _popupCell = cell;
            }
        }

        /// <summary>Whether the corridor can still take another hall.</summary>
        private bool CanBuyHall => _boughtHalls.Count < MaxRooms - 3;

        /// <summary>Every lattice cell the player could put a new hall on.</summary>
        /// <returns>The cells, or an empty list for a layout built without a plan.</returns>
        private List<Vector2Int> ExpansionCells()
        {
            return _raid?.Layout?.Plan?.Expansions() ?? new List<Vector2Int>();
        }

        /// <summary>
        /// Buys a hall in a direction and keeps the existing furniture in the right rooms.
        /// </summary>
        /// <remarks>
        /// Growing left or down moves the lattice anchor, and every carved cell moves with it -- so
        /// a spawner the player placed at an absolute cell would silently end up in a different
        /// room, or in the rock. Purchases are translated by the same amount the grid moved.
        /// </remarks>
        /// <param name="lattice">Lattice cell to build the hall on.</param>
        private void BuyHallAt(Vector2Int lattice)
        {
            Vector2Int before = _raid.Layout.LatticeAnchor;
            _boughtHalls.Add(lattice);
            _loadout.Add(ShopItem.Door);

            RoomPlan grown = PlannedRooms();
            grown.Extent(out Vector2Int after, out _);

            Vector2Int shift = before - after;
            if (shift != Vector2Int.zero)
            {
                _loadout.Translate(new Vector2Int(shift.x * 6, shift.y * 6));
            }

            AudioFacade.Cue(Sfx.Purchase, 0.7f);
            ShowPreview();
        }

        /// <summary>Buys an item onto a cell and rebuilds the preview so the player sees it land.</summary>
        /// <param name="item">Item to buy.</param>
        /// <param name="cell">Cell to put it on.</param>
        private void BuyOnto(ShopItem item, Vector2Int cell)
        {
            if (_shop.BuyAt(item, cell))
            {
                _loadout.Add(item, cell);
                AudioFacade.Cue(Sfx.Purchase, 0.7f);
                ShowPreview();
            }

            _popupCell = null;
        }

        /// <summary>Where a dungeon cell sits on screen, in GUI space.</summary>
        /// <remarks>
        /// GUI space is measured from the top of the screen and Unity's screen space from the bottom,
        /// so the flip happens here once rather than at each of the four call sites that need it.
        /// </remarks>
        /// <param name="cell">Cell to locate.</param>
        /// <returns>The point in GUI space.</returns>
        private Vector2 GuiPointOf(Vector2Int cell)
        {
            Vector3 screen = _camera.WorldToScreenPoint(DungeonView.CellToWorld(cell));
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        /// <summary>
        /// Reads a click or a screen tap, whichever this device offers.
        /// </summary>
        /// <remarks>
        /// A touchscreen reports nothing through <c>Mouse</c>, so a mouse-only poll leaves every verb
        /// dead on a phone -- which is exactly what shipped. Both devices are checked because a
        /// WebGL build runs on either, and a tablet with a mouse attached has both.
        /// <para>
        /// A tap is ignored while a second finger is down, so the pinch-zoom gesture cannot also fire
        /// a verb and, say, spend energy on a trap the player never meant to trigger.
        /// </para>
        /// </remarks>
        /// <param name="position">Screen position of the tap, when there was one.</param>
        /// <returns>True when the player tapped or clicked this frame.</returns>
        private static bool TryReadTap(out Vector2 position)
        {
            position = default;

            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                if (ActiveTouchCount() > 1)
                {
                    return false;
                }

                position = touch.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return false;
            }

            position = mouse.position.ReadValue();
            return true;
        }

        /// <summary>Counts fingers currently on the screen.</summary>
        private static int ActiveTouchCount()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null)
            {
                return 0;
            }

            int count = 0;
            foreach (TouchControl finger in touch.touches)
            {
                if (finger.press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Turns a click at a screen position into whichever verb the clicked cell offers.
        /// </summary>
        /// <remarks>
        /// Clicking dungeon elements directly is the whole input scheme -- there is no cursor mode
        /// and no selected tool, so a verb is never armed and mis-fired.
        /// <para>
        /// Public so tests can exercise the real handler from a real screen coordinate. Everything
        /// downstream of here is identical for a simulated and a physical click; only the device
        /// poll above differs.
        /// </para>
        /// </remarks>
        /// <param name="screenPosition">Screen-space point that was clicked.</param>
        public void ClickAt(Vector2 screenPosition)
        {
            if (_raid == null || !_raid.IsRunning)
            {
                return;
            }

            Vector3 world = _camera.ScreenToWorldPoint(screenPosition);
            Vector2Int cell = DungeonView.WorldToCell(world);

            if (_raid.ToggleDoor(cell))
            {
                return;
            }

            if (_raid.Layout.SpawnerCells.Contains(cell))
            {
                // No kind passed: the spawner decides. A slime pit bought in the shop spawns slimes,
                // everything else spawns skeletons.
                _raid.SpawnMob(cell);
                return;
            }

            if (_raid.Layout.TrapCells.Contains(cell))
            {
                _raid.FireTrap(cell);
            }
        }

        /// <summary>
        /// Draws the HUD: clock, banked energy, and the rate as a large pulsing number.
        /// </summary>
        /// <remarks>
        /// Immediate-mode GUI is used deliberately. It needs no font asset and no canvas, so it
        /// cannot fail in a WebGL build the way a missing dynamic font silently can, and the whole
        /// HUD stays in one readable place next to the state it displays.
        /// </remarks>
        private void OnGUI()
        {
            if (_raid == null || _league == null)
            {
                return;
            }

            float scale = Screen.height / 720f;

            if (_phase == Phase.Shopping)
            {
                var halls = new List<Vector2>();
                if (CanBuyHall)
                {
                    foreach (Vector2Int lattice in ExpansionCells())
                    {
                        halls.Add(GuiPointOf(_raid.Layout.CentreOfLattice(lattice)));
                    }
                }
                Vector2? popup = _popupCell.HasValue ? GuiPointOf(_popupCell.Value) : null;
                ShopScreen.Draw(_shop, scale, halls, _shop.Price(ShopItem.Door), popup);
                return;
            }

            if (_phase == Phase.Reviewing)
            {
                ReviewScreen.Draw(_review, _raid.EnergyHarvested, scale, _reviewAge);
                return;
            }

            if (_phase != Phase.Raiding)
            {
                string prompt = _phase == Phase.Won
                    ? "EVERY RIVAL HAS COLLAPSED.  YOURS IS THE LAST DUNGEON STANDING."
                    : _phase == Phase.Destroyed
                    ? $"YOUR DUNGEON COLLAPSED IN {Ordinal(_finalPosition)}.  PRESS ANY KEY TO BEGIN AGAIN"
                    : _league.Round == 0
                        ? "PRESS ANY KEY  -  THE FIRST PARTY ENTERS"
                        : "PRESS ANY KEY  -  SPEND WHAT YOU HAVE LEFT";

                LeagueScreen.Draw(_league, scale, _shift, prompt,
                    _phase is Phase.Destroyed or Phase.Won ? null : _nextParty);
                return;
            }

            CombatNumbers.Draw(_raid.Feed, _camera, scale);
            LeagueScreen.DrawStrip(_league, scale, _raid.EnergyHarvested);
            var clock = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(34 * scale),
                fontStyle = FontStyle.Bold
            };
            var caption = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * scale) };

            clock.normal.textColor = _raid.TimeRemaining <= 10f
                ? new Color(0.95f, 0.35f, 0.35f)
                : Color.white;
            GUI.Label(new Rect(24f * scale, 16f * scale, 320f * scale, 50f * scale),
                $"{Mathf.FloorToInt(_raid.TimeRemaining / 60f):0}:{Mathf.FloorToInt(_raid.TimeRemaining % 60f):00}",
                clock);

            // The rate is the game, so it is the biggest thing on screen and it breathes. A player
            // has to *see* dead time costing them without reading a tutorial.
            //
            // Scaled against the curve's real ceiling. This used to saturate at 12/s, chosen when the
            // rate could not in practice exceed 4 -- so once the curve was fixed and rates reached
            // 37/s, a spectacular spike pulsed exactly like a routine scuffle. SPEC.md section 9 asks
            // specifically for the number to pulse "when it spikes", which means the spike has to
            // look different from the floor.
            float intensity = Mathf.Clamp01(_raid.CurrentRate / 30f);
            float beat = 9f + (intensity * 7f);
            float pulse = 1f + (Mathf.Sin(_ratePulse * beat) * (0.05f + (intensity * 0.13f)));
            var rate = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(52 * scale * pulse),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            // Grey when idle, green when earning, and gold at the top of the curve -- so the colour
            // says which of the three states the player is in without reading the figure. Two stops
            // rather than three left everything above 20/s looking identical, and the whole point of
            // the wound curve is what happens beyond that.
            Color heat = _raid.CurrentRate < 20f
                ? Color.Lerp(new Color(0.45f, 0.45f, 0.5f), new Color(0.55f, 1f, 0.45f),
                    Mathf.Clamp01(_raid.CurrentRate / 14f))
                : Color.Lerp(new Color(0.55f, 1f, 0.45f), new Color(1f, 0.85f, 0.3f),
                    Mathf.Clamp01((_raid.CurrentRate - 20f) / 15f));
            rate.normal.textColor = heat;
            // Invariant culture throughout the HUD. The build picks up the machine's locale, and on
            // this one the rate rendered as "0,1/s" -- a comma reads as a thousands separator to
            // most players and makes the game's most important number ambiguous.
            GUI.Label(new Rect(0f, 10f * scale, Screen.width, 80f * scale),
                _raid.CurrentRate.ToString("0.0", CultureInfo.InvariantCulture) + "/s", rate);

            caption.normal.textColor = new Color(0.7f, 0.7f, 0.78f);
            GUI.Label(new Rect(0f, 66f * scale, Screen.width, 30f * scale), "ENERGY RATE",
                new GUIStyle(caption) { alignment = TextAnchor.UpperCenter });

            var total = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            total.normal.textColor = new Color(0.85f, 0.7f, 1f);
            GUI.Label(new Rect(0f, 16f * scale, Screen.width - (24f * scale), 44f * scale),
                _raid.EnergyHarvested.ToString("0", CultureInfo.InvariantCulture), total);
            GUI.Label(new Rect(0f, 50f * scale, Screen.width - (24f * scale), 30f * scale),
                "HARVESTED   spend " + _raid.TotalEnergy.ToString("0", CultureInfo.InvariantCulture),
                new GUIStyle(caption) { alignment = TextAnchor.UpperRight });

            // Who is inside, kept on screen for the whole raid. The player has to be able to check
            // mid-minute whether this is the party with two healers or the one with none, without
            // having to have memorised the standings screen.
            var who = new GUIStyle(caption) { fontStyle = FontStyle.Bold };
            who.normal.textColor = new Color(0.85f, 0.7f, 1f);
            GUI.Label(new Rect(24f * scale, 62f * scale, Screen.width, 30f * scale),
                _raid.Party.Composition.Name, who);

            GUI.Label(new Rect(24f * scale, Screen.height - (44f * scale), Screen.width, 30f * scale),
                "TAP A DOOR TO STALL   /   A SPAWNER TO AMBUSH   /   A TRAP TO WOUND"
                + "   /   SCROLL OR PINCH TO ZOOM   /   RIGHT-DRAG OR TWO FINGERS TO MOVE",
                caption);

        }

        /// <summary>Formats a league position as an ordinal, for the collapse message.</summary>
        private static string Ordinal(int position)
        {
            string suffix = (position % 100) is >= 11 and <= 13
                ? "th"
                : (position % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return $"{position}{suffix}";
        }
    }
}
