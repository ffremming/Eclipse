// Life in a character who is not doing anything.
//
// The Move tree's centre is a calm, almost-still idle, because that is what a base idle has to be:
// it is blended against at every speed near zero, so anything busy there leaks into the start of
// every walk. The personality goes here instead, as occasional breaks played on top of that calm
// base after the player has been still for a while.
//
// Its own component rather than more of PlayerMovement, which already owns the rigidbody, the
// ground probe, the dash and the fall table. Standing still doing nothing is not a movement rule.
using SpaceGame.Core;
using UnityEngine;

namespace SpaceGame.Characters
{
    [DisallowMultipleComponent]
    public class PlayerIdleBreaks : MonoBehaviour
    {
        private static readonly int IdleBreakTrigger = Animator.StringToHash("IdleBreak");
        private static readonly int IdleBreakIndex = Animator.StringToHash("IdleBreakIndex");
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMovement movement;

        [Tooltip("How many break clips the controller cycles through. Must match the number of " +
                 "break states that IdleBreakIndex selects between.")]
        [SerializeField] private int breakVariations = 2;

        [Tooltip("Roughly how long the player stands still before the character does something. " +
                 "The actual gap is this give or take the jitter below.")]
        [SerializeField] private float breakInterval = 7f;

        [Tooltip("How much the gap varies. Zero makes the breaks metronomic, which reads worse " +
                 "than having none at all.")]
        [SerializeField] private float breakJitter = 2.5f;

        [Tooltip("Stick movement below this still counts as standing still, so a resting stick " +
                 "does not keep resetting the timer and suppress breaks forever.")]
        [SerializeField] private float stillThreshold = 0.05f;

        private PlayerInputManager input;
        private float stillFor;
        private float delay;
        private int nextBreak;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (movement == null) movement = GetComponent<PlayerMovement>();
            input = GetComponent<PlayerInputManager>();

            delay = IdleBreakSchedule.NextDelay(breakInterval, breakJitter, Random.value);
        }

        private bool Ready => animator != null && animator.runtimeAnimatorController != null;

        /// <summary>
        /// Still means not asking to move and not in the air. Deliberately read off the input
        /// rather than off the rigidbody: a player pinned against a wall is holding the stick and
        /// should not start looking around, even though their velocity is zero.
        /// </summary>
        private bool StandingStill()
        {
            if (input == null || movement == null) return false;
            if (!movement.IsOnGround) return false;
            return input.MoveInput.sqrMagnitude < stillThreshold * stillThreshold;
        }

        private void Update()
        {
            if (!Ready) return;

            bool still = StandingStill();

            // Owned here rather than in PlayerMovement's animator block because it means exactly
            // "the idle break should give way", not "the body has velocity" — it is what cuts a
            // break short the frame the player asks to move, instead of making them watch it out.
            animator.SetBool(IsMoving, !still);

            if (!still)
            {
                stillFor = 0f;
                return;
            }

            stillFor += Time.deltaTime;
            if (stillFor < delay) return;

            animator.SetInteger(IdleBreakIndex, nextBreak);
            animator.SetTrigger(IdleBreakTrigger);
            nextBreak = (nextBreak + 1) % Mathf.Max(1, breakVariations);

            stillFor = 0f;
            delay = IdleBreakSchedule.NextDelay(breakInterval, breakJitter, Random.value);
        }
    }
}
