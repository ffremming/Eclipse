using System.IO;
using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the torch prefab and its inventory asset from the imported torch model.
    /// <para>
    /// Re-runnable, and it replaces the prefab wholesale — so every number that matters is a
    /// constant here rather than something tuned in the Inspector and lost on the next run.
    /// </para>
    /// </summary>
    public static class TorchBuilder
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Items/Artifacts/Gadgets";
        private const string PrefabPath = PrefabFolder + "/Torch.prefab";

        // Must live under Resources/Items: RegistryLoader does Resources.LoadAll there, and an asset
        // anywhere else never registers and comes back empty from every save that held it.
        private const string ItemFolder = "Assets/Game/Resources/Items/Artifacts";
        private const string ItemPath = ItemFolder + "/Torch.asset";

        private const string TorchModel = "Assets/Game/Art/Models/Items/Torch/torch.glb";
        private const string FlameMaterialPath = "Assets/Game/Art/Materials/Light/LightFlame.mat";
        private const string ArcMaterialPath = "Assets/Game/Art/Materials/Light/LightArc.mat";
        private const string OrbMaterialPath = "Assets/Game/Art/Materials/Light/LightOrb.mat";

        private const string FlameShader = "SpaceGame/Light/LightFlame";

        /// <summary>Length of the torch once held, in metres.</summary>
        private const float TorchLength = 0.62f;

        /// <summary>Height of the flame above the torch head, in metres.</summary>
        private const float FlameHeight = 0.34f;

        /// <summary>Width of the flame, in metres.</summary>
        private const float FlameWidth = 0.17f;

        /// <summary>Crossed quads the flame is drawn on. Three reads as volume from any angle.</summary>
        private const int FlameCards = 3;

        // The light. These are the numbers that decide whether the world is navigable, so they are
        // deliberately far larger than the first pass: a torch that lights two metres of ground is a
        // torch the player resents carrying. Swinging roughly doubles the reach, which is what makes
        // an attack also a way to see what is out there.
        private const float IdleRange = 26f;
        private const float IdleIntensity = 40f;
        private const float SwingRange = 46f;
        private const float SwingIntensity = 110f;

        /// <summary>
        /// The fill light: huge, dim, shadowless.
        /// <para>
        /// A point light falls off with the square of distance, so range is only where it is cut
        /// off, not how far it is useful — doubling the range of a single light does almost nothing
        /// to what the player can see at the far end of it. Two lights is the fix every game with a
        /// torch uses: a bright shadow-casting one for contact and shape near the player, and a
        /// wide, weak, shadowless one that lifts the whole surrounding area just off black.
        /// </para>
        /// </summary>
        private const float FillRange = 80f;
        private const float FillIdleIntensity = 15f;
        private const float FillSwingIntensity = 48f;

        /// <summary>
        /// The throw: a long spot down the player's view.
        /// <para>
        /// This is what distance actually comes from. A point light spreads its energy over the
        /// whole sphere around it, so its brightness falls with the square of distance and setting a
        /// bigger range only moves where it is cut off — it is already almost nothing out there. A
        /// spot puts the same energy through a cone instead, so for the same intensity it carries
        /// many times further. Every game where a torch lights a corridor is doing this.
        /// </para>
        /// </summary>
        private const float ThrowRange = 170f;
        private const float ThrowAngle = 74f;
        private const float ThrowInnerAngle = 26f;
        private const float ThrowIdleIntensity = 260f;
        private const float ThrowSwingIntensity = 700f;

        private const int GroundLayerMask = 128;

        [MenuItem("Tools/Eclipse/Items/Build Torch")]
        private static void Build()
        {
            Material flameMaterial = EnsureMaterial(FlameMaterialPath, FlameShader);
            Material arc = AssetDatabase.LoadAssetAtPath<Material>(ArcMaterialPath);
            if (flameMaterial == null || arc == null)
            {
                Debug.LogError($"[Torch] Missing {FlameMaterialPath} or {ArcMaterialPath}.");
                return;
            }

            GameObject root = new GameObject("Torch");
            Transform head = ModelMount.Mount(root.transform, TorchModel, TorchLength);
            StopModelShadowing(root);

            Material orb = AssetDatabase.LoadAssetAtPath<Material>(OrbMaterialPath);

            Renderer flame = BuildFlame(head, flameMaterial);
            Light light = BuildLight(head);
            Light fill = BuildFillLight(head);
            Light throwLight = BuildThrowLight(root.transform);
            TrailRenderer trail = BuildTrail(head, arc);
            Renderer burst = BuildBurst(head, orb);

            TorchArtifact torch = root.AddComponent<TorchArtifact>();
            Wire(torch, "flame", flame);
            Wire(torch, "burst", burst);
            Wire(torch, "tipLight", light);
            Wire(torch, "fill", fill);
            Wire(torch, "throwLight", throwLight);
            WireFloat(torch, "throwIdleIntensity", ThrowIdleIntensity);
            WireFloat(torch, "throwSwingIntensity", ThrowSwingIntensity);
            Wire(torch, "trail", trail);
            WireFloat(torch, "fillIdleIntensity", FillIdleIntensity);
            WireFloat(torch, "fillSwingIntensity", FillSwingIntensity);
            WireFloat(torch, "idleRange", IdleRange);
            WireFloat(torch, "idleIntensity", IdleIntensity);
            WireFloat(torch, "swingRange", SwingRange);
            WireFloat(torch, "swingIntensity", SwingIntensity);
            WireFloat(torch, "swingDuration", 0.5f);
            WireInt(torch, "damage", 16);

            Finish(root);
        }

        /// <summary>
        /// Stops the torch's own geometry casting shadows.
        /// <para>
        /// The light sits inside the torch head, so with shadows on the head occludes its own light
        /// and throws a hard wedge of shadow across the ground the player is standing on. The head
        /// still RECEIVES shadows from everything else, so it does not look unlit.
        /// </para>
        /// </summary>
        private static void StopModelShadowing(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// The flame: crossed quads, not a particle system.
        /// <para>
        /// A particle system would have to be re-tuned to match the flame shader's own motion, and
        /// the two would drift. The shader already animates rise, turbulence and licking tongues
        /// from its own UVs, so the geometry only has to give it somewhere to draw — and three
        /// crossed cards are enough for that to read as volume from any angle the player stands at.
        /// </para>
        /// </summary>
        private static Renderer BuildFlame(Transform head, Material material)
        {
            GameObject flameObject = new GameObject("Flame");
            flameObject.transform.SetParent(head, false);

            Renderer first = null;

            for (int i = 0; i < FlameCards; i++)
            {
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Card{i}";
                card.transform.SetParent(flameObject.transform, false);

                // Quad's pivot is its centre, so it is lifted by half its height to put v = 0 at the
                // wick. The shader measures the flame from the bottom of the UVs upwards.
                card.transform.localPosition = new Vector3(0f, FlameHeight * 0.5f, 0f);
                card.transform.localRotation = Quaternion.Euler(0f, 180f / FlameCards * i, 0f);
                card.transform.localScale = new Vector3(FlameWidth, FlameHeight, 1f);

                Object.DestroyImmediate(card.GetComponent<Collider>());

                MeshRenderer renderer = card.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (first == null) first = renderer;
            }

            return first;
        }

        /// <summary>
        /// The light, sitting inside the flame.
        /// <para>
        /// Shadows are on and this is the one carried light that casts them. A torch that casts no
        /// shadow lights the far side of every rock it stands behind, which turns a readable place
        /// into a flat wash — and in a world this dark, reading the place is the whole game.
        /// </para>
        /// </summary>
        private static Light BuildLight(Transform head)
        {
            GameObject lightObject = new GameObject("Flamelight");
            lightObject.transform.SetParent(head, false);
            lightObject.transform.localPosition = new Vector3(0f, FlameHeight * 0.45f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;

            // Bias pulled off the default because a light sitting inside its own geometry at this
            // range shadow-acnes the ground around the player otherwise.
            light.shadowBias = 0.1f;
            light.shadowNormalBias = 0.6f;
            light.range = IdleRange;
            light.intensity = IdleIntensity;
            return light;
        }

        /// <summary>
        /// The ball of light thrown off at the moment of a swing. Uses the orb material, so the
        /// burst is visibly the same light as a thrown projectile rather than a second effect that
        /// happens to be near it.
        /// </summary>
        private static Renderer BuildBurst(Transform head, Material orb)
        {
            GameObject burstObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burstObject.name = "Burst";
            burstObject.transform.SetParent(head, false);
            burstObject.transform.localPosition = new Vector3(0f, FlameHeight * 0.5f, 0f);
            burstObject.transform.localScale = Vector3.zero;
            Object.DestroyImmediate(burstObject.GetComponent<Collider>());

            MeshRenderer renderer = burstObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = orb;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Off until a swing switches it on, so an idle torch is not carrying an invisible
            // sphere through the depth pass.
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>
        /// The wide fill. No shadows on purpose — it is doing reach, not shape, and a second
        /// shadow-casting light at this radius costs as much as the rest of the frame.
        /// </summary>
        private static Light BuildFillLight(Transform head)
        {
            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(head, false);

            Light light = fillObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.range = FillRange;
            light.intensity = FillIdleIntensity;

            // Cooler and weaker than the flame, so the far reach reads as light bouncing around the
            // place rather than as a second torch hovering over the player.
            light.color = new Color(0.72f, 0.58f, 0.46f);
            return light;
        }

        /// <summary>
        /// The throw. Parented to the item ROOT rather than the head, because the torch's own
        /// rotation is whatever the swing animation is doing with the arm — and a cone welded to
        /// that sprays the landscape every time the player attacks. TorchArtifact points it down the
        /// camera instead.
        /// </summary>
        private static Light BuildThrowLight(Transform root)
        {
            GameObject throwObject = new GameObject("Throw");
            throwObject.transform.SetParent(root, false);

            Light light = throwObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = ThrowRange;
            light.spotAngle = ThrowAngle;
            light.innerSpotAngle = ThrowInnerAngle;
            light.intensity = ThrowIdleIntensity;

            // Shadowless. A spot shadow at this range needs a shadow map covering 170 metres, which
            // is both expensive and so low-resolution that what it draws is blocky rather than
            // informative. The flame's own point light is what puts shadows near the player.
            light.shadows = LightShadows.None;

            // Paler than the flame. Distant light from a fire has lost its warmth by the time it
            // arrives, and keeping the far reach cool also stops it competing with the flame for
            // the player's eye.
            light.color = new Color(0.78f, 0.66f, 0.55f);
            return light;
        }

        /// <summary>The arc of light the head drags through a swing.</summary>
        private static TrailRenderer BuildTrail(Transform head, Material arc)
        {
            GameObject trailObject = new GameObject("Trail");
            trailObject.transform.SetParent(head, false);

            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = arc;
            trail.time = 0.3f;
            trail.widthMultiplier = 0.42f;
            trail.minVertexDistance = 0.03f;
            trail.emitting = false;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        private static void Finish(GameObject root)
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

            ItemGrip itemGrip = root.AddComponent<ItemGrip>();
            Wire(itemGrip, "gripPoint", grip.transform);
            WireEnum(itemGrip, "holdStyle", (int)ItemGrip.HoldStyle.OneHanded);
            WireFloat(itemGrip, "holdSize", TorchLength);

            root.AddComponent<PickupableItem>();

            Directory.CreateDirectory(PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            InventoryItem item = EnsureItemAsset(prefab);

            // The back-reference, which can only be made once both files exist.
            Wire(prefab.GetComponent<PickupableItem>(), "item", item);
            PrefabUtility.SavePrefabAsset(prefab);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Torch] Built {PrefabPath}.");
        }

        /// <summary>Loads a material, creating it from its shader the first time. Never overwrites.</summary>
        private static Material EnsureMaterial(string path, string shaderName)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Torch] Shader '{shaderName}' not found.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Material created = new Material(shader);
            AssetDatabase.CreateAsset(created, path);
            return created;
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
