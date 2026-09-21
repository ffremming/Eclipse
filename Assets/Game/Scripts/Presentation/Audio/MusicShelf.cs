using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// Everything one mood is: the clips it may play, how loud, how long it takes to become the
    /// music, and how much quiet it leaves between clips.
    /// <para>
    /// The rest is the part that is easy to leave out and shouldn't be. Eclipse's score is one
    /// instrument playing alone, and one instrument playing without pause for a twenty-minute run
    /// stops being music and becomes a texture the player edits out
    /// (<c>GDC-L1-AUDIO-0005</c>). Exploration rests; a fight does not, because a fight is the
    /// contrast the rest was saved up for.
    /// </para>
    /// <para>
    /// The fade is per mood rather than one number for the whole system because the transitions are
    /// not alike: a fight should arrive before the player has finished turning round, and a castle
    /// should come up under them slowly enough that they do not notice it starting.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class MusicShelf
    {
        [Tooltip("Which mood these clips are for. One shelf per mood; a second shelf for the same " +
                 "mood is never reached.")]
        [SerializeField] private MusicMood mood = MusicMood.Explore;

        [Tooltip("The clips, played in a shuffled order that gets through all of them before it " +
                 "repeats any.")]
        [SerializeField] private AudioClip[] clips = Array.Empty<AudioClip>();

        [Tooltip("Trim on top of the loudness already baked into the clips. 1 is as rendered; this " +
                 "is for correcting a mood that sits wrong against the others, not for the " +
                 "player's volume, which is applied separately.")]
        [Range(0f, 2f)]
        [SerializeField] private float gain = 1f;

        [Tooltip("Seconds this mood takes to fade up, and the outgoing mood takes to fade down. " +
                 "Short reads as an interruption, long reads as weather.")]
        [Min(0.05f)]
        [SerializeField] private float fadeSeconds = 3.5f;

        [Tooltip("Seconds of silence between one clip of this mood and the next. Zero means the " +
                 "clips crossfade into each other with no gap at all.")]
        [Min(0f)]
        [SerializeField] private float restSeconds;

        [Tooltip("Play one clip and then stay quiet, instead of going on to the next. For a sting " +
                 "— death — where the point is the silence afterwards.")]
        [SerializeField] private bool playsOnce;

        private AudioClip[] playable;

        public MusicMood Mood => mood;
        public float Gain => gain;
        public float FadeSeconds => fadeSeconds;
        public float RestSeconds => restSeconds;
        public bool PlaysOnce => playsOnce;

        /// <summary>
        /// The clips that actually exist, with the empty rows an Inspector array always collects
        /// taken out — so nothing downstream has to decide what to do when the shuffle hands it a
        /// hole. Worked out once: the array cannot change at runtime.
        /// </summary>
        public AudioClip[] Playable
        {
            get
            {
                if (playable != null) return playable;

                var kept = new List<AudioClip>(clips?.Length ?? 0);
                if (clips != null)
                {
                    foreach (AudioClip clip in clips)
                        if (clip != null) kept.Add(clip);
                }

                playable = kept.ToArray();
                return playable;
            }
        }
    }
}
