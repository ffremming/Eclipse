using UnityEngine;

namespace SpaceGame.Core
{
    public interface IWorldService
    {
        public void Despawn(GameObject gameObject);

        /// <summary>Instantiate a prefab into the world.</summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
    }
}
