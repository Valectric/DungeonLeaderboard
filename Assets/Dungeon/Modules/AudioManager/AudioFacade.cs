using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.AudioManager
{
    /// <summary>
    /// Plays the game's sound effects.
    /// </summary>
    /// <remarks>
    /// This is a Module. It builds every clip procedurally on first use -- see <see cref="SfxSynth"/>
    /// -- so the game ships with no audio assets and nothing to go missing in a build.
    /// <para>
    /// Self-bootstrapping: the first caller creates it. Nothing has to be placed in the scene, which
    /// matters here because the play scene is generated and would discard anything wired by hand.
    /// </para>
    /// </remarks>
    public sealed class AudioFacade : MonoBehaviour
    {
        /// <summary>How many sounds can overlap before the oldest is reused.</summary>
        /// <remarks>
        /// A fight lands several blows a second across four adventurers and a monster. With too few
        /// voices the hits cut each other off and the fight sounds thinner the busier it gets, which
        /// is exactly backwards.
        /// </remarks>
        public const int Voices = 8;

        /// <summary>
        /// Shortest gap between two plays of the same sound.
        /// </summary>
        /// <remarks>
        /// Identical clips landing on the same frame sum into one much louder clip, and a swarm of
        /// slimes taking simultaneous hits made a nasty crack. Suppressing the duplicates costs
        /// nothing audible and removes the spike.
        /// </remarks>
        public const float RepeatGuardSeconds = 0.045f;

        private static AudioFacade _instance;

        private readonly Dictionary<Sfx, AudioClip> _clips = new();
        private readonly Dictionary<Sfx, float> _lastPlayed = new();
        private AudioSource[] _sources;
        private int _next;

        /// <summary>Whether sound is switched on.</summary>
        public static bool Muted { get; set; }

        /// <summary>
        /// The live instance, created on first use.
        /// </summary>
        /// <returns>The facade, or null when the application is shutting down.</returns>
        public static AudioFacade Instance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("Audio");
            _instance = go.AddComponent<AudioFacade>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        /// <summary>Builds the voice pool.</summary>
        private void Awake()
        {
            _sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                // Flat 2D audio. The camera pans and zooms freely, so positional falloff would make
                // a fight fade out simply because the player looked at their shop instead.
                source.spatialBlend = 0f;
                _sources[i] = source;
            }
        }

        /// <summary>
        /// Plays a sound.
        /// </summary>
        /// <param name="sfx">Sound to play.</param>
        /// <param name="volume">Volume scale, 0 to 1.</param>
        public void Play(Sfx sfx, float volume = 1f)
        {
            if (Muted || _sources == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(sfx, out float last) &&
                now - last < RepeatGuardSeconds)
            {
                return;
            }

            _lastPlayed[sfx] = now;

            if (!_clips.TryGetValue(sfx, out AudioClip clip))
            {
                clip = SfxSynth.Build(sfx);
                _clips[sfx] = clip;
            }

            AudioSource source = _sources[_next];
            _next = (_next + 1) % _sources.Length;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        /// <summary>Plays a sound without the caller needing to hold the facade.</summary>
        /// <param name="sfx">Sound to play.</param>
        /// <param name="volume">Volume scale.</param>
        public static void Cue(Sfx sfx, float volume = 1f)
        {
            AudioFacade facade = Instance();
            if (facade != null)
            {
                facade.Play(sfx, volume);
            }
        }

        /// <summary>Forgets the instance so a test can start from a clean pool.</summary>
        /// <remarks>Not intended for production use -- only for automated testing.</remarks>
        public static void ResetForTests()
        {
            if (_instance != null)
            {
                DestroyImmediate(_instance.gameObject);
            }

            _instance = null;
        }
    }
}
