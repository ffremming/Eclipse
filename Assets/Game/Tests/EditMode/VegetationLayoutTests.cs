using System.Collections.Generic;
using NUnit.Framework;
using SpaceGame.Vegetation;

namespace SpaceGame.Tests
{
    /// <summary>
    /// What would ruin a field if it were wrong: plants fusing into a wall, a field that reshuffles
    /// itself on every bake, and a coverage number that does not mean what it says.
    /// </summary>
    public sealed class VegetationLayoutTests
    {
        [Test]
        public void ScatterKeepsPlantsApart()
        {
            VegetationLayerSettings settings = Trees();

            IReadOnlyList<VegetationPlacement> placed = VegetationLayout.Build(settings);

            Assert.That(placed.Count, Is.GreaterThan(0));
            for (int a = 0; a < placed.Count; a++)
            {
                for (int b = a + 1; b < placed.Count; b++)
                {
                    float dx = placed[a].X - placed[b].X;
                    float dz = placed[a].Z - placed[b].Z;
                    Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(settings.MinDistance * settings.MinDistance),
                                "two plants stood closer than the layer's spacing");
                }
            }
        }

        [Test]
        public void SameSeedGrowsTheSameField()
        {
            IReadOnlyList<VegetationPlacement> first = VegetationLayout.Build(Trees());
            IReadOnlyList<VegetationPlacement> again = VegetationLayout.Build(Trees());

            VegetationLayerSettings other = Trees();
            other.Seed += 1;
            IReadOnlyList<VegetationPlacement> different = VegetationLayout.Build(other);

            Assert.That(again.Count, Is.EqualTo(first.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(again[index].X, Is.EqualTo(first[index].X));
                Assert.That(again[index].Z, Is.EqualTo(first[index].Z));
                Assert.That(again[index].Scale, Is.EqualTo(first[index].Scale));
            }

            Assert.That(different[0].X, Is.Not.EqualTo(first[0].X));
        }

        [Test]
        public void PatchCoverageLandsWhereItWasAsked()
        {
            VegetationLayerSettings sparse = Undergrowth(0.2f);
            VegetationLayerSettings thick = Undergrowth(0.8f);

            float ratio = VegetationLayout.Build(thick).Count / (float)VegetationLayout.Build(sparse).Count;

            Assert.That(ratio, Is.EqualTo(4f).Within(0.35f),
                        "four times the coverage should grow roughly four times the plants");
        }

        [Test]
        public void ClustersGrowStandsRatherThanAnOrchard()
        {
            VegetationLayerSettings settings = Wood();

            IReadOnlyList<VegetationPlacement> placed = VegetationLayout.Build(settings);

            Assert.That(placed.Count, Is.GreaterThan(settings.ClusterCount));
            Assert.That(placed.Count, Is.LessThanOrEqualTo(settings.ClusterCount * settings.PerCluster));

            // Nothing hands back the centres, so the stands are recovered from the plants: anything
            // within a stand's width of another plant belongs to the same stand.
            List<List<VegetationPlacement>> stands = Stands(placed, settings.ClusterRadius);
            Assert.That(stands.Count, Is.LessThanOrEqualTo(settings.ClusterCount),
                        "plants spread into more stands than the layer asked for");
            foreach (List<VegetationPlacement> stand in stands)
            {
                Assert.That(stand.Count, Is.GreaterThan(1), "a stand of one is a scatter, not a wood");
            }
        }

        /// <summary>Plants grouped by being within <paramref name="reach"/> of another in the group.</summary>
        private static List<List<VegetationPlacement>> Stands(IReadOnlyList<VegetationPlacement> placed, float reach)
        {
            List<List<VegetationPlacement>> stands = new List<List<VegetationPlacement>>();
            bool[] taken = new bool[placed.Count];

            for (int index = 0; index < placed.Count; index++)
            {
                if (taken[index]) continue;

                List<VegetationPlacement> stand = new List<VegetationPlacement> { placed[index] };
                taken[index] = true;

                for (int scan = 0; scan < stand.Count; scan++)
                {
                    for (int other = 0; other < placed.Count; other++)
                    {
                        if (taken[other]) continue;

                        float dx = stand[scan].X - placed[other].X;
                        float dz = stand[scan].Z - placed[other].Z;
                        if (dx * dx + dz * dz > reach * reach) continue;

                        taken[other] = true;
                        stand.Add(placed[other]);
                    }
                }

                stands.Add(stand);
            }

            return stands;
        }

        private static VegetationLayerSettings Wood() => new VegetationLayerSettings
        {
            Mode = VegetationDistribution.Cluster,
            SizeX = 400f,
            SizeZ = 400f,
            MinDistance = 4.5f,
            ClusterCount = 12,
            ClusterRadius = 24f,
            PerCluster = 25,
            Seed = 20260918,
            Items = new[] { VegetationItemSettings.Default },
        };

        private static VegetationLayerSettings Trees() => new VegetationLayerSettings
        {
            Mode = VegetationDistribution.Scatter,
            SizeX = 120f,
            SizeZ = 120f,
            Count = 120,
            MinDistance = 8f,
            Seed = 20260918,
            Items = new[] { VegetationItemSettings.Default },
        };

        private static VegetationLayerSettings Undergrowth(float coverage) => new VegetationLayerSettings
        {
            Mode = VegetationDistribution.Patch,
            SizeX = 60f,
            SizeZ = 60f,
            PatchSize = 6f,
            Coverage = coverage,
            Spacing = 1f,
            PositionJitter = 0.4f,
            NoiseOctaves = 3,
            NoisePersistence = 0.45f,
            Seed = 20260918,
            Items = new[] { VegetationItemSettings.Default },
        };
    }
}
