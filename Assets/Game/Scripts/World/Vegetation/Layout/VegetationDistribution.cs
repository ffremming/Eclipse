namespace SpaceGame.Vegetation
{
    /// <summary>
    /// How a layer spreads its plants over the ground.
    /// </summary>
    public enum VegetationDistribution
    {
        /// <summary>Even spread with a guaranteed gap. Trees, boulders, anything that is a landmark.</summary>
        Scatter,

        /// <summary>Noise blobs with bare lanes between them. Undergrowth, ground cover, grass.</summary>
        Patch,

        /// <summary>Tight stands with open ground between them. Woods, copses, thickets.</summary>
        Cluster,
    }
}
