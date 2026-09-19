using System.IO;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// What every item builder needs and none of them should carry its own copy of: writing a
    /// private field on a component, turning a finished hierarchy into a pickup-able prefab with an
    /// inventory asset, and the light and trail every glowing item hangs off its business end.
    /// <para>
    /// <c>TorchBuilder</c> and <c>LightBladeBuilder</c> each still carry their own copy of the same
    /// helpers, from before this existed. New builders use this; moving those two over is a change
    /// to their output nobody has asked for.
    /// </para>
    /// </summary>
    public static class ItemBuilderKit
    {
        /// <summary>
        /// Must live under Resources/Items: RegistryLoader does Resources.LoadAll there, and an asset
        /// anywhere else never registers and comes back empty from every save that held it.
        /// </summary>
        public const string ItemFolder = "Assets/Game/Resources/Items/Artifacts";

        /// <summary>What a dropped item comes to rest on. The mask every artifact uses.</summary>
        public const int GroundLayerMask = 128;

        private const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>Radius of the sphere a pickup is grabbed by, in metres.</summary>
        private const float PickupRadius = 0.16f;

        /// <summary>
        /// Gives a finished item hierarchy everything a thing lying in the world or held in a hand
        /// needs, saves it as <c>{prefabFolder}/{name}.prefab</c>, and makes its inventory asset.
        /// Destroys <paramref name="root"/>: the prefab is what is left.
        /// </summary>
        /// <param name="colliderCentre">
        /// Where the pickup sphere sits, in the item's own space. The body of the item, not
        /// necessarily its origin — an item held by its top has its origin at the top, and a sphere
        /// there would leave the rest of it below the ground it was dropped on.
        /// </param>
        public static GameObject SavePickupable(GameObject root, string prefabFolder, string name,
                                                float holdSize, Vector3 colliderCentre)
        {
            GiveDropPhysics(root, colliderCentre);

            GameObject grip = new GameObject("Grip");
            grip.transform.SetParent(root.transform, false);

            ItemGrip itemGrip = root.AddComponent<ItemGrip>();
            Wire(itemGrip, "gripPoint", grip.transform);
            WireEnum(itemGrip, "holdStyle", (int)ItemGrip.HoldStyle.OneHanded);
            WireFloat(itemGrip, "holdSize", holdSize);

            root.AddComponent<PickupableItem>();

            Directory.CreateDirectory(prefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);

            InventoryItem item = EnsureItemAsset(name, prefab);

            // The back-reference, which can only be made once both files exist.
            Wire(prefab.GetComponent<PickupableItem>(), "item", item);
            PrefabUtility.SavePrefabAsset(prefab);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        /// <summary>
        /// What a thing lying in the world needs to get there: the solid sphere it is caught on,
        /// a body to fall with, and the <see cref="DropItemPhysics"/> that freezes it once it lands.
        /// <para>
        /// The collider is deliberately NOT a trigger. <see cref="DropItemPhysics"/> settles the
        /// item from <c>OnCollisionEnter</c>, which a trigger never raises — an item given a
        /// trigger here falls through the island and is never seen again. Anything that wants to be
        /// collected by being walked into puts a second, trigger collider on a child.
        /// </para>
        /// </summary>
        public static void GiveDropPhysics(GameObject root, Vector3 colliderCentre)
        {
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = PickupRadius;
            collider.center = colliderCentre;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;

            DropItemPhysics drop = root.AddComponent<DropItemPhysics>();
            Wire(drop, "rb", body);
            WireInt(drop, "groundLayer", GroundLayerMask);
        }

        /// <summary>
        /// The inventory asset for <paramref name="name"/>, made on first build and re-pointed at
        /// <paramref name="prefab"/> afterwards. Public because not every item is built through
        /// <see cref="SavePickupable"/> — a castle key is put in the world as a prefab but is never
        /// carried in a hotbar, and still needs the one asset that IS its identity.
        /// </summary>
        public static InventoryItem EnsureItemAsset(string name, GameObject prefab)
        {
            string path = $"{ItemFolder}/{name}.asset";

            InventoryItem existing = AssetDatabase.LoadAssetAtPath<InventoryItem>(path);
            if (existing != null)
            {
                existing.itemPrefab = prefab;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Directory.CreateDirectory(ItemFolder);
            InventoryItem item = ScriptableObject.CreateInstance<InventoryItem>();
            item.itemName = name;
            item.itemPrefab = prefab;
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        /// <summary>
        /// Loads a material, creating it on <paramref name="shaderName"/> the first time and
        /// handing the new one to <paramref name="authorOnCreate"/>. Never overwrites: what is
        /// tuned by hand in the Inspector afterwards survives the next build, which the prefab does
        /// not.
        /// </summary>
        public static Material EnsureMaterial(string path, string shaderName,
                                              System.Action<Material> authorOnCreate = null)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[ItemBuilderKit] Shader '{shaderName}' not found.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            Material material = new Material(shader);
            authorOnCreate?.Invoke(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>A lit material, created the first time with this colour and this emission.</summary>
        public static Material EnsureLitMaterial(string path, Color baseColor, Color emission)
            => EnsureMaterial(path, LitShader, material =>
            {
                material.SetColor("_BaseColor", baseColor);
                material.SetColor("_EmissionColor", emission);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            });

        /// <summary>
        /// Stops the item's own geometry casting shadows. A light sitting inside it would otherwise
        /// be occluded by it and throw a hard wedge of shadow across the ground the player stands
        /// on. It still receives shadows, so it does not look unlit.
        /// </summary>
        public static void StopCastingShadows(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        /// <summary>A point light. Shadowless unless it is the one carried light that should cast.</summary>
        public static Light AddPointLight(Transform parent, string name, Color color, float range,
                                          float intensity, LightShadows shadows)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = shadows;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            return light;
        }

        /// <summary>The arc of light a business end drags through a swing. Off until the swing starts it.</summary>
        public static TrailRenderer AddTrail(Transform parent, Material arc, float width, float time)
        {
            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(parent, false);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = arc;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.minVertexDistance = 0.03f;
            trail.emitting = false;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        // Serialized-property writes, because every one of these fields is private.

        public static void Wire(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Fills a serialized array of object references, replacing whatever was in it.</summary>
        public static void WireArray(Object target, string field, params Object[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;

            for (int index = 0; index < values.Length; index++)
                array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireFloat(Object target, string field, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireColor(Object target, string field, Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireInt(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireBool(Object target, string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireEnum(Object target, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(field).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

    }
}
