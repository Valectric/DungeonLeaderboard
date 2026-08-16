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

        /// <summary>
        /// What one room the party has walked into pays, for the rest of the raid.
        /// </summary>
        /// <remarks>
        /// <b>Half the other bonuses, and the author chose the number.</b> At the full 2 the
        /// stall-versus-stroll gap fell to 2.09x against a 2.5x floor -- a party that walks through
        /// and leaves gained three times more than one that stayed and bled, which is the door verb
        /// being paid to fail. At 1 the bonus still makes a three-deep dungeon worth building
        /// instead of dead floor the party walks over, which is what it is for.
        /// <para>
        /// The author's reasoning, worth keeping because it is a design argument rather than a
        /// balance one: leading the party with spawners in the direction they are already heading is
        /// good play, and it should be rewarded -- they are going that way regardless, and the rooms
        /// they open pay for the trip.
        /// </para>
        /// </remarks>
        public const float RoomBonus = 1f;

        /// <summary>Seconds the disarm bonus lasts.</summary>
        public const float DisarmBonusSeconds = 7f;

        /// <summary>
        /// Seconds the "you just gained two seconds" notice stays up after a new room.
        /// </summary>
        /// <remarks>
        /// This is the length of the <i>notice</i>, not of the bonus. The rate bonus a room pays is
        /// permanent for the raid and stacks — see <see cref="RoomsEntered"/>. It used to be this
        /// timer that gated the bonus itself, and the author replaced that: a room is a lasting
        /// gain now, not a three-second flourish.
        /// </remarks>
        public const float NewRoomNoticeSeconds = 3f;

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

        /// <summary>Seconds the new-room notice still has to run.</summary>
        private float _newRoomNoticeLeft;

        /// <summary>
        /// Rooms the party has walked into this raid, each paying <see cref="Bonus"/> for good.
        /// </summary>
        /// <remarks>
        /// The author's rule: <b>"after entering a new room the team gains +2/s for the rest of the
        /// run per room."</b> A count rather than a timer, because the bonus no longer expires — a
        /// party three rooms deep is earning +6/s from depth alone, on top of whatever it is doing
        /// there.
        /// <para>
        /// This is the first modifier that only ever grows, which is deliberate: it makes pushing
        /// the party <i>onward</i> pay, where every other bonus rewards what is happening right now.
        /// It is also why the room bonus is not eased away by a quiet corridor.
        /// </para>
        /// </remarks>
        private int _roomsEntered;

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

        /// <summary>
        /// What is currently moving the rate, in words the player can act on.
        /// </summary>
        /// <remarks>
        /// A bonus the player cannot see is a bonus they cannot learn to chase, which would make
        /// the whole variation system decoration: the rate would simply move and nothing would say
        /// why. Named causes, not arithmetic -- "+ CROWD" teaches that more monsters pay, where
        /// "+4.0" teaches nothing.
        /// <para>
        /// The grind decay is shown even though it is a penalty, and especially because it is. It
        /// is the one modifier whose whole purpose is to change what the player does, and they
        /// cannot respond to a number quietly shrinking for reasons nobody told them.
        /// </para>
        /// </remarks>
        /// <returns>A short line, or empty when nothing is active.</returns>
        public string Summary()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (_disarmLeft > 0f)
            {
                parts.Add("+ DISARM");
            }

            if (_roomsEntered > 0)
            {
                // Two states for one cause, because a room pays twice and the payments have
                // different lifetimes. For three seconds it announces the arrival and names the
                // CLOCK gain -- without that number the timer visibly counts up while the player
                // watches it count down, which reads as a glitch, not a reward. After that it
                // settles into the running total, so the player can see that depth is still paying
                // long after the moment has passed.
                parts.Add(_newRoomNoticeLeft > 0f
                    ? $"+ NEW ROOM +{Raid.NewRoomSeconds:0}s"
                    : $"+ ROOMS x{_roomsEntered}");
            }

            if (_extraEnemies > 0)
            {
                parts.Add($"+ CROWD x{_extraEnemies}");
            }

            float decay = Decay();
            if (decay > 0f)
            {
                parts.Add($"- GRINDING {decay:0}");
            }

            return parts.Count == 0 ? string.Empty : string.Join("   ", parts);
        }

        /// <summary>Records that a trap was disarmed.</summary>
        public void RecordDisarm()
        {
            _disarmLeft = DisarmBonusSeconds;
        }

        /// <summary>Records that the party reached a room it had not seen this raid.</summary>
        /// <remarks>
        /// Two effects with different lifetimes, which is why the count and the notice are separate
        /// fields: the rate bonus is permanent and stacks, the notice is three seconds of the HUD
        /// saying so.
        /// </remarks>
        public void RecordNewRoom()
        {
            _roomsEntered++;
            _newRoomNoticeLeft = NewRoomNoticeSeconds;
        }

        /// <summary>Rooms walked into this raid. Each pays <see cref="Bonus"/> for the rest of it.</summary>
        public int RoomsEntered => _roomsEntered;

        /// <summary>
        /// Advances every timer by one tick.
        /// </summary>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        /// <param name="enemiesFacing">Living monsters sharing the party's room.</param>
        public void Tick(float deltaTime, int enemiesFacing)
        {
            _disarmLeft = Mathf.Max(0f, _disarmLeft - deltaTime);

            // Only the NOTICE runs down. _roomsEntered is not touched here -- the bonus it carries
            // lasts the whole raid, which is the point of it.
            _newRoomNoticeLeft = Mathf.Max(0f, _newRoomNoticeLeft - deltaTime);
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

            // Every room the party has ever walked into this raid, permanently. Depth pays.
            total += RoomBonus * _roomsEntered;

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
