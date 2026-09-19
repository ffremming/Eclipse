// The combo rule: rhythm walks the chain, a dropped rhythm starts it over.
//
// Worth pinning because both failure modes are quiet. Make the chain window shorter than the
// cooldown and there is no press rate that both clears one and stays inside the other, so the
// player can never reach the finisher and it just looks like the later swings are not hooked up.
// Forget to reset on a lapse and they open every fight on the finisher.
//
// In Editor/ rather than beside the other EditMode tests for the same reason CarryMomentumTests
// is: MeleeSwingSequence lives in Assembly-CSharp, which SpaceGame.Tests.EditMode does not
// reference.
using NUnit.Framework;
using SpaceGame.Characters;

namespace SpaceGame.EditorTools
{
    public class MeleeComboTests
    {
        private const int Variations = 5;
        private const float Cooldown = 0.35f;
        private const float Window = 1.1f;

        private static MeleeSwingSequence Fresh() => new MeleeSwingSequence(Variations, Cooldown, Window);

        [Test]
        public void ASteadyRhythmWalksTheChainToTheFinisherAndWrapsRoundAgain()
        {
            var seq = Fresh();
            float t = 0f;

            // A little over the cooldown rather than exactly on it: a player presses at a rhythm,
            // and pressing on the boundary itself is knife-edge once float error accumulates.
            const float Rhythm = Cooldown + 0.05f;

            for (int expected = 0; expected < Variations; expected++)
            {
                Assert.IsTrue(seq.TrySwing(t, out int index), $"swing {expected} should be allowed");
                Assert.AreEqual(expected, index);
                t += Rhythm;                         // inside the window, clear of the cooldown
            }

            // Past the finisher the chain comes round to the opening swing again.
            Assert.IsTrue(seq.TrySwing(t, out int wrapped));
            Assert.AreEqual(0, wrapped);
        }

        [Test]
        public void APressInsideTheCooldownIsRefusedAndDoesNotBurnAChainStep()
        {
            var seq = Fresh();

            Assert.IsTrue(seq.TrySwing(0f, out int first));
            Assert.AreEqual(0, first);

            Assert.IsFalse(seq.TrySwing(Cooldown * 0.5f, out _), "a press inside the cooldown is refused");

            // The refusal must not have advanced anything: the next real swing is still step 1.
            Assert.IsTrue(seq.TrySwing(Cooldown, out int second));
            Assert.AreEqual(1, second);
        }

        [Test]
        public void LettingTheRhythmLapseStartsTheChainOverAtTheOpeningSwing()
        {
            var seq = Fresh();

            Assert.IsTrue(seq.TrySwing(0f, out _));
            Assert.IsTrue(seq.TrySwing(Cooldown, out int second));
            Assert.AreEqual(1, second, "still mid-chain");

            // Wander off for longer than the window, then come back.
            Assert.IsTrue(seq.TrySwing(Cooldown + Window + 0.01f, out int afterLapse));
            Assert.AreEqual(0, afterLapse, "a lapsed chain reopens rather than resuming");
        }

        [Test]
        public void AWindowShorterThanTheCooldownIsWidenedRatherThanStrandingTheChain()
        {
            // Misconfiguration in the Inspector must not produce a combo that cannot advance.
            var seq = new MeleeSwingSequence(Variations, Cooldown, chainWindow: 0.01f);

            Assert.IsTrue(seq.TrySwing(0f, out int first));
            Assert.AreEqual(0, first);
            Assert.IsTrue(seq.TrySwing(Cooldown, out int second));
            Assert.AreEqual(1, second, "the chain still advances at the fastest legal press rate");
        }
    }
}
