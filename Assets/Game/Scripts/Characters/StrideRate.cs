// How fast to play the walk cycle so the feet keep up with the ground.
//
// A blend tree picks WHICH clip plays, never how fast; the clip runs at the pace it was authored at
// whatever the body is doing. So the tree's fastest anchor is also the fastest the legs can honestly
// go, and a sprint past it is a run animation sliding along the floor. Above that anchor the state's
// whole playback rate is scaled by however far past it the body is, which is the only field that
// actually changes stride length.
//
// Below the anchor it returns exactly 1, so ordinary walking is untouched — and so is the idle at the
// centre of the tree, which a rate derived from speed would otherwise freeze solid the moment the
// character stood still.
//
// Shared by the player and the enemies because they wear the same controller, and that controller
// multiplies its Move and Crouch states by MoveAnimSpeed. The parameter defaults to zero, so a body
// that never writes it has legs that do not move at all.
using UnityEngine;

namespace SpaceGame.Characters
{
    public static class StrideRate
    {
        /// <summary>
        /// The playback rate for the Move state, given the body's <paramref name="velocity"/> and the
        /// ground speed the tree's fastest clip was authored to cover. Only the horizontal part of
        /// the velocity counts, in whichever space it is given.
        /// </summary>
        public static float For(Vector3 velocity, float clipSpeed)
        {
            if (clipSpeed <= 0.01f) return 1f;

            float planar = new Vector2(velocity.x, velocity.z).magnitude;
            return planar <= clipSpeed ? 1f : planar / clipSpeed;
        }
    }
}
