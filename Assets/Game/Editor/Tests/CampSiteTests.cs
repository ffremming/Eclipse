// Where a camp settles when the ground it was promised is not there.
//
// The world is generated from a seed, so every camp's nominal centre is a guess about noise. The
// three outcomes that matter are all invisible from a diff: a camp that stays where it was asked
// for, a camp that walks to the nearest dry land, and a camp that cannot be placed at all and must
// say so instead of standing its creatures in the sea.
//
// In Editor/ rather than beside the other EditMode tests: CampSite lives in Assembly-CSharp, which
// SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.World;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class CampSiteTests
    {
        private const float MinHeight = 8.5f;
        private const float MaxSteepness = 18f;
        private const float SearchRadius = 55f;

        /// <summary>An island: dry and flat east of x = 0, under water west of it.</summary>
        private static Ground SplitWorld(Vector2 point) =>
            point.x >= 0f ? new Ground(20f, 4f) : new Ground(2f, 4f);

        [Test]
        public void StaysWhereItWasAskedForWhenTheGroundThereWillDo()
        {
            Vector2 nominal = new Vector2(40f, 10f);

            Assert.IsTrue(CampSite.TryFind(nominal, SearchRadius, SplitWorld, MinHeight,
                                           MaxSteepness, out Vector2 site));
            Assert.AreEqual(nominal, site, "the first candidate is the centre itself");
        }

        [Test]
        public void WalksOutToTheNearestStandableGroundWhenTheCentreIsUnderwater()
        {
            Vector2 nominal = new Vector2(-20f, 0f);

            Assert.IsTrue(CampSite.TryFind(nominal, SearchRadius, SplitWorld, MinHeight,
                                           MaxSteepness, out Vector2 site));
            Assert.GreaterOrEqual(site.x, 0f, "it settled on the dry half");
            Assert.LessOrEqual(Vector2.Distance(nominal, site), SearchRadius,
                               "and not further out than it was allowed to look");
        }

        [Test]
        public void RefusesRatherThanPlacingACampOnGroundNothingCanStandOn()
        {
            // Dry enough everywhere, and a cliff everywhere: the failure that a height check alone
            // would miss, and the one that puts a fight on a 60-degree slope.
            Ground Cliffs(Vector2 point) => new Ground(30f, 60f);

            Assert.IsFalse(CampSite.TryFind(Vector2.zero, SearchRadius, Cliffs, MinHeight,
                                            MaxSteepness, out Vector2 site));
            Assert.AreEqual(Vector2.zero, site, "a refused search hands back what it was given");
        }
    }
}
