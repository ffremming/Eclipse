using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// A light whose reach grows with the thing carrying it. The reach is authored per unit of scale, so
    /// a mushroom planted at eight times its size lights eight times as far as one at scale one, from the
    /// one prefab. A light's range is in world metres and ignores the transform's scale on its own.
    /// <para>
    /// Applied on enable in edit mode as well, so the light seen in the scene view is the light the
    /// game gets, and <em>set</em> rather than multiplied, so applying it twice changes nothing.
    /// Measured on the height axis: the plants it is used on are scaled evenly.
    /// </para>
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public sealed class ScaledLight : MonoBehaviour
    {
        [SerializeField] private Light lamp;

        [Tooltip("Reach of the light per unit of the object's scale, in metres. At 2, a mushroom " +
                 "planted at 5x lights 10 m around it.")]
        [SerializeField, Min(0f)] private float rangePerScale = 2f;

        private void OnEnable() => Apply();

        private void OnValidate() => Apply();

        private void Apply()
        {
            if (lamp == null) lamp = GetComponent<Light>();
            lamp.range = rangePerScale * transform.lossyScale.y;
        }
    }
}
