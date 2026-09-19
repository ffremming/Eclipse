// How fast the walk cycle plays for a given ground speed.
//
// The rate multiplies the Move state, and the controller defaults the parameter to zero — so the
// failure worth pinning is a rate that is ever less than one, which is a character whose legs stop
// at a standstill or crawl at a walk. Above the anchor it must scale, or a fast body skates.
//
// In Editor/ for the same reason IdleBreakScheduleTests is: StrideRate lives in Assembly-CSharp,
// which SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.Characters;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class StrideRateTests
    {
        private const float ClipSpeed = 4.92f;

        [Test]
        public void AtOrBelowTheClipSpeedTheCycleNeverSlowsDown()
        {
            Assert.AreEqual(1f, StrideRate.For(Vector3.zero, ClipSpeed), "standing still must not freeze the tree's idle");
            Assert.AreEqual(1f, StrideRate.For(new Vector3(0f, 0f, 2f), ClipSpeed));
            Assert.AreEqual(1f, StrideRate.For(new Vector3(0f, 0f, ClipSpeed), ClipSpeed));
        }

        [Test]
        public void PastTheClipSpeedTheCycleScalesWithTheOverrunWhicheverWayTheBodyMoves()
        {
            Assert.AreEqual(2f, StrideRate.For(new Vector3(0f, 0f, ClipSpeed * 2f), ClipSpeed), 0.001f);
            Assert.AreEqual(2f, StrideRate.For(new Vector3(-ClipSpeed * 2f, 0f, 0f), ClipSpeed), 0.001f);
        }

        [Test]
        public void FallingSpeedIsNotStrideSpeed()
        {
            Assert.AreEqual(1f, StrideRate.For(new Vector3(0f, -30f, 0f), ClipSpeed));
        }

        [Test]
        public void AnUnconfiguredClipSpeedLeavesTheCycleAlone()
        {
            Assert.AreEqual(1f, StrideRate.For(new Vector3(0f, 0f, 9f), 0f));
        }
    }
}
