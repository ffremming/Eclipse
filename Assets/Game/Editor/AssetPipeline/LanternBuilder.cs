using SpaceGame.Items;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the lantern prefab and its inventory asset from the imported lantern model.
    /// <para>
    /// Re-runnable, and it replaces the prefab wholesale — so every number that matters is a
    /// constant here rather than something tuned in the Inspector and lost on the next run.
    /// </para>
    /// <para>
    /// The prefab is a root, a <c>Hanging</c> transform at the origin, and the model and the light
    /// under that. The origin is the top of the carrying loop, which is where the hand closes, and
    /// <c>Hanging</c> is what <see cref="LanternArtifact"/> keeps upright, so the model swings from
    /// the hand rather than being fixed to it.
    /// </para>
    /// <para>
    /// The glass is found by the name the export gives it, <c>Glass</c>, and everything else in the
    /// model is frame. Both are contracts with <c>lantern_export.py</c>.
    /// </para>
    /// </summary>
    public static class LanternBuilder
    {
        private const string PrefabFolder = "Assets/Game/Prefabs/Items/Artifacts/Gadgets";
        private const string ItemName = "Lantern";

        private const string ModelPath = "Assets/Game/Art/Models/Items/Lantern/lantern.fbx";
        private const string GlassMaterialPath = "Assets/Game/Art/Materials/Items/LanternGlass.mat";
        private const string FrameMaterialPath = "Assets/Game/Art/Materials/Items/LanternFrame.mat";

        private const string GlassNodeName = "Glass";

        /// <summary>Loop to spike, in metres. The source is nine units tall; this is a hand lantern.</summary>
        private const float Height = 0.36f;

        // From lantern.blend: warm orange glass over a near-black frame.
        private static readonly Color GlassBodyColor = new Color(0.8f, 0.42f, 0.01f);
        private static readonly Color GlassGlowColor = new Color(1f, 0.52f, 0.29f);
        private static readonly Color FrameColor = new Color(0.04f, 0.04f, 0.07f);
        private const float GlowStrength = 1.0f;

        // Wider and steadier than the sword's glow, well short of the torch: it lights the walking,
        // and the torch is still what to reach for to see across a field.
        private static readonly Color LightColor = new Color(1f, 0.62f, 0.32f);
        private const float LitIntensity = 30f;
        private const float LightRange = 20f;

        [MenuItem("Tools/Eclipse/Items/Build Lantern")]
        private static void Build()
        {
            Material glassMaterial = ItemBuilderKit.EnsureLitMaterial(
                GlassMaterialPath, GlassBodyColor, GlassGlowColor * GlowStrength);
            Material frameMaterial = ItemBuilderKit.EnsureLitMaterial(
                FrameMaterialPath, FrameColor, Color.black);
            if (glassMaterial == null || frameMaterial == null) return;

            GameObject root = new GameObject(ItemName);

            Transform hanging = new GameObject("Hanging").transform;
            hanging.SetParent(root.transform, false);

            Transform model = ModelMount.MountAt(hanging, ModelPath, Height, ModelMount.Anchor.Top);

            Renderer glass = null;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                bool isGlass = renderer.name == GlassNodeName;
                renderer.sharedMaterial = isGlass ? glassMaterial : frameMaterial;
                if (isGlass) glass = renderer;
            }

            if (glass == null)
            {
                Object.DestroyImmediate(root);
                Debug.LogError($"[Lantern] {ModelPath} has no '{GlassNodeName}' mesh. Re-run lantern_export.py.");
                return;
            }

            ItemBuilderKit.StopCastingShadows(root);

            // In the middle of the glass, wherever a re-export puts it.
            Light flame = ItemBuilderKit.AddPointLight(hanging, "Flame", LightColor, LightRange,
                                                       LitIntensity, LightShadows.Soft);
            flame.transform.position = glass.bounds.center;

            // The one carried light of its kind that casts shadows, like the torch, and for the same
            // reason: a lantern that lights the far side of every rock flattens the place it is
            // meant to make readable. Biased as the torch's is, because it too sits inside its own
            // geometry.
            flame.shadowStrength = 0.8f;
            flame.shadowBias = 0.1f;
            flame.shadowNormalBias = 0.6f;

            LanternArtifact lantern = root.AddComponent<LanternArtifact>();
            ItemBuilderKit.Wire(lantern, "flame", flame);
            ItemBuilderKit.WireFloat(lantern, "litIntensity", LitIntensity);
            ItemBuilderKit.Wire(lantern, "glass", glass);
            ItemBuilderKit.WireColor(lantern, "glowColor", GlassGlowColor * GlowStrength);
            ItemBuilderKit.Wire(lantern, "hanging", hanging);

            // Its body hangs below the origin, and that is where a dropped lantern has to be
            // grabbed and where it has to rest.
            ItemBuilderKit.SavePickupable(root, PrefabFolder, ItemName, Height, new Vector3(0f, -Height * 0.5f, 0f));
            Debug.Log($"[Lantern] Built {PrefabFolder}/{ItemName}.prefab.");
        }
    }
}
