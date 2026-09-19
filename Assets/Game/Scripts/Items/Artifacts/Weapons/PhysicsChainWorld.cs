using UnityEngine;
using SpaceGame.Chain;

namespace SpaceGame.Items
{
    /// <summary>
    /// The chain's view of the scene: sphere sweeps against the physics world, blind to the holder.
    /// <para>
    /// The holder is skipped because the chain starts in their hand and hangs against their body.
    /// Letting it collide with them would shove the character around and snag the chain on the
    /// hip it is hooked to.
    /// </para>
    /// </summary>
    public sealed class PhysicsChainWorld : IChainWorld
    {
        private const int MaxHitsPerSweep = 8;

        private readonly LayerMask mask;
        private readonly Transform holder;
        private readonly RaycastHit[] hits = new RaycastHit[MaxHitsPerSweep];

        public PhysicsChainWorld(LayerMask mask, Transform holder)
        {
            this.mask = mask;
            this.holder = holder;
        }

        public bool Sweep(Vector3 from, Vector3 to, float radius, out Vector3 centre, out Vector3 normal)
        {
            centre = to;
            normal = Vector3.up;

            Vector3 move = to - from;
            float distance = move.magnitude;
            if (distance < 1e-5f) return false;

            Vector3 direction = move / distance;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, hits, distance, mask,
                                                   QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];

                // A distance of zero is a sweep that began inside the collider. It has no surface to
                // stop against, so it is let through rather than pinned to a made-up normal.
                if (hit.distance <= 0f || hit.distance >= nearest) continue;
                if (holder != null && hit.collider.transform.IsChildOf(holder)) continue;

                nearest = hit.distance;
                centre = from + direction * hit.distance;
                normal = hit.normal;
            }

            return nearest < float.MaxValue;
        }
    }
}
