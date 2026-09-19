using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// The boomerang: thrown out along a curve, it hurts what it passes, turns, and comes home to the
    /// hand.
    /// <para>
    /// The throw is the commitment. While the boomerang is away there is nothing in the hand to
    /// throw, so a throw at nothing costs the whole flight — reach and safety are paid for in the
    /// time the player is unarmed. That is what keeps it a different choice from the blades rather
    /// than a sword that also reaches: it is the answer to something out of reach or behind a gap,
    /// and the wrong answer to something already on top of you.
    /// </para>
    /// <para>
    /// It hits twice. A target is struck on the way out and again on the way back, so standing in
    /// the route is punished and a target that dodges the first pass still has to dodge the second.
    /// </para>
    /// <para>
    /// The route is <see cref="BoomerangPath"/>, a function of the base class's own swing clock, so
    /// this class holds no timer of its own: the boomerang, its light and its trail all run on the
    /// one clock and cannot disagree about where the throw has got to. The prefab lays the
    /// boomerang out with its face along the flying transform's local +Z, which is what lets the
    /// throw lay it flat.
    /// </para>
    /// </summary>
    public class LightBoomerang : LightWeapon
    {
        [Header("Throw")]
        [Tooltip("The boomerang itself: the model, and the light and trail that ride on it. Sits in the " +
                 "hand until thrown and is put back there on the catch. Its local +Z must be the " +
                 "face of the blade, so the throw can lay it flat.")]
        [SerializeField] private Transform flying;

        [Tooltip("How far out it goes before turning, in metres.")]
        [SerializeField] private float range = 9f;

        [Tooltip("How far it swings out to the side on the way, in metres. Bigger reads as a wider " +
                 "loop and sweeps more ground, at the price of a shorter straight line.")]
        [SerializeField] private float bow = 3f;

        [Tooltip("How fast it spins, in degrees per second.")]
        [SerializeField] private float spinSpeed = 1080f;

        [Header("Strike")]
        [Tooltip("Radius it damages within as it travels, in metres.")]
        [SerializeField] private float strikeRadius = 0.6f;

        [Header("Terrain")]
        [Tooltip("What the boomerang flies over rather than through. Ground only: including the " +
                 "holder would lift the catch out of the hand.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("The lowest it flies above the ground, in metres. Over flat ground the throw is " +
                 "higher than this and it changes nothing; it only matters where the ground rises.")]
        [SerializeField] private float groundClearance = 0.8f;

        [Tooltip("How far above the boomerang the ground is looked for from, in metres. Must clear " +
                 "the steepest hill the throw can cross.")]
        [SerializeField] private float groundProbeHeight = 10f;

        private readonly HashSet<Component> struck = new HashSet<Component>();

        private BoomerangPath path;
        private Quaternion flat;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool thrown;
        private bool returning;

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);

            // Where the boomerang sits in the hand, remembered so the catch can put it back. Taken
            // on equip because that is the pose the prefab authored, before anything has moved it.
            restPosition = flying.localPosition;
            restRotation = flying.localRotation;
        }

        /// <summary>
        /// The throw goes where the camera looks, pitch included. The body is not asked: it lags the
        /// camera through every turn, and a throw down the body's heading is a throw at whatever the
        /// player has just stopped looking at.
        /// </summary>
        public override void OnRequestUse(ref UseContext context)
        {
            Camera view = Camera.main;
            if (view != null) context.Aim = view.transform.rotation;
        }

        /// <summary>One boomerang, one flight: a press while it is away throws nothing.</summary>
        protected override bool CanUse() => base.CanUse() && !thrown;

        /// <summary>
        /// A press while the boomerang is out is not shown as a second throw: restarting the flare
        /// and clearing the trail would cut the flight in progress in half.
        /// </summary>
        protected override void Present()
        {
            if (thrown) return;
            base.Present();
        }

        protected override void Use()
        {
            struck.Clear();
            returning = false;
            thrown = true;

            Vector3 aim = UseRequest.HasAim ? UseRequest.AimDirection : HolderForward();
            path = new BoomerangPath(flying.position, aim, range, bow);

            // Turned so its face points up, and spun about the vertical from there. From the third
            // person camera that shows the whole crescent turning, where a boomerang spun on edge
            // would be a thin line for half of every revolution.
            flat = Quaternion.FromToRotation(flying.forward, Vector3.up) * flying.rotation;
        }

        protected override void GetSweep(out Vector3 centre, out float radius)
        {
            centre = flying.position;
            radius = strikeRadius;
        }

        /// <summary>
        /// Runs at the end of the frame, after the animator has posed the holder's hand: the catch
        /// point read here is where the hand really is, not where it was last frame.
        /// </summary>
        protected override void OnFlareChanged(float flare)
        {
            if (!thrown) return;

            if (!Swinging)
            {
                Catch();
                return;
            }

            Fly(SwingProgress);
        }

        private void Fly(float progress)
        {
            Vector3 catchPoint = flying.parent.TransformPoint(restPosition);
            flying.position = AboveGround(path.PointAt(progress, catchPoint));

            float spin = progress * SwingDuration * spinSpeed;
            flying.rotation = Quaternion.AngleAxis(spin, Vector3.up) * flat;

            // A second chance at everything once it turns, or the way home would only ever hit what
            // the way out had missed.
            bool nowReturning = BoomerangPath.IsReturning(progress);
            if (nowReturning && !returning) struck.Clear();
            returning = nowReturning;

            SweepForHits(struck);
        }

        private void Catch()
        {
            thrown = false;
            flying.localPosition = restPosition;
            flying.localRotation = restRotation;
        }

        /// <summary>Lifts a point clear of the ground beneath it, and leaves it alone where it is already clear.</summary>
        private Vector3 AboveGround(Vector3 point)
        {
            Vector3 probe = point + Vector3.up * groundProbeHeight;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, groundProbeHeight * 2f,
                                 groundMask, QueryTriggerInteraction.Ignore))
            {
                return point;
            }

            point.y = Mathf.Max(point.y, ground.point.y + groundClearance);
            return point;
        }

        /// <summary>The way the holder faces, level. Only for a use that reported no aim.</summary>
        private Vector3 HolderForward()
        {
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        }
    }
}
