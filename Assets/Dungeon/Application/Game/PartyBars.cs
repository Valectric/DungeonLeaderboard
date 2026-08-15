using System.Collections.Generic;
using Dungeon.PartyManager;
using UnityEngine;

namespace Dungeon.Game
{
    /// <summary>
    /// Draws the health and mana bars that float over the party.
    /// </summary>
    /// <remarks>
    /// Split out of <see cref="DungeonView"/>, which was over the project's 400-line cap. Bars are a
    /// self-contained job: they own their own pool of quads, they are positioned from wherever the
    /// sprite ended up rather than from the simulation, and nothing else needs to know how many of
    /// them exist.
    /// </remarks>
    public sealed class PartyBars
    {
        private readonly SpriteWorkshop _sprites;
        private readonly List<SpriteRenderer> _backs = new();
        private readonly List<SpriteRenderer> _fills = new();
        private readonly List<SpriteRenderer> _manaBacks = new();
        private readonly List<SpriteRenderer> _manaFills = new();

        /// <summary>Creates bars that build through a shared sprite workshop.</summary>
        /// <param name="sprites">Workshop to make quads with.</param>
        public PartyBars(SpriteWorkshop sprites)
        {
            _sprites = sprites;
        }

        /// <summary>Hides the bars belonging to one party slot, for a dead member.</summary>
        /// <param name="index">Party slot.</param>
        public void Hide(int index)
        {
            if (index < _backs.Count)
            {
                _backs[index].enabled = false;
                _fills[index].enabled = false;
            }

            if (index < _manaBacks.Count)
            {
                _manaBacks[index].enabled = false;
                _manaFills[index].enabled = false;
            }
        }

        /// <summary>
        /// Hides every bar, for a screen where nobody's health means anything.
        /// </summary>
        /// <remarks>
        /// The league screen draws the player's own dungeon behind the standings and darkens it with
        /// a quad at 82% opacity. Masonry near luminance 0.12 falls to about 0.02 under that, but a
        /// bar is saturated — 0.90 green, 1.0 blue — and keeps roughly 0.16, some eight times the
        /// wall it sits on. So the brightest thing on the title screen was a health bar for a party
        /// that is not raiding, laid across the standings rows.
        /// <para>
        /// Found in the shipped WebGL build rather than in the editor, on the one screen SPEC.md
        /// calls the ten-second hook.
        /// </para>
        /// </remarks>
        public void HideAll()
        {
            for (int i = 0; i < _backs.Count; i++)
            {
                Hide(i);
            }
        }

        /// <summary>Width of an adventurer's health bar, in world units.</summary>
        public const float BarWidth = 0.62f;

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
        public void Draw(int index, Adventurer member, Vector3 spritePosition)
        {
            while (_backs.Count <= index)
            {
                _backs.Add(_sprites.MakeBar($"hpback_{_backs.Count}", 60));
                _fills.Add(_sprites.MakeBar($"hpfill_{_fills.Count}", 61));
            }

            SpriteRenderer back = _backs[index];
            SpriteRenderer fill = _fills[index];
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

            DrawMana(index, member, origin);
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
        private void DrawMana(int index, Adventurer member, Vector3 healthOrigin)
        {
            while (_manaBacks.Count <= index)
            {
                _manaBacks.Add(_sprites.MakeBar($"manaback_{_manaBacks.Count}", 58));
                _manaFills.Add(_sprites.MakeBar($"manafill_{_manaFills.Count}", 59));
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

    }
}
