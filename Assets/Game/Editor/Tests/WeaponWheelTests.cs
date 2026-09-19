// What the weapon wheel decides without drawing anything: which wedge a pointer is over, and how far
// a pointer may travel.
//
// The seam is the part worth pinning. Wedges are centred on their slot's angle, so slot 0 straddles
// twelve o'clock; an off-by-half-a-wedge mistake here is invisible until someone flicks the pointer
// up and equips the wrong weapon.
//
// In Editor/ rather than beside the other EditMode tests for the same reason IdleBreakScheduleTests
// is: RadialSelection lives in Assembly-CSharp, which SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using SpaceGame.Items;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class WeaponWheelTests
    {
        private const float Deadzone = 10f;

        [Test]
        public void FourSlotsSitAtTheFourPointsOfTheCompassInSlotOrderClockwise()
        {
            Assert.AreEqual(0, RadialSelection.IndexAt(Vector2.up * 50f, 4, Deadzone));
            Assert.AreEqual(1, RadialSelection.IndexAt(Vector2.right * 50f, 4, Deadzone));
            Assert.AreEqual(2, RadialSelection.IndexAt(Vector2.down * 50f, 4, Deadzone));
            Assert.AreEqual(3, RadialSelection.IndexAt(Vector2.left * 50f, 4, Deadzone));
        }

        [Test]
        public void TheFirstWedgeStraddlesTwelveOClockAndOwnsBothSidesOfTheSeam()
        {
            // 40 degrees either side of straight up is still inside a 90 degree wedge; 50 is the
            // neighbour's. Without the half-wedge shift the counter-clockwise side would fall through
            // to the last slot.
            Assert.AreEqual(0, RadialSelection.IndexAt(Rotated(-40f), 4, Deadzone));
            Assert.AreEqual(0, RadialSelection.IndexAt(Rotated(40f), 4, Deadzone));
            Assert.AreEqual(3, RadialSelection.IndexAt(Rotated(-50f), 4, Deadzone));
            Assert.AreEqual(1, RadialSelection.IndexAt(Rotated(50f), 4, Deadzone));
        }

        [Test]
        public void ThePointerChoosesNothingWhileItRestsInTheHubOrThereIsNothingToChoose()
        {
            Assert.AreEqual(RadialSelection.NoSelection, RadialSelection.IndexAt(Vector2.up * 5f, 4, Deadzone));
            Assert.AreEqual(RadialSelection.NoSelection, RadialSelection.IndexAt(Vector2.up * 50f, 0, Deadzone));
        }

        [Test]
        public void ThePointerStopsAtTheRimHoweverHardItIsPushed()
        {
            Vector2 pointer = RadialSelection.MovePointer(Vector2.zero, new Vector2(1000f, 0f), 2f, 270f);

            Assert.AreEqual(270f, pointer.magnitude, 0.001f);
            Assert.AreEqual(Vector2.right, pointer.normalized);
        }

        [Test]
        public void AssetNamesReadAsWordsAndEmptyNamesReadAsNothing()
        {
            Assert.AreEqual("Light Khopesh", ItemDisplayName.From("LightKhopesh"));
            Assert.AreEqual("Torch", ItemDisplayName.From("Torch"));
            Assert.AreEqual(string.Empty, ItemDisplayName.From(null));
        }

        /// <summary>A point 50 out from the centre, turned clockwise from straight up by <paramref name="degrees"/>.</summary>
        private static Vector2 Rotated(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * 50f;
        }
    }
}
