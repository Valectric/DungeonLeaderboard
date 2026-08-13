using System;
using UnityEngine;

namespace Dungeon.AudioManager
{
    /// <summary>Every sound the game makes.</summary>
    public enum Sfx
    {
        /// <summary>A door swinging. The cheap, spammable verb.</summary>
        DoorToggle = 0,

        /// <summary>A monster arriving.</summary>
        MobSpawn = 1,

        /// <summary>A trap going off.</summary>
        TrapFire = 2,

        /// <summary>An adventurer taking a blow.</summary>
        HitAdventurer = 3,

        /// <summary>A monster taking a blow.</summary>
        HitMonster = 4,

        /// <summary>The healer casting.</summary>
        Heal = 5,

        /// <summary>A monster dying, which ends a stall.</summary>
        MobDied = 6,

        /// <summary>Something bought in the shop.</summary>
        Purchase = 7,

        /// <summary>The mage blinking away.</summary>
        Blink = 8,

        /// <summary>A raid ending.</summary>
        RaidEnd = 9
    }

    /// <summary>
    /// Builds every sound effect procedurally, so the game ships with no audio assets at all.
    /// </summary>
    /// <remarks>
    /// Synthesised rather than recorded for the same reason the art is generated rather than drawn:
    /// it is reproducible from code, costs nothing in the build, and cannot go missing. It also suits
    /// the material -- a chiptune blip sits with pixel art in a way a sampled sword clang would not.
    /// <para>
    /// Deterministic throughout. The noise generator is seeded per sound, so a given effect is
    /// byte-identical on every machine and every run, which keeps the project's
    /// reproduce-from-a-seed property intact for audio as much as for combat rolls.
    /// </para>
    /// </remarks>
    public static class SfxSynth
    {
        /// <summary>Sample rate for every generated clip.</summary>
        public const int SampleRate = 22050;

        /// <summary>
        /// Builds the clip for a sound.
        /// </summary>
        /// <param name="sfx">Sound to build.</param>
        /// <returns>A ready-to-play clip.</returns>
        public static AudioClip Build(Sfx sfx)
        {
            return sfx switch
            {
                // A dull wooden thunk: low, fast decay, no sweep.
                Sfx.DoorToggle => Tone($"sfx_{sfx}", 0.13f, 180f, 120f, 0.35f, 0.55f, 11),
                // Rising, so arrival reads as something coming up out of the floor.
                Sfx.MobSpawn => Tone($"sfx_{sfx}", 0.28f, 150f, 430f, 0.30f, 0.35f, 23),
                // Harsh and loud: the most expensive verb should sound like it hurt.
                Sfx.TrapFire => Tone($"sfx_{sfx}", 0.34f, 620f, 90f, 0.42f, 0.85f, 47),
                // Short, dry click. These play constantly during a fight, so they must not fatigue.
                Sfx.HitAdventurer => Tone($"sfx_{sfx}", 0.07f, 340f, 190f, 0.18f, 0.65f, 71),
                Sfx.HitMonster => Tone($"sfx_{sfx}", 0.07f, 500f, 300f, 0.16f, 0.55f, 89),
                // A clean rising chime, the only pure tone in the set, so healing stands out.
                Sfx.Heal => Tone($"sfx_{sfx}", 0.30f, 520f, 900f, 0.22f, 0.05f, 101),
                // Falling and noisy: something collapsing.
                Sfx.MobDied => Tone($"sfx_{sfx}", 0.32f, 420f, 70f, 0.30f, 0.70f, 131),
                // Bright two-step blip, the classic "that worked" sound.
                Sfx.Purchase => Tone($"sfx_{sfx}", 0.18f, 660f, 990f, 0.26f, 0.0f, 149),
                // Fast upward whoosh.
                Sfx.Blink => Tone($"sfx_{sfx}", 0.22f, 300f, 1200f, 0.24f, 0.45f, 167),
                _ => Tone($"sfx_{sfx}", 0.5f, 300f, 150f, 0.32f, 0.25f, 191)
            };
        }

        /// <summary>
        /// Renders one clip: a square-wave sweep mixed with seeded noise, under a decay envelope.
        /// </summary>
        /// <remarks>
        /// A square wave rather than a sine because it carries through a busy mix at low volume, and
        /// because it is the sound of the era the art belongs to. The noise share is what separates a
        /// trap's crunch from a heal's chime.
        /// </remarks>
        /// <param name="name">Clip name.</param>
        /// <param name="seconds">Length.</param>
        /// <param name="startHz">Pitch at the start.</param>
        /// <param name="endHz">Pitch at the end.</param>
        /// <param name="volume">Peak amplitude, 0 to 1.</param>
        /// <param name="noise">How much of the signal is noise rather than tone, 0 to 1.</param>
        /// <param name="seed">Seed for the noise, so the clip is identical every run.</param>
        /// <returns>The clip.</returns>
        private static AudioClip Tone(
            string name, float seconds, float startHz, float endHz,
            float volume, float noise, int seed)
        {
            int samples = Mathf.Max(1, (int)(SampleRate * seconds));
            var data = new float[samples];
            var random = new System.Random(seed);
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;

                // Exponential sweep: pitch changes fast at the start and settles, which reads as a
                // single event rather than a slide.
                float hz = Mathf.Lerp(startHz, endHz, 1f - ((1f - t) * (1f - t)));
                phase += hz / SampleRate;
                float square = (phase % 1f) < 0.5f ? 1f : -1f;

                float hiss = ((float)random.NextDouble() * 2f) - 1f;
                float mixed = Mathf.Lerp(square, hiss, noise);

                // Percussive decay. Everything here is a hit of some kind, so nothing sustains.
                float envelope = Mathf.Pow(1f - t, 2.2f);

                // A short fade in kills the click that a waveform starting at full amplitude makes.
                float attack = Mathf.Clamp01(i / (SampleRate * 0.004f));

                data[i] = mixed * envelope * attack * volume;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
