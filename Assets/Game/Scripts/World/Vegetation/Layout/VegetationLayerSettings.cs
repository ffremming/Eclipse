namespace SpaceGame.Vegetation
{
    /// <summary>
    /// One layer's numbers, engine-free, so a layout can be built and tested with no scene open.
    /// <see cref="VegetationLayerAsset"/> is the authored half and hands one of these over.
    /// <para>
    /// The three modes share one struct and each reads the fields it cares about: a Patch layer
    /// ignores <see cref="MinDistance"/> exactly as a Scatter layer ignores <see cref="PatchSize"/>.
    /// One struct rather than three because a layer changes mode while it is being tuned, and three
    /// would throw the other modes' numbers away every time it did.
    /// </para>
    /// </summary>
    public struct VegetationLayerSettings
    {
        public VegetationDistribution Mode;

        /// <summary>Box the layer fills, in metres, centred on the field.</summary>
        public float SizeX;
        public float SizeZ;

        /// <summary>Bare circle at the centre. 0 for a layer with nothing to keep clear of.</summary>
        public float ClearingRadius;

        /// <summary>Scatter mode: ceiling on how many to place. Spacing, not this, stops clumping.</summary>
        public int Count;

        /// <summary>Scatter mode: closest two plants of this layer may stand.</summary>
        public float MinDistance;

        /// <summary>Patch mode: rough width of one patch, and so of the lane beside it.</summary>
        public float PatchSize;

        /// <summary>Patch mode: fraction of the ground that ends up covered, 0..1.</summary>
        public float Coverage;

        /// <summary>Patch mode: distance between plants inside a patch, before jitter.</summary>
        public float Spacing;

        /// <summary>Patch mode: random offset per plant, as a fraction of <see cref="Spacing"/>.</summary>
        public float PositionJitter;

        /// <summary>Patch mode: noise octaves. One gives smooth blobs, three gives frayed edges.</summary>
        public int NoiseOctaves;

        /// <summary>Patch mode: how loud each octave is next to the one before it.</summary>
        public float NoisePersistence;

        /// <summary>Cluster mode: how many stands to spread over the field.</summary>
        public int ClusterCount;

        /// <summary>Cluster mode: how wide one stand is, and the closest two stands may stand.</summary>
        public float ClusterRadius;

        /// <summary>Cluster mode: how many plants to crowd into one stand.</summary>
        public int PerCluster;

        public int Seed;

        public VegetationItemSettings[] Items;
    }
}
