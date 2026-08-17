using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.AudioManager
{
    /// <summary>
    /// The looping tracks the game can play.
    /// </summary>
    /// <remarks>
    /// Two, because the game has two moods: the sixty seconds with a party inside it, and
    /// everything else. Both are CC0 -- see CREDITS.md -- which is the only kind of asset this
    /// repository can carry, being public and MIT.
    /// </remarks>
    public enum Music
    {
        /// <summary>Silence.</summary>
        None = 0,

        /// <summary>The raid: driving, and the only minute that scores.</summary>
        Raid = 1,

        /// <summary>Standings, shop, review and the endings.</summary>
        Calm = 2
    }

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
        /// <summary>How many sources the plays are spread across, round-robin.</summary>
        /// <remarks>
        /// A fight lands several blows a second across a party — <b>up to nine adventurers</b> since
        /// the growth curve was fixed, not the four this said until 2026-08-17 — and the monsters
        /// meeting them.
        /// <para>
        /// <b>The reason this said it was here is wrong, and it was nearly "fixed" on the strength
        /// of it.</b> The old note claimed that with too few voices the hits cut each other off. They
        /// do not: <c>PlayOneShot</c> layers, and repeated calls on a single source play over one
        /// another rather than truncating. So this pool distributes plays; it is not what stops them
        /// interrupting, and scaling it with the party buys nothing on that account. What actually
        /// bounds the pile-up is <see cref="RepeatGuardSeconds"/>, which is per-sound and unaffected
        /// by party size.
        /// </para>
        /// <para>
        /// Left at eight deliberately. Whether a nine-strong fight needs more sources is a question
        /// about mixing and headroom that can only be answered by listening to one, and nothing here
        /// can assert it.
        /// </para>
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

        /// <summary>The looping voice music plays on.</summary>
        private AudioSource _music;

        /// <summary>Which track is playing, so re-cueing the same one does not restart it.</summary>
        private Music _playing = Music.None;

        /// <summary>
        /// How loud music sits under the effects.
        /// </summary>
        /// <remarks>
        /// Deliberately well down. The game's sounds carry information -- a door forcing, a heal
        /// landing, a body dropping -- and the rate is read from the HUD while they play. Music that
        /// competes with those is music that costs the player points.
        /// </remarks>
        public const float MusicVolume = 0.38f;
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

        /// <summary>Builds the voice pool, and an ear to hear it with.</summary>
        /// <remarks>
        /// The listener is not an afterthought — without one Unity plays nothing at all, silently.
        /// The play scene is built from code and its camera is created with
        /// <c>AddComponent&lt;Camera&gt;</c>, which does <b>not</b> bring an <see cref="AudioListener"/>
        /// with it the way the editor's default Main Camera does. So every sound the game synthesised
        /// was dispatched correctly to a room with nobody in it, and the only trace was a lone Unity
        /// warning that a passing test suite buried.
        /// <para>
        /// Adding it here rather than in the scene builder keeps the module responsible for its own
        /// bootstrap, and means the audio works in any scene that touches the facade — including the
        /// stripped scenes tests build.
        /// </para>
        /// </remarks>
        private void Awake()
        {
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }

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

            // Music gets a voice of its own rather than sharing the pool. The pool is a round-robin
            // of one-shots that steal from each other by design; a track has to keep playing while
            // forty of those fire over the top of it.
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = MusicVolume;
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

        /// <summary>
        /// Starts a looping track, or switches to another, or stops.
        /// </summary>
        /// <remarks>
        /// Re-cueing whatever is already playing does nothing, which is what lets the caller say
        /// what the music should be every frame without the track stuttering back to its first bar.
        /// </remarks>
        /// <param name="track">Track to loop, or <see cref="Music.None"/> for silence.</param>
        public void PlayMusic(Music track)
        {
            if (_music == null || _playing == track)
            {
                return;
            }

            _playing = track;

            if (track == Music.None || Muted)
            {
                _music.Stop();
                return;
            }

            AudioClip clip = Resources.Load<AudioClip>(
                track == Music.Raid ? "music/raid" : "music/calm");

            // Quietly, like every other optional asset here: a missing track costs the music and
            // not the game.
            if (clip == null)
            {
                return;
            }

            _music.clip = clip;
            _music.volume = MusicVolume;
            _music.Play();
        }

        /// <summary>Cues music without the caller needing to hold the facade.</summary>
        /// <param name="track">Track to loop, or <see cref="Music.None"/>.</param>
        public static void CueMusic(Music track)
        {
            AudioFacade facade = Instance();
            if (facade != null)
            {
                facade.PlayMusic(track);
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
