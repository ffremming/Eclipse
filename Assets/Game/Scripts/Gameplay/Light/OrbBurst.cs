// The throws that scatter a burst of orbs out of whatever dropped them.
//
// Thrown at evenly spaced angles rather than in random directions: a random spread will now and
// then send two orbs the same way, and the player sees one orb where the game paid out two. Even
// angles make that impossible. What varies is how hard each is thrown — stepped along a golden-ratio
// sequence so that neighbours never share a distance and the burst reads as scattered rather than
// as a wreath.
using UnityEngine;

namespace SpaceGame.Gameplay
{
    public static class OrbBurst
    {
        /// <summary>Fractional part of 1/phi. Successive multiples of it never bunch up on [0, 1).</summary>
        private const float GoldenRatioConjugate = 0.61803398875f;

        /// <summary>
        /// Launch velocities for <paramref name="count"/> orbs, one per orb, for <c>LightOrb.Launch</c>.
        /// Each is thrown <paramref name="lift"/> upwards and sideways at a speed between
        /// <paramref name="minSpeed"/> and <paramref name="maxSpeed"/>. The whole pattern is turned by
        /// <paramref name="startAngleDegrees"/>, which is where the randomness of one burst against the
        /// next belongs.
        /// </summary>
        public static Vector3[] Velocities(int count, float minSpeed, float maxSpeed, float lift,
                                           float startAngleDegrees)
        {
            if (count <= 0) return System.Array.Empty<Vector3>();

            Vector3[] velocities = new Vector3[count];
            float step = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = (startAngleDegrees + step * i) * Mathf.Deg2Rad;
                float speed = Mathf.Lerp(minSpeed, maxSpeed, Mathf.Repeat(GoldenRatioConjugate * i, 1f));
                velocities[i] = new Vector3(Mathf.Sin(angle) * speed, lift, Mathf.Cos(angle) * speed);
            }

            return velocities;
        }
    }
}
