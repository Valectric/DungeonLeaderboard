using System.Globalization;
using Dungeon.LeagueManager;
using Dungeon.RaidManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws the heads-up display over a raid in progress: the clock, the rate, why the rate is
    /// what it is, the harvest, who is inside, and the verb reminder.
    /// </summary>
    /// <remarks>
    /// Split out of <c>GameController</c>, which held it inline while every other screen in the
    /// game -- the standings, the shop, the review, the loading card -- already had a file of its
    /// own. The raid HUD was the only one that did not, which made the largest file in the project
    /// larger for no reason a reader could infer.
    /// <para>
    /// <b>The rate is the game.</b> SPEC.md asks for it to be the biggest thing on screen and to
    /// pulse when it spikes, because a player has to <i>see</i> dead time costing them without
    /// reading a tutorial. Everything else here is arranged around not covering it.
    /// </para>
    /// </remarks>
    public static class RaidHud
    {
        /// <summary>
        /// Draws the whole raid HUD for one frame.
        /// </summary>
        /// <remarks>
        /// Must be called from <c>OnGUI</c> -- it builds <c>GUIStyle</c>s from <c>GUI.skin</c>,
        /// which throws outside a GUI pass.
        /// </remarks>
        /// <param name="raid">The raid in progress.</param>
        /// <param name="league">Standings, for the strip along the top.</param>
        /// <param name="camera">Camera the dungeon is drawn with, for placing world-space labels.</param>
        /// <param name="scale">Interface scale, unfloored.</param>
        /// <param name="ratePulse">Seconds of accumulated pulse phase, so the rate breathes.</param>
        public static void Draw(
            Raid raid, LeagueTable league, Camera camera, float scale, float ratePulse)
        {
            CombatNumbers.Draw(raid.Feed, camera, scale);
            LeagueScreen.DrawStrip(league, scale, raid.EnergyHarvested);
            // The HUD lays itself out from a FLOORED scale, and the review screen's note explains
            // why the floor goes on the scale rather than on each font: every offset here -- the
            // clock's inset, the rate's band, the modifier line, the harvest block -- is derived
            // from it too, so flooring the type alone would grow the text inside a layout that did
            // not grow with it.
            //
            // Measured on a 360x780 phone, where the interface scale is 0.28 and only the modifier
            // line had a floor: the clock drew at 10 pixels, "ENERGY RATE" and "HARVESTED" at four,
            // and the harvest figure at eight. The rate is described three comments above as the
            // game itself -- "the biggest thing on screen", the number the player has to SEE cost
            // them -- and on a phone it was 15 pixels tall.
            //
            // 0.6 rather than the review screen's 0.7: it is the smallest value that brings this
            // screen's smallest type to the nine the rest of the interface floors at, and the HUD
            // sits over the dungeon rather than on a screen of its own, so every pixel it grows is
            // a pixel of the board it covers.
            float hud = Mathf.Max(scale, GameController.HudMinimumScale);

            var clock = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(34 * hud),
                fontStyle = FontStyle.Bold
            };
            var caption = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * hud) };

            clock.normal.textColor = raid.TimeRemaining <= 10f
                ? new Color(0.95f, 0.35f, 0.35f)
                : Color.white;
            GUI.Label(new Rect(24f * hud, 16f * hud, 320f * hud, 50f * hud),
                $"{Mathf.FloorToInt(raid.TimeRemaining / 60f):0}:{Mathf.FloorToInt(raid.TimeRemaining % 60f):00}",
                clock);

            // The rate is the game, so it is the biggest thing on screen and it breathes. A player
            // has to *see* dead time costing them without reading a tutorial.
            //
            // Scaled against the curve's real ceiling. This used to saturate at 12/s, chosen when the
            // rate could not in practice exceed 4 -- so once the curve was fixed and rates reached
            // 37/s, a spectacular spike pulsed exactly like a routine scuffle. SPEC.md section 9 asks
            // specifically for the number to pulse "when it spikes", which means the spike has to
            // look different from the floor.
            float intensity = Mathf.Clamp01(raid.CurrentRate / 30f);
            float beat = 9f + (intensity * 7f);
            float pulse = 1f + (Mathf.Sin(ratePulse * beat) * (0.05f + (intensity * 0.13f)));
            var rate = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(52 * hud * pulse),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter
            };
            // Grey when idle, green when earning, and gold at the top of the curve -- so the colour
            // says which of the three states the player is in without reading the figure. Two stops
            // rather than three left everything above 20/s looking identical, and the whole point of
            // the wound curve is what happens beyond that.
            Color heat = raid.CurrentRate < 20f
                ? Color.Lerp(new Color(0.45f, 0.45f, 0.5f), new Color(0.55f, 1f, 0.45f),
                    Mathf.Clamp01(raid.CurrentRate / 14f))
                : Color.Lerp(new Color(0.55f, 1f, 0.45f), new Color(1f, 0.85f, 0.3f),
                    Mathf.Clamp01((raid.CurrentRate - 20f) / 15f));
            rate.normal.textColor = heat;
            // Invariant culture throughout the HUD. The build picks up the machine's locale, and on
            // this one the rate rendered as "0,1/s" -- a comma reads as a thousands separator to
            // most players and makes the game's most important number ambiguous.
            GUI.Label(new Rect(0f, 10f * hud, Screen.width, 80f * hud),
                raid.CurrentRate.ToString("0.0", CultureInfo.InvariantCulture) + "/s", rate);

            caption.normal.textColor = new Color(0.7f, 0.7f, 0.78f);
            GUI.Label(new Rect(0f, 66f * hud, Screen.width, 30f * hud), "ENERGY RATE",
                new GUIStyle(caption) { alignment = TextAnchor.UpperCenter });

            // WHY the rate is what it is. Without this the modifiers are invisible: the number
            // moves and nothing tells the player their party just found a room, or that this fight
            // has gone on long enough to start costing them. A bonus nobody can see is a bonus
            // nobody learns to chase.
            string why = raid.Modifiers.Summary();
            if (!string.IsNullOrEmpty(why))
            {
                var modifierStyle = new GUIStyle(GUI.skin.label)
                {
                    // Floored with a minimum, because the itch embed runs at 0.4 scale and that is
                    // where a menu row once came out twelve pixels tall and unreadable.
                    fontSize = Mathf.Max(9, Mathf.RoundToInt(13 * hud)),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperCenter
                };

                // Green while anything is being added, warm while the grind is eating it -- so the
                // colour says which way the rate is going before the words are read.
                bool losing = why.Contains("- GRINDING");
                modifierStyle.normal.textColor = losing
                    ? new Color(0.85f, 0.45f, 0.35f)
                    : new Color(0.55f, 0.85f, 0.5f);

                GUI.Label(
                    new Rect(0f, Mathf.Floor(86f * hud), Screen.width, Mathf.Floor(24f * hud)),
                    why, modifierStyle);
            }

            var total = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(28 * hud),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            total.normal.textColor = new Color(0.85f, 0.7f, 1f);
            GUI.Label(new Rect(0f, 16f * hud, Screen.width - (24f * hud), 44f * hud),
                raid.EnergyHarvested.ToString("0", CultureInfo.InvariantCulture), total);
            GUI.Label(new Rect(0f, 50f * hud, Screen.width - (24f * hud), 30f * hud),
                "HARVESTED   spend " + raid.TotalEnergy.ToString("0", CultureInfo.InvariantCulture),
                new GUIStyle(caption) { alignment = TextAnchor.UpperRight });

            // Who is inside, kept on screen for the whole raid. The player has to be able to check
            // mid-minute whether this is the party with two healers or the one with none, without
            // having to have memorised the standings screen.
            var who = new GUIStyle(caption) { fontStyle = FontStyle.Bold };
            who.normal.textColor = new Color(0.85f, 0.7f, 1f);
            GUI.Label(new Rect(24f * scale, 62f * scale, Screen.width, 30f * scale),
                raid.Party.Composition.Name, who);

            GUI.Label(new Rect(24f * scale, Screen.height - GameController.VerbBarHeight, Screen.width, 30f * scale),
                "TAP A DOOR TO STALL   /   A SPAWNER TO AMBUSH   /   A TRAP TO WOUND"
                + "   /   SCROLL OR PINCH TO ZOOM   /   RIGHT-DRAG OR TWO FINGERS TO MOVE",
                caption);
        }
    }
}
