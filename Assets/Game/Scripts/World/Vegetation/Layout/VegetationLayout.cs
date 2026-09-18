using System;
using System.Collections.Generic;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// Turns one authored layer into the plants it wants. Where each one stands comes from the
    /// layout the mode names; how big it is, which way it faces, how far it leans and how deep it
    /// sits are drawn here from the item's own ranges.
    /// <para>
    /// The generator is seeded off the layer's seed and drawn from in a fixed order, so the same
    /// settings always produce the same field. Nothing may read it conditionally, or changing one
    /// item's range would shift every plant after it.
    /// </para>
    /// </summary>
    public static class VegetationLayout
    {
        public static IReadOnlyList<VegetationPlacement> Build(VegetationLayerSettings settings)
        {
            VegetationItemSettings[] items = settings.Items;
            if (items == null || items.Length == 0) return Array.Empty<VegetationPlacement>();

            float[] weights = Weights(items);
            IReadOnlyList<LayoutPoint> points = Points(settings, weights);

            // Its own generator, not the layout's: a second one seeded off the same number is
            // reproducible just the same, and the layouts keep their draw order to themselves.
            Random random = new Random(unchecked(settings.Seed * 31 + 17));
            List<VegetationPlacement> placed = new List<VegetationPlacement>(points.Count);

            foreach (LayoutPoint point in points)
            {
                if (point.ItemIndex < 0 || point.ItemIndex >= items.Length) continue;
                VegetationItemSettings item = items[point.ItemIndex];

                // Drawn unconditionally and always in this order, so one item's range cannot shift
                // the plants that follow it.
                float scale = item.Scale.At(Next(random));
                float yaw = item.Yaw.At(Next(random));
                float tilt = item.Tilt.At(Next(random));
                float tiltYaw = Next(random) * 360f;
                float sink = item.Sink.At(Next(random));

                placed.Add(new VegetationPlacement(point.X, point.Z, point.ItemIndex,
                                                   yaw, tilt, tiltYaw, scale, sink));
            }

            return placed;
        }

        private static IReadOnlyList<LayoutPoint> Points(VegetationLayerSettings settings, float[] weights)
        {
            switch (settings.Mode)
            {
                case VegetationDistribution.Patch: return PatchLayout.Build(settings, weights);
                case VegetationDistribution.Cluster: return ClusterLayout.Build(settings, weights);
                default: return ScatterLayout.Build(settings, weights);
            }
        }

        /// <summary>A negative weight is read as zero, so a typo hides one item rather than skewing all of them.</summary>
        private static float[] Weights(VegetationItemSettings[] items)
        {
            float[] weights = new float[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                weights[index] = items[index].Weight > 0f ? items[index].Weight : 0f;
            }

            return weights;
        }

        private static float Next(Random random) => (float)random.NextDouble();
    }
}
