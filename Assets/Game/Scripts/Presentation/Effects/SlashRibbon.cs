// The geometry of a slash of light: a ribbon stretched between two points that move.
//
// A TrailRenderer follows ONE point, so it can only ever draw a line. A blade sweeping through the
// air needs a surface — the whole length of it, hilt to tip, at every moment — and that is what
// this records: a pair of points per frame, joined into a ribbon that the shader paints.
//
// The plain class is the part with no frame and no renderer in it. It owns the rules that decide
// whether the ribbon looks right — where it breaks, how it thins, how it is smoothed — so they can
// be tested without entering play mode. SwingTrail is the MonoBehaviour that feeds it.
//
// UVs are the contract with the LightSlash shader: u runs 0 at the oldest point of the sweep to 1
// at the leading edge, v runs 0 at the base of the blade to 1 at its tip.
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Presentation
{
    public class SlashRibbon
    {
        private struct Sample
        {
            public Vector3 Base;
            public Vector3 Tip;
            public float Time;
            public bool JoinsPrevious;
        }

        private readonly List<Sample> samples = new List<Sample>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();

        /// <summary>True while anything is left to draw or to age out.</summary>
        public bool HasSamples => samples.Count > 0;

        /// <summary>
        /// Record where the blade is at <paramref name="time"/>.
        /// <para>
        /// <paramref name="joinsPrevious"/> is false for the first point after the blade stopped
        /// sweeping. Without it the ribbon would bridge the gap between two separate swings with a
        /// long streak from where the last one ended to where the next one began.
        /// </para>
        /// </summary>
        public void Add(Vector3 bladeBase, Vector3 bladeTip, float time, bool joinsPrevious)
        {
            samples.Add(new Sample
            {
                Base = bladeBase,
                Tip = bladeTip,
                Time = time,
                JoinsPrevious = joinsPrevious && samples.Count > 0,
            });
        }

        /// <summary>Drop everything older than <paramref name="lifetime"/> seconds.</summary>
        public void Prune(float now, float lifetime)
        {
            int expired = 0;
            while (expired < samples.Count && now - samples[expired].Time > lifetime) expired++;
            if (expired == 0) return;

            samples.RemoveRange(0, expired);

            // What it used to join is gone, so it is the start of a strip now.
            if (samples.Count > 0)
            {
                Sample first = samples[0];
                first.JoinsPrevious = false;
                samples[0] = first;
            }
        }

        public void Clear()
        {
            samples.Clear();
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();
        }

        /// <summary>
        /// Turn the recorded points into a mesh.
        /// <para>
        /// The ribbon narrows towards its oldest end by <c>u ^ taperPower</c>, so the trail ends in
        /// a point rather than a cut. Between recorded points it follows a Catmull-Rom curve
        /// rather than straight lines: at 60 frames a second a fast swing moves a hand-span per
        /// frame, and joined with straight segments the arc would read as a polygon.
        /// </para>
        /// </summary>
        public void WriteTo(Mesh mesh, float now, float lifetime, int subdivisions, float taperPower)
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();

            int stripStart = 0;
            for (int i = 1; i <= samples.Count; i++)
            {
                bool stripEnds = i == samples.Count || !samples[i].JoinsPrevious;
                if (!stripEnds) continue;

                if (i - stripStart >= 2) AppendStrip(stripStart, i - 1, now, lifetime, subdivisions, taperPower);
                stripStart = i;
            }

            mesh.Clear();
            if (vertices.Count == 0) return;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, false);
            mesh.RecalculateBounds();
        }

        private void AppendStrip(int first, int last, float now, float lifetime, int subdivisions, float taperPower)
        {
            int steps = Mathf.Max(1, subdivisions);
            int firstVertex = vertices.Count;

            for (int s = first; s < last; s++)
            {
                for (int k = 0; k < steps; k++)
                    AppendPoint(s, k / (float)steps, first, last, now, lifetime, taperPower);
            }

            AppendPoint(last - 1, 1f, first, last, now, lifetime, taperPower);

            int points = (vertices.Count - firstVertex) / 2;
            for (int p = 0; p < points - 1; p++)
            {
                int a = firstVertex + p * 2;
                triangles.Add(a);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 3);
            }
        }

        private void AppendPoint(int segment, float t, int first, int last, float now, float lifetime, float taperPower)
        {
            Sample p0 = samples[Mathf.Max(segment - 1, first)];
            Sample p1 = samples[segment];
            Sample p2 = samples[segment + 1];
            Sample p3 = samples[Mathf.Min(segment + 2, last)];

            Vector3 bladeBase = CatmullRom(p0.Base, p1.Base, p2.Base, p3.Base, t);
            Vector3 bladeTip = CatmullRom(p0.Tip, p1.Tip, p2.Tip, p3.Tip, t);
            float time = Mathf.Lerp(p1.Time, p2.Time, t);

            // 0 at the oldest point still alive, 1 at a point recorded this instant.
            float u = 1f - Mathf.Clamp01((now - time) / Mathf.Max(lifetime, 1e-4f));
            float width = Mathf.Pow(u, taperPower);

            Vector3 centre = (bladeBase + bladeTip) * 0.5f;
            Vector3 half = (bladeTip - bladeBase) * 0.5f * width;

            vertices.Add(centre - half);
            vertices.Add(centre + half);
            uvs.Add(new Vector2(u, 0f));
            uvs.Add(new Vector2(u, 1f));
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1
                           + (p2 - p0) * t
                           + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                           + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
