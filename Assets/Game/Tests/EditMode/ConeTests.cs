// The arc test that sight and melee both run on.
//
// Worth testing because both callers get it wrong in the same invisible way: an enemy that cannot
// see a player standing on a crate, and a sword that passes through one, are the same bug in the
// Y axis.
using NUnit.Framework;
using SpaceGame.Enemies;
using UnityEngine;

namespace SpaceGame.Tests
{
    public class ConeTests
    {
        private const float Range = 10f;
        private const float HalfAngle = 60f;

        [Test]
        public void SeesWhatIsInFrontAndNotWhatIsBehind()
        {
            Assert.IsTrue(Cone.Contains(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 5f),
                                        Range, HalfAngle));

            Assert.IsFalse(Cone.Contains(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -5f),
                                         Range, HalfAngle),
                "directly behind is outside a 60 degree half-angle");

            Assert.IsFalse(Cone.Contains(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 20f),
                                         Range, HalfAngle),
                "in front but well out of range");
        }

        [Test]
        public void HeightDoesNotTakeSomethingOutOfTheCone()
        {
            // Two metres up and three along: a player on a crate, or on the step of the outpost.
            // Measured in 3D this falls outside a 60 degree cone about a flat forward; it must not.
            var raised = new Vector3(0f, 2f, 3f);

            Assert.IsTrue(Cone.Contains(Vector3.zero, Vector3.forward, raised, Range, HalfAngle),
                "a target above the eyeline is still in front of you");
        }

        [Test]
        public void SomethingStandingOnTopOfYouCounts()
        {
            // No direction to measure, and both callers want the same answer: a swing at point-blank
            // range connects, and an eye at point-blank range sees.
            Assert.IsTrue(Cone.Contains(Vector3.zero, Vector3.forward, Vector3.zero, Range, HalfAngle));
        }
    }
}
