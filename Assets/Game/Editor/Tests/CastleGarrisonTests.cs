// Where a castle's garrison stands, and how the lighthouse's dawn is timed.
//
// Both are decisions the player meets head-on — fifteen creatures bunched in one corner is not a
// garrison, and a dawn that snaps reads as a rendering bug rather than as the payoff of the game.
// Neither is visible in a diff, which is what they are pinned here.
//
// In Editor/ rather than beside the other EditMode tests because these live in Assembly-CSharp,
// which SpaceGame.Tests.EditMode does not reference. Same reason as LightCostTests.
using System.Collections.Generic;
using NUnit.Framework;
using SpaceGame.Castle;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class CastleGarrisonTests
    {
        private const int FullCastleGarrison = 15;
        private const int KeepGarrison = 6;

        private const float CourtyardInner = 16f;
        private const float CourtyardOuter = 29f;
        private const int Seed = 20260918;

        private static IReadOnlyList<Vector2> Courtyard(int count, float jitter = 1.5f) =>
            GarrisonLayout.Ring(count, CourtyardInner, CourtyardOuter, jitter, Seed);

        [Test]
        public void PlacesExactlyTheNumberAskedFor()
        {
            Assert.AreEqual(FullCastleGarrison, Courtyard(FullCastleGarrison).Count);
            Assert.AreEqual(KeepGarrison, Courtyard(KeepGarrison).Count);
            Assert.AreEqual(0, Courtyard(0).Count);
            Assert.AreEqual(0, Courtyard(-3).Count);
        }

        /// <summary>
        /// Inside the walls and outside the keep. A creature placed inside the keep is inside solid
        /// geometry — the keep has no interior — and one placed past the wall is outside the castle
        /// the player just unlocked.
        /// </summary>
        [Test]
        public void EveryDefenderIsInTheCourtyard()
        {
            const float jitter = 1.5f;

            foreach (Vector2 spot in Courtyard(FullCastleGarrison, jitter))
            {
                float radius = spot.magnitude;
                Assert.GreaterOrEqual(radius, CourtyardInner - jitter,
                    "a defender was placed inside the keep, which is solid");
                Assert.LessOrEqual(radius, CourtyardOuter + jitter,
                    "a defender was placed outside the ring wall");
            }
        }

        /// <summary>
        /// The reason for the spiral: no two defenders standing on each other, and no empty half to
        /// the courtyard. Both are what random points in an annulus actually produce.
        /// </summary>
        [Test]
        public void DefendersAreSpreadRatherThanClumped()
        {
            IReadOnlyList<Vector2> spots = Courtyard(FullCastleGarrison);

            float closest = float.PositiveInfinity;
            for (int a = 0; a < spots.Count; a++)
            for (int b = a + 1; b < spots.Count; b++)
                closest = Mathf.Min(closest, Vector2.Distance(spots[a], spots[b]));

            // Two capsules are 0.9 m across. Three metres is comfortably clear of standing inside
            // each other, and is what an even spread over this courtyard actually achieves.
            Assert.Greater(closest, 3f, "two defenders were placed on top of each other");

            // Every quarter of the courtyard occupied, so the player cannot walk in through an
            // empty side of a castle that is supposed to be defended.
            var quadrants = new HashSet<int>();
            foreach (Vector2 spot in spots)
                quadrants.Add((spot.x >= 0 ? 1 : 0) + (spot.y >= 0 ? 2 : 0));

            Assert.AreEqual(4, quadrants.Count, "one side of the courtyard has no defenders in it");
        }

        /// <summary>
        /// The key is certain and its holder is unknown. A drop chance per defender would let a
        /// player clear the whole garrison and find no key at all, which is a dead end they cannot
        /// tell from not having searched hard enough.
        /// </summary>
        [Test]
        public void ExactlyTheAskedNumberOfDefendersCarriesTheKey()
        {
            IReadOnlyList<int> one = GarrisonLayout.Bearers(KeepGarrison, 1, Seed);
            Assert.AreEqual(1, one.Count);
            Assert.GreaterOrEqual(one[0], 0);
            Assert.Less(one[0], KeepGarrison);

            Assert.AreEqual(0, GarrisonLayout.Bearers(KeepGarrison, 0, Seed).Count);
            Assert.AreEqual(0, GarrisonLayout.Bearers(0, 2, Seed).Count);
        }

        [Test]
        public void NoDefenderIsPickedTwice()
        {
            IReadOnlyList<int> three = GarrisonLayout.Bearers(FullCastleGarrison, 3, Seed);
            CollectionAssert.AllItemsAreUnique(three);
            Assert.AreEqual(3, three.Count);

            // Asking for more bearers than there are defenders gives every one of them, once.
            IReadOnlyList<int> all = GarrisonLayout.Bearers(KeepGarrison, KeepGarrison + 4, Seed);
            CollectionAssert.AllItemsAreUnique(all);
            Assert.AreEqual(KeepGarrison, all.Count);
        }

        [Test]
        public void TheSameCastleHidesTheKeyOnTheSameDefender()
        {
            CollectionAssert.AreEqual(GarrisonLayout.Bearers(KeepGarrison, 1, Seed),
                                      GarrisonLayout.Bearers(KeepGarrison, 1, Seed));
        }

        /// <summary>
        /// Not always the first one. A shuffle that left index 0 in place would pass every test
        /// above while putting the key on the same defender in every castle in the game.
        /// </summary>
        [Test]
        public void TheKeyIsNotAlwaysOnTheSameDefender()
        {
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 40; seed++)
                seen.Add(GarrisonLayout.Bearers(KeepGarrison, 1, seed)[0]);

            Assert.Greater(seen.Count, 1, "every seed put the key on the same defender");
        }

        [Test]
        public void SameSeedLaysOutTheSameCastle()
        {
            IReadOnlyList<Vector2> first = Courtyard(FullCastleGarrison);
            IReadOnlyList<Vector2> again = Courtyard(FullCastleGarrison);

            for (int index = 0; index < first.Count; index++)
                Assert.AreEqual(first[index], again[index], "the same seed laid the castle out differently");

            IReadOnlyList<Vector2> other =
                GarrisonLayout.Ring(FullCastleGarrison, CourtyardInner, CourtyardOuter, 1.5f, Seed + 1);

            CollectionAssert.AreNotEqual(first, other, "a different seed laid out the same castle");
        }
    }

    public class LightfallProgressTests
    {
        private const float Hold = 1.1f;
        private const float Rise = 9f;

        private static LightfallProgress Fresh() => new LightfallProgress(Hold, Rise);

        private static void Run(LightfallProgress progress, float seconds, float step = 1f / 60f)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += step) progress.Tick(step);
        }

        [Test]
        public void NothingHappensUntilTheBeaconIsStruck()
        {
            LightfallProgress progress = Fresh();
            Run(progress, 5f);

            Assert.IsFalse(progress.Lit);
            Assert.AreEqual(0f, progress.Blend);
            Assert.IsFalse(progress.Finished);
        }

        /// <summary>
        /// The held beat. Without it the world starts changing on the same frame as the blow and
        /// the dawn reads as coincident with the hit rather than caused by it — and since the
        /// project has no audio, this pause is the only acknowledgement the strike gets.
        /// </summary>
        [Test]
        public void TheWorldHoldsStillBeforeTheLightComes()
        {
            LightfallProgress progress = Fresh();
            progress.Light();

            Run(progress, Hold * 0.8f);
            Assert.AreEqual(0f, progress.Blend, 1e-5f, "the light started before the beat was up");

            Run(progress, Hold);
            Assert.Greater(progress.Blend, 0f, "the light never started");
        }

        [Test]
        public void LightRisesToFullAndStopsThere()
        {
            LightfallProgress progress = Fresh();
            progress.Light();

            float previous = 0f;
            for (int frame = 0; frame < 60 * 12; frame++)
            {
                progress.Tick(1f / 60f);
                Assert.GreaterOrEqual(progress.Blend, previous - 1e-6f, "the dawn went backwards");
                Assert.LessOrEqual(progress.Blend, 1f, "the dawn overshot full daylight");
                previous = progress.Blend;
            }

            Assert.AreEqual(1f, progress.Blend, 1e-4f);
            Assert.IsTrue(progress.Finished);
        }

        /// <summary>
        /// A player who keeps swinging at a lit beacon must not restart the sunrise.
        /// </summary>
        [Test]
        public void StrikingAgainDoesNotRestartTheDawn()
        {
            LightfallProgress progress = Fresh();
            Assert.IsTrue(progress.Light(), "the first strike did not light it");

            Run(progress, Hold + Rise * 0.5f);
            float midway = progress.Blend;

            Assert.IsFalse(progress.Light(), "a second strike lit an already-lit beacon");
            Assert.AreEqual(midway, progress.Blend, 1e-6f, "a second strike reset the dawn");
        }

        [Test]
        public void ZeroRiseIsASnapRatherThanADivisionByZero()
        {
            var progress = new LightfallProgress(0f, 0f);
            progress.Light();
            progress.Tick(1f / 60f);

            Assert.IsFalse(float.IsNaN(progress.Blend), "a zero rise produced NaN");
            Assert.AreEqual(1f, progress.Blend, 1e-4f);
        }

        [Test]
        public void NonPositiveTicksDoNotMoveTheDawn()
        {
            LightfallProgress progress = Fresh();
            progress.Light();
            Run(progress, Hold + 1f);

            float before = progress.Blend;
            progress.Tick(0f);
            progress.Tick(-5f);

            Assert.AreEqual(before, progress.Blend, 1e-6f);
        }
    }
}
