using UnityEngine;

namespace SpaceGame.Core
{
    public class WorldService : IWorldService
    {
        public void Despawn(GameObject gameObject)
        {
            Object.Destroy(gameObject);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return null;

            return Object.Instantiate(prefab, position, rotation);
        }
    }
}
