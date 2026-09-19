using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// The orb of light that forms over a lighthouse once its beacon is struck.
    /// <para>
    /// A thing of its own rather than the <c>LightOrb</c> pickup blown up large, which is what
    /// <c>Lightfall</c> used to spawn. That was wrong in every way that mattered: the pickup falls
    /// under gravity, so the dawn slid off the tower; it destroys itself after six seconds in the
    /// air; and it is collectable, so the one moment the game is building towards ended with the
    /// player walking into it for a single point of light.
    /// </para>
    /// <para>
    /// It shares the <c>SpaceGame/Light/LightOrb</c> shader with the pickup, and that IS the point
    /// — the small orbs an enemy sheds and the sun that ends the eclipse are the same substance,
    /// one of them enormous. Additive and writing no depth, so it lies over the tower rather than
    /// hiding it.
    /// </para>
    /// </summary>
    public static class LightfallOrbBuilder
    {
        public const string PrefabPath = "Assets/Game/Prefabs/World/LightfallOrb.prefab";

        private const string MaterialPath = "Assets/Game/Art/Materials/Light/LightfallOrb.mat";
        private const string OrbShader = "SpaceGame/Light/LightOrb";

        /// <summary>Unity's built-in sphere mesh, which the orb is drawn on.</summary>
        private const string SphereMesh = "Sphere.fbx";

        /// <summary>
        /// How far the orb's own light carries, in metres. Enough to reach down the tower and onto
        /// the ground at its foot, so the player returned there can see what is lighting them — and
        /// no further, because how far a lit castle reaches is <c>Lightfall</c>'s decision to make
        /// and it makes a different one for each of the two.
        /// </summary>
        private const float GlowRange = 60f;

        /// <summary>
        /// What the orb's light settles at. <c>Lightfall</c> ramps it up from nothing over the
        /// rise, so this is the end of the dawn rather than the start of it.
        /// </summary>
        private const float GlowIntensity = 18f;

        /// <summary>
        /// The colour of the thing that undoes the eclipse: the player's own warm light, not the
        /// turquoise fringe the small orbs carry. Kept in step with <c>TowerBeacon</c>'s core
        /// colour, so the core the player struck and the sun it becomes are one light.
        /// </summary>
        private static readonly Color GlowColour = new Color(1f, 0.93f, 0.72f);

        [MenuItem("Tools/Eclipse/World/Build Lightfall Orb")]
        private static void BuildFromMenu()
        {
            if (Build() != null) Debug.Log($"[Lightfall] Built the orb at {PrefabPath}.");
        }

        /// <summary>Builds the orb prefab, replacing any previous copy. Null when it cannot be.</summary>
        public static GameObject Build()
        {
            Material material = EnsureMaterial();
            if (material == null) return null;

            // Asked for by name rather than made with CreatePrimitive, so the prefab references
            // Unity's own mesh instead of saving a copy of it into the asset.
            Mesh sphere = Resources.GetBuiltinResource<Mesh>(SphereMesh);
            if (sphere == null)
            {
                Debug.LogError("[Lightfall] Unity's built-in sphere mesh could not be loaded, so " +
                               "the orb has nothing to be drawn on.");
                return null;
            }

            var root = new GameObject("LightfallOrb");

            // The body is a child at unit scale so the ROOT's scale is the orb's diameter in
            // metres — which is what Lightfall grows, and what anyone reading its orbScale in the
            // Inspector will assume the number means.
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = sphere;

            MeshRenderer renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // No collider anywhere on it. The orb hangs over the deck the player is standing on,
            // and one they could be shoved off the tower by is not a reward.
            ItemBuilderKit.AddPointLight(root.transform, "Glow", GlowColour, GlowRange,
                                         GlowIntensity, LightShadows.None);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            return prefab;
        }

        /// <summary>
        /// The orb's material: the pickup's shader, turned up and thinned out. A low density is
        /// what stops a twenty-metre ball reading as a solid white disc — at the pickup's own
        /// density anything this large saturates long before its edge.
        /// </summary>
        private static Material EnsureMaterial()
            => ItemBuilderKit.EnsureMaterial(MaterialPath, OrbShader, material =>
            {
                material.SetFloat("_Intensity", 3.2f);
                material.SetFloat("_Density", 0.55f);
                material.SetFloat("_FringePower", 1.4f);
                material.SetFloat("_FilamentScale", 3.5f);
                material.SetFloat("_FilamentSpeed", 0.35f);
                material.SetFloat("_FilamentAmount", 0.3f);
                material.SetFloat("_PulsePeriod", 3.4f);
                material.SetFloat("_PulseDepth", 0.12f);
            });
    }
}
