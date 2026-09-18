namespace SpaceGame.Vegetation
{
    /// <summary>
    /// One spot a layout chose, in metres on the XZ plane, and which of the layer's items belongs
    /// there. Size, turn and lean are not here: those are the item's business and are drawn in
    /// <see cref="VegetationLayout"/>, so the two layouts stay about position alone.
    /// </summary>
    public readonly struct LayoutPoint
    {
        public LayoutPoint(float x, float z, int itemIndex)
        {
            X = x;
            Z = z;
            ItemIndex = itemIndex;
        }

        public float X { get; }
        public float Z { get; }

        /// <summary>Which of the layer's items to plant, drawn from their weights.</summary>
        public int ItemIndex { get; }
    }
}
