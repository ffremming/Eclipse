// The shelf a castle stands on, and the hillside that carries the land up to it.
//
// What is actually being protected here is the one thing a player sees immediately when it is
// wrong: the outer wall meeting the ground all the way round. That needs the plateau to be dead
// flat over the whole footprint — not "nearly flat", because the wall is a rigid 68 m mesh and a
// centimetre of tilt under one corner is a centimetre of daylight under the other.
//
// In Editor/ rather than beside the other EditMode tests because CastleSite lives in
// Assembly-CSharp, which SpaceGame.Tests.EditMode does not reference. Same reason as LightCostTests.
using NUnit.Framework;
using SpaceGame.Castle;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class CastleSiteTests
    {
        private const float Height = 0.62f;
        private const float PlateauRadius = 40f;
        private const float SkirtWidth = 35f;

        private static readonly Vector2 Centre = new Vector2(120f, 300f);

        private static CastleSite Site() =>
            new CastleSite(Centre, PlateauRadius, SkirtWidth, Height);

        /// <summary>
        /// Every point of the footprint at exactly the shelf height, sampled right out to the rim
        /// of the plateau and over wildly different underlying land. This is the test the castle
        /// actually stands on.
        /// </summary>
        [Test]
        public void PlateauIsFlatAcrossTheWholeFootprint()
        {
            CastleSite site = Site();

            for (int ring = 0; ring <= 8; ring++)
            {
                for (int step = 0; step < 16; step++)
                {
                    float distance = PlateauRadius * ring / 8f;
                    float angle = step / 16f * Mathf.PI * 2f;
                    float x = Centre.x + Mathf.Cos(angle) * distance;
                    float z = Centre.y + Mathf.Sin(angle) * distance;

                    // A different underlying height at every sample, so a shelf that quietly let
                    // the land through anywhere would show up.
                    float land = 0.1f + 0.8f * Mathf.PingPong(ring * 3 + step, 1f);

                    Assert.AreEqual(Height, site.Reshape(x, z, land), 1e-6f,
                        $"the plateau is not flat at ring {ring}, step {step}");
                }
            }
        }

        [Test]
        public void LandIsUntouchedBeyondTheSkirt()
        {
            CastleSite site = Site();
            float outside = PlateauRadius + SkirtWidth + 5f;

            Assert.AreEqual(0.27f, site.Reshape(Centre.x + outside, Centre.y, 0.27f), 1e-6f);
            Assert.IsFalse(site.Touches(Centre.x + outside, Centre.y));
        }

        /// <summary>
        /// The hillside has to run all the way from the shelf to the land with no step at either
        /// end. A crease at the top shows as a rim round the castle and one at the bottom as a
        /// ring in the grass, and both read as a construction line rather than as a hill.
        /// </summary>
        [Test]
        public void SkirtRunsFromShelfToLandWithoutAStep()
        {
            CastleSite site = Site();
            const float land = 0.2f;

            float atPlateauEdge = site.Reshape(Centre.x + PlateauRadius, Centre.y, land);
            float justOutside = site.Reshape(Centre.x + PlateauRadius + 0.01f, Centre.y, land);
            Assert.AreEqual(atPlateauEdge, justOutside, 1e-4f, "there is a step at the top of the skirt");

            float justInsideReach = site.Reshape(Centre.x + site.Reach - 0.01f, Centre.y, land);
            Assert.AreEqual(land, justInsideReach, 1e-4f, "there is a step at the foot of the skirt");
        }

        [Test]
        public void SkirtFallsAwayFromTheShelfAllTheWayDown()
        {
            CastleSite site = Site();
            const float land = 0.2f;

            float previous = Height;
            for (int step = 1; step <= 20; step++)
            {
                float distance = PlateauRadius + SkirtWidth * step / 20f;
                float height = site.Reshape(Centre.x + distance, Centre.y, land);

                Assert.LessOrEqual(height, previous + 1e-5f,
                    $"the hillside rises again at {distance:0.0} m from the centre");
                previous = height;
            }

            Assert.AreEqual(land, previous, 1e-4f);
        }

        /// <summary>
        /// A zero skirt is a division by zero inside Reshape. It has to come back as a cliff — the
        /// thing that was asked for — rather than as a heightmap full of NaN, which arrives much
        /// later as a terrain that will not render and says nothing about where it came from.
        /// </summary>
        [Test]
        public void ZeroSkirtIsACliffRatherThanNaN()
        {
            var site = new CastleSite(Centre, PlateauRadius, 0f, Height);

            float justOutside = site.Reshape(Centre.x + PlateauRadius + 0.02f, Centre.y, 0.2f);
            Assert.IsFalse(float.IsNaN(justOutside), "a zero skirt produced NaN heights");
            Assert.AreEqual(0.2f, justOutside, 1e-3f);
        }

        [Test]
        public void HeightIsClampedToTheHeightmapsRange()
        {
            Assert.AreEqual(1f, new CastleSite(Centre, 10f, 5f, 4f).Reshape(Centre.x, Centre.y, 0f));
            Assert.AreEqual(0f, new CastleSite(Centre, 10f, 5f, -2f).Reshape(Centre.x, Centre.y, 0.5f));
        }
    }
}
