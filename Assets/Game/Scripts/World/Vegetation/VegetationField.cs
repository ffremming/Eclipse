using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// A patch of ground that grows vegetation: an area, a seed, and the layers to plant in it.
    /// <para>
    /// The field only plans — it says which prefab stands where, at what size and turn, and drops
    /// the plants the ground will not take. Putting them in the scene is the editor's job
    /// (<c>Tools ▸ SpaceGame ▸ World ▸ Bake Vegetation</c>), so what ships is ordinary scene
    /// geometry: no spawning at runtime and nothing to save.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VegetationField : MonoBehaviour
    {
        [Tooltip("Size of the field on X and Z, in metres, centred on this transform.")]
        [SerializeField] private Vector2 size = new Vector2(120f, 120f);

        [Tooltip("Changing this reshuffles every layer. The same seed always grows the same field.")]
        [SerializeField] private int seed = 20260918;

        [Tooltip("The layers to plant, in the order they are baked.")]
        [SerializeField] private List<VegetationLayerAsset> layers = new List<VegetationLayerAsset>();

        [Header("Ground")]
        [Tooltip("What counts as ground to plant on.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Metres above the field the ground ray starts, so it clears the tallest terrain in it.")]
        [SerializeField, Min(1f)] private float rayHeight = 200f;

        [Tooltip("Lowest world height a plant will stand at, in metres. Keeps a field off its beaches and out of its water.")]
        [SerializeField] private float minAltitude = float.MinValue;

        [Tooltip("Steepest ground a plant will stand on, in degrees. Steeper ground is left bare.")]
        [SerializeField, Range(0f, 89f)] private float maxSlope = 35f;

        [Tooltip("How far a plant leans with the slope: 0 stands upright, 1 follows the ground normal.")]
        [SerializeField, Range(0f, 1f)] private float alignToGround = 0.5f;

        public IReadOnlyList<VegetationLayerAsset> Layers => layers;

        /// <summary>
        /// Every plant this field wants, for every layer, already dropped onto the ground. Layers
        /// with nothing authored in them are skipped; see <see cref="VegetationLayerAsset.Problem"/>
        /// for why one is refused.
        /// </summary>
        public List<PlannedPlant> Plan()
        {
            List<PlannedPlant> planned = new List<PlannedPlant>();

            for (int index = 0; index < layers.Count; index++)
            {
                VegetationLayerAsset layer = layers[index];
                if (layer == null || layer.Problem() != null) continue;

                // Each layer gets its own seed off the field's, so adding a layer does not reshuffle
                // the ones already tuned.
                VegetationLayerSettings settings = layer.ToSettings(size.x, size.y, seed + index * 7919);
                foreach (VegetationPlacement placement in VegetationLayout.Build(settings))
                {
                    if (TryPlant(layer, placement, out PlannedPlant plant)) planned.Add(plant);
                }
            }

            return planned;
        }

        /// <summary>
        /// The plant at one placement, or false where the ground refuses it: nothing under the ray,
        /// ground below the field's lowest height, or ground too steep to stand on.
        /// </summary>
        private bool TryPlant(VegetationLayerAsset layer, VegetationPlacement placement, out PlannedPlant plant)
        {
            plant = default;

            Vector3 above = transform.position + new Vector3(placement.X, rayHeight, placement.Z);
            if (!Physics.Raycast(above, Vector3.down, out RaycastHit hit, rayHeight * 2f,
                                 groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            if (hit.point.y < minAltitude) return false;
            if (Vector3.Angle(hit.normal, Vector3.up) > maxSlope) return false;

            Quaternion upright = Quaternion.AngleAxis(placement.YawDegrees, Vector3.up);
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, hit.normal);
            Quaternion lean = Quaternion.AngleAxis(placement.TiltDegrees,
                                                   Quaternion.AngleAxis(placement.TiltYawDegrees, Vector3.up) * Vector3.right);

            plant = new PlannedPlant(
                layer.Items[placement.ItemIndex].Prefab,
                hit.point - Vector3.up * placement.Sink,
                Quaternion.Slerp(Quaternion.identity, slope, alignToGround) * lean * upright,
                placement.Scale);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.1f, size.y));
        }

        /// <summary>One plant, ready to be put in the scene.</summary>
        public readonly struct PlannedPlant
        {
            public PlannedPlant(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
            {
                Prefab = prefab;
                Position = position;
                Rotation = rotation;
                Scale = scale;
            }

            public GameObject Prefab { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }

            /// <summary>Multiplier on the prefab's own scale, which is already its life size.</summary>
            public float Scale { get; }
        }
    }
}
