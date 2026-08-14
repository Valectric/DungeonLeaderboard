using UnityEngine;

namespace Dungeon.RaidManager
{
    /// <summary>
    /// The bonuses and penalties that pay a party for varying what it does.
    /// </summary>
    /// <remarks>
    /// The author's brief, after playing M8: <i>"if you just push a lot after thirty seconds it
    /// starts to get boring, it's just enemy wave after wave after wave, that's no fun. Exploration
    /// is fun. The variation is important for the team."</i>
    /// <para>
    /// So the curve pays for <b>doing different things</b>: disarming, reaching somewhere new,
    /// fighting a crowd rather than a single monster — and it stops paying for the same fight going
    /// on and on. Every one of them is a flat amount added to the party's summed rate, in the same
    /// units and the same place as the existing chest bonus, which already had exactly this shape
    /// (an amount, a duration, and an anti-spam rule).
    /// </para>
    /// <para>
    /// <b>Why flat, and why team-wide.</b> The alternatives were measured against the shipped curve
    /// and each is a fourfold difference or worse. Per-member would make a five-room walkthrough
    /// worth about 126 energy — a good review for a party that never fought, and SPEC's "an
    /// unengaged party walking a corridor must earn almost nothing" dead. As a multiplier, stacked
    /// across four modifiers, it is eight to twenty-four times and annihilates the wound term.
    /// Inside the per-member wound multiply it pays roughly 16/s off a single member at 5% health,
    /// which is D12's explicit warning about parking one body at death's door and farming it.
    /// </para>
    /// <para>
    /// <b>What this must never do</b> is make a wipe or an early finish attractive. The decay has a
    /// floor for that reason, and the floor is not a detail: an unfloored penalty would eventually
    /// pay the player to let the party die, which inverts the one rule the whole game rests on.
    /// </para>
    /// </remarks>
    public sealed class RateModifiers
    {
        /// <summary>The size of every bonus the author specified, in energy per second.</summary>
        /// <remarks>
        /// His "+2". Read as a flat addition to the team's summed rate, the same units as
        /// <see cref="EnergyCurve.ChestBonus"/> at 6/s. Yardsticks: four members walking earn
        /// 0.24/s together, a normal fight runs around 8/s, and the measured peak across rosters is
        /// 25–38/s. So one bonus is a noticeable lift and not a takeover.
        /// </remarks>
        public const float Bonus = 2f;

        /// <summary>Seconds the disarm bonus lasts.</summary>
        public const float DisarmBonusSeconds = 7f;

        /// <summary>Seconds the new-room bonus lasts.</summary>
        public const float NewRoomBonusSeconds = 3f;

        /// <summary>
        /// Seconds of unbroken combat before the same fight starts paying less.
        /// </summary>
        public const float DecayStartsAfter = 30f;

        /// <summary>How often the decay deepens, once it has started.</summary>
        public const float DecayStepSeconds = 5f;

        /// <summary>
        /// How far the decay may pull the rate down.
        /// </summary>
        /// <remarks>
        /// The floor exists so that grinding becomes <i>dull</i> rather than <i>ruinous</i>. Without
        /// it a long fight would eventually earn less than no fight, and the cheapest way out of a
        /// deep hole would be to let the party die — which is the losing state the entire design is
        /// built to make unattractive.
        /// </remarks>
        public const float MaxDecay = 12f;

        /// <summary>
        /// Seconds of unbroken combat before the party tires and slows down.
        /// </summary>
        public const float FatigueAfter = 10f;

        /// <summary>How fast a tired party moves, as a fraction of its normal pace.</summary>
        /// <remarks>
        /// The author said "thirty percent slower" and then settled on "eighty percent of normal
        /// speed"; the later and more specific figure is the one implemented. He also described the
        /// recovery as "a significant twenty percent speed up" — that is this multiplier lifting,
        /// not a separate bonus above full speed.
        /// </remarks>
        public const float FatigueSpeed = 0.8f;

        /// <summary>
        /// Seconds clear of combat before the fight timer resets.
        /// </summary>
        /// <remarks>
        /// The author's answer when asked what resets it. It is what stops a stream of fresh spawns
        /// reading as many short fights instead of one long one, which is the entire substance of
        /// the request — and it stops the party stepping out of a room for a single tick to wipe the
        /// penalty.
        /// </remarks>
        public const float ResetAfterSeconds = 10f;

        /// <summary>Seconds of the disarm bonus still to run.</summary>
        private float _disarmLeft;

        /// <summary>Seconds of the new-room bonus still to run.</summary>
        private float _newRoomLeft;

        /// <summary>Seconds the party has been in unbroken combat.</summary>
        private float _fightingFor;

        /// <summary>Seconds since the party was last in combat.</summary>
        private float _clearFor;

        /// <summary>Extra monsters beyond the first that the party is facing.</summary>
        private int _extraEnemies;

        /// <summary>The eased total, which is what the rate actually reads.</summary>
        private float _smoothed;

        /// <summary>
        /// Seconds a change in the modifiers takes to be felt.
        /// </summary>
        /// <remarks>
        /// The crowd bonus steps by a whole <see cref="Bonus"/> the instant a monster dies or
        /// spawns, and the rate is a large pulsing number the player is meant to read at a glance.
        /// Stepped, it flickers: measured, it crossed its own average far more often than the
        /// stability tests allow, which is the HUD becoming noise rather than a signal.
        /// <para>
        /// Slower than the rate's own easing on purpose, so the bonus fades in and out rather than
        /// arriving with the kill. It costs nothing in total energy -- what is smoothed is when it
        /// is paid, not how much.
        /// </para>
        /// </remarks>
        public const float EaseSeconds = 2.2f;

        /// <summary>Seconds the party has been fighting without a break.</summary>
        public float FightingFor => _fightingFor;

        /// <summary>Whether the party is tired enough to have slowed down.</summary>
        public bool IsFatigued => _fightingFor >= FatigueAfter;

        /// <summary>How fast the party may move right now, as a fraction of its normal pace.</summary>
        public float SpeedMultiplier => IsFatigued ? FatigueSpeed : 1f;

        /// <summary>Records that a trap was disarmed.</summary>
        public void RecordDisarm()
        {
            _disarmLeft = DisarmBonusSeconds;
        }

        /// <summary>Records that the party reached a room it had not seen this raid.</summary>
        public void RecordNewRoom()
        {
            _newRoomLeft = NewRoomBonusSeconds;
        }

        /// <summary>
        /// Advances every timer by one tick.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="enemiesFacing">Living monsters sharing the party's room.</param>
        public void Tick(float deltaTime, int enemiesFacing)
        {
            _disarmLeft = Mathf.Max(0f, _disarmLeft - deltaTime);
            _newRoomLeft = Mathf.Max(0f, _newRoomLeft - deltaTime);
            _extraEnemies = Mathf.Max(0, enemiesFacing - 1);

            if (enemiesFacing > 0)
            {
                _fightingFor += deltaTime;
                _clearFor = 0f;
                Ease(deltaTime);
                return;
            }

            // Out of combat. The fight timer does NOT reset on the first quiet tick -- it takes a
            // clear run of ResetAfterSeconds, so a party that steps through a doorway for an instant,
            // or whose last monster dies a moment before the next spawns, is still in the same fight.
            _clearFor += deltaTime;
            if (_clearFor >= ResetAfterSeconds)
            {
                _fightingFor = 0f;
            }

            Ease(deltaTime);
        }

        /// <summary>Eases the felt total toward the raw one. Call once per tick, last.</summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        private void Ease(float deltaTime)
        {
            _smoothed = Mathf.Lerp(
                _smoothed, RawTotal(), Mathf.Clamp01(deltaTime / EaseSeconds));
        }

        /// <summary>
        /// What every modifier adds up to right now, in energy per second.
        /// </summary>
        /// <returns>The total, which may be negative during a long grind.</returns>
        public float Total()
        {
            return _smoothed;
        }

        /// <summary>
        /// What every modifier adds up to before easing, in energy per second.
        /// </summary>
        /// <returns>The raw total, which may be negative during a long grind.</returns>
        public float RawTotal()
        {
            float total = 0f;

            if (_disarmLeft > 0f)
            {
                total += Bonus;
            }

            if (_newRoomLeft > 0f)
            {
                total += Bonus;
            }

            // Every monster past the first. A crowd is more interesting than a duel and pays for it.
            total += Bonus * _extraEnemies;

            return total - Decay();
        }

        /// <summary>
        /// How much a fight that has gone on too long is currently losing.
        /// </summary>
        /// <returns>The penalty, never more than <see cref="MaxDecay"/>.</returns>
        public float Decay()
        {
            if (_fightingFor < DecayStartsAfter)
            {
                return 0f;
            }

            // -2 the moment it starts, then a further -2 every five seconds.
            float over = _fightingFor - DecayStartsAfter;
            float steps = 1f + Mathf.Floor(over / DecayStepSeconds);
            return Mathf.Min(MaxDecay, Bonus * steps);
        }
    }
}
