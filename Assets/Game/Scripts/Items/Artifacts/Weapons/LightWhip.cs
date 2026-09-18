using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// The chain: a whip with an orb of light on the end of it.
    /// <para>
    /// The orb is the weapon. The chain is how it gets there, and the reason the weapon feels
    /// different from the blade — a blade hits where the player is, a whip hits much further out.
    /// The sweep is therefore reported at full extension rather than at the orb's hanging position,
    /// so a press connects with what the player was aiming past, not with their own feet.
    /// </para>
    /// <para>
    /// The chain is simulated, not animated. Each link chases the one before it with a spring, so
    /// the whip lags, overshoots and settles on its own, and a swing that changes direction mid-air
    /// drags the orb through a curve nobody had to author. It is also what makes the trailing light
    /// read as weight rather than as a ribbon stuck to a bone.
    /// </para>
    /// </summary>
    public class LightWhip : LightWeapon
    {
        [Header("Chain")]
        [Tooltip("The links, ordered from the handle outwards. The last one carries the orb.")]
        [SerializeField] private Transform[] links;

        [Tooltip("Spacing between links, in metres.")]
        [SerializeField] private float linkSpacing = 0.22f;

        [Tooltip("How hard a link is pulled towards where it should be. Higher is stiffer and " +
                 "less whip-like.")]
        [SerializeField] private float followStiffness = 26f;

        [Tooltip("How quickly a link's motion dies. Lower keeps the overshoot for longer.")]
        [SerializeField] private float followDamping = 5.5f;

        [Header("Throw")]
        [Tooltip("How far ahead of the holder the orb is thrown, in metres.")]
        [SerializeField] private float throwReach = 4.2f;

        [Tooltip("Radius the orb damages within when it lands, in metres.")]
        [SerializeField] private float orbRadius = 1.0f;

        private Vector3[] velocities;

        public override void OnEquipped(GameObject holder)
        {
            base.OnEquipped(holder);
            velocities = new Vector3[links != null ? links.Length : 0];
        }

        /// <summary>
        /// Where the orb is at the moment it strikes. Read by the base at the press, so it reports
        /// full extension rather than wherever the chain happens to be hanging.
        /// </summary>
        protected override void GetSweep(out Vector3 centre, out float radius)
        {
            radius = orbRadius;

            if (owner == null)
            {
                centre = transform.position;
                return;
            }

            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : owner.transform.forward;

            centre = owner.transform.position + forward * throwReach + Vector3.up * 1.0f;
        }

        private void Update()
        {
            SimulateChain();
        }

        /// <summary>
        /// Each link chases a point one spacing behind the link before it, under a critically-ish
        /// damped spring. Running it in order from the handle outwards means a link is chasing this
        /// frame's position of its parent rather than last frame's, which is what stops the chain
        /// stretching under fast motion.
        /// </summary>
        private void SimulateChain()
        {
            if (links == null || links.Length == 0 || velocities == null) return;

            for (int i = 1; i < links.Length; i++)
            {
                if (links[i] == null || links[i - 1] == null) continue;

                Vector3 anchor = links[i - 1].position;
                Vector3 toLink = links[i].position - anchor;

                // A link sitting exactly on its parent has no direction to be pushed along, so it
                // is given one rather than normalising a zero vector into a NaN that propagates
                // down the rest of the chain and never comes back.
                Vector3 direction = toLink.sqrMagnitude > 1e-6f
                    ? toLink.normalized
                    : -links[i - 1].up;

                Vector3 target = anchor + direction * linkSpacing;
                Vector3 offset = target - links[i].position;

                velocities[i] += offset * (followStiffness * Time.deltaTime);
                velocities[i] -= velocities[i] * Mathf.Min(followDamping * Time.deltaTime, 1f);
                links[i].position += velocities[i] * Time.deltaTime;

                // Point each link back down the chain so the geometry reads as one continuous thing
                // rather than as beads that happen to be near each other.
                links[i].rotation = Quaternion.LookRotation(links[i].position - anchor);
            }
        }
    }
}
