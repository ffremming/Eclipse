using UnityEngine;

namespace SpaceGame.Chain
{
    /// <summary>
    /// Where the hand is trying to take the orb this step, and how hard. A default value is "no
    /// pull": the orb is free and the chain falls under its own weight.
    /// </summary>
    public readonly struct TipPull
    {
        /// <summary>The point the orb is drawn towards.</summary>
        public readonly Vector3 Target;

        /// <summary>How much of the gap to <see cref="Target"/> is closed this step, 0 to 1.</summary>
        public readonly float Fraction;

        public TipPull(Vector3 target, float fraction)
        {
            Target = target;
            Fraction = Mathf.Clamp01(fraction);
        }
    }
}
