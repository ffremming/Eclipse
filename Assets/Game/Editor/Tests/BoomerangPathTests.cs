// The shape of a boomerang throw, which is the whole of what makes it a boomerang.
//
// Quiet failures, all of them: a boomerang that lands where the hand used to be is caught by
// nothing and the weapon is gone for the length of the flight with no error anywhere; one that
// retraces its own line hits a target once, in one lane, where the design says twice; one that
// turns short of its range is a weapon whose reach is a number in the Inspector that lies.
//
// In Editor/ for the same reason MeleeComboTests is: these live in Assembly-CSharp.
using NUnit.Framework;
using SpaceGame.Items;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class BoomerangPathTests
    {
        private const float Range = 9f;
        private const float Bow = 3f;
        private const float Tolerance = 1e-3f;

        private static readonly Vector3 Origin = new Vector3(2f, 1.2f, -4f);

        private static BoomerangPath ThrownForward() => new BoomerangPath(Origin, Vector3.forward, Range, Bow);

        [Test]
        public void ItComesHomeToAHandThatMovedWhileItWasAway()
        {
            // The player ran on and to the side for the whole flight.
            Vector3 handNow = Origin + new Vector3(6f, 0.5f, 7f);

            Vector3 arrival = ThrownForward().PointAt(1f, handNow);

            Assert.That(Vector3.Distance(arrival, handNow), Is.LessThan(Tolerance));
        }

        [Test]
        public void ItTurnsForHomeAtTheFullRangeOfTheThrow()
        {
            Vector3 turn = ThrownForward().PointAt(BoomerangPath.Turnaround, Origin);

            // Straight out from the hand by exactly the range, and level with the throw: the
            // outward and return curves cross the line of the throw here and nowhere else.
            Assert.AreEqual(Range, Vector3.Dot(turn - Origin, Vector3.forward), Tolerance);
            Assert.AreEqual(0f, Vector3.Dot(turn - Origin, Vector3.right), Tolerance);
        }

        [Test]
        public void ItTurnsAtTheFullRangeAlongAThrowPitchedUpward()
        {
            Vector3 aim = Quaternion.Euler(-40f, 0f, 0f) * Vector3.forward;
            BoomerangPath path = new BoomerangPath(Origin, aim, Range, Bow);

            Vector3 turn = path.PointAt(BoomerangPath.Turnaround, Origin);

            // On the line the player looked along, by exactly the range: the throw goes where it
            // was aimed rather than where the body was facing.
            Assert.AreEqual(Range, Vector3.Dot(turn - Origin, aim), Tolerance);
            Assert.AreEqual(0f, Vector3.Distance(turn, Origin + aim * Range), Tolerance);
        }

        [Test]
        public void ThePitchOfAThrowDoesNotShrinkItsBow()
        {
            Vector3 aim = Quaternion.Euler(-70f, 0f, 0f) * Vector3.forward;
            BoomerangPath path = new BoomerangPath(Origin, aim, Range, Bow);

            float outward = Vector3.Dot(path.PointAt(0.25f, Origin) - Origin, Vector3.right);

            Assert.AreEqual(Bow, outward, Tolerance);
        }

        [Test]
        public void ItGoesOutOnOneSideAndComesHomeOnTheOther()
        {
            BoomerangPath path = ThrownForward();

            float outward = Vector3.Dot(path.PointAt(0.25f, Origin) - Origin, Vector3.right);
            float homeward = Vector3.Dot(path.PointAt(0.75f, Origin) - Origin, Vector3.right);

            // Opposite sides by the full bow. A path that came back along the way it went would
            // sweep one lane twice and leave the other untouched.
            Assert.AreEqual(Bow, outward, Tolerance);
            Assert.AreEqual(-Bow, homeward, Tolerance);
        }
    }
}
