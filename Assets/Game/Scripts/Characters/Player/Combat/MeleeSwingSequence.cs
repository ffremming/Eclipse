// Which swing comes next, and whether one is allowed yet.
//
// Split out of PlayerMeleeSwing for the same reason AimPose is split out of PlayerAimRig: it is
// the part with no Animator and no frame in it, so the rule — swings cycle, and a swing inside
// the cooldown is refused rather than queued — can be tested without entering play mode.
using UnityEngine;

namespace SpaceGame.Characters
{
    public class MeleeSwingSequence
    {
        private readonly int variations;
        private readonly float cooldown;

        private int nextIndex;
        private float lastSwingTime = float.NegativeInfinity;

        public MeleeSwingSequence(int variations, float cooldown)
        {
            this.variations = Mathf.Max(1, variations);
            this.cooldown = Mathf.Max(0f, cooldown);
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
        /// </summary>
        public bool TrySwing(float time, out int index)
        {
            index = nextIndex;
            if (time - lastSwingTime < cooldown) return false;

            lastSwingTime = time;
            nextIndex = (nextIndex + 1) % variations;
            return true;
        }
    }
}
