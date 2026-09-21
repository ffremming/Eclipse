// What the lantern holds when a dead player stands up.
//
// The whole price of dying lives in this one number, so the edges are where it matters: a share
// that rounds to nothing must still light the lantern (zero light is death, and a respawn that
// hands back a corpse raises the death screen again from inside the standing up meant to close it),
// and a share of everything must not exceed what the lantern holds.
using NUnit.Framework;
using SpaceGame.Gameplay;

namespace SpaceGame.EditorTools
{
    public class RekindleTests
    {
        private const int Lantern = 30;

        [Test]
        public void ReturnsTheShareOfTheMaximum()
        {
            Assert.AreEqual(10, Rekindle.OnReturn(Lantern, 1f / 3f));
            Assert.AreEqual(15, Rekindle.OnReturn(Lantern, 0.5f));
        }

        [Test]
        public void NeverReturnsNone()
        {
            Assert.AreEqual(1, Rekindle.OnReturn(Lantern, 0f));
            Assert.AreEqual(1, Rekindle.OnReturn(Lantern, 0.01f));
        }

        [Test]
        public void NeverReturnsMoreThanTheLanternHolds()
        {
            Assert.AreEqual(Lantern, Rekindle.OnReturn(Lantern, 1f));
            Assert.AreEqual(Lantern, Rekindle.OnReturn(Lantern, 2f));
        }

        [Test]
        public void ShareIsOfTheMaximum_SoResizingTheLanternResizesWhatDeathCosts()
        {
            Assert.AreEqual(10, Rekindle.OnReturn(30, 0.34f));
            Assert.AreEqual(20, Rekindle.OnReturn(60, 0.34f));
        }

        /// <summary>
        /// A lantern that holds nothing is a misconfigured prefab, not a player to revive. Handing
        /// back 1 would raise the dead on a health component whose own maximum says they cannot be.
        /// </summary>
        [Test]
        public void AnEmptyLanternGivesNothingBack()
        {
            Assert.AreEqual(0, Rekindle.OnReturn(0, 0.5f));
            Assert.AreEqual(0, Rekindle.OnReturn(-5, 0.5f));
        }
    }
}
