using UnityEngine;

namespace SpaceGame.Presentation.Menu
{
    /// <summary>
    /// Where the menu's spotlight should be looking, given where the cursor is pointing.
    /// <para>
    /// Split out of <see cref="CursorSpotlight"/> so the two decisions it makes — how far the light
    /// is allowed to roam, and how quickly it catches up — can be tested without the Editor. Both
    /// are the kind of thing that is only wrong once you are looking at it, which is exactly when
    /// it is most expensive to find out.
    /// </para>
    /// </summary>
    public static class SpotlightAim
    {
        // Below this the ray is level enough with the floor that its intersection is meaningless:
        // dividing by it produces distances in the millions, and at exactly zero, an infinity.
        private const float MinimumDescent = 1e-4f;

        /// <summary>
        /// The point on the floor the cursor is pointing at, reined in to a circle of
        /// <paramref name="maxRadius"/> around <paramref name="setCentre"/>.
        /// </summary>
        public static Vector3 Resolve(Ray cursorRay, float floorHeight, Vector3 setCentre, float maxRadius)
        {
            var centre = new Vector3(setCentre.x, floorHeight, setCentre.z);
            float descent = cursorRay.direction.y;
            Vector3 offset;

            if (descent < -MinimumDescent)
            {
                offset = cursorRay.GetPoint((floorHeight - cursorRay.origin.y) / descent) - centre;
                offset.y = 0f;
            }
            else
            {
                // The cursor is level with the floor or above it, so there is no intersection at
                // all. Its horizontal heading stands in for one, pushed out to the rim: the light
                // keeps travelling the way the cursor is moving rather than snapping back to the
                // middle of the set as the player sweeps up past the horizon.
                Vector3 heading = cursorRay.direction;
                heading.y = 0f;
                return heading.sqrMagnitude > 0f ? centre + heading.normalized * maxRadius : centre;
            }

            // The clamp is what keeps the light on the set. As the cursor nears the horizon the
            // intersection runs away to hundreds of metres, and an unclamped light would swing off
            // the ground entirely and leave the player looking at a black screen.
            float reach = offset.magnitude;
            if (reach > maxRadius) offset *= maxRadius / reach;

            return centre + offset;
        }

        /// <summary>
        /// Moves <paramref name="current"/> a frame's worth of the way towards
        /// <paramref name="target"/>, reaching roughly 63% of the remaining gap every
        /// <paramref name="timeConstant"/> seconds.
        /// <para>
        /// The exponential is not decoration. A plain <c>Lerp(current, target, k)</c> converges
        /// faster the more often it is called, so the light would trail the cursor further on a
        /// 60Hz machine than on a 144Hz one — the same tuning value producing two different feels.
        /// </para>
        /// </summary>
        public static Vector3 Follow(Vector3 current, Vector3 target, float timeConstant, float deltaTime)
        {
            if (timeConstant <= 0f) return target;

            float caught = 1f - Mathf.Exp(-deltaTime / timeConstant);
            return current + (target - current) * caught;
        }
    }
}
