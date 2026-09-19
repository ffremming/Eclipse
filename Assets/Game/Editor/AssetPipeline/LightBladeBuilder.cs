using System.IO;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the swung blades — sword, khopesh and axe — and their inventory assets.
    /// <para>
    /// One builder for all three, because what has to be true of them is that they match. Sharing a
    /// builder means they share their light settings, their trail and their grip conventions by
    /// construction, and the only way to make one look like it came from a different game is to
    /// deliberately edit the numbers below.
    /// </para>
    /// <para>
    /// The chain whip is deliberately NOT here. Its prefab carries a hand-built rope rig — an
    /// anchor, a tip and a <c>ChainLinkRenderer</c> that <c>LightWhip</c> simulates against — and a
    /// builder that replaced the prefab wholesale, which is what these do, would throw that away.
    /// </para>
    /// </summary>
    public static class LightBladeBuilder
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Items/Artifacts/Weapons";
        private const string ItemFolder = "Assets/Game/Resources/Items/Artifacts";
        private const string ArcMaterialPath = "Assets/Game/Art/Materials/Light/LightArc.mat";

        private const string SwordModel = "Assets/Game/Art/Models/Weapons/Sword/sword.fbx";
        private const string KhopeshModel = "Assets/Game/Art/Models/Weapons/Khopesh/khopesh.fbx";
        private const string AxeModel = "Assets/Game/Art/Models/Weapons/Axe/axe.obj";

        /// <summary>What a dropped weapon comes to rest on. The mask every artifact uses.</summary>
        private const int GroundLayerMask = 128;

        // A blade is not a light source — the torch is. These are set so the weapon is VISIBLE in a
        // dark room and throws a little light on what it is about to hit, an order of magnitude below
        // the torch so that carrying a sword never makes the torch pointless.
        private const float IdleIntensity = 6f;
        private const float SwingIntensity = 34f;
        private const float IdleRange = 7f;
        private const float SwingRange = 16f;

        [MenuItem("Tools/Eclipse/Items/Build Light Blades")]
        private static void BuildAll()
        {
            Material arc = AssetDatabase.LoadAssetAtPath<Material>(ArcMaterialPath);
            if (arc == null)
            {
                Debug.LogError($"[LightBlades] Missing {ArcMaterialPath}.");
                return;
            }

            // Fast, tight and light. The baseline the other two are felt against.
            Build("LightSword", SwordModel, arc, length: 0.95f, handle: HandleEnd.LowEnd, gripAlong: 0.16f,
                  reach: 1.6f, arcRadius: 1.2f, damage: 22, swing: 0.36f, trailWidth: 0.34f, trailTime: 0.22f);

            // A curved hook of a blade, so it is the short quick one — less reach than the sword,
            // faster, and the widest trail because the curve is what the eye follows.
            Build("LightKhopesh", KhopeshModel, arc, length: 0.72f, handle: HandleEnd.HighEnd, gripAlong: 0.15f,
                  reach: 1.35f, arcRadius: 1.35f, damage: 19, swing: 0.30f, trailWidth: 0.42f, trailTime: 0.24f);

            // Slower, wider and harder. These numbers ARE the difference between an axe and a
            // sword — there is no axe class, only an axe prefab.
            Build("LightAxe", AxeModel, arc, length: 0.88f, handle: HandleEnd.HighEnd, gripAlong: 0.30f,
                  reach: 1.5f, arcRadius: 1.7f, damage: 38, swing: 0.58f, trailWidth: 0.52f, trailTime: 0.26f);

            AssetDatabase.SaveAssets();
            Debug.Log("[LightBlades] Built sword, khopesh and axe. " +
                      "Run Tools/Generate All Item Icons to give them icons.");
        }

        private static void Build(string name, string modelPath, Material arc, float length,
                                  HandleEnd handle, float gripAlong, float reach, float arcRadius,
                                  int damage, float swing, float trailWidth, float trailTime)
        {
            GameObject root = new GameObject(name);
            MountedModel mounted = ModelMount.Mount(root.transform, modelPath, length, handle, gripAlong);
            Transform tip = mounted.Tip;

            // The blade must not cast shadows from the light sitting inside it, or it throws a wedge
            // of shadow across whatever the player is about to hit. It still receives them.
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            LightBlade blade = root.AddComponent<LightBlade>();
            Wire(blade, "tipLight", BuildTipLight(tip));
            Wire(blade, "trail", BuildTrail(tip, arc, trailWidth, trailTime));
            WireFloat(blade, "idleIntensity", IdleIntensity);
            WireFloat(blade, "swingIntensity", SwingIntensity);
            WireFloat(blade, "idleRange", IdleRange);
            WireFloat(blade, "swingRange", SwingRange);
            WireFloat(blade, "reach", reach);
            WireFloat(blade, "arcRadius", arcRadius);
            WireFloat(blade, "swingDuration", swing);
            WireInt(blade, "damage", damage);

            Finish(root, name, length, mounted.GripPoint);
        }

        /// <summary>
        /// The glow at the business end. No shadows: the torch is the one carried light that casts
        /// them, and a second shadow-caster swinging through an arc thrashes too fast to read while
        /// costing as much as the first.
        /// </summary>
        private static Light BuildTipLight(Transform tip)
        {
            GameObject lightObject = new GameObject("TipLight");
            lightObject.transform.SetParent(tip, false);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.range = IdleRange;
            light.intensity = IdleIntensity;
            light.color = new Color(1f, 0.72f, 0.42f);
            return light;
        }

        private static TrailRenderer BuildTrail(Transform tip, Material arc, float width, float time)
        {
            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(tip, false);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = arc;
            trail.time = time;
            trail.widthMultiplier = width;
            trail.minVertexDistance = 0.03f;
            trail.emitting = false;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        private static void Finish(GameObject root, string name, float holdSize, Vector3 gripPoint)
        {
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.16f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = true;

            DropItemPhysics drop = root.AddComponent<DropItemPhysics>();
            Wire(drop, "rb", body);
            WireInt(drop, "groundLayer", GroundLayerMask);

            GameObject grip = new GameObject("Grip");
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = gripPoint;

            ItemGrip itemGrip = root.AddComponent<ItemGrip>();
            Wire(itemGrip, "gripPoint", grip.transform);
            WireEnum(itemGrip, "holdStyle", (int)ItemGrip.HoldStyle.OneHanded);
            WireFloat(itemGrip, "holdSize", holdSize);

            root.AddComponent<PickupableItem>();

            Directory.CreateDirectory(PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
            Object.DestroyImmediate(root);

            InventoryItem item = EnsureItemAsset(name, prefab);

            // The back-reference, which can only be made once both files exist.
            Wire(prefab.GetComponent<PickupableItem>(), "item", item);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        private static InventoryItem EnsureItemAsset(string name, GameObject prefab)
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

        // Serialized-property writes, because every one of these fields is private.

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
