using NUnit.Framework;
using UnityEngine;
using SpaceGame.Presentation.Menu;

namespace SpaceGame.Tests
{
    /// <summary>
    /// What would ruin the menu if it were wrong: the one action on the screen fading below the
    /// floor the designer set for it, a reveal that brightens as the light moves away, and a
    /// division by zero when the two radii meet.
    /// </summary>
    public sealed class RevealFieldTests
    {
        private const float Inner = 80f;
        private const float Outer = 320f;
        private static readonly Vector2 Cursor = new(600f, 400f);

        [Test]
        public void UnderTheLightTheElementIsFullyLit()
        {
            Assert.That(RevealField.Alpha(Cursor, Cursor, Inner, Outer, floorAlpha: 0f),
                        Is.EqualTo(1f).Within(1e-4f));

            Vector2 justInside = Cursor + new Vector2(Inner - 1f, 0f);
            Assert.That(RevealField.Alpha(Cursor, justInside, Inner, Outer, floorAlpha: 0f),
                        Is.EqualTo(1f).Within(1e-4f), "anything within the inner radius is fully lit");
        }

        [Test]
        public void BeyondTheOuterRadiusOnlyTheFloorRemains()
        {
            Vector2 farAway = Cursor + new Vector2(Outer + 500f, 0f);

            Assert.That(RevealField.Alpha(Cursor, farAway, Inner, Outer, floorAlpha: 0.25f),
                        Is.EqualTo(0.25f).Within(1e-4f));
        }

        [Test]
        public void TheRevealNeverBrightensAsTheLightMovesAway()
        {
            float previous = float.MaxValue;

            for (float distance = 0f; distance <= Outer + 100f; distance += 5f)
            {
                float alpha = RevealField.Alpha(Cursor, Cursor + new Vector2(distance, 0f), Inner, Outer,
                                                floorAlpha: 0.1f);

                Assert.That(alpha, Is.LessThanOrEqualTo(previous + 1e-5f),
                            $"alpha rose again at {distance}px from the light");
                previous = alpha;
            }
        }

        [Test]
        public void TheFloorIsHonouredAtBothEnds()
        {
            Vector2 farAway = Cursor + new Vector2(Outer * 4f, 0f);

            // A title that may vanish entirely.
            Assert.That(RevealField.Alpha(Cursor, farAway, Inner, Outer, floorAlpha: 0f),
                        Is.EqualTo(0f).Within(1e-4f));

            // A button that must never fall below a quarter, wherever the light is.
            Assert.That(RevealField.Alpha(Cursor, farAway, Inner, Outer, floorAlpha: 0.25f),
                        Is.GreaterThanOrEqualTo(0.25f));

            // An element pinned fully on stays fully on.
            Assert.That(RevealField.Alpha(Cursor, farAway, Inner, Outer, floorAlpha: 1f),
                        Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void RadiiThatMeetDegradeToAHardEdgeRatherThanNaN()
        {
            Vector2 justInside = Cursor + new Vector2(Inner - 1f, 0f);
            Vector2 justOutside = Cursor + new Vector2(Inner + 1f, 0f);

            float lit = RevealField.Alpha(Cursor, justInside, Inner, outerRadius: Inner, floorAlpha: 0.2f);
            float dark = RevealField.Alpha(Cursor, justOutside, Inner, outerRadius: Inner, floorAlpha: 0.2f);

            Assert.That(float.IsNaN(lit), Is.False, "equal radii must not divide by zero");
            Assert.That(lit, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(dark, Is.EqualTo(0.2f).Within(1e-4f));
        }
    }
}
