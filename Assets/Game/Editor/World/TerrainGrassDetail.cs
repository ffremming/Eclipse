using System.Collections.Generic;
using SpaceGame.Vegetation;
using UnityEditor;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Carpets a terrain in tall grass using its own detail layers.
    /// <para>
    /// Baked prefabs cannot do this. One grass clump per square metre over a 500 m island is a
    /// quarter of a million GameObjects and a scene file to match; the same grass as terrain detail
    /// is a density map the terrain draws instanced, costs a few kilobytes on disk, and is culled
    /// and batched by the terrain rather than by the scene.
    /// </para>
    /// <para>
    /// The prototypes are instanced and keep the prefab's own material, which is what lets the
    /// grass keep bending to <see cref="VegetationWind"/> — a vertex-lit detail would drop the wind
    /// shader and stand still.
    /// </para>
    /// </summary>
    public static class TerrainGrassDetail
    {
        private const string GrassFolder = "Assets/ThirdParty/BugWarNature/BugWar/Prefabs/Sandbox/LowPolyWind";

        /// <summary>Detail map cells across the terrain. One cell is about a metre at 500 m.</summary>
        private const int Resolution = 512;

        /// <summary>Cells per patch the terrain culls and draws as one unit.</summary>
        private const int ResolutionPerPatch = 16;

        /// <summary>Clumps per cell where the grass is thickest.</summary>
        private const int MaxPerCell = 5;

        /// <summary>
        /// Metres of ground one blob of grass covers. Wide, so a blob is a thicket to cross rather
        /// than a tuft to step over.
        /// </summary>
        private const float BlobSize = 64f;

        /// <summary>
        /// Share of the ground the grass covers at all. Under half, so open lanes run between the
        /// thickets and there is somewhere to see and fight as well as somewhere to hide.
        /// </summary>
        private const float Coverage = 0.4f;

        /// <summary>
        /// Blade height and width as multiples of the prefab, whose blades are 40-50 cm. These stand
        /// 1.2-2.2 m, over a goblin's head and the camera behind it, so a player in the grass is
        /// hidden and not merely partly covered. Width grows more slowly than height, or the
        /// blades would turn into paddles.
        /// </summary>
        private const float MinHeight = 3f;
        private const float MaxHeight = 4.5f;
        private const float MinWidth = 1.6f;
        private const float MaxWidth = 2.6f;

        /// <summary>
        /// How far from the camera the terrain still draws detail, in metres. Taller grass pops in
        /// more visibly than short, so this reaches further than the terrain's default of 80.
        /// </summary>
        private const float DrawDistance = 120f;

        /// <summary>Steepest ground grass grows on, in degrees.</summary>
        private const float MaxSlope = 38f;

        /// <summary>
        /// Fills the terrain's detail layers with the wind pack's grass.
        /// </summary>
        /// <param name="minAltitude">World height the grass starts at, so beaches stay bare.</param>
        public static void Paint(Terrain terrain, float minAltitude, int seed)
        {
            TerrainData data = terrain.terrainData;
            List<GameObject> prefabs = GrassPrefabs();
            if (prefabs.Count == 0)
            {
                Debug.LogWarning("[Vegetation] No grass prefabs under " + GrassFolder);
                return;
            }

            data.SetDetailResolution(Resolution, ResolutionPerPatch);
            data.detailPrototypes = Prototypes(prefabs);
            data.wavingGrassStrength = 0f; // The wind lives in the shader, not in Unity's grass waving.
            terrain.detailObjectDistance = DrawDistance;

            for (int layer = 0; layer < prefabs.Count; layer++)
            {
                data.SetDetailLayer(0, 0, layer, Density(data, terrain.transform.position.y, minAltitude,
                                                         seed + layer * 7919, layer, prefabs.Count));
            }

            EditorUtility.SetDirty(data);
        }

        private static List<GameObject> GrassPrefabs()
        {
            List<GameObject> prefabs = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { GrassFolder }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.name.Contains("Grass")) prefabs.Add(prefab);
            }

            return prefabs;
        }

        private static DetailPrototype[] Prototypes(List<GameObject> prefabs)
        {
            DetailPrototype[] prototypes = new DetailPrototype[prefabs.Count];
            for (int index = 0; index < prefabs.Count; index++)
            {
                prototypes[index] = new DetailPrototype
                {
                    prototype = prefabs[index],
                    renderMode = DetailRenderMode.VertexLit,
                    useInstancing = true,
                    usePrototypeMesh = true,
                    minWidth = MinWidth,
                    maxWidth = MaxWidth,
                    minHeight = MinHeight,
                    maxHeight = MaxHeight,
                    noiseSeed = 1 + index,
                    noiseSpread = 0.2f,
                    alignToGround = 0.4f,
                    useDensityScaling = true,
                    healthyColor = Color.white,
                    dryColor = Color.white,
                };
            }

            return prototypes;
        }

        /// <summary>
        /// How many clumps of one kind stand in each cell: thick inside the noise blobs, none on the
        /// beaches or the steep faces. Each kind gets its own blobs, so the kinds mix in bands
        /// instead of every cell holding one of each.
        /// </summary>
        private static int[,] Density(TerrainData data, float terrainBaseY, float minAltitude,
                                      int seed, int layer, int layerCount)
        {
            int[,] density = new int[Resolution, Resolution];
            float frequency = 1f / BlobSize;
            float step = data.size.x / (Resolution - 1);

            for (int z = 0; z < Resolution; z++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float u = x / (float)(Resolution - 1);
                    float v = z / (float)(Resolution - 1);

                    if (terrainBaseY + data.GetInterpolatedHeight(u, v) < minAltitude) continue;
                    if (Vector3.Angle(data.GetInterpolatedNormal(u, v), Vector3.up) > MaxSlope) continue;

                    float blob = ValueNoise.Fractal(x * step * frequency, z * step * frequency, seed, 3, 0.5f);
                    if (blob < 1f - Coverage) continue;

                    // Detail maps are indexed [z, x], as the heightmap is.
                    float strength = Mathf.InverseLerp(1f - Coverage, 1f, blob);
                    density[z, x] = Mathf.Max(1, Mathf.RoundToInt(strength * MaxPerCell / layerCount));
                }
            }

            return density;
        }
    }
}
