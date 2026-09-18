// Deals melee damage to a target when within attack range.
// Claims movement: returns StopAndFace while in range (preempting ChaseModule) and null otherwise,
// so ChaseModule at lower priority can drive the approach when the target is out of melee reach.
using System;
using UnityEngine;
using UnityEngine.Events;
using FMODUnity;
using SpaceGame.Audio;
using SpaceGame.Core;
using SpaceGame.Gameplay;

namespace SpaceGame.Agents
{
    public class CloseCombatModule : BehaviourModuleBase
    {
        [Header("Attack")]
        [SerializeField] private float attackRange = 5f;
        [Tooltip("Fraction of attackRange the target must exceed before the agent gives up the swing " +
                 "and starts closing again. 1.15 means it holds position out to 115% of attackRange. " +
                 "Without this gap the winner alternates every frame at the range boundary and the " +
                 "NavMesh path is discarded and re-requested until the agent visibly stutters.")]
        [SerializeField] [Range(1f, 2f)] private float rangeExitFactor = 1.15f;
        [SerializeField] private float attackCooldown = 1.2f;
        [SerializeField] private int attackDamage = 10;
        [Tooltip("Seconds the agent stays locked in StopAndFace after a swing fires — keeps the attack committed so it can't start walking mid-animation if the target drifts out of attackRange. Typically set to the length of the attack animation.")]
        [SerializeField] private float attackCommitDuration = 0.5f;

        [Header("Animation")]
        [Tooltip("Trigger to fire on each attack. Leave empty to disable.")]
        [SerializeField] private string attackAnimTrigger = "Meele";

        [Header("Events")]
        public UnityEvent<Transform> OnAttack;
        public event Action OnAttackEvent;

        [Header("Audio")]
        [SerializeField] private SfxId attackId = SfxId.EntityAttack;
        [SerializeField] private EventReference attackSound;

        private float cooldownTimer;
        // Ticks down after a swing fires; while > 0, the module keeps returning StopAndFace regardless
        // of target distance so the in-progress swing can't be interrupted by Chase.
        private float commitTimer;
        // True while the agent is holding position to fight. Combined with rangeExitFactor this is
        // the hysteresis: entering costs attackRange, leaving costs attackRange * rangeExitFactor.
        private bool engaged;
        private Animator animator;

        // Read by ChaseModule (to tighten chaseStopDistance and skip herd-spread offsets that would
        // park the agent outside melee reach) and by AgentTargeting (to cover the range in its
        // acquisition window).
        public float AttackRange => attackRange;

        // ── Save/restore ──────────────────────────────────────────────────────────
        //
        // A swing's cadence outlives a session. Reloading at zero cooldown hands whoever reloaded a
        // free hit — a creature caught mid-recovery comes back able to strike immediately — and
        // reloading with commitTimer at zero lets Chase reclaim a frame that a swing had committed.
        //
        // OnEnable clears all three, and runs on either side of a restore depending on the
        // hydration path, hence the latch. See Core/Persistence/Adapters/CombatCadenceSaveable.cs.
        private bool cadenceRestored;

        public float CooldownTimer => cooldownTimer;
        public float CommitTimer => commitTimer;
        public bool Engaged => engaged;

        /// <summary>Restore-only. Called by the save system; do not call from gameplay.</summary>
        public void RestoreCadence(float cooldown, float commit, bool wasEngaged)
        {
            cadenceRestored = true;
            cooldownTimer = cooldown;
            commitTimer = commit;
            engaged = wasEngaged;
        }

        private void Reset() => SetPriorityDefault(ModulePriority.MeleeAttack);

        private void OnEnable()
        {
            // A restore already set this module up. Consumed, so a later genuine enable — a
            // threshold reaction, an ownership change — still resets the cadence as it always did.
            if (cadenceRestored)
            {
                cadenceRestored = false;
            }
            else
            {
                cooldownTimer = 0f;
                commitTimer = 0f;
                engaged = false;
            }
        }

        private void Awake()
        {
            FindChildByName("Sword")?.SetActive(IsActive);
            animator = GetComponentInChildren<Animator>();
        }

        public override MoveIntent? Tick(in AgentContext context, float deltaTime)
        {
            // Advance timers every frame so a target stepping out and back can't instant-hit,
            // and so the commit window decays even on frames we're not returning an intent.
            cooldownTimer -= deltaTime;
            commitTimer -= deltaTime;

            AgentTargeting targeting = context.Targeting;
            Transform target = targeting != null && targeting.HasTarget ? targeting.Target : null;
            if (target == null)
            {
                engaged = false;
                return null;
            }

            // Mid-swing: keep the agent planted and facing the target regardless of distance,
            // so Chase can't reclaim the frame and start walking while the attack animation plays.
            if (commitTimer > 0f)
                return MoveIntent.StopAndFace(target.position);

            float distance = targeting.DistanceToTarget;
            float threshold = engaged ? attackRange * rangeExitFactor : attackRange;
            if (distance > threshold)
            {
                engaged = false;
                return null;
            }

            engaged = true;

            // Only swing when genuinely inside attackRange — the exit factor exists to stop the
            // agent walking away, not to extend its reach.
            if (cooldownTimer <= 0f && distance <= attackRange)
            {
                Attack(target);
                cooldownTimer = attackCooldown;
                commitTimer = attackCommitDuration;
            }

            return MoveIntent.StopAndFace(target.position);
        }

        /// <summary>
        /// One swing. Runs on the machine that simulates this agent and on no other.
        ///
        /// <para>
        /// The consequence used to be that the sound and the trigger only fired on the simulating
        /// machine, so nobody else saw the swing at all. They now go out as
        /// <see cref="NetMsg.AgentActed"/> — see <see cref="PresentSwing"/>, which is the half of
        /// this method that runs everywhere.
        /// </para>
        /// </summary>
        private void Attack(Transform target)
        {
            var health = target.GetComponentInChildren<HealthComponent>();
            if (health != null && health.Alive)
                Damage.Apply(health.gameObject, attackDamage, transform);

            // Deliberately NOT part of the presentation below: this hands out the TARGET, and a
            // handler holding the victim is one edit away from being a second thing that damages
            // it. The presentation gets a position and nothing else.
            OnAttack?.Invoke(target);

            // The agent's own position — a swing has no muzzle, so its origin is the body that
            // made it.
            PresentSwing(transform.position);
        }

        /// <summary>
        /// One swing's look and sound.
        ///
        /// <para>
        /// Nothing below this line may damage, spawn or consume anything — that is the whole
        /// boundary, and <see cref="Attack"/> above it is the only side that decides.
        /// </para>
        /// </summary>
        private void PresentSwing(Vector3 origin)
        {
            Sfx.Play(attackId, origin, attackSound, GetInstanceID());

            if (animator && !string.IsNullOrEmpty(attackAnimTrigger))
                animator.SetTrigger(attackAnimTrigger);

            OnAttackEvent?.Invoke();
        }


        private GameObject FindChildByName(string childName)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t.gameObject;
            return null;
        }

        protected override void OnValidate()
        {
            attackRange = Mathf.Max(0.1f, attackRange);
            attackCooldown = Mathf.Max(0.1f, attackCooldown);
            attackDamage = Mathf.Max(0, attackDamage);
            attackCommitDuration = Mathf.Max(0f, attackCommitDuration);
            SetMinPriority(ModulePriority.MeleeAttack);
        }
    }
}
