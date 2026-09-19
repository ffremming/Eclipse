// When the next idle break is due.
//
// Its own type rather than a couple of lines in Update for the reason SlideDecay and DodgeSelector
// are: it is the part with no Animator and no frame in it, so it can be pinned by a test without
// entering play mode.
//
// The jitter is the whole point. A break on a fixed timer is worse than no break at all — the eye
// picks up the period within two or three repetitions and the character reads as a machine
// waiting out a cooldown rather than as somebody standing there.
using UnityEngine;

namespace SpaceGame.Characters
{
    public static class IdleBreakSchedule
    {
        /// <summary>Shortest gap we will ever schedule, so a misconfigured jitter cannot chain breaks.</summary>
        public const float MinimumDelay = 1f;

        /// <summary>
        /// How long to stand still before the next break, given a <paramref name="roll"/> in 0..1
        /// from the caller's random source.
        /// </summary>
        public static float NextDelay(float baseInterval, float jitter, float roll)
        {
            float spread = Mathf.Abs(jitter);
            float delay = baseInterval + Mathf.Lerp(-spread, spread, Mathf.Clamp01(roll));
            return Mathf.Max(MinimumDelay, delay);
        }
    }
}
