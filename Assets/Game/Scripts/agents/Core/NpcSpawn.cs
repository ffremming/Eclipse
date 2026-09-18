// How an NPC comes into the world.
using UnityEngine;

namespace SpaceGame.Agents
{
    public static class NpcSpawn
    {
        /// <summary>Instantiate <paramref name="prefab"/> as a live NPC.</summary>
        /// <param name="context">Logged as the object to select when a spawn fails.</param>
        public static GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation,
                                        UnityEngine.Object context = null)
        {
            if (prefab == null) return null;

            return UnityEngine.Object.Instantiate(prefab, position, rotation);
        }
    }
}
