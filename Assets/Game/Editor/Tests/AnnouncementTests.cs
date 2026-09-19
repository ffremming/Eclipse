// The one line of text this game says, and how long it says it for.
//
// The curve is the whole decision, which is why it is a plain class and why it is tested. Eclipse
// has no message line and does not want one: a line that appears and disappears reads as a HUD
// flickering, and a line that stays reads as a HUD. The difference between those and "something
// arrived" is entirely in these three numbers. See KeyBanner and GDC-L1-UX-0003.
//
// In Editor/ rather than beside the other EditMode tests because this lives in Assembly-CSharp,
// which SpaceGame.Tests.EditMode does not reference. Same reason as CastleLockTests.
using NUnit.Framework;
using SpaceGame.Presentation;

namespace SpaceGame.EditorTools
{
    public class AnnouncementTests
    {
        private const float Rise = 0.5f;
        private const float Hold = 2f;
        private const float Fall = 1f;

        private Announcement announcement;

        [SetUp]
        public void SetUp() => announcement = new Announcement(Rise, Hold, Fall);

        [Test]
        public void NothingIsSaidUntilSomethingIsAnnounced()
        {
            Assert.IsFalse(announcement.IsShowing);
            Assert.AreEqual(0f, announcement.Strength, 1e-4f);

            announcement.Tick(10f);
            Assert.IsFalse(announcement.IsShowing, "a banner nobody asked for came up");
        }

        [Test]
        public void TheLineComesUpAndIsHeldAtFullStrength()
        {
            announcement.Show();
            Assert.AreEqual(0f, announcement.Strength, 1e-4f, "the line snapped in at full strength");

            announcement.Tick(Rise * 0.5f);
            float rising = announcement.Strength;
            Assert.Greater(rising, 0f);
            Assert.Less(rising, 1f);

            announcement.Tick(Rise * 0.5f + Hold * 0.5f);
            Assert.AreEqual(1f, announcement.Strength, 1e-4f, "the line never reached full strength");
        }

        [Test]
        public void TheLineGoesAwayAndStaysAway()
        {
            announcement.Show();
            announcement.Tick(announcement.Length);

            Assert.AreEqual(0f, announcement.Strength, 1e-4f);
            Assert.IsFalse(announcement.IsShowing);

            announcement.Tick(60f);
            Assert.AreEqual(0f, announcement.Strength, 1e-4f, "the line came back on its own");
        }

        /// <summary>
        /// The newest key is the one the player wants named. A second announcement mid-line starts
        /// over rather than queueing behind the first, which would hold the screen for as long as
        /// it took to drain.
        /// </summary>
        [Test]
        public void AnnouncingAgainStartsOver()
        {
            announcement.Show();
            announcement.Tick(Rise + Hold);

            announcement.Show();
            Assert.AreEqual(0f, announcement.Strength, 1e-4f, "the second line began mid-way");
            Assert.IsTrue(announcement.IsShowing);
        }

        [Test]
        public void ZeroRiseAndFallAreSnapsRatherThanDivisionsByZero()
        {
            var snap = new Announcement(0f, 1f, 0f);
            snap.Show();
            snap.Tick(0.5f);

            Assert.AreEqual(1f, snap.Strength, 1e-4f);

            snap.Tick(1f);
            Assert.AreEqual(0f, snap.Strength, 1e-4f);
            Assert.IsFalse(snap.IsShowing);
        }
    }
}
