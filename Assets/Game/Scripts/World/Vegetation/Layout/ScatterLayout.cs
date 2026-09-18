using System;
using System.Collections.Generic;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// Places a handful of large plants — trees, stumps, rock formations — by throwing darts and
    /// rejecting any that land too close to one already placed.
    /// <para>
    /// The gap is what does the work: it is what stops two trees fusing into one wall and what
    /// keeps a corner of the field from coming out bare. <see cref="VegetationLayerSettings.Count"/>
    /// is only a ceiling, so a field that cannot hold the asked-for number comes out sparser rather
    /// than clumped.
    /// </para>
    /// </summary>
    public static class ScatterLayout
    {
        /// <summary>Darts thrown per plant before giving up on it. Higher packs tighter, slower.</summary>
        private const int AttemptsPerPlant = 40;

        public static IReadOnlyList<LayoutPoint> Build(VegetationLayerSettings settings, float[] weights)
        {
            Random random = new Random(settings.Seed);
            List<LayoutPoint> placed = new List<LayoutPoint>();

            // Sized to the gap being enforced, which is the largest radius this loop ever asks
            // about, so a spacing test sweeps nine cells instead of scanning everything placed.
            SpatialHash spacing = new SpatialHash(Math.Max(settings.MinDistance, 0.0001f));

            float halfX = settings.SizeX * 0.5f;
            float halfZ = (settings.SizeZ > 0f ? settings.SizeZ : settings.SizeX) * 0.5f;
            float clearing = settings.ClearingRadius;

            for (int index = 0; index < settings.Count; index++)
            {
                for (int attempt = 0; attempt < AttemptsPerPlant; attempt++)
                {
                    float x = Range(random, -halfX, halfX);
                    float z = Range(random, -halfZ, halfZ);

                    if (x * x + z * z <= clearing * clearing) continue;
                    if (spacing.AnyWithin(x, z, settings.MinDistance)) continue;

                    spacing.Add(x, z);
                    placed.Add(new LayoutPoint(x, z, WeightedIndex.Pick(weights, (float)random.NextDouble())));
                    break;
                }
            }

            return placed;
        }

        private static float Range(Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
