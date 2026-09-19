// The locks on the castle's doors: whether the player has found the key, and how the door says so
// without a HUD.
//
// The refusal tests are the point of this file. Eclipse has no interaction prompt and no message
// line, so a door that turns the player away has exactly one way to tell them — the light on it.
// If that flash is not clearly louder than the door's resting state, the refusal is invisible, and
// an invisible refusal is indistinguishable from a door that is not interactive, from a broken key
// check, and from an input that never arrived. See LockSignal and GDC-L1-UX-0004.
//
// In Editor/ rather than beside the other EditMode tests because these live in Assembly-CSharp,
// which SpaceGame.Tests.EditMode does not reference. Same reason as LightCostTests.
using NUnit.Framework;
using SpaceGame.Castle;
using SpaceGame.Items;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    public class KeyRingTests
    {
        private InventoryItem towerKey;
        private InventoryItem wallKey;
        private KeyRing ring;

        [SetUp]
        public void SetUp()
        {
            towerKey = ScriptableObject.CreateInstance<InventoryItem>();
            towerKey.itemName = "Tower Key";

            wallKey = ScriptableObject.CreateInstance<InventoryItem>();
            wallKey.itemName = "Wall Key";

            ring = new KeyRing();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(towerKey);
            Object.DestroyImmediate(wallKey);
        }

        [Test]
        public void ANewRingHoldsNothing()
        {
            Assert.AreEqual(0, ring.Count);
            Assert.IsFalse(ring.Holds(towerKey));
        }

        [Test]
        public void TakingAKeyHoldsItFromThenOn()
        {
            Assert.IsTrue(ring.Take(towerKey));
            Assert.IsTrue(ring.Holds(towerKey));
        }

        [Test]
        public void DoesNotMistakeOneKeyForAnother()
        {
            ring.Take(wallKey);

            Assert.IsFalse(ring.Holds(towerKey),
                "the wall key opened a door that wants the tower key");
        }

        /// <summary>
        /// A key is a permanent unlock, so opening a door must not take it back off the ring. This
        /// is the test that would have caught the old consumable behaviour: a player who used the
        /// tower key at the first castle arrived at the second one unable to open its keep.
        /// </summary>
        [Test]
        public void AKeyIsNeverSpent()
        {
            ring.Take(towerKey);

            for (int opening = 0; opening < 5; opening++)
                Assert.IsTrue(ring.Holds(towerKey), "a key stopped working after being used");
        }

        /// <summary>
        /// Walking over a second copy of a key changes nothing. It is not an error — the pickup
        /// still has to be consumed rather than left lying there — but it is not a second key.
        /// </summary>
        [Test]
        public void TakingTheSameKeyTwiceAddsNothing()
        {
            Assert.IsTrue(ring.Take(towerKey));
            Assert.IsFalse(ring.Take(towerKey), "a duplicate key was reported as newly found");
            Assert.AreEqual(1, ring.Count);
        }

        [Test]
        public void ANullKeyIsNeitherHeldNorTaken()
        {
            Assert.IsFalse(ring.Take(null));
            Assert.IsFalse(ring.Holds(null));
            Assert.AreEqual(0, ring.Count);
        }

        /// <summary>
        /// The event is what anything reacting to a find would hang off, and it has to fire once
        /// per key rather than once per pickup walked over.
        /// </summary>
        [Test]
        public void FindingAKeyIsAnnouncedOnce()
        {
            int announced = 0;
            InventoryItem last = null;
            ring.Taken += key => { announced++; last = key; };

            ring.Take(towerKey);
            ring.Take(towerKey);

            Assert.AreEqual(1, announced, "a duplicate key was announced as a new find");
            Assert.AreSame(towerKey, last);
        }
    }

    public class LockSignalTests
    {
        private static readonly LockSignalTuning Tuning = LockSignalTuning.Default;

        private const float Never = float.PositiveInfinity;

        private static LockSignal At(bool hasKey, float time, float sinceRefusal) =>
            LockSignal.Evaluate(hasKey, 0f, time, sinceRefusal, Tuning);

        private static LockSignal Aimed(bool hasKey, float aim01, float time) =>
            LockSignal.Evaluate(hasKey, aim01, time, Never, Tuning);

        /// <summary>
        /// A door you can open must not look like one you cannot, since the colour is the only
        /// thing saying which it is.
        /// </summary>
        [Test]
        public void HavingTheKeyLooksDifferentFromNotHavingIt()
        {
            LockSignal locked = At(false, 0f, Never);
            LockSignal open = At(true, 0f, Never);

            float difference = Mathf.Abs(locked.Colour.r - open.Colour.r)
                               + Mathf.Abs(locked.Colour.g - open.Colour.g)
                               + Mathf.Abs(locked.Colour.b - open.Colour.b);

            Assert.Greater(difference, 0.5f,
                "a door you can open glows almost the same colour as one you cannot");
        }

        /// <summary>The whole reason this class exists: a refusal has to be seen.</summary>
        [Test]
        public void ARefusalIsLouderThanEitherRestingState()
        {
            float resting = At(false, 3f, Never).Intensity;
            float refused = At(false, 3f, 0f).Intensity;

            Assert.Greater(refused, resting * 2f,
                "the refusal flash is not clearly brighter than a door sitting idle, so a player " +
                "without the key gets no answer at all");
        }

        [Test]
        public void ARefusalFadesBackToRest()
        {
            float atRefusal = At(false, 3f, 0f).Intensity;
            float midway = At(false, 3f, Tuning.RefusalSeconds * 0.5f).Intensity;
            float after = At(false, 3f, Tuning.RefusalSeconds * 1.5f).Intensity;
            float never = At(false, 3f, Never).Intensity;

            Assert.Less(midway, atRefusal, "the refusal did not start fading");
            Assert.Greater(midway, never, "the refusal was over before it was seen");
            Assert.AreEqual(never, after, 1e-4f, "the refusal never finished fading");
        }

        /// <summary>
        /// A locked door breathes and an unlocked one sits still. The stillness is a signal in its
        /// own right: among a world of slowly pulsing red, the one steady thing reads as different
        /// before the player works out why.
        /// </summary>
        [Test]
        public void LockedDoorsBreatheAndOpenableOnesDoNot()
        {
            float lockedLow = float.PositiveInfinity;
            float lockedHigh = 0f;
            float openLow = float.PositiveInfinity;
            float openHigh = 0f;

            for (int step = 0; step < 64; step++)
            {
                float time = step / 64f * Tuning.BreathPeriod * 2f;
                lockedLow = Mathf.Min(lockedLow, At(false, time, Never).Intensity);
                lockedHigh = Mathf.Max(lockedHigh, At(false, time, Never).Intensity);
                openLow = Mathf.Min(openLow, At(true, time, Never).Intensity);
                openHigh = Mathf.Max(openHigh, At(true, time, Never).Intensity);
            }

            Assert.Greater(lockedHigh - lockedLow, 0.1f, "a locked door does not breathe");
            Assert.AreEqual(openLow, openHigh, 1e-5f, "an openable door flickers instead of sitting still");
        }

        /// <summary>
        /// The whole point of the highlight: a door the player is looking at has to be visibly
        /// different from the same door a moment earlier, or it is not telling them anything.
        /// </summary>
        [Test]
        public void LookingAtADoorBrightensIt()
        {
            // Both at the top of a breath, so what is being compared is the highlight and not
            // where in its cycle each one happened to be caught.
            Assert.Greater(Aimed(false, 1f, 0f).Intensity, Aimed(false, 0f, 0f).Intensity * 1.5f,
                "a locked door under the crosshair is barely brighter than one nobody is looking at");
            Assert.Greater(Aimed(true, 1f, 0f).Intensity, Aimed(true, 0f, 0f).Intensity * 1.5f,
                "an openable door gives no sign that the player is now close enough to open it");
        }

        /// <summary>
        /// Brightness carries where the player is looking; colour carries whether they can open it.
        /// Letting the highlight touch the colour would make a locked door you are staring at read
        /// as a door you have the key for.
        /// </summary>
        [Test]
        public void LookingAtADoorDoesNotChangeWhatItIsSaying()
        {
            Assert.AreEqual(Aimed(false, 0f, 0f).Colour, Aimed(false, 1f, 0f).Colour,
                "aiming at a locked door changed its colour");
            Assert.AreEqual(Aimed(true, 0f, 0f).Colour, Aimed(true, 1f, 0f).Colour,
                "aiming at an openable door changed its colour");
        }

        /// <summary>
        /// A locked door still breathes while the player looks at it. The breath is the only thing
        /// saying "you cannot open this", and a highlight that flattened it would take that
        /// sentence away at exactly the moment the player is close enough to need it.
        /// </summary>
        [Test]
        public void AHighlightedLockedDoorStillBreathes()
        {
            float low = float.PositiveInfinity;
            float high = 0f;

            for (int step = 0; step < 64; step++)
            {
                float intensity = Aimed(false, 1f, step / 64f * Tuning.BreathPeriod).Intensity;
                low = Mathf.Min(low, intensity);
                high = Mathf.Max(high, intensity);
            }

            Assert.Greater(high - low, 0.1f, "a locked door stops breathing when looked at");
        }

        /// <summary>
        /// A refusal is an answer to something the player did; a highlight is an answer to where
        /// they are looking. If the loudest highlight reaches the refusal, the two become one
        /// signal and the door's "no" disappears into it.
        /// </summary>
        [Test]
        public void ARefusalIsStillLouderThanTheBrightestHighlight()
        {
            float highlighted = Aimed(false, 1f, 0f).Intensity;
            float refused = LockSignal.Evaluate(false, 1f, 0f, 0f, Tuning).Intensity;

            Assert.Greater(refused, highlighted * 1.5f,
                "a door turning the player away looks much like a door they happen to be facing");
        }

        /// <summary>
        /// The ease is the caller's, so anything between the two ends has to land between them
        /// rather than snapping at a threshold — a highlight that jumped would flicker as the
        /// player turned past the edge of the interactor's reach.
        /// </summary>
        [Test]
        public void TheHighlightComesUpSmoothly()
        {
            float off = Aimed(false, 0f, 0f).Intensity;
            float half = Aimed(false, 0.5f, 0f).Intensity;
            float full = Aimed(false, 1f, 0f).Intensity;

            Assert.Greater(half, off, "the highlight does nothing until it is all the way on");
            Assert.Less(half, full, "the highlight is already fully on halfway through");
        }

        /// <summary>
        /// Out-of-range aim is clamped rather than trusted. An eased value that overshoots past 1
        /// would otherwise drive the door past the refusal's brightness and swallow it.
        /// </summary>
        [Test]
        public void AimOutsideItsRangeIsClamped()
        {
            Assert.AreEqual(Aimed(false, 1f, 0f).Intensity, Aimed(false, 4f, 0f).Intensity, 1e-4f);
            Assert.AreEqual(Aimed(false, 0f, 0f).Intensity, Aimed(false, -2f, 0f).Intensity, 1e-4f);
        }

        [Test]
        public void ADoorThatHasNeverRefusedAnyoneDoesNotFlash()
        {
            Assert.AreEqual(At(false, 2f, Never).Intensity, At(false, 2f, float.NaN).Intensity, 1e-5f);
            Assert.AreEqual(At(false, 2f, Never).Intensity, At(false, 2f, -1f).Intensity, 1e-5f);
        }
    }
}
