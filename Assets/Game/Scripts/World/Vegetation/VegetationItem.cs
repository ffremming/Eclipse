using UnityEngine;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// One prefab a layer may plant, and the ranges it varies over. The authored half of
    /// <see cref="VegetationItemSettings"/>.
    /// </summary>
    [System.Serializable]
    public sealed class VegetationItem
    {
        [Tooltip("The prefab to plant. Already at its life size, so scale 1 means life-size.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Relative frequency against the other items in this layer.")]
        [SerializeField, Min(0f)] private float weight = 1f;

        [Tooltip("Size multiplier on the prefab. 1 is the size the artist built it at.")]
        [SerializeField] private FloatRange scale = new FloatRange(0.85f, 1.2f);

        [Tooltip("Spin around the plant's own up axis, in degrees.")]
        [SerializeField] private FloatRange yaw = new FloatRange(0f, 360f);

        [Tooltip("Lean away from vertical, in degrees.")]
        [SerializeField] private FloatRange tilt = new FloatRange(0f, 6f);

        [Tooltip("Metres pushed down into the ground, so the plant is planted rather than perched.")]
        [SerializeField] private FloatRange sink = new FloatRange(0f, 0.03f);

        public GameObject Prefab => prefab;

        public VegetationItemSettings ToSettings() => new VegetationItemSettings
        {
            Weight = weight,
            Scale = scale,
            Yaw = yaw,
            Tilt = tilt,
            Sink = sink,
        };
    }
}
