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
        /// One key: the name its files are under, what the player is told it is, and the colour of
        /// the light it will eventually let loose.
        /// <para>
        /// The display name is not the asset name, and the difference is the point. On the disk a
        /// key is <c>WallKey</c>, which says which door it fits; on the screen it is "Key to the
        /// Fortress", which says what it just bought the player — see <c>KeyBanner</c>, the one
        /// place in this game that puts a sentence in front of anyone. A key with a filename for a
        /// display name would name the door to a player who has never seen it.
        /// </para>
        /// </summary>
        private readonly struct Key
        {
            public readonly string Name;
            public readonly string DisplayName;
            public readonly Color Colour;

            public Key(string name, string displayName, Color colour)
            {
                Name = name;
                DisplayName = displayName;
                Colour = colour;
            }
        }

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
        private static readonly Key[] Keys =
        {
            new Key(WallKeyName, "Key to the Fortress", new Color(0.95f, 0.62f, 0.25f)),
            new Key(TowerKeyName, "Key to the Keep", new Color(0.55f, 0.92f, 1f)),
        };

        [MenuItem("Tools/Eclipse/Items/Build Castle Keys")]
        private static void BuildBoth()
        {
            foreach (Key key in Keys) Build(key);

            AssetDatabase.SaveAssets();
            Debug.Log("[Keys] Built the wall key and the tower key.");
        }

        /// <summary>
        /// Builds one key and returns its inventory asset, or null when its model is missing.
        /// </summary>
        private static InventoryItem Build(Key key)
        {
            string name = key.Name;
            Color colour = key.Colour;
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
            Name(item, key);

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
            foreach (Key key in Keys)
                if (key.Name == name) return Load(key);

            Debug.LogError($"[Keys] There is no key called {name}.");
            return null;
        }

        private static InventoryItem Load(Key key)
        {
            var existing = AssetDatabase.LoadAssetAtPath<InventoryItem>(
                $"{ItemBuilderKit.ItemFolder}/{key.Name}.asset");

            if (existing == null) return Build(key);

            Name(existing, key);
            return existing;
        }

        /// <summary>
        /// Gives the asset the name the player sees. Done on load as well as on build, because
        /// <c>EnsureItemAsset</c> names a NEW asset after its file and leaves an existing one
        /// alone — so a key made before this display name existed would go on calling itself
        /// "WallKey" on screen until somebody rebuilt it by hand.
        /// </summary>
        private static void Name(InventoryItem item, Key key)
        {
            if (item == null || item.itemName == key.DisplayName) return;

            item.itemName = key.DisplayName;
            EditorUtility.SetDirty(item);
        }

        /// <summary>The pickup prefab a key is dropped into the world as.</summary>
        public static GameObject LoadPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
    }
}
