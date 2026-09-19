using System.Collections.Generic;
using UnityEngine;
using SpaceGame.Chain;

namespace SpaceGame.Items
{
    /// <summary>
    /// The chain: a whip with an orb of light on the end of it, simulated link by link.
    /// <para>
    /// The orb is the weapon and the chain is how it gets there. A press does not hit anything; it
    /// starts a lash, and the orb is drawn back, thrown forward while the chain pays out, and reeled
    /// in. The damage is dealt by the orb as it travels, so what the whip can reach is what the orb
    /// actually reaches, and a lash that runs into a wall stops at the wall.
    /// </para>
    /// <para>
    /// The shape of the chain is not authored anywhere. <see cref="ChainRope"/> is a rope under
    /// gravity that cannot stretch, and the hang, the swing, the drag along the ground and the crack
    /// at the end of a lash are what a rope does. This class only steps it, moves the orb to the end
    /// of it and asks the base to hurt what the orb is inside.
    /// </para>
    /// </summary>
    public class LightWhip : LightWeapon
    {
        [Header("Chain")]
        [Tooltip("Where the chain is fixed to the handle. The rope hangs from here.")]
        [SerializeField] private Transform anchor;

        [Tooltip("Moved to the end of the chain every frame. The orb, its light and its trail are " +
                 "children of it, so they ride the chain without knowing about it.")]
        [SerializeField] private Transform tip;

        [SerializeField] private ChainLinkRenderer linkRenderer;
        [SerializeField] private ChainTuning rope = new ChainTuning();
        [SerializeField] private WhipLashTuning lash = new WhipLashTuning();

        [Tooltip("What the chain collides with. The holder is always ignored.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Strike")]
        [Tooltip("Radius the orb damages within as it travels, in metres.")]
        [SerializeField] private float orbRadius = 0.5f;

        [Tooltip("The orb only hurts while it is travelling outwards along the lash at least this " +
                 "fast, in metres per second. A chain lying against someone is not a blow.")]
        [SerializeField] private float minStrikeSpeed = 8f;

        [Header("Simulation")]
        [Tooltip("Fixed step the rope is simulated at, in seconds. Verlet needs it constant.")]
        [SerializeField] private float simulationStep = 1f / 90f;

        [Tooltip("The most steps run in one frame. Past this the chain runs slow rather than " +
                 "spending a long frame catching up, which only makes the next one longer.")]
        [SerializeField] private int maxStepsPerFrame = 6;

        [Tooltip("If the handle moves further than this in one frame it has been teleported, and " +
                 "the chain is laid straight down again rather than being dragged across the map.")]
        [SerializeField] private float teleportDistance = 3f;

        private readonly HashSet<Component> struck = new HashSet<Component>();

        private ChainRope chain;
        private WhipLash whip;
        private PhysicsChainWorld world;
        private Vector3 lastAnchor;
        private float pendingTime;

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);

            chain = new ChainRope(rope, anchor.position, lash.RestLength);
            whip = new WhipLash(lash);
            world = new PhysicsChainWorld(collisionMask, holder.transform);
            lastAnchor = anchor.position;
        }

        protected override bool CanUse() => base.CanUse() && !(whip is { Active: true });

        /// <summary>
        /// A press while the chain is out is not a second lash, so it is not shown as one either:
        /// restarting the flare and clearing the trail would cut the lash in progress in half.
        /// </summary>
        protected override void Present()
        {
            if (whip is { Active: true }) return;
            base.Present();
        }

        protected override void Use()
        {
            struck.Clear();
            whip.Begin(HolderForward(), SwingDuration);
        }

        /// <summary>The orb, wherever the chain has put it.</summary>
        protected override void GetSweep(out Vector3 centre, out float radius)
        {
            centre = chain != null ? chain.Tip : transform.position;
            radius = orbRadius;
        }

        /// <summary>
        /// Runs at the end of the frame, after the animator has posed the holder's hand: the anchor
        /// read here is where the handle really is, not where it was last frame.
        /// </summary>
        protected override void OnFlareChanged(float flare)
        {
            if (chain == null) return;

            AdvanceSimulation(Time.deltaTime);
            tip.position = chain.Tip;
            linkRenderer.Draw(chain.Points);
        }

        private void AdvanceSimulation(float frameTime)
        {
            Vector3 from = lastAnchor;
            Vector3 to = anchor.position;
            lastAnchor = to;

            if ((to - from).sqrMagnitude > teleportDistance * teleportDistance)
            {
                chain.Reset(to);
                from = to;
            }

            pendingTime = Mathf.Min(pendingTime + frameTime, simulationStep * maxStepsPerFrame);
            int steps = Mathf.FloorToInt(pendingTime / simulationStep);
            pendingTime -= steps * simulationStep;

            for (int step = 1; step <= steps; step++)
            {
                // The handle moves through the steps rather than jumping at the first, so a fast turn
                // drags the chain round instead of snapping it.
                chain.Pin(Vector3.Lerp(from, to, (float)step / steps));
                StepChain();
            }

            // Whatever the steps did or did not run, the chain is drawn from the handle as it is now.
            chain.Pin(to);
        }

        private void StepChain()
        {
            whip.Advance(simulationStep);
            chain.Length = whip.PaidOutLength;

            TipPull pull = whip.TipPullFor(owner.transform.position, simulationStep);
            chain.Step(simulationStep, Physics.gravity, pull, world);

            // A blow is the orb travelling outwards fast. Measured along the lash, so the wind-up
            // going backwards, the chain being reeled in and the orb lying against something all
            // fail it, and the crack at the very end of the lash — which is after the hand has
            // stopped pulling — still counts.
            float outwardSpeed = Vector3.Dot(chain.TipVelocity(simulationStep), whip.Direction);
            if (whip.Active && outwardSpeed >= minStrikeSpeed) SweepForHits(struck);
        }

        /// <summary>The way the holder faces, level, so looking at the sky does not lash the sky.</summary>
        private Vector3 HolderForward()
        {
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
        }
    }
}
