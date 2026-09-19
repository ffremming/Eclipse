// "Is that point in front of me, and close enough?"
//
// One piece of arc math, two callers: an eye deciding whether it can see you, and a sword deciding
// whether it connected. They are the same question — a range and a half-angle about a facing — so
// they are one function rather than the same dot product written twice.
//
// Neither caller gets the whole answer from here. Sight still owes a line-of-sight raycast and a
// swing still owes an overlap query; this is the cheap test they run first, before paying for
// physics.
using UnityEngine;

namespace SpaceGame.Enemies
{
    public static class Cone
    {
        /// <summary>
        /// True when <paramref name="point"/> lies within <paramref name="range"/> of
        /// <paramref name="origin"/> and within <paramref name="halfAngleDegrees"/> of
        /// <paramref name="forward"/>.
        ///
        /// <para>
        /// Measured flat: the Y axis is dropped from both the offset and the facing before the
        /// angle is taken. A creature standing on a crate is still in front of you, and without this
        /// a target directly below or above would fall out of the cone on height alone.
        /// </para>
        /// </summary>
        public static bool Contains(Vector3 origin, Vector3 forward, Vector3 point,
                                    float range, float halfAngleDegrees)
        {
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude > range * range) return false;

            // Standing inside each other: there is no direction to measure, and every answer is as
            // good as any other. True is the useful one — a target this close is unquestionably
            // "right here" for both a swing and an eye.
            Vector3 flatOffset = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (flatOffset.sqrMagnitude < 1e-6f) return true;

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-6f) return false;

            return Vector3.Angle(flatForward, flatOffset) <= halfAngleDegrees;
        }
    }
}
