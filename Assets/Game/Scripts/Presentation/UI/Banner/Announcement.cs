using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// How loud a line of on-screen text is right now: silent, then up, held, and away again.
    /// <para>
    /// A plain class the banner ticks, because the curve is the whole of the decision. Eclipse has
    /// no message line and does not want one — the shape here is what keeps a single sentence from
    /// becoming a HUD: it arrives, it is unmissable for a moment, and then it is gone and the
    /// screen is the world again.
    /// </para>
    /// <para>
    /// Re-announcing while a line is still up restarts it rather than queueing, which is the right
    /// answer for what this shows: the newest thing the player picked up is the thing they want
    /// named, and a queue would hold the screen for as long as it took to drain.
    /// </para>
    /// </summary>
    public sealed class Announcement
    {
        private readonly float riseSeconds;
        private readonly float holdSeconds;
        private readonly float fallSeconds;

        private float elapsed;

        /// <param name="riseSeconds">Seconds the line takes to come up.</param>
        /// <param name="holdSeconds">Seconds it stays at full strength.</param>
        /// <param name="fallSeconds">Seconds it takes to go away.</param>
        public Announcement(float riseSeconds, float holdSeconds, float fallSeconds)
        {
            // Guarded rather than trusted: rise and fall are divided by, and a zero there is a snap,
            // which is a legitimate thing to ask for and not a legitimate thing to divide by.
            this.riseSeconds = Mathf.Max(riseSeconds, 0.0001f);
            this.holdSeconds = Mathf.Max(holdSeconds, 0f);
            this.fallSeconds = Mathf.Max(fallSeconds, 0.0001f);

            // Starts finished. Nothing is on screen until something is announced.
            elapsed = Length;
        }

        /// <summary>Seconds one announcement lasts from start to gone.</summary>
        public float Length => riseSeconds + holdSeconds + fallSeconds;

        /// <summary>How strongly the line is drawn, 0 to 1.</summary>
        public float Strength
        {
            get
            {
                if (elapsed < riseSeconds)
                    return Mathf.SmoothStep(0f, 1f, elapsed / riseSeconds);

                if (elapsed < riseSeconds + holdSeconds) return 1f;

                float falling = (elapsed - riseSeconds - holdSeconds) / fallSeconds;
                return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(falling));
            }
        }

        /// <summary>Whether there is anything on screen at all.</summary>
        public bool IsShowing => elapsed < Length;

        /// <summary>Start the line, from the beginning, whether or not one is already up.</summary>
        public void Show() => elapsed = 0f;

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || !IsShowing) return;

            // Clamped rather than left to run on, so a banner left alone for an hour does not need
            // a float with an hour of seconds in it to answer what it is drawing.
            elapsed = Mathf.Min(elapsed + deltaTime, Length);
        }
    }
}
