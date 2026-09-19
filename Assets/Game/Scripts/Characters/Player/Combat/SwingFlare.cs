// How hard a swing is burning right now, 0 before it starts to 1 at its peak and back.
//
// Split out of LightWeapon for the reason MeleeSwingSequence was split out of PlayerMeleeSwing: it
// is the part with no Light, no TrailRenderer and no frame in it, so the shape of a swing can be
// tested without entering play mode.
//
// It is shared rather than written once for the player's light weapons and once for the enemies'
// dark ones, because there is only one question here — where in its swing is this weapon — and two
// answers would drift apart the first time either was retuned. A light blade flaring on a curve the
// enemy's blade does not follow is a family that no longer looks like a family, in the same way the
// light palette guards against.
//
// The curve peaks early on purpose: a swing is brightest as it commits, not as it finishes.
using UnityEngine;

namespace SpaceGame.Characters
{
    public class SwingFlare
    {
        /// <summary>Idle. Negative rather than 0 so the first frame of a swing is told apart from no swing.</summary>
        private const float NotSwinging = -1f;

        private readonly float duration;
        private readonly AnimationCurve curve;

        private float elapsed = NotSwinging;

        /// <param name="duration">How long one swing takes, in seconds.</param>
        /// <param name="curve">Flare across the swing, sampled over 0..1. Null holds a flat 1.</param>
        public SwingFlare(float duration, AnimationCurve curve)
        {
            this.duration = Mathf.Max(duration, 1e-4f);
            this.curve = curve;
        }

        /// <summary>True between the start of a swing and the end of it.</summary>
        public bool Swinging => elapsed >= 0f;

        /// <summary>0 while idle, 0..1 through a swing. What a blade of light is drawn up to.</summary>
        public float Progress => elapsed < 0f ? 0f : Mathf.Clamp01(elapsed / duration);

        /// <summary>0 idle, up to 1 at the peak of the swing. What the light and the trail ride on.</summary>
        public float Flare { get; private set; }

        /// <summary>
        /// Start a swing, from the beginning. Restarting one already underway is deliberate: a
        /// second press is a second swing, and carrying the first one's progress into it would
        /// hand the new swing a flare it has not earned yet.
        /// </summary>
        public void Begin() => elapsed = 0f;

        /// <summary>Drop a swing in progress, back to idle.</summary>
        public void Cancel()
        {
            elapsed = NotSwinging;
            Flare = 0f;
        }

        /// <summary>
        /// Advance by <paramref name="deltaTime"/> and refresh <see cref="Flare"/>.
        /// Returns true on the frame the swing ends, which is where a caller stops its trail.
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (elapsed < 0f)
            {
                Flare = 0f;
                return false;
            }

            elapsed += deltaTime;

            // The swing is over the frame it runs out of time, and its flare is 0 from then on
            // rather than whatever the end of the curve happens to sit at. A curve authored to
            // finish above zero would otherwise leave the weapon flared for good.
            if (elapsed >= duration)
            {
                Cancel();
                return true;
            }

            Flare = curve != null ? curve.Evaluate(Progress) : 1f;
            return false;
        }
    }
}
