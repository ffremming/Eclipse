using System.Collections.Generic;
using System.IO;
using SpaceGame.Gameplay;
using SpaceGame.Vegetation;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Turns the BugWar mushrooms into glowing, breakable ones: a prefab variant of each with a
    /// collider, health, a burst of light orbs on death and a light that shines on its surroundings,
    /// in a glow colour that matches its cap — and points the <c>Mushrooms</c> vegetation layer at
    /// them, grown to several times their size.
    /// <para>
    /// Variants rather than edits, so the imported pack stays as it shipped and a new pack version
    /// drops in. Re-runnable: the variants and the layer are rewritten from the constants here, so
    /// the numbers that matter live here; the glow materials are created once and left alone, so a
    /// glow tuned by eye survives.
    /// </para>
    /// </summary>
    public static class GlowMushroomBuilder
    {
        private const string SourceFolder = "Assets/ThirdParty/BugWarNature/BugWar/Prefabs/Sandbox/Foliage/Mushroom";
        private const string PrefabFolder = "Assets/Game/Prefabs/Environment/GlowMushroom";
        private const string MaterialFolder = "Assets/Game/Art/Materials/Environment";
        private const string OrbPrefabPath = "Assets/Game/Prefabs/Items/LightOrb.prefab";

        // The layer keeps its asset, and so its GUID, so a scene's VegetationField that already lists
        // it needs no rewiring.
        private const string LayerPath = "Assets/Game/ScriptableObjects/Vegetation/Mushrooms.asset";

        /// <summary>
        /// Glow colour by the colour word each pack prefab's name ends in. The emission is the cap's own
        /// texture times this, so each keeps its markings and reads as a glowing version of itself.
        /// </summary>
        private static readonly (string Suffix, Color Glow)[] Glows =
        {
            ("Brown", new Color(1f, 0.72f, 0.28f)),
            ("Purple", new Color(0.72f, 0.42f, 1f)),
            ("Red", new Color(1f, 0.28f, 0.3f)),
        };

        /// <summary>How hard the caps themselves glow. Above 1 so that bloom, where there is any, catches them.</summary>
        private const float EmissionIntensity = 2.5f;

        // The light each mushroom shines on the ground and on whatever stands near it, in its own glow
        // colour. The world has no sun, so this is a good part of what there is to see by. Shadowless:
        // a shadow map per mushroom is not affordable, and a glow that fills in round the stems is the
        // look wanted anyway. The reach is per unit of scale, so a mushroom planted at 3x lights 6.6 m
        // and one at 8x lights 17.6 m.
        private const float LightIntensity = 12f;
        private const float LightRangePerScale = 2.2f;

        /// <summary>One bare-handed hit does 12, so this is what "a basic hit destroys it" means.</summary>
        private const int MushroomHealth = 10;

        /// <summary>Light a mushroom gives up. One orb is one light, so this is orbs.</summary>
        private const int OrbsPerMushroom = 5;

        // The pack's mushrooms are 16 cm tall, and grown by this much they stand from knee to chest.
        private const float ScaleMin = 3f;
        private const float ScaleMax = 8f;
        private const float MaxTilt = 4f;
        private const float MaxSink = 0.1f;

        // How many there are. Every mushroom is a finite gift of light that does not grow back, so this
        // is the total healing the island offers: 30 groves of 5, at 5 light each, is 750 — some
        // twenty-five full lanterns. The pack's own layer planted over three thousand, which would have
        // been sixteen thousand light.
        private const int GroveCount = 30;
        private const float GroveRadius = 9f;
        private const int MushroomsPerGrove = 5;

        /// <summary>Closest two mushrooms in one grove stand: wider than the widest one at full size.</summary>
        private const float SpacingInGrove = 2.2f;

        [MenuItem("Tools/Eclipse/World/Build Glow Mushrooms")]
        private static void Build()
        {
            GameObject orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
            if (orbPrefab == null || !orbPrefab.TryGetComponent(out LightOrb orb))
            {
                Debug.LogError($"[GlowMushroom] No LightOrb prefab at {OrbPrefabPath}.");
                return;
            }

            Directory.CreateDirectory(PrefabFolder);

            List<GameObject> variants = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SourceFolder }))
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!TryGlowFor(source.name, out string suffix, out Color glow))
                {
                    Debug.LogWarning($"[GlowMushroom] '{source.name}' ends in no known colour; left out.");
                    continue;
                }

                Material material = EnsureGlowMaterial(suffix, glow, source);
                variants.Add(BuildVariant(source, material, glow, orb));
            }

            WriteLayer(variants);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GlowMushroom] Built {variants.Count} variants in {PrefabFolder} and pointed {LayerPath} at them. " +
                      "Bake Vegetation to plant them.");
        }

        private static bool TryGlowFor(string prefabName, out string suffix, out Color glow)
        {
            foreach ((string candidate, Color colour) in Glows)
            {
                if (!prefabName.EndsWith("_" + candidate)) continue;

                suffix = candidate;
                glow = colour;
                return true;
            }

            suffix = null;
            glow = default;
            return false;
        }

        /// <summary>The glowing copy of the pack's material for one colour, made once and never overwritten.</summary>
        private static Material EnsureGlowMaterial(string suffix, Color glow, GameObject source)
        {
            string path = $"{MaterialFolder}/GlowMushroom_{suffix}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material original = source.GetComponentInChildren<Renderer>().sharedMaterial;
            Material material = new Material(original);
            material.SetTexture("_EmissionMap", original.GetTexture("_BaseMap"));
            material.SetColor("_EmissionColor", glow * EmissionIntensity);

            // The pack's material is flagged as having black emission, which is what stops the keyword
            // below being kept; the Lit inspector clears the flag the same way when emission is turned on.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");

            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject BuildVariant(GameObject source, Material material, Color glow, LightOrb orb)
        {
            // An instance of the pack prefab, saved as a prefab, is a variant of it: the pack's mesh and
            // markings stay where they are and only what is added or overridden here is stored.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                }

                Bounds bounds = LocalRenderBounds(instance.transform);
                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;

                BuildLight(instance.transform, glow, bounds.center);

                HealthComponent health = instance.AddComponent<HealthComponent>();
                SerializedObject healthFields = new SerializedObject(health);
                SerializedFields.SetInt(healthFields, "maxHealth", MushroomHealth);
                SerializedFields.SetInt(healthFields, "currentHealth", MushroomHealth);
                healthFields.ApplyModifiedPropertiesWithoutUndo();

                OrbBurstOnDeath burst = instance.AddComponent<OrbBurstOnDeath>();
                SerializedObject burstFields = new SerializedObject(burst);
                SerializedFields.Set(burstFields, "health", health);
                SerializedFields.Set(burstFields, "orbPrefab", orb);
                SerializedFields.SetInt(burstFields, "minOrbs", OrbsPerMushroom);
                SerializedFields.SetInt(burstFields, "maxOrbs", OrbsPerMushroom);
                burstFields.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(instance, $"{PrefabFolder}/Glow{source.name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// The mushroom's own light, at the middle of its body so it reaches under the cap as well as
        /// out to the sides. <see cref="ScaledLight"/> sets its range from how big the mushroom is
        /// planted, so the range stored on the light itself is only a starting value.
        /// </summary>
        private static void BuildLight(Transform root, Color glow, Vector3 centre)
        {
            GameObject lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = centre;

            Light lamp = lightObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.shadows = LightShadows.None;
            lamp.color = glow;
            lamp.intensity = LightIntensity;
            lamp.range = LightRangePerScale;

            ScaledLight scaled = lightObject.AddComponent<ScaledLight>();
            SerializedObject scaledFields = new SerializedObject(scaled);
            SerializedFields.Set(scaledFields, "lamp", lamp);
            SerializedFields.SetFloat(scaledFields, "rangePerScale", LightRangePerScale);
            scaledFields.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The box round every renderer under <paramref name="root"/>, in the root's own space. Taken
        /// corner by corner, so a rotated or scaled child still gets a box that fits it.
        /// </summary>
        private static Bounds LocalRenderBounds(Transform root)
        {
            Bounds local = default;
            bool first = true;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                Bounds world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = world.center + Vector3.Scale(world.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    point = root.InverseTransformPoint(point);

                    if (first) local = new Bounds(point, Vector3.zero);
                    else local.Encapsulate(point);
                    first = false;
                }
            }

            return local;
        }

        private static void WriteLayer(List<GameObject> variants)
        {
            VegetationLayerAsset layer = AssetDatabase.LoadAssetAtPath<VegetationLayerAsset>(LayerPath);
            if (layer == null)
            {
                layer = ScriptableObject.CreateInstance<VegetationLayerAsset>();
                AssetDatabase.CreateAsset(layer, LayerPath);
            }

            SerializedObject fields = new SerializedObject(layer);
            SerializedFields.SetEnumByName(fields, "mode", nameof(VegetationDistribution.Cluster));
            SerializedFields.SetInt(fields, "clusterCount", GroveCount);
            SerializedFields.SetFloat(fields, "clusterRadius", GroveRadius);
            SerializedFields.SetInt(fields, "perCluster", MushroomsPerGrove);
            SerializedFields.SetFloat(fields, "minDistance", SpacingInGrove);

            SerializedProperty items = fields.FindProperty("items");
            items.arraySize = variants.Count;
            for (int index = 0; index < variants.Count; index++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("prefab").objectReferenceValue = variants[index];
                item.FindPropertyRelative("weight").floatValue = 1f;
                SetRange(item, "scale", ScaleMin, ScaleMax);
                SetRange(item, "yaw", 0f, 360f);
                SetRange(item, "tilt", 0f, MaxTilt);
                SetRange(item, "sink", 0f, MaxSink);
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRange(SerializedProperty item, string name, float min, float max)
        {
            SerializedProperty range = item.FindPropertyRelative(name);
            range.FindPropertyRelative("Min").floatValue = min;
            range.FindPropertyRelative("Max").floatValue = max;
        }
    }
}
