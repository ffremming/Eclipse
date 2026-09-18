namespace SpaceGame.Vegetation
{
    /// <summary>
    /// A number that varies between two ends. Every randomized value in a forest layer is one of
    /// these, so "give me a scale between 0.8 and 1.4" is one field in an inspector rather than
    /// two that can drift apart.
    /// <para>
    /// <see cref="At"/> takes a 0..1 sample rather than drawing its own random number, so a layout
    /// keeps the single seeded generator that makes a forest reproducible.
    /// </para>
    /// <para>
    /// A plain serializable struct of two floats on purpose: it has to survive Unity serialization
    /// on an authored asset, and a property drawer needs the two ends as fields it can find.
    /// </para>
    /// </summary>
    [System.Serializable]
    public struct FloatRange
    {
        /// <summary>The value at sample 0. Nothing forbids it standing above <see cref="Max"/>.</summary>
        public float Min;

        /// <summary>The value at sample 1.</summary>
        public float Max;

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>The value at <paramref name="t"/>, 0 being <see cref="Min"/> and 1 <see cref="Max"/>.</summary>
        /// <param name="t">
        /// A 0..1 sample, usually one roll of a layout's seeded generator. Values outside that band
        /// are clamped rather than extrapolated, so a caller feeding it a raw noise value or a
        /// distance never places a tree at ten times its intended size.
        /// </param>
        public float At(float t)
        {
            float clamped = t < 0f ? 0f : t > 1f ? 1f : t;
            return Min + (Max - Min) * clamped;
        }

        /// <summary>A range that does not vary, for a layer that wants one fixed number.</summary>
        public static FloatRange Of(float value) => new FloatRange(value, value);

        /// <summary>A range between two ends.</summary>
        public static FloatRange Of(float min, float max) => new FloatRange(min, max);
    }
}
