namespace SpaceGame.Vegetation
{
    /// <summary>
    /// One item's ranges, engine-free. <see cref="VegetationItem"/> is the authored half.
    /// <para>
    /// <see cref="Scale"/> multiplies a prefab that is already at its life size, so 1 means "as
    /// the artist built it" and the range around it is the natural spread between neighbours.
    /// </para>
    /// </summary>
    public struct VegetationItemSettings
    {
        /// <summary>Relative frequency against the other items in the same layer.</summary>
        public float Weight;

        public FloatRange Scale;
        public FloatRange Yaw;
        public FloatRange Tilt;
        public FloatRange Sink;

        /// <summary>Life size, full spin, no lean, no sink: what an item with nothing set means.</summary>
        public static VegetationItemSettings Default => new VegetationItemSettings
        {
            Weight = 1f,
            Scale = FloatRange.Of(1f),
            Yaw = FloatRange.Of(0f, 360f),
            Tilt = FloatRange.Of(0f),
            Sink = FloatRange.Of(0f),
        };
    }
}
