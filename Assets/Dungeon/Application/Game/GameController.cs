using System.Globalization;
using System.Linq;
using Dungeon.DungeonManager;
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
        private Raid _raid;
        private DungeonView _view;
        private Camera _camera;
        private float _ratePulse;

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

            StartRaid();
        }

        /// <summary>Tears down any previous view and starts a fresh sixty seconds.</summary>
        public void StartRaid()
        {
            foreach (Transform child in transform.Cast<Transform>().ToList())
            {
                Destroy(child.gameObject);
            }

            _raid = new Raid(DungeonLayout.BuildCorridor());
            _view = new DungeonView(transform);
            _view.BuildStatic(_raid.Layout);
            FrameCamera();
            _view.Refresh(_raid);
        }

        /// <summary>How far in the player may zoom, as a fraction of the fitted view.</summary>
        private const float MaxZoomIn = 0.35f;

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

        /// <summary>Points the camera at the whole dungeon so nothing sits off screen.</summary>
        private void FrameCamera()
        {
            DungeonGrid grid = _raid.Layout.Grid;
            var centre = new Vector3((grid.Width - 1) * 0.5f, (grid.Height - 1) * 0.5f, -10f);
            _camera.transform.position = centre;

            // Fit the wider of the two axes, leaving room at the bottom for the HUD strip.
            float halfHeight = (grid.Height * 0.5f) + 1.6f;
            float halfWidth = (grid.Width * 0.5f) + 0.5f;
            _fittedSize = Mathf.Max(halfHeight, halfWidth / _camera.aspect);
            _camera.orthographicSize = _fittedSize * _zoom;
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
            if (!Mathf.Approximately(previous, _zoom) && _fittedSize > 0f)
            {
                _camera.orthographicSize = _fittedSize * _zoom;
            }
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
            _view.Refresh(_raid, Time.deltaTime);

            // The project runs the Input System package (activeInputHandler: 1), so the legacy
            // UnityEngine.Input class throws on every call. It did, silently, on every frame -- all
            // three verbs were dead in the shipped scene while the suite stayed green, because the
            // tests drove the simulation directly instead of clicking.
            HandleZoom();

            bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            bool tapped = TryReadTap(out Vector2 tapPosition);

            if (!_raid.IsRunning)
            {
                if (spacePressed || tapped)
                {
                    StartRaid();
                }

                return;
            }

            if (tapped)
            {
                ClickAt(tapPosition);
            }
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
            if (_raid == null)
            {
                return;
            }

            float scale = Screen.height / 720f;
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
                "TAP A DOOR TO STALL  ·  A SPAWNER TO AMBUSH  ·  A TRAP TO WOUND  ·  PINCH OR SCROLL TO ZOOM",
                caption);

            if (!_raid.IsRunning)
            {
                DrawEndCard(scale);
            }
        }

        /// <summary>Draws the end-of-raid summary and the prompt to run another.</summary>
        private void DrawEndCard(float scale)
        {
            string headline = _raid.Outcome switch
            {
                RaidOutcome.PartyWiped => "THEY DIED. DEAD PARTIES PAY NOTHING.",
                RaidOutcome.PartyEscaped => "THEY REACHED THE BOSS ROOM AND LEFT.",
                _ => "THE PARTY STAGGERED OUT AT THE BELL."
            };

            var banner = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            banner.normal.textColor = _raid.Outcome == RaidOutcome.TimeExpired
                ? new Color(0.55f, 1f, 0.45f)
                : new Color(1f, 0.5f, 0.5f);

            float y = Screen.height * 0.42f;
            GUI.Label(new Rect(0f, y, Screen.width, 44f * scale), headline, banner);
            GUI.Label(new Rect(0f, y + (46f * scale), Screen.width, 40f * scale),
                "HARVESTED " + _raid.EnergyHarvested.ToString("0", CultureInfo.InvariantCulture),
                new GUIStyle(banner) { fontSize = Mathf.RoundToInt(24 * scale) });
            GUI.Label(new Rect(0f, y + (86f * scale), Screen.width, 34f * scale),
                "CLICK OR PRESS SPACE FOR THE NEXT PARTY",
                new GUIStyle(banner) { fontSize = Mathf.RoundToInt(16 * scale) });
        }
    }
}
