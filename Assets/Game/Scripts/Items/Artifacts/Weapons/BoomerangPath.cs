using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// The route of one boomerang throw: out along one side of an ellipse, round the far end, and
    /// home along the other side into wherever the hand has got to.
    /// <para>
    /// A function of progress rather than a simulation, and that is the point. Nothing is
    /// integrated, so the boomerang cannot drift, cannot be late, and cannot miss the hand: at
    /// progress 1 it is at the catch point by construction, however far the player has run since
    /// the throw. The throw's own clock already exists — <c>LightWeapon</c> runs one for the flare
    /// and the trail — and reading progress from it means the flight, the light and the trail
    /// cannot fall out of step with one another.
    /// </para>
    /// <para>
    /// The catch point is an argument to every call, not part of the throw, for the same reason.
    /// The far end of the route is fixed where the throw put it; the way home bends toward a hand
    /// that is still moving.
    /// </para>
    /// </summary>
    public readonly struct BoomerangPath
    {
        /// <summary>
        /// Progress at which the boomerang turns for home. Exposed so a weapon can let a target be
        /// hit once on the way out and once on the way back, without knowing the shape of the route.
        /// </summary>
        public const float Turnaround = 0.5f;

        private readonly Vector3 origin;
        private readonly Vector3 forward;
        private readonly Vector3 side;
        private readonly float range;
        private readonly float bow;

        /// <param name="origin">Where the boomerang leaves the hand.</param>
        /// <param name="forward">The way it is thrown, pitch included. Only straight up or down has no side to bow to.</param>
        /// <param name="range">How far out it goes at the turnaround, in metres.</param>
        /// <param name="bow">How far it swings out to the side on the way, in metres.</param>
        public BoomerangPath(Vector3 origin, Vector3 forward, float range, float bow)
        {
            this.origin = origin;
            this.forward = forward.normalized;
            // Normalised because a pitched throw shortens the cross product by the cosine of the pitch,
            // and the bow is a distance in metres, not a distance scaled by where the player looked.
            side = Vector3.Cross(Vector3.up, this.forward).normalized;
            this.range = range;
            this.bow = bow;
        }

        /// <summary>True from the turnaround to the catch.</summary>
        public static bool IsReturning(float progress) => progress >= Turnaround;

        /// <summary>
        /// Where the boomerang is at <paramref name="progress"/> (0 leaving the hand, 1 in it),
        /// given where the hand is <paramref name="catchPoint"/>.
        /// </summary>
        public Vector3 PointAt(float progress, Vector3 catchPoint)
        {
            float turn = Mathf.Clamp01(progress) * 2f * Mathf.PI;

            // Both the range and the bow are measured from a spine that slides from the throw to the
            // catch point. With a stationary hand it is a fixed line; with a moving one it bends
            // the way home toward the hand instead of arriving where the hand used to be.
            Vector3 spine = Vector3.Lerp(origin, catchPoint, Mathf.Clamp01(progress));

            float outward = range * 0.5f * (1f - Mathf.Cos(turn));
            float across = bow * Mathf.Sin(turn);

            return spine + forward * outward + side * across;
        }
    }
}
