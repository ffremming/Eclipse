using System.IO;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the torch prefab and its inventory asset from nothing.
    /// <para>
    /// A builder rather than a hand-authored prefab because the two files have to reference each
    /// other — the item asset points at the prefab and the prefab's <see cref="PickupableItem"/>
    /// points back at the asset — and that cycle cannot be authored in one pass by hand without
    /// leaving one side null. It is also re-runnable, so the torch can be retuned by editing the
    /// constants here and rebuilding.
    /// </para>
    /// <para>
    /// The mesh is primitives. When a real torch FBX exists, replace <see cref="BuildModel"/> with
    /// an instance of it — everything else here stays.
    /// </para>
    /// </summary>
    public static class TorchBuilder
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Items/Artifacts/Gadgets";
        private const string PrefabPath = PrefabFolder + "/Torch.prefab";

        // Must live under Resources/Items: RegistryLoader does Resources.LoadAll there, and an
        // asset anywhere else never registers, never reaches the dev browser, and comes back empty
        // from every save that held it.
        private const string ItemFolder = "Assets/Game/Resources/Items/Artifacts";
        private const string ItemPath = ItemFolder + "/Torch.asset";

        private const float ShaftLength = 0.52f;
        private const float ShaftRadius = 0.022f;
        private const float HeadRadius = 0.055f;

        /// <summary>Longest-axis size once held. Hand tools sit between 0.2 and 0.4 metres.</summary>
        private const float HoldSize = 0.55f;

        /// <summary>Where the light sits above the shaft's top, in metres.</summary>
        private const float FlameHeight = 0.06f;

        /// <summary>What a dropped torch comes to rest on. Layer 7, the mask every artifact uses.</summary>
        private const int GroundLayerMask = 128;

        [MenuItem("Tools/Eclipse/Items/Build Torch")]
        private static void Build()
        {
            GameObject root = new GameObject("Torch");

            BuildModel(root.transform);
            Light flame = BuildFlame(root.transform);
            Transform grip = BuildGrip(root.transform);

            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.16f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;

            DropItemPhysics drop = root.AddComponent<DropItemPhysics>();
            Wire(drop, "rb", body);
            WireInt(drop, "groundLayer", GroundLayerMask);

            ItemGrip itemGrip = root.AddComponent<ItemGrip>();
            Wire(itemGrip, "gripPoint", grip);
            WireEnum(itemGrip, "holdStyle", (int)ItemGrip.HoldStyle.OneHanded);
            WireFloat(itemGrip, "holdSize", HoldSize);

            TorchArtifact torch = root.AddComponent<TorchArtifact>();
            Wire(torch, "torchLight", flame);

            PickupableItem pickup = root.AddComponent<PickupableItem>();

            Directory.CreateDirectory(PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            InventoryItem item = EnsureItemAsset(prefab);

            // The back-reference, which only exists once both files do. Written onto the saved
            // prefab rather than the scene object, because the scene object is already gone.
            PickupableItem prefabPickup = prefab.GetComponent<PickupableItem>();
            Wire(prefabPickup, "item", item);
            PrefabUtility.SavePrefabAsset(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Torch] Built {PrefabPath} and {ItemPath}. " +
                      "Run Tools/Generate All Item Icons to give it an icon.");
        }

        /// <summary>A shaft and a charred head. Placeholder geometry until an FBX exists.</summary>
        private static void BuildModel(Transform parent)
        {
            GameObject model = new GameObject("Model");
            model.transform.SetParent(parent, false);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(model.transform, false);
            shaft.transform.localScale = new Vector3(ShaftRadius * 2f, ShaftLength * 0.5f, ShaftRadius * 2f);
            Object.DestroyImmediate(shaft.GetComponent<Collider>());

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(model.transform, false);
            head.transform.localPosition = new Vector3(0f, ShaftLength * 0.5f, 0f);
            head.transform.localScale = Vector3.one * (HeadRadius * 2f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
        }

        /// <summary>
        /// The light itself. Shadows are on: a torch that casts none lights the far side of every
        /// rock it stands behind, which in a dark world is the difference between a place the
        /// player can read and a flat wash.
        /// </summary>
        private static Light BuildFlame(Transform parent)
        {
            GameObject flameObject = new GameObject("Flame");
            flameObject.transform.SetParent(parent, false);
            flameObject.transform.localPosition = new Vector3(0f, ShaftLength * 0.5f + FlameHeight, 0f);

            Light light = flameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;

            // Off until the torch says otherwise. A prefab that spawns lit would light the ground
            // around every unlit torch lying in the world.
            light.enabled = false;
            return light;
        }

        /// <summary>
        /// Where the hand closes. Below the middle of the shaft, so the flame ends up above the
        /// fist rather than inside it.
        /// </summary>
        private static Transform BuildGrip(Transform parent)
        {
            GameObject grip = new GameObject("Grip");
            grip.transform.SetParent(parent, false);
            grip.transform.localPosition = new Vector3(0f, -ShaftLength * 0.25f, 0f);
            return grip.transform;
        }

        private static InventoryItem EnsureItemAsset(GameObject prefab)
        {
            InventoryItem existing = AssetDatabase.LoadAssetAtPath<InventoryItem>(ItemPath);
            if (existing != null)
            {
                existing.itemPrefab = prefab;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Directory.CreateDirectory(ItemFolder);
            InventoryItem item = ScriptableObject.CreateInstance<InventoryItem>();
            item.itemName = "Torch";
            item.itemPrefab = prefab;
            AssetDatabase.CreateAsset(item, ItemPath);
            return item;
        }

        // Serialized-property writes, because every one of these fields is private. Reflection would
        // also reach them, but SerializedObject is what actually marks the object dirty and survives
        // a domain reload.

        private static void Wire(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireEnum(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
