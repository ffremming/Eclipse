using UnityEngine;

namespace SpaceGame.Castle
{
    /// <summary>
    /// How far through the lighting of a beacon the world is: 0 before it is struck, 1 once the
    /// light has fully arrived.
    /// <para>
    /// A plain class the <see cref="Lightfall"/> component ticks, because the shape of this curve
    /// IS the moment. Lighting the lighthouse is the biggest event the game has, and the difference
    /// between it landing and it looking like a bug is entirely in the timing — a snap to daylight
    /// reads as a rendering glitch, and a slow fade with no beat at the start reads as nothing
    /// happening at all. That is a decision worth being able to test rather than one to bury in an
    /// <c>Update</c>.
    /// </para>
    /// <para>
    /// Two phases, deliberately. A short <see cref="HoldSeconds"/> where the world has been struck
    /// but has not answered yet, then the rise. The hold is what makes the rise feel caused by the
    /// blow rather than coincident with it — and it is the only acknowledgement the strike gets,
    /// since this project has no audio to put a sound on it.
    /// </para>
    /// </summary>
    public sealed class LightfallProgress
    {
        private readonly float holdSeconds;
        private readonly float riseSeconds;

        private float elapsed;

        /// <param name="holdSeconds">Beat between the blow landing and the light starting to come.</param>
        /// <param name="riseSeconds">How long the light takes to arrive once it starts.</param>
        public LightfallProgress(float holdSeconds, float riseSeconds)
        {
            this.holdSeconds = Mathf.Max(holdSeconds, 0f);

            // Guarded because Blend divides by it. A zero rise is a snap, which is a legitimate
            // thing to ask for and not a legitimate thing to divide by.
            this.riseSeconds = Mathf.Max(riseSeconds, 0.0001f);
        }

        /// <summary>Whether the beacon has been struck. One-way: a lit world does not go back.</summary>
        public bool Lit { get; private set; }

        /// <summary>Seconds of the beat still to run before the light starts to come.</summary>
        public float HoldSeconds => holdSeconds;

        /// <summary>
        /// How much of the new world is here, eased. 0 until the hold is over, 1 once the rise is
        /// done.
        /// </summary>
        public float Blend
        {
            get
            {
                if (!Lit) return 0f;

                float rising = (elapsed - holdSeconds) / riseSeconds;
                // SmoothStep at both ends: the light eases in so it does not start with a jolt, and
                // eases out so it does not stop dead at full brightness.
                return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rising));
            }
        }

        /// <summary>True once the world has finished arriving and there is nothing left to drive.</summary>
        public bool Finished => Lit && elapsed >= holdSeconds + riseSeconds;

        /// <summary>
        /// Strike the beacon. Ignored if it is already lit, so a player who keeps swinging at a lit
        /// beacon does not restart the dawn.
        /// </summary>
        /// <returns>Whether this call is the one that lit it.</returns>
        public bool Light()
        {
            if (Lit) return false;

            Lit = true;
            elapsed = 0f;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!Lit || deltaTime <= 0f) return;
            elapsed += deltaTime;
        }
    }
}
