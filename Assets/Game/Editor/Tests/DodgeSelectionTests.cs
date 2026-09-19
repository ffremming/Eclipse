// What the dash button turns into, and why.
//
// Most of the evasive moveset hangs off one binding, so the only thing standing between "a dodge"
// and "a roll" is this ordering.
//
// Slide is deliberately absent: PlayerStance drops isSprinting the moment crouch goes down, so a
// "sprinting and crouching" test here could never come out true. The slide is taken off the crouch
// press instead, and the test that matters for it is that this selector never claims it.
//
// In Editor/ rather than beside the other EditMode tests for the same reason CarryMomentumTests
// is: DodgeSelector lives in Assembly-CSharp, which SpaceGame.Tests.EditMode does not reference.
using NUnit.Framework;
using UnityEngine;
using SpaceGame.Characters;

namespace SpaceGame.EditorTools
{
    public class DodgeSelectionTests
    {
        private const float BackThreshold = 0.5f;

        [Test]
        public void ASprintSpendsItselfOnARollWhicheverWayTheStickIsHeld()
        {
            Assert.AreEqual(DodgeMove.Roll,
                DodgeSelector.Choose(Vector2.up, sprinting: true, BackThreshold));

            // Holding back while sprinting is a turn, not a backstep: the roll still wins.
            Assert.AreEqual(DodgeMove.Roll,
                DodgeSelector.Choose(Vector2.down, sprinting: true, BackThreshold));
        }

        [Test]
        public void OnlyADeliberateBackwardsStickGivesABackstep()
        {
            Assert.AreEqual(DodgeMove.DodgeBack,
                DodgeSelector.Choose(new Vector2(0f, -1f), sprinting: false, BackThreshold));

            // A stick resting just off centre must not turn every neutral dodge into a backstep.
            Assert.AreEqual(DodgeMove.Dodge,
                DodgeSelector.Choose(new Vector2(0f, -0.2f), sprinting: false, BackThreshold));
        }

        [Test]
        public void AnythingElseIsAPlainDodgeAndNeverASlide()
        {
            foreach (var stick in new[] { Vector2.zero, Vector2.up, Vector2.right, Vector2.left })
            {
                Assert.AreEqual(DodgeMove.Dodge, DodgeSelector.Choose(stick, sprinting: false, BackThreshold),
                    $"stick {stick} should dodge");
            }
        }
    }
}
