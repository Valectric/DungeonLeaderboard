using System.Globalization;
using System.Linq;
using Dungeon.DungeonManager;
using Dungeon.LeagueManager;
using Dungeon.MobManager;
using Dungeon.RaidManager;
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
            Destroyed = 2
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

        /// <summary>The raid in progress. Read-only; tests observe, they do not drive.</summary>
        public Raid CurrentRaid => _raid;

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

            // A raid exists even on the title screen, so the dungeon is drawn behind the standings
            // rather than the player opening on an empty void.
            StartRaid();
            _phase = Phase.Standings;
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
            foreach (Transform child in transform.Cast<Transform>().ToList())
            {
                Destroy(child.gameObject);
            }

            _phase = Phase.Raiding;
            _raid = new Raid(DungeonLayout.BuildCorridor());
            _view = new DungeonView(transform);
            _view.BuildStatic(_raid.Layout);
            FrameCamera();
            _view.Refresh(_raid);
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

        /// <summary>Advances the simulation on the physics clock, per the project's Unity practice.</summary>
        private void FixedUpdate()
        {
            if (_raid == null)
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

            if (_phase != Phase.Raiding)
            {
                HandleZoom();
                if (TryReadTap(out _) || AnyKeyPressed())
                {
                    Advance();
                }

                return;
            }

            // A raid that has just finished banks itself and returns to the standings, where the
            // player watches their position move. That shift is the payoff for the whole minute.
            if (!_raid.IsRunning)
            {
                BankRaid();
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

            if (_league.PlayerRelegated)
            {
                _finalPosition = _league.PlayerPosition;
                _phase = Phase.Destroyed;
                return;
            }

            _league.CollapseRelegated();
            _phase = Phase.Standings;
        }

        /// <summary>Moves on from the standings: into the next raid, or into a new run.</summary>
        private void Advance()
        {
            if (_shift < 1f)
            {
                // Let the shift finish first, so a keen player cannot skip past the one moment the
                // whole raid was played for.
                _shift = 1f;
                return;
            }

            if (_phase == Phase.Destroyed)
            {
                NewRun();
                return;
            }

            StartRaid();
            _phase = Phase.Raiding;
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
                _raid.SpawnMob(cell, MobKind.Skeleton);
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

            if (_phase != Phase.Raiding)
            {
                string prompt = _phase == Phase.Destroyed
                    ? $"YOUR DUNGEON COLLAPSED IN {Ordinal(_finalPosition)}.  PRESS ANY KEY TO BEGIN AGAIN"
                    : _league.Round == 0
                        ? "PRESS ANY KEY  -  THE FIRST PARTY ENTERS"
                        : "PRESS ANY KEY  -  THE NEXT PARTY ENTERS";

                LeagueScreen.Draw(_league, scale, _shift, prompt);
                return;
            }

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
            float pulse = 1f + (Mathf.Sin(_ratePulse * 9f) * 0.06f * Mathf.Clamp01(_raid.CurrentRate / 12f));
            var rate = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(52 * scale * pulse),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            rate.normal.textColor = Color.Lerp(
                new Color(0.45f, 0.45f, 0.5f), new Color(0.55f, 1f, 0.45f),
                Mathf.Clamp01(_raid.CurrentRate / 20f));
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
