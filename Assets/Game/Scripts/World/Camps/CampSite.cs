// Where a camp of enemies actually settles.
//
// The world is generated from a seed, so a camp cannot simply be dropped at a hand-picked
// coordinate the way a castle can — the castles stamp a flat shelf into the heightmap and stand on
// what they made, and a camp stamps nothing. Its nominal centre is a wish, and the ground under it
// is whatever the noise produced: a cliff, a beach, or the sea.
//
// So the placement is a search. The camp is asked for near where the designer wanted it, and it
// takes the nearest ground that is above the water and flat enough to stand a fight on.
//
// A plain class with the ground handed to it, rather than a loop inside the builder holding a
// Terrain, for the usual reason: "the camp ended up in the sea" is not visible in a diff and is
// only visible in the scene if you happen to swim out there. Here it is a test.
using System;
using UnityEngine;

namespace SpaceGame.World
{
    /// <summary>What the ground is doing at a point: how high, and how steep.</summary>
    public readonly struct Ground
    {
        public readonly float Height;

        /// <summary>Slope in degrees. 0 is flat.</summary>
        public readonly float Steepness;

        public Ground(float height, float steepness)
        {
            Height = height;
            Steepness = steepness;
        }
    }

    public static class CampSite
    {
        /// <summary>
        /// The golden angle, as <c>GarrisonLayout</c> uses it and for the same reason: successive
        /// candidates never line up into spokes, so the search covers the disc instead of walking
        /// out along a handful of rays.
        /// </summary>
        private const float GoldenAngle = 2.39996323f;

        /// <summary>How many places are tried before the search gives up.</summary>
        private const int Candidates = 96;

        /// <summary>
        /// The closest spot to <paramref name="nominal"/>, within <paramref name="searchRadius"/>
        /// metres, that is above <paramref name="minHeight"/> and no steeper than
        /// <paramref name="maxSteepness"/> degrees.
        /// <para>
        /// Candidates are tried from the centre outwards, so a camp lands where it was asked for
        /// whenever that ground is usable and only wanders when it is not.
        /// </para>
        /// </summary>
        /// <returns>False when nothing within the radius is standable — the caller should say so
        /// rather than place a camp underwater.</returns>
        public static bool TryFind(Vector2 nominal, float searchRadius, Func<Vector2, Ground> ground,
                                   float minHeight, float maxSteepness, out Vector2 site)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));

            site = nominal;
            float radius = Mathf.Max(0f, searchRadius);

            for (int index = 0; index < Candidates; index++)
            {
                // Square-rooted, so the candidates thin out towards the edge instead of crowding
                // it — the same reason GarrisonLayout spaces its ring by area.
                float distance = Mathf.Sqrt(index / (float)(Candidates - 1)) * radius;
                float angle = index * GoldenAngle;
                Vector2 candidate = nominal + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                Ground here = ground(candidate);
                if (here.Height < minHeight || here.Steepness > maxSteepness) continue;

                site = candidate;
                return true;
            }

            return false;
        }
    }
}
