using System.Collections.Generic;
using SpaceGame.Castle;
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
        /// <param name="sites">
        /// Flat shelves to stamp into the land for buildings to stand on, or null for bare island.
        /// Applied after the noise and the shore falloff, so a shelf is the last word on its own
        /// ground — see <see cref="CastleSite"/>.
        /// </param>
        public static float[,] Heights(int resolution, float sizeMetres, int seed,
                                       IReadOnlyList<CastleSite> sites = null)
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
                    heights[z, x] = Flatten(worldX, worldZ, land * Falloff(worldX, worldZ, sizeMetres), sites);
                }
            }

            return heights;
        }

        /// <summary>
        /// The height a point ends up at once every building's shelf has had its say.
        /// <para>
        /// Sites are applied in order and each one takes the height it wants, so two that overlap
        /// leave the later one's shelf intact rather than averaging into a shelf that is level with
        /// neither building. Overlapping sites are a level-design mistake rather than a case to
        /// support, and this way it is a visible one.
        /// </para>
        /// </summary>
        private static float Flatten(float worldX, float worldZ, float height,
                                     IReadOnlyList<CastleSite> sites)
        {
            if (sites == null) return height;

            for (int index = 0; index < sites.Count; index++)
            {
                CastleSite site = sites[index];
                if (site.Touches(worldX, worldZ)) height = site.Reshape(worldX, worldZ, height);
            }

            return height;
        }

        /// <summary>
        /// The highest the land gets inside a circle, in the heightmap's 0..1.
        /// <para>
        /// What a castle's shelf height is chosen from: putting the shelf at the top of what is
        /// already there turns the hill the noise happened to make into the hill the castle stands
        /// on, instead of stamping a plateau through the side of it.
        /// </para>
        /// </summary>
        public static float HighestWithin(Vector2 centre, float radius, float sizeMetres, int seed,
                                          int samples = 64)
        {
            float highest = 0f;

            for (int ring = 0; ring <= samples; ring++)
            {
                for (int step = 0; step < samples; step++)
                {
                    float distance = radius * ring / samples;
                    float angle = step / (float)samples * Mathf.PI * 2f;
                    float x = centre.x + Mathf.Cos(angle) * distance;
                    float z = centre.y + Mathf.Sin(angle) * distance;

                    float hills = ValueNoise.Fractal(x / HillSize, z / HillSize, seed, 4, 0.5f);
                    float detail = ValueNoise.Fractal(x / DetailSize, z / DetailSize, seed + 101, 3, 0.45f);
                    float land = Mathf.Lerp(hills, detail, DetailWeight);

                    highest = Mathf.Max(highest, land * Falloff(x, z, sizeMetres));
                }
            }

            return highest;
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
