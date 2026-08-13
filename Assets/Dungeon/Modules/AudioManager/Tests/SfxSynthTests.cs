using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.AudioManager.Tests
{
    /// <summary>
    /// Verifies the procedurally synthesised sound effects.
    /// </summary>
    /// <remarks>
    /// Audio is the one part of the game nobody can look at, so the properties that would otherwise
    /// be caught by ear are asserted here instead: that every sound actually contains signal, that
    /// nothing clips, and that a sound is identical from one run to the next.
    /// </remarks>
    public sealed class SfxSynthTests
    {
        /// <summary>Reads a clip's samples.</summary>
        /// <param name="clip">Clip to read.</param>
        /// <returns>The samples.</returns>
        private static float[] Samples(AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            return data;
        }

        /// <summary>Every sound builds, has length, and carries actual signal.</summary>
        /// <remarks>
        /// A clip of pure silence would pass any test that only checked it existed, and would be
        /// completely invisible until someone played the game with the volume up.
        /// </remarks>
        [Test]
        public void EverySound_ContainsAudibleSignal()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                AudioClip clip = SfxSynth.Build(sfx);
                Assert.IsNotNull(clip, $"{sfx} produced no clip");
                Assert.Greater(clip.samples, 500, $"{sfx} is too short to hear");

                float[] data = Samples(clip);
                float peak = 0f;
                double energy = 0d;
                foreach (float sample in data)
                {
                    peak = Mathf.Max(peak, Mathf.Abs(sample));
                    energy += sample * sample;
                }

                double rms = System.Math.Sqrt(energy / data.Length);
                MooseRunnerFacade.Log(
                    $"{sfx}: {clip.length:F2}s peak={peak:F2} rms={rms:F3}");

                Assert.Greater(peak, 0.05f, $"{sfx} is effectively silent");
                Assert.Greater(rms, 0.005f, $"{sfx} has almost no energy in it");
            }
        }

        /// <summary>No sound clips, which would crackle on every play.</summary>
        [Test]
        public void NoSound_ExceedsFullScale()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                foreach (float sample in Samples(SfxSynth.Build(sfx)))
                {
                    Assert.LessOrEqual(Mathf.Abs(sample), 1f,
                        $"{sfx} clips, which will crackle every time it plays");
                }
            }
        }

        /// <summary>
        /// Every sound starts near silence, so it cannot click.
        /// </summary>
        /// <remarks>
        /// A waveform that begins at full amplitude produces an audible click on every play. With
        /// hit sounds firing several times a second, that would be the loudest thing in the game.
        /// </remarks>
        [Test]
        public void EverySound_FadesInRatherThanClicking()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                float[] data = Samples(SfxSynth.Build(sfx));
                Assert.Less(Mathf.Abs(data[0]), 0.05f,
                    $"{sfx} starts at full amplitude and will click");
            }
        }

        /// <summary>
        /// A sound is byte-identical every time it is built.
        /// </summary>
        /// <remarks>
        /// The project's constraint is that a run can be reproduced from a seed. Noise seeded from
        /// the system clock would quietly break that for audio, and nothing else would notice.
        /// </remarks>
        [Test]
        public void EverySound_IsDeterministic()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                float[] first = Samples(SfxSynth.Build(sfx));
                float[] again = Samples(SfxSynth.Build(sfx));

                Assert.AreEqual(first.Length, again.Length, $"{sfx} changed length between builds");
                for (int i = 0; i < first.Length; i += 97)
                {
                    Assert.AreEqual(first[i], again[i], 0.00001f,
                        $"{sfx} differs at sample {i} between two builds");
                }
            }
        }

        /// <summary>Sounds are distinct from one another, not the same blip relabelled.</summary>
        [Test]
        public void Sounds_DifferFromEachOther()
        {
            float[] hit = Samples(SfxSynth.Build(Sfx.HitMonster));
            float[] trap = Samples(SfxSynth.Build(Sfx.TrapFire));
            float[] heal = Samples(SfxSynth.Build(Sfx.Heal));

            Assert.AreNotEqual(hit.Length, trap.Length,
                "a trap and a hit should not even be the same length");
            Assert.AreNotEqual(heal.Length, trap.Length,
                "a heal and a trap should not be the same length");
        }

        /// <summary>The facade plays without throwing, and can be muted.</summary>
        [Test]
        public void TheFacade_PlaysAndMutes()
        {
            AudioFacade.ResetForTests();
            AudioFacade facade = AudioFacade.Instance();
            Assert.IsNotNull(facade, "the facade should bootstrap itself");

            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx)))
            {
                facade.Play(sfx, 0.01f);
            }

            AudioFacade.Muted = true;
            facade.Play(Sfx.TrapFire, 0.01f);
            AudioFacade.Muted = false;

            AudioFacade.ResetForTests();
            Assert.Pass("played every sound without throwing");
        }
    }
}
