// The chain whip's rope and the lash that throws it.
//
// The bugs worth catching here are the ones a player sees as "that is not a chain": one that
// stretches like rubber when it is reeled in, one that lies underground, and a lash that does not
// reach as far as the weapon claims.
using NUnit.Framework;
using SpaceGame.Chain;
using UnityEngine;

namespace SpaceGame.Tests
{
    public class ChainTests
    {
        private const float Step = 1f / 90f;
        private static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

        /// <summary>A floor at y = 0: the whole world, in one line of arithmetic.</summary>
        private sealed class Floor : IChainWorld
        {
            public bool Sweep(Vector3 from, Vector3 to, float radius, out Vector3 centre, out Vector3 normal)
            {
                normal = Vector3.up;
                centre = to;
                if (to.y >= radius) return false;

                centre = Vector3.Lerp(from, to, Mathf.Clamp01(Mathf.InverseLerp(from.y, to.y, radius)));
                centre.y = radius;
                return true;
            }
        }

        private static float PolylineLength(ChainRope rope)
        {
            float sum = 0f;
            for (int i = 0; i < rope.Points.Count - 1; i++)
                sum += (rope.Points[i + 1] - rope.Points[i]).magnitude;
            return sum;
        }

        [Test]
        public void AChainHangsFromItsAnchorWithoutStretching()
        {
            var anchor = new Vector3(0f, 5f, 0f);
            var rope = new ChainRope(new ChainTuning(), anchor, 2f);

            for (int i = 0; i < 270; i++)
            {
                rope.Pin(anchor);
                rope.Step(Step, Gravity, default, new Floor());
            }

            Assert.AreEqual(3f, rope.Tip.y, 0.1f, "hangs its full length straight down");
            Assert.Less(PolylineLength(rope), 2f * 1.05f, "a chain does not stretch under its own weight");
        }

        [Test]
        public void ALashThrowsTheOrbToFullReachAndReelsItBackWithoutSinkingIntoTheFloor()
        {
            var tuning = new ChainTuning();
            var lashTuning = new WhipLashTuning();
            var lash = new WhipLash(lashTuning);
            var floor = new Floor();

            var holder = Vector3.zero;
            var anchor = new Vector3(0.1f, 0.6f, 0.1f);
            var rope = new ChainRope(tuning, anchor, lashTuning.RestLength);

            lash.Begin(Vector3.forward, 0.62f);

            float farthest = 0f;
            float lowest = float.MaxValue;
            float mostStretched = 0f;
            for (int i = 0; i < 144; i++)
            {
                lash.Advance(Step);
                rope.Pin(anchor);
                rope.Length = lash.PaidOutLength;
                rope.Step(Step, Gravity, lash.TipPullFor(holder, Step), floor);

                farthest = Mathf.Max(farthest, rope.Tip.z);
                mostStretched = Mathf.Max(mostStretched, PolylineLength(rope) / rope.Length);
                foreach (Vector3 point in rope.Points) lowest = Mathf.Min(lowest, point.y);
            }

            Assert.GreaterOrEqual(farthest, lashTuning.FullLength * 0.85f, "the lash reaches what the weapon claims");
            Assert.GreaterOrEqual(lowest, 0f, "no link ever ends up under the floor");
            Assert.Less(mostStretched, 1.4f, "a fast lash and reel-in must not turn the chain to rubber");
            Assert.IsFalse(lash.Active, "the lash is over once its time is up");
            Assert.AreEqual(lashTuning.RestLength, rope.Length, 1e-4f, "and the rope is all reeled back in");
        }

        [Test]
        public void TheHandleOnlyDrawsTheOrbWhileWindingUpAndThrowing()
        {
            var lash = new WhipLash(new WhipLashTuning());
            Assert.AreEqual(0f, lash.TipPullFor(Vector3.zero, Step).Fraction, "idle: the orb is free");

            lash.Begin(Vector3.forward, 0.62f);
            Assert.AreEqual(WhipLash.Phase.WindUp, lash.Current);
            Assert.Greater(lash.TipPullFor(Vector3.zero, Step).Fraction, 0f);

            while (lash.Current != WhipLash.Phase.Recover) lash.Advance(Step);
            Assert.AreEqual(0f, lash.TipPullFor(Vector3.zero, Step).Fraction,
                "recovering: the hand lets go and the reel brings the orb home");
        }
    }
}
