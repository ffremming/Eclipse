using NUnit.Framework;
using UnityEngine;
using SpaceGame.Presentation.Menu;

namespace SpaceGame.Tests
{
    /// <summary>
    /// What would ruin the menu if it were wrong: a light that flies off the set when the cursor
    /// nears the horizon, a light that snaps home instead of following, and a follow that feels
    /// different on a 60Hz machine than on a 144Hz one.
    /// </summary>
    public sealed class SpotlightAimTests
    {
        private const float FloorHeight = 0f;
        private const float MaxRadius = 12f;
        private static readonly Vector3 Centre = new(0f, 0f, 0f);

        [Test]
        public void RayStraightDownLandsBeneathItsOrigin()
        {
            var ray = new Ray(new Vector3(3f, 10f, -2f), Vector3.down);

            Vector3 aimed = SpotlightAim.Resolve(ray, FloorHeight, Centre, MaxRadius);

            Assert.That(aimed.x, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(aimed.z, Is.EqualTo(-2f).Within(1e-4f));
            Assert.That(aimed.y, Is.EqualTo(FloorHeight).Within(1e-4f), "the aim point must sit on the floor");
        }

        [Test]
        public void NearParallelRayIsClampedToMaxRadius()
        {
            // Grazing the floor: the true intersection is hundreds of metres away, which would
            // swing the light off the set and black the screen out.
            var ray = new Ray(new Vector3(0f, 2f, 0f), new Vector3(0f, -0.002f, 1f).normalized);

            Vector3 aimed = SpotlightAim.Resolve(ray, FloorHeight, Centre, MaxRadius);

            float distance = Vector3.Distance(aimed, Centre);
            Assert.That(distance, Is.EqualTo(MaxRadius).Within(1e-3f),
                        "a grazing ray must be reined in to the edge of the set");
        }

        [Test]
        public void RayPointingAwayFromTheFloorKeepsTravellingWithTheCursor()
        {
            // Cursor above the horizon: there is no intersection at all. Falling back to the set
            // centre would make the light jump backwards as the cursor keeps moving forwards.
            var ray = new Ray(new Vector3(0f, 2f, 0f), new Vector3(0f, 0.5f, 1f).normalized);

            Vector3 aimed = SpotlightAim.Resolve(ray, FloorHeight, Centre, MaxRadius);

            Assert.That(aimed.z, Is.EqualTo(MaxRadius).Within(1e-3f));
            Assert.That(aimed.x, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void FollowClosesOnTheTargetWithoutOvershooting()
        {
            Vector3 current = new(0f, 0f, 0f);
            var target = new Vector3(10f, 0f, 0f);
            float previous = Vector3.Distance(current, target);

            for (int step = 0; step < 40; step++)
            {
                current = SpotlightAim.Follow(current, target, timeConstant: 0.06f, deltaTime: 1f / 60f);
                float remaining = Vector3.Distance(current, target);

                Assert.That(remaining, Is.LessThan(previous), "each step must close the gap");
                previous = remaining;
            }

            Assert.That(previous, Is.LessThan(0.01f), "the light should have arrived by now");
        }

        [Test]
        public void FollowLandsInTheSamePlaceWhateverTheFrameRate()
        {
            var target = new Vector3(10f, 0f, 0f);
            const float timeConstant = 0.06f;
            const float slice = 1f / 60f;

            Vector3 oneBigStep = SpotlightAim.Follow(Vector3.zero, target, timeConstant, slice);

            Vector3 twoSmallSteps = SpotlightAim.Follow(Vector3.zero, target, timeConstant, slice * 0.5f);
            twoSmallSteps = SpotlightAim.Follow(twoSmallSteps, target, timeConstant, slice * 0.5f);

            Assert.That(Vector3.Distance(oneBigStep, twoSmallSteps), Is.LessThan(1e-4f),
                        "the same elapsed time must produce the same position, however it is sliced");
        }
    }
}
