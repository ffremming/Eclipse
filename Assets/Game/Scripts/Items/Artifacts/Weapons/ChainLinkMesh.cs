using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// One chain link: a tube swept round a stadium (two straight sides, two half-circles), lying
    /// along local Z with its flat face towards local Y.
    /// <para>
    /// Built in code rather than imported because it is the one shape the chain repeats, and its
    /// proportions are what the Inspector tunes. There is nothing to author that a torus with
    /// straight sides is not.
    /// </para>
    /// </summary>
    public static class ChainLinkMesh
    {
        private const int PathSegments = 32;
        private const int TubeSegments = 6;

        /// <param name="length">Outer length along Z, in metres.</param>
        /// <param name="width">Outer width along X, in metres.</param>
        /// <param name="wireRadius">Radius of the wire the link is bent from, in metres.</param>
        public static Mesh Build(float length, float width, float wireRadius)
        {
            float bend = Mathf.Max(1e-3f, (width - 2f * wireRadius) * 0.5f);
            float straight = Mathf.Max(0f, (length - width) * 0.5f);

            float side = 2f * straight;
            float arc = Mathf.PI * bend;
            float perimeter = 2f * (side + arc);

            var vertices = new Vector3[PathSegments * TubeSegments];
            var normals = new Vector3[vertices.Length];

            for (int j = 0; j < PathSegments; j++)
            {
                StadiumPoint(perimeter * j / PathSegments, straight, bend, side, arc,
                             out Vector3 centre, out Vector3 outward);

                for (int k = 0; k < TubeSegments; k++)
                {
                    float phi = 2f * Mathf.PI * k / TubeSegments;
                    Vector3 direction = outward * Mathf.Cos(phi) + Vector3.up * Mathf.Sin(phi);
                    vertices[j * TubeSegments + k] = centre + direction * wireRadius;
                    normals[j * TubeSegments + k] = direction;
                }
            }

            var triangles = new int[PathSegments * TubeSegments * 6];
            int t = 0;
            for (int j = 0; j < PathSegments; j++)
            {
                int nextJ = (j + 1) % PathSegments;
                for (int k = 0; k < TubeSegments; k++)
                {
                    int nextK = (k + 1) % TubeSegments;
                    int a = j * TubeSegments + k;
                    int b = j * TubeSegments + nextK;
                    int c = nextJ * TubeSegments + k;
                    int d = nextJ * TubeSegments + nextK;

                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = c; triangles[t++] = b; triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = "ChainLink", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The point <paramref name="s"/> metres round the stadium, and which way is out of it.</summary>
        private static void StadiumPoint(float s, float straight, float bend, float side, float arc,
                                         out Vector3 point, out Vector3 outward)
        {
            if (s < side)
            {
                point = new Vector3(bend, 0f, -straight + s);
                outward = Vector3.right;
            }
            else if (s < side + arc)
            {
                float angle = (s - side) / bend;
                outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                point = new Vector3(0f, 0f, straight) + outward * bend;
            }
            else if (s < 2f * side + arc)
            {
                point = new Vector3(-bend, 0f, straight - (s - side - arc));
                outward = Vector3.left;
            }
            else
            {
                float angle = Mathf.PI + (s - 2f * side - arc) / bend;
                outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                point = new Vector3(0f, 0f, -straight) + outward * bend;
            }
        }
    }
}
