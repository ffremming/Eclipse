// How a burst of orbs is thrown.
//
// The failure worth pinning is two orbs leaving the same way: the player sees one orb where the
// game paid out two. The count matters as much — a burst that throws one fewer than it was asked to
// quietly shortchanges the reward.
//
// In Editor/ for the same reason StrideRateTests is: OrbBurst lives in Assembly-CSharp, which
// SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.Gameplay;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class OrbBurstTests
    {
        private const float MinSpeed = 2f;
        private const float MaxSpeed = 4f;
        private const float Lift = 3.5f;

        [Test]
        public void EveryOrbIsThrownItsOwnWayAtAnAllowedSpeed()
        {
            Vector3[] throws = OrbBurst.Velocities(5, MinSpeed, MaxSpeed, Lift, 0f);

            Assert.AreEqual(5, throws.Length);
            for (int i = 0; i < throws.Length; i++)
            {
                Assert.AreEqual(Lift, throws[i].y, 0.001f, "lift is the same for every orb");

                float sideways = new Vector2(throws[i].x, throws[i].z).magnitude;
                Assert.That(sideways, Is.InRange(MinSpeed - 0.001f, MaxSpeed + 0.001f));

                for (int j = i + 1; j < throws.Length; j++)
                {
                    Vector2 a = new Vector2(throws[i].x, throws[i].z).normalized;
                    Vector2 b = new Vector2(throws[j].x, throws[j].z).normalized;
                    Assert.Greater(Vector2.Angle(a, b), 1f, $"orbs {i} and {j} leave the same way");
                }
            }
        }

        [Test]
        public void TheStartAngleTurnsTheWholeBurst()
        {
            Vector3[] upright = OrbBurst.Velocities(4, MinSpeed, MaxSpeed, Lift, 0f);
            Vector3[] turned = OrbBurst.Velocities(4, MinSpeed, MaxSpeed, Lift, 90f);

            for (int i = 0; i < upright.Length; i++)
            {
                Vector3 expected = Quaternion.Euler(0f, 90f, 0f) * upright[i];
                Assert.AreEqual(expected.x, turned[i].x, 0.001f);
                Assert.AreEqual(expected.z, turned[i].z, 0.001f);
            }
        }

        [Test]
        public void NoOrbsMeansNoThrows()
        {
            Assert.IsEmpty(OrbBurst.Velocities(0, MinSpeed, MaxSpeed, Lift, 0f));
            Assert.IsEmpty(OrbBurst.Velocities(-2, MinSpeed, MaxSpeed, Lift, 0f));
        }
    }
}
