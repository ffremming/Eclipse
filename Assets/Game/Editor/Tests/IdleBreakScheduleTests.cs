// When the character does something while standing still.
//
// The jitter is the part worth pinning. A break on a fixed timer is worse than no break at all:
// the period becomes visible within a couple of repetitions and the character reads as a machine
// waiting out a cooldown. The floor matters too — a jitter larger than the interval would
// otherwise schedule a zero or negative delay and chain breaks back to back.
//
// In Editor/ rather than beside the other EditMode tests for the same reason CarryMomentumTests
// is: IdleBreakSchedule lives in Assembly-CSharp, which SpaceGame.Tests.EditMode does not
// reference.
using NUnit.Framework;
using SpaceGame.Characters;

namespace SpaceGame.EditorTools
{
    public class IdleBreakScheduleTests
    {
        private const float Interval = 7f;
        private const float Jitter = 2.5f;

        [Test]
        public void TheDelaySpansTheWholeJitterRangeAroundTheInterval()
        {
            Assert.AreEqual(Interval - Jitter, IdleBreakSchedule.NextDelay(Interval, Jitter, 0f), 0.001f);
            Assert.AreEqual(Interval, IdleBreakSchedule.NextDelay(Interval, Jitter, 0.5f), 0.001f);
            Assert.AreEqual(Interval + Jitter, IdleBreakSchedule.NextDelay(Interval, Jitter, 1f), 0.001f);
        }

        [Test]
        public void NoJitterMeansNoVariationAtAll()
        {
            // Worth stating explicitly: this is the configuration that makes breaks metronomic,
            // so if someone sets it they should be doing it on purpose.
            Assert.AreEqual(Interval, IdleBreakSchedule.NextDelay(Interval, 0f, 0f), 0.001f);
            Assert.AreEqual(Interval, IdleBreakSchedule.NextDelay(Interval, 0f, 1f), 0.001f);
        }

        [Test]
        public void AJitterWiderThanTheIntervalStillCannotChainBreaksBackToBack()
        {
            float delay = IdleBreakSchedule.NextDelay(baseInterval: 2f, jitter: 10f, roll: 0f);

            Assert.AreEqual(IdleBreakSchedule.MinimumDelay, delay, 0.001f,
                "a delay that would land at or below zero is floored instead");
        }

        [Test]
        public void ARollOutsideZeroToOneIsClampedRatherThanExtrapolated()
        {
            Assert.AreEqual(Interval - Jitter, IdleBreakSchedule.NextDelay(Interval, Jitter, -5f), 0.001f);
            Assert.AreEqual(Interval + Jitter, IdleBreakSchedule.NextDelay(Interval, Jitter, 5f), 0.001f);
        }
    }
}
