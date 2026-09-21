using System;
using System.Collections.Generic;
using UnityEngine;
using SpaceGame.Core;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// The thing that actually plays the music: two voices, and a crossfade between them.
    /// <para>
    /// Two is the whole trick. One source can only cut, and a cut between two takes of the same
    /// instrument in the same room is the most obvious edit there is. With two, every change — a
    /// new mood, and the end of a clip inside one mood — is the same operation: the incoming voice
    /// rises while the outgoing one falls, and the player hears a piece of music becoming another
    /// one rather than a file being swapped.
    /// </para>
    /// <para>
    /// Nothing ever loops. <c>AudioSource.loop</c> would take the end of a clip back to its start
    /// with a hard seam once a minute, forever; instead the next clip is scheduled to begin while
    /// the current one is still fading, so a mood is a continuous performance assembled from
    /// however many takes it has (<c>GDC-L1-AUDIO-0003</c> — resequencing, rather than a fixed loop
    /// worn thin by a long run).
    /// </para>
    /// <para>
    /// Everything runs on unscaled time. The pause screen sets <c>Time.timeScale</c> to zero and
    /// the music has no reason to stop with the world — it is the only thing still holding the
    /// player's attention while they are in there.
    /// </para>
    /// <para>
    /// Volume comes from <see cref="GameSettings"/> rather than from a field, and is re-read on
    /// <see cref="GameSettings.Changed"/>, so the slider on the pause screen moves the music while
    /// it is playing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicDirector : MonoBehaviour
    {
        [Tooltip("Every piece of music in the game. Without one this component does nothing and " +
                 "says so.")]
        [SerializeField] private MusicLibrary library;

        [Tooltip("What to play from the moment the scene comes up. The menu scene wants Menu; the " +
                 "world wants Explore, which WorldMusic then takes over from.")]
        [SerializeField] private MusicMood startMood = MusicMood.Explore;

        [Tooltip("Start on the opening mood by itself. Off leaves the scene silent until " +
                 "something calls Play.")]
        [SerializeField] private bool playOnStart = true;

        [Tooltip("Seeds the shuffle. Zero takes the seed from the clock, so two runs do not open " +
                 "on the same piece; anything else makes the order repeatable, which is what you " +
                 "want while tuning.")]
        [SerializeField] private int shuffleSeed;

        [Tooltip("Seconds the opening fade takes, before any mood's own fade applies. A scene " +
                 "that starts at full volume reads as a recording someone else started.")]
        [Min(0f)]
        [SerializeField] private float openingFadeSeconds = 4f;

        private readonly List<Voice> voices = new List<Voice>(2);
        private readonly Dictionary<MusicMood, MusicPlaylist> playlists =
            new Dictionary<MusicMood, MusicPlaylist>();

        private System.Random shuffler;
        private Voice current;

        private MusicMood mood;
        private bool hasMood;

        // Unscaled timestamps for the two things a playing clip is still going to do.
        private float fadeOutAt = float.PositiveInfinity;
        private float nextClipAt = float.PositiveInfinity;

        // How long the clip on air takes to fade, kept so the end of it falls over the same
        // number of seconds the start of it rose over.
        private float currentFadeSeconds = 1f;

        private float level = 1f;

        /// <summary>What is playing, for anything that needs to avoid asking for it again.</summary>
        public MusicMood Mood => mood;

        private void Awake()
        {
            if (library == null)
            {
                Debug.LogError("[MusicDirector] " + name + " has no music library, so the game " +
                               "will be silent.", this);
                enabled = false;
                return;
            }

            shuffler = shuffleSeed != 0
                ? new System.Random(shuffleSeed)
                : new System.Random(Environment.TickCount);

            voices.Add(new Voice(gameObject));
            voices.Add(new Voice(gameObject));

            EnsureEar();
        }

        private void OnEnable()
        {
            GameSettings.Changed += ReadVolume;
            ReadVolume();
        }

        private void OnDisable() => GameSettings.Changed -= ReadVolume;

        private void Start()
        {
            if (playOnStart) Play(startMood, openingFadeSeconds);
        }

        /// <summary>
        /// Ask for a mood. Asking for the one already playing does nothing, so this is safe to
        /// call every frame — which is how <see cref="WorldMusic"/> uses it.
        /// </summary>
        public void Play(MusicMood wanted) => Play(wanted, fadeOverride: -1f);

        private void Play(MusicMood wanted, float fadeOverride)
        {
            if (hasMood && wanted == mood) return;

            mood = wanted;
            hasMood = true;

            MusicShelf shelf = library.Shelf(wanted);
            if (shelf == null)
            {
                // Not an error: a mood may be deliberately silent. The mood is still adopted, so
                // the score falls quiet instead of carrying on with the wrong music.
                Silence(fadeOverride >= 0f ? fadeOverride : currentFadeSeconds);
                return;
            }

            StartClip(shelf, fadeOverride);
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            if (now >= fadeOutAt)
            {
                fadeOutAt = float.PositiveInfinity;
                current?.FadeOut(currentFadeSeconds);
            }

            if (now >= nextClipAt)
            {
                nextClipAt = float.PositiveInfinity;
                MusicShelf shelf = hasMood ? library.Shelf(mood) : null;
                if (shelf != null) StartClip(shelf, fadeOverride: -1f);
            }

            foreach (Voice voice in voices) voice.Tick(Time.unscaledDeltaTime, level);
        }

        /// <summary>
        /// Puts the next clip of <paramref name="shelf"/> on the idle voice, fades the playing one
        /// out under it, and works out when this one will in turn have to hand over.
        /// </summary>
        private void StartClip(MusicShelf shelf, float fadeOverride)
        {
            AudioClip[] clips = shelf.Playable;
            int index = Playlist(shelf).Next();
            if (index < 0) return;

            AudioClip clip = clips[index];
            float fade = FadeOf(shelf, fadeOverride);

            Voice incoming = Idle();
            current?.FadeOut(fade);
            incoming.Begin(clip, shelf.Gain, fade, level);
            current = incoming;
            currentFadeSeconds = fade;

            if (shelf.PlaysOnce)
            {
                // A sting: it fades itself out at the end and then leaves the silence alone, which
                // is the whole point of it.
                fadeOutAt = Time.unscaledTime + Mathf.Max(clip.length - fade, 0f);
                nextClipAt = float.PositiveInfinity;
                return;
            }

            // The outgoing fade starts early enough to be finished by the time the clip would have
            // run out, so nothing ever reaches its own last sample.
            fadeOutAt = Time.unscaledTime + Mathf.Max(clip.length - fade, 0f);

            // With no rest the two coincide, which is a crossfade. With a rest the next clip waits
            // out the gap, and the mood breathes.
            nextClipAt = fadeOutAt + shelf.RestSeconds;
        }

        /// <summary>Fades everything out and schedules nothing. For a mood with nothing to play.</summary>
        private void Silence(float fade)
        {
            foreach (Voice voice in voices) voice.FadeOut(fade);
            current = null;
            fadeOutAt = float.PositiveInfinity;
            nextClipAt = float.PositiveInfinity;
        }

        private MusicPlaylist Playlist(MusicShelf shelf)
        {
            if (playlists.TryGetValue(shelf.Mood, out MusicPlaylist existing)) return existing;

            var playlist = new MusicPlaylist(shelf.Playable.Length, shuffler);
            playlists[shelf.Mood] = playlist;
            return playlist;
        }

        private static float FadeOf(MusicShelf shelf, float fadeOverride)
        {
            if (fadeOverride >= 0f) return fadeOverride;
            return shelf != null ? shelf.FadeSeconds : 1f;
        }

        /// <summary>The voice that is not currently the music, which is the one to build on.</summary>
        private Voice Idle()
        {
            foreach (Voice voice in voices)
                if (voice != current) return voice;

            return voices[0];
        }

        private void ReadVolume() => level = GameSettings.MasterVolume * GameSettings.MusicVolume;

        /// <summary>
        /// Makes sure something in the scene can hear.
        /// <para>
        /// Audio was stripped out of this project entirely, listener included, so no scene has an
        /// ear and a 2D source in one of them is silent for a reason nothing reports. The listener
        /// goes on the main camera rather than on this object, because that is where sound played
        /// in the world will want it the day there is any.
        /// </para>
        /// </summary>
        private void EnsureEar()
        {
            if (FindFirstObjectByType<AudioListener>(FindObjectsInactive.Include) != null) return;

            Camera camera = Camera.main;
            if (camera != null) camera.gameObject.AddComponent<AudioListener>();
            else gameObject.AddComponent<AudioListener>();
        }

        /// <summary>
        /// One of the two players, and the fade it is currently on.
        /// <para>
        /// The fade is kept as a 0..1 of its own rather than written straight onto the source's
        /// volume, because the player's volume setting multiplies it and can change halfway through
        /// a fade — folding the two together would make the slider jump the fade.
        /// </para>
        /// </summary>
        private sealed class Voice
        {
            private readonly AudioSource source;

            private float fade;
            private float target;
            private float rate;
            private float gain = 1f;

            public Voice(GameObject host)
            {
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;

                // Never looped: the director schedules the next clip itself, so that the seam is a
                // crossfade rather than a cut back to the top of the same take.
                source.loop = false;

                // Flat 2D. The score is not somewhere in the world, so it must not thin out when
                // the player turns their head.
                source.spatialBlend = 0f;

                // Music is the thing that must not be culled when something else wants a channel.
                source.priority = 0;

                source.volume = 0f;
            }

            public void Begin(AudioClip clip, float clipGain, float fadeSeconds, float level)
            {
                source.clip = clip;
                gain = clipGain;
                fade = 0f;
                source.volume = 0f;

                // AudioSource.Play does nothing at all on a clip whose data is not loaded, and it
                // says nothing about it — no warning, no exception, just silence that looks like a
                // mixing problem. The import preloads every clip, so this only ever has to catch
                // one that was imported some other way.
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();

                source.Play();
                FadeTo(1f, fadeSeconds);
                Apply(level);
            }

            private void FadeTo(float to, float seconds)
            {
                target = Mathf.Clamp01(to);
                rate = seconds > 0f ? 1f / seconds : float.PositiveInfinity;
            }

            public void FadeOut(float seconds) => FadeTo(0f, seconds);

            public void Tick(float unscaledDelta, float level)
            {
                // Idle means faded out with nowhere to go. Deliberately NOT "not playing": a voice
                // whose clip is still opening its stream is at zero and rising, and skipping it
                // because the source has not started yet latches it at silence forever.
                if (fade <= 0f && target <= 0f) return;

                fade = Mathf.MoveTowards(fade, target, rate * unscaledDelta);
                Apply(level);

                // Stopped rather than left running at zero, so a faded-out voice is not still
                // decoding a stream nobody can hear.
                if (fade <= 0f && target <= 0f && source.isPlaying) source.Stop();
            }

            private void Apply(float level) => source.volume = fade * gain * level;
        }
    }
}
