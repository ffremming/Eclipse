// Which swing comes next, and whether one is allowed yet.
//
// Split out of PlayerMeleeSwing for the same reason AimPose is split out of PlayerAimRig: it is
// the part with no Animator and no frame in it, so the rule can be tested without entering play
// mode.
//
// The rule is a combo rather than a rotation. Keeping the button pressed at a steady rhythm walks
// the chain from the opening swing to the finisher; letting the rhythm drop starts over from the
// opening swing. That is what makes a moveset feel like something the player is driving rather
// than a bag of clips being shuffled — the same press produces a different swing depending on what
// they did a moment ago, so the escalation is theirs to earn.
using UnityEngine;

namespace SpaceGame.Characters
{
    public class MeleeSwingSequence
    {
        private readonly int variations;
        private readonly float cooldown;
        private readonly float chainWindow;

        private int nextIndex;
        private float lastSwingTime = float.NegativeInfinity;

        /// <param name="variations">How many swings the chain runs through before wrapping.</param>
        /// <param name="cooldown">Shortest gap between swings. A press inside it is refused.</param>
        /// <param name="chainWindow">
        /// Longest gap that still counts as continuing the combo. Must be longer than
        /// <paramref name="cooldown"/> or there is no rhythm that both clears the cooldown and
        /// stays inside the window, and the chain can never advance past its first swing.
        /// </param>
        public MeleeSwingSequence(int variations, float cooldown, float chainWindow)
        {
            this.variations = Mathf.Max(1, variations);
            this.cooldown = Mathf.Max(0f, cooldown);
            this.chainWindow = Mathf.Max(this.cooldown, chainWindow);
        }

        /// <summary>The swing that would play next, for callers that want to look without asking.</summary>
        public int NextIndex => nextIndex;

        /// <summary>
        /// Ask for a swing at <paramref name="time"/>.
        ///
        /// <para>
        /// Returns false while the previous swing is still inside its cooldown. Refused rather
        /// than buffered: a held mouse button would otherwise stack a swing per frame and the
        /// player would keep swinging seconds after letting go.
        /// </para>
        /// <para>
        /// A gap longer than the chain window drops the combo back to its opening swing, so
        /// wandering off and coming back does not hand the player the finisher for free.
        /// </para>
        /// </summary>
        public bool TrySwing(float time, out int index)
        {
            float since = time - lastSwingTime;

            if (since < cooldown)
            {
                index = nextIndex;
                return false;
            }

            if (since > chainWindow) nextIndex = 0;

            index = nextIndex;
            lastSwingTime = time;
            nextIndex = (nextIndex + 1) % variations;
            return true;
        }
    }
}
