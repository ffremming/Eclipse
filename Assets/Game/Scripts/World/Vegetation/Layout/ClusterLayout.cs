using System;
using System.Collections.Generic;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// Places plants in tight stands with open ground between them: a handful of centres spread over
    /// the field, and a crowd of plants around each one.
    /// <para>
    /// This is what <see cref="ScatterLayout"/> cannot do. An even spread with a guaranteed gap gives
    /// an orchard — every tree the same distance from its neighbour, no wood and no clearing. A wood
    /// is the opposite shape: dense where it stands, empty where it does not, and it is the empty
    /// ground between the stands that makes the stands read as landmarks.
    /// </para>
    /// <para>
    /// Inside a stand the plants still keep <see cref="VegetationLayerSettings.MinDistance"/> apart,
    /// so a crowd is not a pile; the centres keep <see cref="VegetationLayerSettings.ClusterRadius"/>
    /// apart, so two stands do not merge into one wall.
    /// </para>
    /// </summary>
    public static class ClusterLayout
    {
        /// <summary>Darts thrown per centre, and per plant inside a stand, before giving up on it.</summary>
        private const int Attempts = 40;

        /// <summary>
        /// How much of the stand's radius pulls plants inward. Below 0.5 the middle is a thicket and
        /// the rim is bare; at 0.5 the disc fills evenly. A wood wants a dense middle.
        /// </summary>
        private const float InwardBias = 0.38f;

        public static IReadOnlyList<LayoutPoint> Build(VegetationLayerSettings settings, float[] weights)
        {
            Random random = new Random(settings.Seed);
            List<LayoutPoint> placed = new List<LayoutPoint>();

            float halfX = settings.SizeX * 0.5f;
            float halfZ = (settings.SizeZ > 0f ? settings.SizeZ : settings.SizeX) * 0.5f;
            float clearing = settings.ClearingRadius;
            float radius = Math.Max(settings.ClusterRadius, 0.01f);

            SpatialHash centres = new SpatialHash(radius);
            SpatialHash spacing = new SpatialHash(Math.Max(settings.MinDistance, 0.0001f));

            for (int cluster = 0; cluster < settings.ClusterCount; cluster++)
            {
                if (!TryCentre(random, halfX, halfZ, clearing, radius, centres, out float centreX, out float centreZ))
                {
                    continue;
                }

                for (int index = 0; index < settings.PerCluster; index++)
                {
                    for (int attempt = 0; attempt < Attempts; attempt++)
                    {
                        float distance = radius * (float)Math.Pow(random.NextDouble(), InwardBias);
                        float angle = (float)(random.NextDouble() * Math.PI * 2.0);
                        float x = centreX + distance * (float)Math.Cos(angle);
                        float z = centreZ + distance * (float)Math.Sin(angle);

                        if (Math.Abs(x) > halfX || Math.Abs(z) > halfZ) continue;
                        if (x * x + z * z <= clearing * clearing) continue;
                        if (spacing.AnyWithin(x, z, settings.MinDistance)) continue;

                        spacing.Add(x, z);
                        placed.Add(new LayoutPoint(x, z, WeightedIndex.Pick(weights, (float)random.NextDouble())));
                        break;
                    }
                }
            }

            return placed;
        }

        /// <summary>
        /// A spot for one stand, at least a radius away from every stand already placed, or false
        /// when the field has no room left for another.
        /// </summary>
        private static bool TryCentre(Random random, float halfX, float halfZ, float clearing,
                                      float radius, SpatialHash centres, out float x, out float z)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                x = Range(random, -halfX, halfX);
                z = Range(random, -halfZ, halfZ);

                if (x * x + z * z <= clearing * clearing) continue;
                if (centres.AnyWithin(x, z, radius)) continue;

                centres.Add(x, z);
                return true;
            }

            x = 0f;
            z = 0f;
            return false;
        }

        private static float Range(Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);
    }
}
