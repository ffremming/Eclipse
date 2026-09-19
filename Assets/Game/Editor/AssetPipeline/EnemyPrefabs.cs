using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Where a creature's prefab lives, for the two builders that place them — the castles'
    /// garrisons and the island's camps.
    /// <para>
    /// One place that knows the naming, because the alternative is each builder holding its own
    /// <c>$"{folder}/{name}Enemy.prefab"</c> and one of them quietly missing the day a creature is
    /// renamed. The failure then is a garrison that builds with a hole in it.
    /// </para>
    /// </summary>
    public static class EnemyPrefabs
    {
        public const string Folder = "Assets/Game/Prefabs/Enemies";

        /// <summary>
        /// The prefab for <paramref name="species"/>, or null with an error saying how to make it.
        /// </summary>
        public static GameObject Load(SculptEnemy species)
        {
            string path = $"{Folder}/{species.Name}Enemy.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                Debug.LogError($"[Enemies] No prefab at {path}. Run " +
                               "Tools/Eclipse/Enemies/Build Sculpt Enemies first.");

            return prefab;
        }
    }
}
