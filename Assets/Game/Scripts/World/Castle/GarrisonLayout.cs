using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Castle
{
    /// <summary>
    /// Where a castle's garrison stands: an even spread over the ground between the keep and the
    /// outer wall.
    /// <para>
    /// A plain class rather than a loop in the builder because "fifteen enemies inside the castle"
    /// is a design decision with a right and a wrong answer — fifteen of them in one corner is not
    /// a garrison, and neither is fifteen standing inside the keep's walls. Both are easy to
    /// produce with random points and neither is visible in a diff.
    /// </para>
    /// <para>
    /// Laid out on a golden-angle spiral rather than by sampling random points and rejecting the
    /// ones that land too close. Rejection sampling can fail — it has no answer for "the annulus is
    /// too small for this many at this spacing" except to loop until it gives up, and what it gives
    /// up and returns is a garrison one creature short that nobody notices until the count matters.
    /// The spiral always returns exactly what was asked for, and spacing falls out of the geometry
    /// instead of being searched for.
    /// </para>
    /// </summary>
    public static class GarrisonLayout
    {
        /// <summary>
        /// The golden angle in radians. Successive points on a spiral separated by this angle never
        /// line up into spokes, which is the whole reason for using it — any rational fraction of a
        /// turn produces visible rows, and creatures standing in rows read as a formation.
        /// </summary>
        private const float GoldenAngle = 2.39996323f;

        /// <summary>
        /// <paramref name="count"/> positions spread over the ring between
        /// <paramref name="innerRadius"/> and <paramref name="outerRadius"/>, on the XZ plane and
        /// relative to the castle's centre.
        /// <para>
        /// Even by AREA, not by radius: stepping the radius linearly puts the same number of them
        /// on the narrow inner ring as on the wide outer one, which crowds the keep's door and
        /// leaves the far side of the courtyard empty.
        /// </para>
        /// </summary>
        /// <param name="jitter">
        /// Metres of random offset per creature, so a garrison does not read as a pattern. Seeded,
        /// so the same castle is laid out the same way on every build.
        /// </param>
        public static IReadOnlyList<Vector2> Ring(int count, float innerRadius, float outerRadius,
                                                  float jitter, int seed)
        {
            var positions = new List<Vector2>(Mathf.Max(count, 0));
            if (count <= 0) return positions;

            float inner = Mathf.Max(Mathf.Min(innerRadius, outerRadius), 0f);
            float outer = Mathf.Max(innerRadius, outerRadius);

            // Its own generator rather than UnityEngine.Random, which is global state: seeding that
            // to lay out a castle changes what every other system's next random number is.
            var random = new System.Random(seed);

            for (int index = 0; index < count; index++)
            {
                // Mid-cell sampling: (index + 0.5) / count rather than index / count, so the first
                // creature is not pinned to the inner edge and the last to the outer one.
                float areaFraction = (index + 0.5f) / count;
                float radius = Mathf.Sqrt(Mathf.Lerp(inner * inner, outer * outer, areaFraction));
                float angle = index * GoldenAngle;

                Vector2 spot = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                positions.Add(spot + Offset(random, jitter));
            }

            return positions;
        }

        /// <summary>
        /// Which of <paramref name="count"/> defenders are carrying something, as indices into the
        /// layout.
        /// <para>
        /// Chosen rather than rolled per creature, and that is the whole point. A drop CHANCE on
        /// each one means a player can clear the entire garrison and come away with nothing, and the
        /// key they need to go on simply is not in the world — a dead end with no way to tell it
        /// from "keep looking". Picking the bearers up front makes the key certain and its holder
        /// unknown, which is the part that was ever interesting.
        /// </para>
        /// <para>
        /// Deterministic from <paramref name="seed"/>, so the same castle hides the key on the same
        /// creature every time it is built.
        /// </para>
        /// </summary>
        public static IReadOnlyList<int> Bearers(int count, int bearers, int seed)
        {
            var chosen = new List<int>();
            if (count <= 0 || bearers <= 0) return chosen;

            // Every one of them, shuffled, then take the front. Sampling with rejection would be
            // the other way and it can pick the same one twice, which quietly produces fewer keys
            // than were asked for.
            var order = new List<int>(count);
            for (int index = 0; index < count; index++) order.Add(index);

            var random = new System.Random(seed);
            for (int index = count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                (order[index], order[swap]) = (order[swap], order[index]);
            }

            for (int index = 0; index < Mathf.Min(bearers, count); index++) chosen.Add(order[index]);
            return chosen;
        }

        /// <summary>A random offset inside a disc of <paramref name="radius"/>.</summary>
        private static Vector2 Offset(System.Random random, float radius)
        {
            if (radius <= 0f) return Vector2.zero;

            // Square-rooted so the offsets fill the disc evenly. Without it they bunch towards the
            // centre, which quietly undoes most of the jitter.
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
