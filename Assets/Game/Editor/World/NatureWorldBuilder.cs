using System.Collections.Generic;
using System.IO;
using SpaceGame.Vegetation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds the whole nature world from nothing: an island of generated terrain, a sea around it,
    /// a sun over it, and every vegetation layer planted on it.
    /// <para>
    /// The scene is an output, not a source: the numbers here and in <see cref="TerrainShape"/> are
    /// what is authored, and running the tool again rebuilds the same world from the same seed. So
    /// hand edits to the scene are lost on the next build — tune the seed and these constants, or
    /// the vegetation layer assets, rather than the scene.
    /// </para>
    /// </summary>
    public static class NatureWorldBuilder
    {
        private const string ScenePath = "Assets/Game/Scenes/World/NatureWorld.unity";
        private const string TerrainDataPath = "Assets/Game/Art/Terrain/NatureWorldTerrain.asset";
        private const string LayerFolder = "Assets/Game/ScriptableObjects/Vegetation";

        private const int Seed = 20260918;
        private const float SizeMetres = 500f;
        private const float HeightMetres = 70f;
        private const int HeightmapResolution = 513;
        private const int AlphamapResolution = 512;

        /// <summary>World height of the sea, and so of the beach the planting keeps off.</summary>
        private const float SeaLevel = 7f;

        /// <summary>Slope, in degrees, above which the terrain shows bare rock.</summary>
        private const float RockSlope = 32f;

        /// <summary>How high above the ground the scene's camera stands.</summary>
        private const float EyeHeight = 2.5f;

        [MenuItem("Tools/Eclipse/World/Build Nature World")]
        private static void Build()
        {
            if (SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[NatureWorld] Save the open scene first — building replaces it.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Terrain terrain = BuildTerrain();
            BuildSea();
            BuildSun();
            BuildWind();
            BuildCamera();
            VegetationField field = BuildField(terrain);

            TerrainGrassDetail.Paint(terrain, SeaLevel + 1.5f, Seed);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            VegetationBaker.Bake(field);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log("[NatureWorld] Built " + ScenePath);
        }

        /// <summary>The island: heightmap, ground layers and the splat map that paints them.</summary>
        private static Terrain BuildTerrain()
        {
            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                data = new TerrainData();
                Directory.CreateDirectory(Path.GetDirectoryName(TerrainDataPath));
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            data.heightmapResolution = HeightmapResolution;
            data.size = new Vector3(SizeMetres, HeightMetres, SizeMetres);
            data.SetHeights(0, 0, TerrainShape.Heights(HeightmapResolution, SizeMetres, Seed));

            data.terrainLayers = TerrainGroundLayers.Load();
            data.alphamapResolution = AlphamapResolution;
            data.SetAlphamaps(0, 0, TerrainShape.SplatWeights(data, SeaLevel, RockSlope));
            EditorUtility.SetDirty(data);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Island";
            terrainObject.transform.position = new Vector3(-SizeMetres * 0.5f, 0f, -SizeMetres * 0.5f);

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
            AssetDatabase.AddObjectToAsset(terrain.materialTemplate, TerrainDataPath);
            return terrain;
        }

        /// <summary>
        /// The sea: one plane at <see cref="SeaLevel"/>, wider than the island so its rim is never
        /// in shot. It carries no collider, so a ground ray meets the terrain under it.
        /// </summary>
        private static void BuildSea()
        {
            GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            sea.name = "Sea";
            sea.transform.position = new Vector3(0f, SeaLevel, 0f);
            sea.transform.localScale = Vector3.one * (SizeMetres * 0.2f);
            Object.DestroyImmediate(sea.GetComponent<Collider>());

            Material water = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            water.color = new Color(0.13f, 0.30f, 0.38f);
            water.SetFloat("_Smoothness", 0.85f);
            sea.GetComponent<MeshRenderer>().sharedMaterial = water;
        }

        private static void BuildSun()
        {
            GameObject sun = new GameObject("Sun");
            sun.transform.rotation = Quaternion.Euler(48f, 35f, 0f);

            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// The wind the plant shaders bend to. Without one in the scene their global uniforms stay
        /// at zero, and the gust wavelength among them is a divisor.
        /// </summary>
        private static void BuildWind()
        {
            new GameObject("Wind").AddComponent<VegetationWind>();
        }

        /// <summary>A camera standing on the shore looking inland, so the scene opens on the island.</summary>
        private static void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = SizeMetres * 1.8f;

            Vector3 spot = new Vector3(-SizeMetres * 0.3f, HeightMetres, -SizeMetres * 0.3f);
            if (Physics.Raycast(spot + Vector3.up * HeightMetres, Vector3.down, out RaycastHit hit, HeightMetres * 3f))
            {
                spot = hit.point + Vector3.up * EyeHeight;
            }

            cameraObject.transform.SetPositionAndRotation(spot, Quaternion.Euler(6f, 45f, 0f));
        }

        /// <summary>The field that plants every starter layer over the island, above its beaches.</summary>
        private static VegetationField BuildField(Terrain terrain)
        {
            GameObject fieldObject = new GameObject("Vegetation Field");
            VegetationField field = fieldObject.AddComponent<VegetationField>();

            SerializedObject serialized = new SerializedObject(field);
            serialized.FindProperty("size").vector2Value = new Vector2(SizeMetres * 0.94f, SizeMetres * 0.94f);
            serialized.FindProperty("seed").intValue = Seed;
            serialized.FindProperty("groundMask").intValue = 1 << terrain.gameObject.layer;
            serialized.FindProperty("rayHeight").floatValue = HeightMetres * 2f;
            serialized.FindProperty("minAltitude").floatValue = SeaLevel + 1.5f;
            serialized.FindProperty("maxSlope").floatValue = RockSlope;
            serialized.FindProperty("alignToGround").floatValue = 0.4f;

            List<VegetationLayerAsset> layers = Layers();
            SerializedProperty list = serialized.FindProperty("layers");
            list.arraySize = layers.Count;
            for (int index = 0; index < layers.Count; index++)
            {
                list.GetArrayElementAtIndex(index).objectReferenceValue = layers[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return field;
        }

        private static List<VegetationLayerAsset> Layers()
        {
            List<VegetationLayerAsset> layers = new List<VegetationLayerAsset>();
            foreach (string guid in AssetDatabase.FindAssets("t:VegetationLayerAsset", new[] { LayerFolder }))
            {
                VegetationLayerAsset layer =
                    AssetDatabase.LoadAssetAtPath<VegetationLayerAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (layer != null) layers.Add(layer);
            }

            if (layers.Count == 0)
            {
                Debug.LogWarning("[NatureWorld] No vegetation layers in " + LayerFolder +
                                 " — run Create Starter Vegetation Layers first.");
            }

            return layers;
        }

        private static void AddToBuildSettings()
        {
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ScenePath) return;
            }

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes)
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
