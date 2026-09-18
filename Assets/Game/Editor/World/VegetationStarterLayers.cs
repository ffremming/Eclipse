using System.Collections.Generic;
using System.IO;
using SpaceGame.Vegetation;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Builds one starter layer per imported prop folder, so a field can be assembled and looked at
    /// before anything has been authored by hand.
    /// <para>
    /// The numbers here are a starting point, not a decision: a layer is an asset, and the point of
    /// it being an asset is that it is tuned in the inspector afterwards rather than in this file.
    /// </para>
    /// </summary>
    public static class VegetationStarterLayers
    {
        private const string PropsRoot = "Assets/ThirdParty/BugWarNature/BugWar/Prefabs/Sandbox";
        private const string OutputFolder = "Assets/Game/ScriptableObjects/Vegetation";

        /// <summary>
        /// folder under <see cref="PropsRoot"/>, layer name, mode, and that mode's shape numbers.
        /// <para>
        /// The undergrowth spacings are metres apart, not centimetres: every plant here is a baked
        /// GameObject, and a carpet at half-metre spacing is half a million of them over a 500 m
        /// island. The real carpet of grass is <see cref="TerrainGrassDetail"/>, drawn instanced by
        /// the terrain, and the grass layer here is only the taller clumps standing in it.
        /// </para>
        /// </summary>
        private static readonly Preset[] Presets =
        {
            new Preset("Foliage/Tree", "Trees", VegetationDistribution.Scatter, count: 70, minDistance: 14f),
            // Landmarks: fewer, further apart and half as big again as the model was built, so a
            // formation reads as an outcrop from across the island rather than as a boulder.
            new Preset("Foliage/RockFormation", "RockFormations", VegetationDistribution.Scatter, count: 90, minDistance: 16f,
                       scaleMin: 1.5f, scaleMax: 3.2f),
            new Preset("Foliage/Stump", "Stumps", VegetationDistribution.Scatter, count: 25, minDistance: 10f),
            new Preset("Foliage/Fern", "Ferns", VegetationDistribution.Patch, patchSize: 14f, coverage: 0.25f, spacing: 6f),
            new Preset("Foliage/Mushroom", "Mushrooms", VegetationDistribution.Patch, patchSize: 8f, coverage: 0.06f, spacing: 8f),
            new Preset("Foliage/GroundCover", "GroundCover", VegetationDistribution.Patch, patchSize: 14f, coverage: 0.3f, spacing: 4.5f),
            new Preset("LowPolyWind", "TallGrass", VegetationDistribution.Patch, patchSize: 22f, coverage: 0.55f, spacing: 2.6f,
                       nameContains: "Tall", scaleMin: 1.3f, scaleMax: 2.4f),
            new Preset("DistantTrees", "TreeClusters", VegetationDistribution.Cluster,
                       minDistance: 4.5f, clusterCount: 80, clusterRadius: 24f, perCluster: 30),
            new Preset("LowPolyWind", "Boulders", VegetationDistribution.Scatter, count: 30, minDistance: 11f,
                       nameContains: "Rock"),
        };

        [MenuItem("Tools/Eclipse/World/Create Starter Vegetation Layers")]
        private static void Create()
        {
            Directory.CreateDirectory(OutputFolder);

            foreach (Preset preset in Presets)
            {
                List<GameObject> prefabs = Prefabs(preset.Folder, preset.NameContains);
                if (prefabs.Count == 0)
                {
                    Debug.LogWarning("[Vegetation] No prefabs under " + PropsRoot + "/" + preset.Folder);
                    continue;
                }

                string path = OutputFolder + "/" + preset.Name + ".asset";
                VegetationLayerAsset layer = AssetDatabase.LoadAssetAtPath<VegetationLayerAsset>(path);
                if (layer == null)
                {
                    layer = ScriptableObject.CreateInstance<VegetationLayerAsset>();
                    AssetDatabase.CreateAsset(layer, path);
                }

                Write(layer, preset, prefabs);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Vegetation] Starter layers written to " + OutputFolder);
        }

        /// <summary>
        /// The preset's numbers onto the asset. Through <see cref="SerializedObject"/> because the
        /// fields are private and serialized, which is where they belong: the inspector owns them
        /// once this has run.
        /// </summary>
        private static void Write(VegetationLayerAsset layer, Preset preset, List<GameObject> prefabs)
        {
            SerializedObject asset = new SerializedObject(layer);
            asset.FindProperty("mode").enumValueIndex = (int)preset.Mode;
            asset.FindProperty("count").intValue = preset.Count;
            asset.FindProperty("minDistance").floatValue = preset.MinDistance;
            asset.FindProperty("patchSize").floatValue = preset.PatchSize;
            asset.FindProperty("coverage").floatValue = preset.Coverage;
            asset.FindProperty("spacing").floatValue = preset.Spacing;
            asset.FindProperty("clusterCount").intValue = preset.ClusterCount;
            asset.FindProperty("clusterRadius").floatValue = preset.ClusterRadius;
            asset.FindProperty("perCluster").intValue = preset.PerCluster;

            SerializedProperty items = asset.FindProperty("items");
            items.arraySize = prefabs.Count;
            for (int index = 0; index < prefabs.Count; index++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("prefab").objectReferenceValue = prefabs[index];
                item.FindPropertyRelative("weight").floatValue = 1f;
                Range(item, "scale", preset.ScaleMin, preset.ScaleMax);
                Range(item, "yaw", 0f, 360f);
                Range(item, "tilt", 0f, preset.Mode == VegetationDistribution.Scatter ? 4f : 10f);
                Range(item, "sink", 0f, 0.03f);
            }

            asset.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Range(SerializedProperty item, string name, float min, float max)
        {
            SerializedProperty range = item.FindPropertyRelative(name);
            range.FindPropertyRelative("Min").floatValue = min;
            range.FindPropertyRelative("Max").floatValue = max;
        }

        /// <summary>
        /// The prefabs of one folder, or only those whose name carries <paramref name="nameContains"/>
        /// where a folder holds two kinds of prop — the wind pack keeps its grass beside its rocks.
        /// </summary>
        private static List<GameObject> Prefabs(string folder, string nameContains)
        {
            List<GameObject> prefabs = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropsRoot + "/" + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                if (nameContains != null && !prefab.name.Contains(nameContains)) continue;
                prefabs.Add(prefab);
            }

            return prefabs;
        }

        private readonly struct Preset
        {
            public Preset(string folder, string name, VegetationDistribution mode,
                          int count = 0, float minDistance = 0f,
                          float patchSize = 0f, float coverage = 0f, float spacing = 0f,
                          string nameContains = null,
                          int clusterCount = 0, float clusterRadius = 20f, int perCluster = 0,
                          float scaleMin = 0.85f, float scaleMax = 1.25f)
            {
                NameContains = nameContains;
                ClusterCount = clusterCount;
                ClusterRadius = clusterRadius;
                PerCluster = perCluster;
                ScaleMin = scaleMin;
                ScaleMax = scaleMax;
                Folder = folder;
                Name = name;
                Mode = mode;
                Count = count;
                MinDistance = minDistance;
                PatchSize = patchSize;
                Coverage = coverage;
                Spacing = spacing;
            }

            /// <summary>Only prefabs carrying this in their name, or null for the whole folder.</summary>
            public string NameContains { get; }

            public string Folder { get; }
            public string Name { get; }
            public VegetationDistribution Mode { get; }
            public int Count { get; }
            public float MinDistance { get; }
            public float PatchSize { get; }
            public float Coverage { get; }
            public float Spacing { get; }
            public int ClusterCount { get; }
            public float ClusterRadius { get; }
            public int PerCluster { get; }

            /// <summary>Size range on the prefab, which is already at its life size.</summary>
            public float ScaleMin { get; }
            public float ScaleMax { get; }
        }
    }
}
