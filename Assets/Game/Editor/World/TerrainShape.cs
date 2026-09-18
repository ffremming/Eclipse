using SpaceGame.Vegetation;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// The shape of a generated island: its heightmap and which ground covers which part of it.
    /// <para>
    /// Kept apart from <see cref="NatureWorldBuilder"/>, which assembles the scene, so the land can
    /// be reshaped without touching the wiring around it. The noise is the same
    /// <see cref="ValueNoise"/> the vegetation layouts use, so a seed shapes the land and the
    /// planting together.
    /// </para>
    /// </summary>
    public static class TerrainShape
    {
        /// <summary>Large rolling shapes: one hill is about this many metres across.</summary>
        private const float HillSize = 140f;

        /// <summary>Smaller bumps laid over the hills, so a slope is never a clean ramp.</summary>
        private const float DetailSize = 28f;
        private const float DetailWeight = 0.12f;

        /// <summary>Where the island stops: beyond this share of the half-width the land falls away.</summary>
        private const float ShoreStart = 0.62f;

        /// <summary>
        /// A square heightmap, 0..1, of an island that falls to sea level at its rim.
        /// </summary>
        /// <param name="resolution">Heightmap resolution, which Unity wants as a power of two plus one.</param>
        /// <param name="sizeMetres">Side length of the terrain, so the noise is sized in metres rather than samples.</param>
        public static float[,] Heights(int resolution, float sizeMetres, int seed)
        {
            float[,] heights = new float[resolution, resolution];
            float step = sizeMetres / (resolution - 1);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = x * step;
                    float worldZ = z * step;

                    float hills = ValueNoise.Fractal(worldX / HillSize, worldZ / HillSize, seed, 4, 0.5f);
                    float detail = ValueNoise.Fractal(worldX / DetailSize, worldZ / DetailSize, seed + 101, 3, 0.45f);
                    float land = Mathf.Lerp(hills, detail, DetailWeight);

                    // Unity indexes a heightmap [z, x]; swapping them mirrors the island against
                    // the splat map and the vegetation, which both work in world metres.
                    heights[z, x] = land * Falloff(worldX, worldZ, sizeMetres);
                }
            }

            return heights;
        }

        /// <summary>
        /// The splat weights per control point: rock on the steep faces, sand along the shore,
        /// grass everywhere else. Indexed [z, x, layer] as Unity wants it.
        /// </summary>
        /// <param name="shoreHeight">World height, in metres, that the beach reaches up to.</param>
        /// <param name="rockSlope">Slope, in degrees, above which bare rock shows through.</param>
        public static float[,,] SplatWeights(TerrainData data, float shoreHeight, float rockSlope)
        {
            int resolution = data.alphamapResolution;
            float[,,] weights = new float[resolution, resolution, 3];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x / (float)(resolution - 1);
                    float v = z / (float)(resolution - 1);

                    float height = data.GetInterpolatedHeight(u, v);
                    float slope = Vector3.Angle(data.GetInterpolatedNormal(u, v), Vector3.up);

                    float rock = Mathf.InverseLerp(rockSlope - 6f, rockSlope + 6f, slope);
                    float sand = Mathf.InverseLerp(shoreHeight + 2.5f, shoreHeight - 0.5f, height) * (1f - rock);

                    weights[z, x, 0] = Mathf.Max(0f, 1f - rock - sand);
                    weights[z, x, 1] = rock;
                    weights[z, x, 2] = sand;
                }
            }

            return weights;
        }

        /// <summary>
        /// How much land is left at a point: 1 inland, easing to 0 at the rim, so the map ends in
        /// a shore rather than in a wall at the terrain's edge.
        /// </summary>
        private static float Falloff(float worldX, float worldZ, float sizeMetres)
        {
            float half = sizeMetres * 0.5f;
            float dx = Mathf.Abs(worldX - half) / half;
            float dz = Mathf.Abs(worldZ - half) / half;
            float edge = Mathf.Max(dx, dz);

            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ShoreStart, 1f, edge));
        }
    }
}
