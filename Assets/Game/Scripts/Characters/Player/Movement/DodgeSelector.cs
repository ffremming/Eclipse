// Which evasive move one press of the dash button should produce.
//
// Split out of PlayerMovement for the reason MeleeSwingSequence is split out of PlayerMeleeSwing:
// it is the part with no Rigidbody and no frame in it, so the rule can be pinned by a test without
// entering play mode.
//
// The moveset hangs off buttons the player already holds, read against what they are already
// doing, rather than asking them to learn a binding per move. Sprinting is the interesting case: a
// sprint is already a commitment, so spending it on a roll reads as a continuation of the run
// rather than as a new verb.
//
// Slide is NOT decided here, and that is deliberate. PlayerStance drops isSprinting the moment
// crouch goes down, so a "sprinting AND crouching" test can never come out true — it is not a rare
// case, it is an unreachable one. The slide is therefore taken off the crouch press itself, while
// the sprint is still live, by PlayerStance.
using UnityEngine;

namespace SpaceGame.Characters
{
    public static class DodgeSelector
    {
        /// <summary>
        /// Pick the move for a dash press.
        /// </summary>
        /// <param name="moveInput">Stick or WASD, in the player's local frame.</param>
        /// <param name="backThreshold">
        /// How far back the stick must be to count as backwards. Above zero so that a stick
        /// resting slightly off centre does not turn every neutral dodge into a backstep.
        /// </param>
        public static DodgeMove Choose(Vector2 moveInput, bool sprinting, float backThreshold)
        {
            if (sprinting) return DodgeMove.Roll;
            if (moveInput.y < -Mathf.Abs(backThreshold)) return DodgeMove.DodgeBack;
            return DodgeMove.Dodge;
        }
    }
}
