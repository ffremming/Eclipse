// The rules that decide whether a swing trail looks like a swing trail.
//
// Quiet failures, all of them: a ribbon that bridges two separate swings draws a streak across the
// room between where one ended and the next began; one that does not taper is a cut-off slab; a
// sweep gate with a single threshold flickers into confetti. None throws, all of them look wrong.
//
// In Editor/ for the same reason MeleeComboTests is: these live in Assembly-CSharp.
using NUnit.Framework;
using SpaceGame.Presentation;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class SlashRibbonTests
    {
        private const float Lifetime = 0.4f;

        private static readonly Vector3 Up = Vector3.up;

        private static void AddBlade(SlashRibbon ribbon, float x, float time, bool joins)
        {
            ribbon.Add(new Vector3(x, 0f, 0f), new Vector3(x, 1f, 0f), time, joins);
        }

        [Test]
        public void ARibbonDoesNotBridgeTheGapBetweenTwoSeparateSwings()
        {
            var ribbon = new SlashRibbon();
            AddBlade(ribbon, 0f, 0.00f, false);
            AddBlade(ribbon, 1f, 0.05f, true);

            // The blade stopped sweeping, then a second swing began somewhere else entirely.
            AddBlade(ribbon, 9f, 0.20f, false);
            AddBlade(ribbon, 10f, 0.25f, true);

            var mesh = new Mesh();
            ribbon.WriteTo(mesh, 0.25f, Lifetime, subdivisions: 1, taperPower: 0f);

            // Two strips of one quad each. A bridged ribbon would be three quads, 18 indices.
            Assert.AreEqual(12, mesh.triangles.Length);
            Assert.AreEqual(8, mesh.vertexCount);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TheRibbonTapersToAPointAtItsOldestEndAndIsFullWidthAtTheHead()
        {
            var ribbon = new SlashRibbon();
            AddBlade(ribbon, 0f, 0.0f, false);
            AddBlade(ribbon, 1f, Lifetime, true);

            var mesh = new Mesh();
            ribbon.WriteTo(mesh, Lifetime, Lifetime, subdivisions: 1, taperPower: 1f);
            Vector3[] v = mesh.vertices;

            // The first sample is exactly one lifetime old, the last was recorded this instant.
            Assert.AreEqual(0f, Vector3.Distance(v[0], v[1]), 1e-4f, "oldest end should be a point");
            Assert.AreEqual(1f, Vector3.Distance(v[2], v[3]), 1e-4f, "head should be the full blade");
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void PruningDropsExpiredPointsAndLeavesTheSurvivorAsAStripStart()
        {
            var ribbon = new SlashRibbon();
            AddBlade(ribbon, 0f, 0.0f, false);
            AddBlade(ribbon, 1f, 0.3f, true);
            AddBlade(ribbon, 2f, 0.6f, true);

            ribbon.Prune(now: 0.65f, lifetime: Lifetime);

            // Only the 0.3 and 0.6 samples survive a 0.4 s lifetime, joined to each other.
            var mesh = new Mesh();
            ribbon.WriteTo(mesh, 0.65f, Lifetime, subdivisions: 1, taperPower: 0f);
            Assert.AreEqual(4, mesh.vertexCount);

            ribbon.Prune(now: 1.5f, lifetime: Lifetime);
            Assert.IsFalse(ribbon.HasSamples);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ASweepStartsFastHoldsSlowerAndDoesNotStartOnTheHoldSpeedAlone()
        {
            var sweep = new SweepDetector(startSpeed: 4f, holdRatio: 0.5f);
            const float dt = 0.1f;

            // 3 m/s: above the hold speed of 2, but below the 4 needed to start.
            Assert.IsFalse(sweep.Update(Vector3.zero, dt));
            Assert.IsFalse(sweep.Update(Up * 0.3f, dt), "the hold speed alone must not start a sweep");

            // 5 m/s starts it, 3 m/s keeps it, 1 m/s ends it.
            Assert.IsTrue(sweep.Update(Up * 0.8f, dt));
            Assert.IsTrue(sweep.Update(Up * 1.1f, dt), "slowing to the hold speed keeps the sweep");
            Assert.IsFalse(sweep.Update(Up * 1.2f, dt));
        }
    }
}
