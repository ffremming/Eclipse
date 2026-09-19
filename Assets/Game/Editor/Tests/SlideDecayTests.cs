// How a slide gives up its speed.
//
// Worth pinning because the failure this replaced was invisible in code review and obvious in
// play: the grounded movement lerp is a hard set, so a slide that does not steer its own speed is
// overwritten with crouchSpeed one physics tick after it starts, and sliding becomes a way of
// braking. The curve below is what the slide uses instead, and "holds speed early" is the part
// that makes it read as momentum rather than as a handbrake.
//
// In Editor/ rather than beside the other EditMode tests for the same reason CarryMomentumTests
// is: SlideDecay lives in Assembly-CSharp, which SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.Characters;

namespace SpaceGame.EditorTools
{
    public class SlideDecayTests
    {
        private const float Launch = 11f;
        private const float Floor = 2.6f;
        private const float Duration = 0.8f;

        [Test]
        public void ASlideStartsAtItsLaunchSpeedAndEndsAtTheFloor()
        {
            Assert.AreEqual(Launch, SlideDecay.SpeedAt(0f, Launch, Floor, Duration), 0.001f);
            Assert.AreEqual(Floor, SlideDecay.SpeedAt(Duration, Launch, Floor, Duration), 0.001f);
        }

        [Test]
        public void ItHoldsMostOfItsSpeedThroughTheFirstHalf()
        {
            // Quadratic, so halfway through only a quarter of the speed is gone. A linear decay
            // would be at the midpoint here, and the slide would feel like brakes.
            float half = SlideDecay.SpeedAt(Duration * 0.5f, Launch, Floor, Duration);
            float linearMidpoint = (Launch + Floor) * 0.5f;

            Assert.Greater(half, linearMidpoint, "a slide should still be fast at its halfway point");
            Assert.AreEqual(Launch - (Launch - Floor) * 0.25f, half, 0.001f);
        }

        [Test]
        public void ItIsClampedAtBothEndsSoAnOverrunNeverGoesBackwards()
        {
            Assert.AreEqual(Floor, SlideDecay.SpeedAt(Duration * 5f, Launch, Floor, Duration), 0.001f,
                "running past the window holds the floor rather than extrapolating below it");
            Assert.AreEqual(Launch, SlideDecay.SpeedAt(-1f, Launch, Floor, Duration), 0.001f);
        }

        [Test]
        public void AZeroDurationSlideIsJustTheFloorRatherThanADivideByZero()
        {
            Assert.AreEqual(Floor, SlideDecay.SpeedAt(0f, Launch, Floor, 0f), 0.001f);
        }
    }
}
