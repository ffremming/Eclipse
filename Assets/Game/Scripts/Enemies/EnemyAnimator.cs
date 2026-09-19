// Drives the Goblin animator from an enemy's movement.
//
// Separate from EnemyAgent because it is the one part tied to a specific animator controller: the
// parameter names below are the Goblin controller's, shared with the player. Swap an enemy onto a
// different rig and this is the only file that has to change.
//
// The velocity arrives in world space and is turned into the controller's local SpeedX/SpeedY pair,
// which is what lets one blend tree cover walking forwards, backwards and sideways.
//
// Those two are metres per second, not a normalised direction: the Move tree anchors every clip at
// the ground speed that clip's stride actually covers, and the player feeds it the same units from
// Movement. Normalising here is what used to pin a chasing enemy between idle and walk.
//
// It also writes MoveAnimSpeed, which the controller multiplies the Move state by and defaults to
// zero. Left unwritten the blend tree still picks the right clip and then plays it at a standstill,
// so a goblin glides across the ground in a frozen stride.
//
// Swings come from the same MeleeSwingSequence the player's do, so a goblin that keeps pressing its
// attack escalates through the chain the way the player does, and one that loses the fight starts
// over from the opening swing.
using SpaceGame.Characters;
using SpaceGame.Gameplay;
using UnityEngine;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyAnimator : MonoBehaviour
    {
        private static readonly int SpeedX = Animator.StringToHash("SpeedX");
        private static readonly int SpeedY = Animator.StringToHash("SpeedY");
        private static readonly int MoveAnimSpeed = Animator.StringToHash("MoveAnimSpeed");
        private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int AttackIndex = Animator.StringToHash("AttackIndex");
        private static readonly int HurtTrigger = Animator.StringToHash("Hurt");
        private static readonly int DieTrigger = Animator.StringToHash("Die");

        [SerializeField] private Animator animator;
        [SerializeField] private HealthComponent health;

        [Tooltip("How many swing clips the Attack layer cycles through. Must match the number of " +
                 "swing states in the controller that AttackIndex selects between.")]
        [SerializeField] private int swingVariations = 5;

        [Tooltip("Longest gap between two swings that still continues the combo. Must be longer than " +
                 "the agent's attack cooldown, or every swing opens a fresh chain and the goblin " +
                 "only ever plays its opening clip.")]
        [SerializeField] private float chainWindow = 2.5f;

        [Tooltip("How quickly the blend tree catches up to a change in direction. Zero snaps, which " +
                 "makes an enemy rounding a corner look like it teleported into the new animation.")]
        [SerializeField] private float damping = 0.12f;

        [Tooltip("Ground speed the Move tree's run clip was authored to travel at — the player's " +
                 "runClipSpeed, because it is the same tree. Above it the walk cycle plays " +
                 "proportionally faster, so a goblin set to outrun its clip does not skate.")]
        [SerializeField] private float runClipSpeed = 4.92f;

        private MeleeSwingSequence swings;
        private bool? drivesStride;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (health == null) health = GetComponentInParent<HealthComponent>();

            // No cooldown of its own: EnemyAgent already paces its swings, and a second gate here
            // would only be a way for the two to disagree about when the goblin may attack.
            swings = new MeleeSwingSequence(swingVariations, cooldown: 0f, chainWindow);
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.OnDamage += OnDamaged;
            health.OnDeath += OnDied;
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.OnDamage -= OnDamaged;
            health.OnDeath -= OnDied;
        }

        private bool Ready => animator != null && animator.runtimeAnimatorController != null;

        // Only the Goblin controller multiplies its walk by MoveAnimSpeed. A rig that does not
        // declare it, like the dragon's, would have Unity warn about the missing parameter on every
        // frame, so it is asked once and remembered. Lazily, because the animator may sit on a child
        // that is still inactive at Awake and has no parameters to list yet.
        private bool DrivesStride => drivesStride ??= DeclaresParameter(MoveAnimSpeed);

        private bool DeclaresParameter(int hash)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == hash) return true;

            return false;
        }

        /// <summary>Feed the blend tree, in metres per second.</summary>
        public void SetMovement(Vector3 worldVelocity)
        {
            if (!Ready) return;

            Vector3 local = transform.InverseTransformDirection(worldVelocity);

            animator.SetFloat(SpeedX, local.x, damping, Time.deltaTime);
            animator.SetFloat(SpeedY, local.z, damping, Time.deltaTime);
            if (DrivesStride) animator.SetFloat(MoveAnimSpeed, StrideRate.For(worldVelocity, runClipSpeed));
            animator.SetBool(IsGrounded, true);
        }

        /// <summary>Play the next swing in the combo, so repeated attacks do not replay one clip.</summary>
        public void PlayAttack()
        {
            if (!Ready) return;
            if (!swings.TrySwing(Time.time, out int index)) return;

            animator.SetInteger(AttackIndex, index);
            animator.SetTrigger(AttackTrigger);
        }

        private void OnDamaged(int amount)
        {
            if (Ready) animator.SetTrigger(HurtTrigger);
        }

        private void OnDied()
        {
            if (Ready) animator.SetTrigger(DieTrigger);
        }
    }
}
