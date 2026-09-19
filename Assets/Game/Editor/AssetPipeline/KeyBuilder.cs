using System.IO;
using SpaceGame.Castle;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// The castle's two keys, as things lying in the world that unlock their doors when walked over.
    /// <para>
    /// The models are authored in <c>tower_key.blend</c> and <c>wall_key.blend</c> and exported by
    /// <c>key_export.py</c>, which normalises each to 0.30 m. This only turns those meshes into
    /// pickups and inventory assets — it does not decide what a key looks like.
    /// </para>
    /// <para>
    /// Both keys glow faintly. That is not decoration: they are small objects lying on the ground
    /// in a world with almost no light in it, and an unlit key on unlit stone is invisible from two
    /// metres away. The glow is what makes them findable at all.
    /// </para>
    /// <para>
    /// A key is built as a walk-into pickup, NOT through <c>ItemBuilderKit.SavePickupable</c> like
    /// every other item. It gets no <c>ItemGrip</c> and no <c>PickupableItem</c>, because it is
    /// never held in a hand and never enters a hotbar — see <see cref="KeyRing"/>. What it gets
    /// instead is the fall physics every dropped thing shares and a wide trigger volume that
    /// <see cref="KeyPickup"/> collects it from.
    /// </para>
    /// </summary>
    public static class KeyBuilder
    {
        private const string ModelFolder = "Assets/Game/Art/Models/Items/Keys";
        private const string PrefabFolder = "Assets/Game/Prefabs/Items/Keys";
        private const string MaterialFolder = "Assets/Game/Art/Materials/Items";

        /// <summary>Name of the key that opens the castle's outer gate.</summary>
        public const string WallKeyName = "WallKey";

        /// <summary>Name of the key that opens a keep's main entrance.</summary>
        public const string TowerKeyName = "TowerKey";

        /// <summary>
        /// Radius of the volume the key is collected from, in metres. Wide — far wider than the key
        /// itself. It is collected by walking rather than by aiming, so what matters is that
        /// crossing the ground the key is on works, not that the player's capsule touched a 30 cm
        /// object. Too tight and a key reads as broken for the one player who ran past it.
        /// </summary>
        private const float PickupRadius = 1.1f;

        private const float GlowRange = 3.2f;
        private const float GlowIntensity = 2.6f;

        // Each key is the colour of the light it will eventually let loose, so a player who has
        // seen one lighthouse lit can read what the second key is for before using it.
        private static readonly Color WallKeyColour = new Color(0.95f, 0.62f, 0.25f);
        private static readonly Color TowerKeyColour = new Color(0.55f, 0.92f, 1f);

        [MenuItem("Tools/Eclipse/Items/Build Castle Keys")]
        private static void BuildBoth()
        {
            Build(WallKeyName, WallKeyColour);
            Build(TowerKeyName, TowerKeyColour);
            AssetDatabase.SaveAssets();
            Debug.Log("[Keys] Built the wall key and the tower key.");
        }

        /// <summary>
        /// Builds one key and returns its inventory asset, or null when its model is missing.
        /// </summary>
        public static InventoryItem Build(string name, Color colour)
        {
            string modelPath = $"{ModelFolder}/{name}.fbx";
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[Keys] No model at {modelPath}. Run key_export.py first.");
                return null;
            }

            var root = new GameObject(name);
            GameObject body = Object.Instantiate(model, root.transform);
            body.name = "Body";

            Directory.CreateDirectory(MaterialFolder);
            Material material = ItemBuilderKit.EnsureLitMaterial(
                $"{MaterialFolder}/{name}.mat", colour * 0.35f, colour);

            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            ItemBuilderKit.StopCastingShadows(root);
            ItemBuilderKit.AddPointLight(root.transform, "Glow", colour, GlowRange, GlowIntensity,
                                         LightShadows.None);

            // The key is centred on itself by the exporter, so both spheres sit at its origin.
            ItemBuilderKit.GiveDropPhysics(root, Vector3.zero);

            // On a child, because the root's own collider is the solid one the key lands on and a
            // GameObject cannot have one sphere that is both. Trigger messages still reach the
            // root, which is where the rigidbody is and so where KeyPickup can act on them.
            var volume = new GameObject("PickupVolume");
            volume.transform.SetParent(root.transform, false);

            SphereCollider reach = volume.AddComponent<SphereCollider>();
            reach.isTrigger = true;
            reach.radius = PickupRadius;

            root.AddComponent<KeyPickup>();

            Directory.CreateDirectory(PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);

            InventoryItem item = ItemBuilderKit.EnsureItemAsset(name, prefab);

            // The back-reference, which can only be made once both files exist.
            ItemBuilderKit.Wire(prefab.GetComponent<KeyPickup>(), "key", item);
            PrefabUtility.SavePrefabAsset(prefab);
            AssetDatabase.SaveAssets();

            return item;
        }

        /// <summary>
        /// The key's inventory asset, building it first if it is not there yet. What the castle
        /// builder calls, so placing a castle cannot produce a door whose key does not exist.
        /// </summary>
        public static InventoryItem Load(string name)
        {
            var existing = AssetDatabase.LoadAssetAtPath<InventoryItem>(
                $"{ItemBuilderKit.ItemFolder}/{name}.asset");

            if (existing != null) return existing;

            return Build(name, name == WallKeyName ? WallKeyColour : TowerKeyColour);
        }

        /// <summary>The pickup prefab a key is dropped into the world as.</summary>
        public static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
    }
}
