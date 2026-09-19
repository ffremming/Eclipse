// The shape of a swing: it rises, it ends, and a new press starts a new one.
//
// Worth pinning because every failure mode here is a weapon that looks broken rather than a weapon
// that throws an error. A swing that never clears leaves the blade flared for the rest of the
// session and its trail drawing forever; one that clears a frame early cuts the arc short; one that
// carries the last swing's progress into the next hands the second press a flare it has not earned.
//
// In Editor/ rather than beside the other EditMode tests for the reason MeleeComboTests gives:
// SwingFlare lives in Assembly-CSharp, which SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.Characters;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class SwingFlareTests
    {
        private const float Duration = 0.4f;
        private const float Step = 0.1f;

        /// <summary>The weapons' own curve: up fast, down slow, zero at both ends.</summary>
        private static SwingFlare Fresh() => new SwingFlare(Duration, new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));

        [Test]
        public void ASwingRisesThroughItsCurveAndIsOverExactlyOnce()
        {
            SwingFlare swing = Fresh();
            swing.Begin();

            Assert.IsTrue(swing.Swinging, "a swing that has begun is under way");

            swing.Tick(Step);
            float earlyFlare = swing.Flare;
            Assert.Greater(earlyFlare, 0.5f, "the curve peaks early, so a quarter in is near its top");

            int endings = 0;
            for (int frame = 0; frame < 6; frame++)
                if (swing.Tick(Step)) endings++;

            Assert.AreEqual(1, endings, "the swing ends once, not once per frame after it finishes");
            Assert.IsFalse(swing.Swinging);
            Assert.AreEqual(0f, swing.Flare, "a finished swing leaves nothing burning");
        }

        [Test]
        public void PressingAgainMidSwingStartsTheNewSwingFromTheBeginning()
        {
            SwingFlare swing = Fresh();
            swing.Begin();
            swing.Tick(Step * 3f);

            float late = swing.Progress;
            swing.Begin();
            swing.Tick(Step);

            Assert.Less(swing.Progress, late, "the second swing is its own swing, not the tail of the first");
        }

        [Test]
        public void TickingWhileIdleDoesNothingAtAll()
        {
            SwingFlare swing = Fresh();

            Assert.IsFalse(swing.Tick(Step), "no swing cannot end");
            Assert.AreEqual(0f, swing.Flare);
            Assert.AreEqual(0f, swing.Progress);
            Assert.IsFalse(swing.Swinging);
        }
    }
}
