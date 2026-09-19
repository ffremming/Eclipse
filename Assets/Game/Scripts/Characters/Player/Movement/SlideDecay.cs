// How fast a slide is still going, part-way through.
//
// Its own type rather than a few lines inside FixedUpdate for the reason MeleeSwingSequence and
// DodgeSelector are: it is the part with no Rigidbody and no frame in it, so the curve can be
// pinned by a test without entering play mode.
//
// A slide holds most of its speed early and gives it up late. That ordering is the whole feel of
// the move: decay it linearly and the player feels the brakes come on the instant they press, and
// the slide reads as a punishment rather than as something momentum bought them.
using UnityEngine;

namespace SpaceGame.Characters
{
    public static class SlideDecay
    {
        /// <summary>
        /// Speed <paramref name="elapsed"/> seconds into a slide that launched at
        /// <paramref name="launchSpeed"/> and bottoms out at <paramref name="floorSpeed"/>.
        ///
        /// <para>
        /// Quadratic rather than linear so the early part of the slide barely bleeds off. Clamped
        /// at both ends, so a caller that runs a frame past the duration gets the floor rather
        /// than an extrapolated negative.
        /// </para>
        /// </summary>
        public static float SpeedAt(float elapsed, float launchSpeed, float floorSpeed, float duration)
        {
            if (duration <= 0f) return floorSpeed;

            float t = Mathf.Clamp01(elapsed / duration);
            return Mathf.Lerp(launchSpeed, floorSpeed, t * t);
        }
    }
}
