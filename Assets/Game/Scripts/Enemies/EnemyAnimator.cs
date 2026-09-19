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
using SpaceGame.Gameplay;
using UnityEngine;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyAnimator : MonoBehaviour
    {
        private static readonly int SpeedX = Animator.StringToHash("SpeedX");
        private static readonly int SpeedY = Animator.StringToHash("SpeedY");
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

        [Tooltip("How quickly the blend tree catches up to a change in direction. Zero snaps, which " +
                 "makes an enemy rounding a corner look like it teleported into the new animation.")]
        [SerializeField] private float damping = 0.12f;

        private int nextSwing;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (health == null) health = GetComponentInParent<HealthComponent>();
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

        /// <summary>Feed the blend tree, in metres per second.</summary>
        public void SetMovement(Vector3 worldVelocity)
        {
            if (!Ready) return;

            Vector3 local = transform.InverseTransformDirection(worldVelocity);

            animator.SetFloat(SpeedX, local.x, damping, Time.deltaTime);
            animator.SetFloat(SpeedY, local.z, damping, Time.deltaTime);
            animator.SetBool(IsGrounded, true);
        }

        /// <summary>Play the next swing in the cycle, so repeated attacks do not replay one clip.</summary>
        public void PlayAttack()
        {
            if (!Ready) return;

            animator.SetInteger(AttackIndex, nextSwing);
            animator.SetTrigger(AttackTrigger);
            nextSwing = (nextSwing + 1) % Mathf.Max(1, swingVariations);
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
