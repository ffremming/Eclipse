using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// A swung edge: the sword and the axe both.
    /// <para>
    /// One class rather than two, because a sword and an axe differ only in numbers — reach, arc
    /// width, how long the swing takes, how much it hurts. Two classes with identical bodies and
    /// different serialized defaults would be the same code twice, and the second copy is where the
    /// two quietly stop behaving alike. The difference between them belongs in their prefabs.
    /// </para>
    /// <para>
    /// The sweep is centred ahead of the holder rather than on the blade itself. The blade's
    /// position during a swing is whatever the animation put it at, which is a visual decision the
    /// animator owns and the hit detection must not inherit — a hit that depends on an animation
    /// frame is a hit the player cannot predict.
    /// </para>
    /// </summary>
    public class LightBlade : LightWeapon
    {
        [Header("Reach")]
        [Tooltip("How far ahead of the holder the sweep is centred, in metres.")]
        [SerializeField] private float reach = 1.6f;

        [Tooltip("Radius of the sweep, in metres. Wider is an axe, tighter is a sword.")]
        [SerializeField] private float arcRadius = 1.25f;

        [Tooltip("How high off the holder's feet the sweep sits, in metres.")]
        [SerializeField] private float sweepHeight = 1.1f;

        protected override void GetSweep(out Vector3 centre, out float radius)
        {
            radius = arcRadius;

            if (owner == null)
            {
                centre = transform.position;
                return;
            }

            // Flattened to the horizontal, so looking at the sky does not lift the swing over the
            // head of the thing in front of the player.
            Vector3 forward = owner.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : owner.transform.forward;

            centre = owner.transform.position + forward * reach + Vector3.up * sweepHeight;
        }
    }
}
