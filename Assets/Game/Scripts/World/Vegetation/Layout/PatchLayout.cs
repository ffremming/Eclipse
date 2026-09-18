using System;
using System.Collections.Generic;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// Places undergrowth wherever a fractal noise field is above a cutoff, which grows dense
    /// patches with winding bare lanes between them.
    /// <para>
    /// Plants sit on a jittered lattice at <see cref="VegetationLayerSettings.Spacing"/> across the
    /// whole field, and each one is kept or dropped by the noise value under it.
    /// <see cref="VegetationLayerSettings.PatchSize"/> sets how big the blobs and the lanes come out;
    /// the octaves fray their edges so nothing reads as a circle. The cutoff is not a fixed number
    /// but the <see cref="VegetationLayerSettings.Coverage"/> quantile of the noise values actually
    /// sampled, so coverage lands where it was asked to whatever the noise happens to do.
    /// </para>
    /// </summary>
    public static class PatchLayout
    {
        public static IReadOnlyList<LayoutPoint> Build(VegetationLayerSettings settings, float[] weights)
        {
            List<Candidate> candidates = Candidates(settings);
            float cutoff = Cutoff(candidates, settings.Coverage);

            Random random = new Random(settings.Seed);
            List<LayoutPoint> plants = new List<LayoutPoint>();

            foreach (Candidate candidate in candidates)
            {
                // Every candidate draws its roll, kept or not, so a coverage tweak leaves the
                // surviving plants where they were instead of reshuffling the whole field.
                int item = WeightedIndex.Pick(weights, (float)random.NextDouble());
                if (candidate.Density < cutoff) continue;

                plants.Add(new LayoutPoint(candidate.X, candidate.Z, item));
            }

            return plants;
        }

        /// <summary>Every lattice point inside the field and outside the clearing, with its noise value.</summary>
        private static List<Candidate> Candidates(VegetationLayerSettings settings)
        {
            Random jitterSource = new Random(settings.Seed ^ 0x5f3759df);
            float spacing = Math.Max(0.01f, settings.Spacing);
            float jitter = spacing * settings.PositionJitter;

            float sizeZ = settings.SizeZ > 0f ? settings.SizeZ : settings.SizeX;
            float halfX = settings.SizeX * 0.5f;
            float halfZ = sizeZ * 0.5f;
            int stepsX = Math.Max(1, (int)(settings.SizeX / spacing));
            int stepsZ = Math.Max(1, (int)(sizeZ / spacing));
            float startX = -0.5f * stepsX * spacing;
            float startZ = -0.5f * stepsZ * spacing;
            float frequency = 1f / Math.Max(0.01f, settings.PatchSize);

            List<Candidate> candidates = new List<Candidate>();
            for (int stepX = 0; stepX <= stepsX; stepX++)
            {
                for (int stepZ = 0; stepZ <= stepsZ; stepZ++)
                {
                    float x = startX + stepX * spacing + Range(jitterSource, -jitter, jitter);
                    float z = startZ + stepZ * spacing + Range(jitterSource, -jitter, jitter);

                    if (Math.Abs(x) > halfX || Math.Abs(z) > halfZ) continue;
                    if (x * x + z * z <= settings.ClearingRadius * settings.ClearingRadius) continue;

                    float density = ValueNoise.Fractal(x * frequency, z * frequency, settings.Seed,
                                                       settings.NoiseOctaves, settings.NoisePersistence);
                    candidates.Add(new Candidate(x, z, density));
                }
            }

            return candidates;
        }

        /// <summary>The noise value that leaves <paramref name="coverage"/> of the candidates above it.</summary>
        private static float Cutoff(List<Candidate> candidates, float coverage)
        {
            if (candidates.Count == 0) return 1f;
            if (coverage >= 1f) return float.MinValue;
            if (coverage <= 0f) return float.MaxValue;

            float[] densities = new float[candidates.Count];
            for (int index = 0; index < candidates.Count; index++) densities[index] = candidates[index].Density;
            Array.Sort(densities);

            int rank = (int)((1f - coverage) * densities.Length);
            return densities[Math.Min(rank, densities.Length - 1)];
        }

        private static float Range(Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        private readonly struct Candidate
        {
            public Candidate(float x, float z, float density)
            {
                X = x;
                Z = z;
                Density = density;
            }

            public float X { get; }
            public float Z { get; }
            public float Density { get; }
        }
    }
}
