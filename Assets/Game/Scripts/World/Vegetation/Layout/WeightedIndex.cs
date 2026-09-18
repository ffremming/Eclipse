namespace SpaceGame.Vegetation
{
    /// <summary>Turns a 0..1 roll into an index, in proportion to a set of weights.</summary>
    public static class WeightedIndex
    {
        /// <summary>
        /// The index <paramref name="roll"/> lands on. Null, empty or all-zero weights give 0, so a
        /// caller with a single unweighted variant needs no weights at all.
        /// </summary>
        public static int Pick(float[] weights, float roll)
        {
            if (weights == null || weights.Length == 0) return 0;

            float total = 0f;
            foreach (float weight in weights) total += weight > 0f ? weight : 0f;
            if (total <= 0f) return 0;

            float target = roll * total;
            float running = 0f;
            for (int index = 0; index < weights.Length; index++)
            {
                running += weights[index] > 0f ? weights[index] : 0f;
                if (target < running) return index;
            }

            return weights.Length - 1;
        }
    }
}
