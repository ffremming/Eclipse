using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceGame.Items
{
    /// <summary>
    /// Draws a chain along a simulated polyline: one link mesh, laid end to end by arc length, each
    /// one turned a quarter-turn from the last so they read as interlocked.
    /// <para>
    /// The links are placed by distance along the rope and not one per simulation node. The rope
    /// changes length as a lash pays it out, and the nodes spread with it, so a link per node would
    /// stretch the chain into a string of beads. Placing by length means a longer chain has more
    /// links, not bigger ones.
    /// </para>
    /// <para>
    /// Drawn with <see cref="Graphics.RenderMesh"/> rather than a pool of GameObjects: the links have
    /// no behaviour, so there is nothing for a GameObject to hold, and nothing to spawn, hide or clean
    /// up when the weapon is put away.
    /// </para>
    /// </summary>
    public class ChainLinkRenderer : MonoBehaviour
    {
        [SerializeField] private Material linkMaterial;

        [Header("Link")]
        [Tooltip("Outer length of one link, in metres.")]
        [SerializeField] private float linkLength = 0.075f;

        [Tooltip("Outer width of one link, in metres.")]
        [SerializeField] private float linkWidth = 0.05f;

        [Tooltip("Radius of the wire a link is bent from, in metres.")]
        [SerializeField] private float wireRadius = 0.01f;

        [Tooltip("Distance between the centres of two neighbouring links. Shorter than the link " +
                 "itself, because interlocked links overlap.")]
        [SerializeField] private float linkPitch = 0.055f;

        [Tooltip("The most links ever drawn. Set it to the full length over the pitch, or the end " +
                 "of a fully extended chain is simply not drawn.")]
        [SerializeField] private int maxLinks = 96;

        private Mesh linkMesh;
        private RenderParams renderParams;

        private void Awake()
        {
            linkMesh = ChainLinkMesh.Build(linkLength, linkWidth, wireRadius);
            renderParams = new RenderParams(linkMaterial)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                layer = gameObject.layer
            };
        }

        private void OnDestroy()
        {
            if (linkMesh != null) Destroy(linkMesh);
        }

        /// <summary>Draw the chain along <paramref name="points"/>, from the handle to the orb.</summary>
        public void Draw(IReadOnlyList<Vector3> points)
        {
            int drawn = 0;

            // Distance from the start of the current segment to where the next link goes. Carried
            // across segments, so the pitch stays even over the joints between them.
            float next = 0f;

            for (int i = 0; i < points.Count - 1 && drawn < maxLinks; i++)
            {
                Vector3 segment = points[i + 1] - points[i];
                float segmentLength = segment.magnitude;
                if (segmentLength < 1e-5f) continue;

                Vector3 direction = segment / segmentLength;
                Quaternion facing = Facing(direction);

                while (next < segmentLength && drawn < maxLinks)
                {
                    Quaternion roll = Quaternion.AngleAxis(90f * (drawn % 2), Vector3.forward);
                    Matrix4x4 pose = Matrix4x4.TRS(points[i] + direction * next, facing * roll, Vector3.one);
                    Graphics.RenderMesh(renderParams, linkMesh, 0, pose);
                    drawn++;
                    next += linkPitch;
                }

                next -= segmentLength;
            }
        }

        /// <summary>
        /// A link along <paramref name="direction"/>. Up is world up unless the chain is hanging
        /// straight down, where it has no meaning and a sideways axis is used instead.
        /// </summary>
        private static Quaternion Facing(Vector3 direction)
        {
            Vector3 up = Mathf.Abs(direction.y) > 0.95f ? Vector3.right : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }
    }
}
