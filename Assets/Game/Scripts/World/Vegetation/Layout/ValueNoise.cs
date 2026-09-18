using System;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// Seeded 2D value noise with fractal octaves, returning 0..1. Engine free, so layout code that
    /// uses it stays testable outside Unity, and deterministic for a given seed.
    /// </summary>
    public static class ValueNoise
    {
        /// <summary>Fractal sum of <paramref name="octaves"/> octaves, each half the size and quieter than the last.</summary>
        /// <param name="persistence">How much of the previous octave's amplitude the next one keeps.</param>
        public static float Fractal(float x, float y, int seed, int octaves, float persistence)
        {
            octaves = Math.Max(1, octaves);
            float total = 0f;
            float amplitude = 1f;
            float range = 0f;
            float frequency = 1f;

            for (int octave = 0; octave < octaves; octave++)
            {
                total += Sample(x * frequency, y * frequency, seed + octave * 7919) * amplitude;
                range += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }

            return total / range;
        }

        /// <summary>One octave: bilinear blend of the lattice corner values, smoothed by a cubic fade.</summary>
        public static float Sample(float x, float y, int seed)
        {
            int cellX = Floor(x);
            int cellY = Floor(y);
            float fadeX = Fade(x - cellX);
            float fadeY = Fade(y - cellY);

            float bottom = Lerp(Corner(cellX, cellY, seed), Corner(cellX + 1, cellY, seed), fadeX);
            float top = Lerp(Corner(cellX, cellY + 1, seed), Corner(cellX + 1, cellY + 1, seed), fadeX);
            return Lerp(bottom, top, fadeY);
        }

        private static float Corner(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return hash / (float)uint.MaxValue;
            }
        }

        private static int Floor(float value) => value >= 0f ? (int)value : (int)value - 1;

        private static float Fade(float t) => t * t * (3f - 2f * t);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
