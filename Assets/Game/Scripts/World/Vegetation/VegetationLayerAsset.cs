using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// One authored layer of a vegetation field — a set of prefabs, how thickly they stand and how
    /// they vary — kept as an asset so a field is assembled from layers rather than from one long
    /// list, and so the same undergrowth can be reused by every field that wants it.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceGame/World/Vegetation Layer", fileName = "VegetationLayer")]
    public sealed class VegetationLayerAsset : ScriptableObject
    {
        [Tooltip("Scatter spreads landmarks with a gap, Patch grows blobs of undergrowth, Cluster grows stands with open ground between them.")]
        [SerializeField] private VegetationDistribution mode = VegetationDistribution.Scatter;

        [Tooltip("Bare circle at the centre of the field, in metres. 0 keeps nothing clear.")]
        [SerializeField, Min(0f)] private float clearingRadius;

        [Header("Scatter and Cluster")]
        [Tooltip("Ceiling on how many to place. The spacing, not this, is what stops clumping.")]
        [SerializeField, Min(0)] private int count = 40;

        [Tooltip("Closest two plants of this layer may stand, in metres. Cluster mode uses it inside a stand.")]
        [SerializeField, Min(0f)] private float minDistance = 6f;

        [Header("Patch")]
        [Tooltip("Rough width of one patch, and so of the bare lane beside it, in metres.")]
        [SerializeField, Min(0.01f)] private float patchSize = 6f;

        [Tooltip("Fraction of the ground this layer ends up covering.")]
        [SerializeField, Range(0f, 1f)] private float coverage = 0.4f;

        [Tooltip("Distance between plants inside a patch, in metres, before jitter.")]
        [SerializeField, Min(0.01f)] private float spacing = 1.2f;

        [Tooltip("Random offset per plant, as a fraction of the spacing.")]
        [SerializeField, Range(0f, 1f)] private float positionJitter = 0.4f;

        [Tooltip("Noise octaves. One gives smooth blobs, three gives frayed edges.")]
        [SerializeField, Range(1, 5)] private int noiseOctaves = 3;

        [Tooltip("How loud each octave is next to the one before it.")]
        [SerializeField, Range(0f, 1f)] private float noisePersistence = 0.45f;

        [Header("Cluster")]
        [Tooltip("How many stands to spread over the field.")]
        [SerializeField, Min(0)] private int clusterCount = 40;

        [Tooltip("How wide one stand is, in metres, and the closest two stands may stand.")]
        [SerializeField, Min(1f)] private float clusterRadius = 20f;

        [Tooltip("How many plants to crowd into one stand.")]
        [SerializeField, Min(0)] private int perCluster = 20;

        [Header("Items")]
        [SerializeField] private List<VegetationItem> items = new List<VegetationItem>();

        public IReadOnlyList<VegetationItem> Items => items;

        /// <summary>
        /// The layer's numbers for one field. The field owns the area and the seed, so the same
        /// layer asset can be used by two fields of different sizes without being copied.
        /// </summary>
        public VegetationLayerSettings ToSettings(float sizeX, float sizeZ, int seed)
        {
            VegetationItemSettings[] settings = new VegetationItemSettings[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                settings[index] = items[index].ToSettings();
            }

            return new VegetationLayerSettings
            {
                Mode = mode,
                SizeX = sizeX,
                SizeZ = sizeZ,
                ClearingRadius = clearingRadius,
                Count = count,
                MinDistance = minDistance,
                PatchSize = patchSize,
                Coverage = coverage,
                Spacing = spacing,
                PositionJitter = positionJitter,
                NoiseOctaves = noiseOctaves,
                NoisePersistence = noisePersistence,
                ClusterCount = clusterCount,
                ClusterRadius = clusterRadius,
                PerCluster = perCluster,
                Seed = seed,
                Items = settings,
            };
        }

        /// <summary>
        /// Why this layer cannot be baked, or null when it can. Checked before a bake rather than
        /// discovered as a null reference halfway through one.
        /// </summary>
        public string Problem()
        {
            if (items.Count == 0) return name + " has no items.";

            for (int index = 0; index < items.Count; index++)
            {
                if (items[index].Prefab == null) return name + " item " + index + " has no prefab.";
            }

            return null;
        }
    }
}
