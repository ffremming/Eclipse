// One enemy: the only behaviour component a hostile goblin needs.
//
// It does three things each tick — gather what can be sensed, ask EnemyBrain what to do, carry the
// answer out — and the middle one is where all the decisions live. Nothing here chooses anything,
// which is deliberate: a decision buried in an Update is a decision nothing can test, and the whole
// point of EnemyBrain being a plain class is that the EditMode suite can reach it.
//
// Replaces AgentController and its stack of prioritised behaviour modules. The modules were built
// for creatures that combined behaviours in ways a switch could not express; these enemies do not,
// and the indirection cost more than it bought.
using SpaceGame.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAgent : MonoBehaviour
    {
        [Header("Camp")]
        [Tooltip("The camp this enemy belongs to. Left empty, it treats wherever it spawned as home " +
                 "and stays a loner — which is a legitimate way to place a lone sentry.")]
        [SerializeField] private EnemyBase home;

        [Header("Sight")]
        [Tooltip("How far it can see.")]
        [SerializeField] private float sightRange = 18f;

        [Tooltip("Half its field of view, in degrees. 70 gives a believable forward cone you can " +
                 "flank; raise it towards 180 for something that sees all round.")]
        [SerializeField] private float sightHalfAngle = 70f;

        [Tooltip("Height its eyes sit at, and the height on the target it looks for. Too low and " +
                 "every raycast clips the ground in front of it.")]
        [SerializeField] private float eyeHeight = 1.6f;

        [Tooltip("What blocks sight. Must not include the layer the enemies or the player are on, " +
                 "or they will block their own line of sight to each other.")]
        [SerializeField] private LayerMask sightBlockers = 1;

        [Tooltip("Seconds between line-of-sight checks. Every frame is wasted work — a fifth of a " +
                 "second is far below what anyone notices, and costs a fifth of the raycasts.")]
        [SerializeField] private float sightInterval = 0.2f;

        [Header("Hearing")]
        [Tooltip("How far its shout carries when it notices the player. This is what makes a camp " +
                 "react together rather than one goblin at a time.")]
        [SerializeField] private float shoutRadius = 14f;

        [Header("Behaviour")]
        [Tooltip("How close it gets before it stops and swings.")]
        [SerializeField] private float attackRange = 2.2f;

        [Tooltip("Seconds between swings.")]
        [SerializeField] private float attackCooldown = 1.4f;

        [Tooltip("How far from camp it will chase before giving up and walking back.")]
        [SerializeField] private float leashRadius = 30f;

        [Tooltip("Seconds it stays angry after losing sight of the player.")]
        [SerializeField] private float aggroMemory = 5f;

        [Tooltip("Seconds it stands still between strolls around camp.")]
        [SerializeField] private float wanderPause = 2f;

        [Tooltip("How close counts as having arrived somewhere.")]
        [SerializeField] private float arrivalRadius = 1f;

        [Header("Parts")]
        [SerializeField] private MeleeStrike strike;
        [SerializeField] private EnemyAnimator enemyAnimator;
        [SerializeField] private HealthComponent health;

        [Tooltip("Degrees per second it turns on the spot while attacking.")]
        [SerializeField] private float turnSpeed = 540f;

        private NavMeshAgent navAgent;
        private EnemyBrain brain;

        private Transform target;
        private float nextTargetSearch;

        private bool canSeeTarget;
        private float nextSightCheck;

        private Vector3 wanderTarget;
        private bool hasWanderTarget;

        private bool heardAlert;
        private bool tookDamage;

        private Vector3 homePosition;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            if (strike == null) strike = GetComponent<MeleeStrike>();
            if (enemyAnimator == null) enemyAnimator = GetComponentInChildren<EnemyAnimator>(true);
            if (health == null) health = GetComponent<HealthComponent>();

            // Where it stands at spawn, used only when it has no camp. Read in Awake rather than on
            // demand so that a loner's idea of home does not follow it as it walks.
            homePosition = transform.position;

            brain = new EnemyBrain(new EnemySettings
            {
                AttackRange = attackRange,
                AttackCooldown = attackCooldown,
                LeashRadius = leashRadius,
                AggroMemory = aggroMemory,
                WanderRadius = home != null ? home.WanderRadius : EnemySettings.Default.WanderRadius,
                WanderPause = wanderPause,
                ArrivalRadius = arrivalRadius,
            });
        }

        private void OnEnable()
        {
            EnemyAlert.Register(this);
            if (health != null)
            {
                health.OnDamage += OnDamaged;
                health.OnDeath += OnDied;
            }

            // Coming back from a knockdown, the body is not where it was when it went down, so
            // whatever it was strolling towards is no longer a plan. It picks somewhere new.
            hasWanderTarget = false;
        }

        private void OnDisable()
        {
            EnemyAlert.Unregister(this);
            if (health != null)
            {
                health.OnDamage -= OnDamaged;
                health.OnDeath -= OnDied;
            }

            // Switched off mid-swing — knocked down, killed, or suspended by the ragdoll. The blade
            // must not stay dangerous for a body that is no longer being driven.
            if (strike != null) strike.Cancel();
        }

        /// <summary>Told by a neighbour that there is something to fight. Consumed on the next tick.</summary>
        public void HearAlert() => heardAlert = true;

        private void Update()
        {
            // Off the mesh as well as merely disabled: a body the ragdoll has left somewhere
            // unwalkable would otherwise throw on every SetDestination until it was moved back.
            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

            ResolveTarget();
            UpdateSight();

            EnemySenses senses = ReadSenses();
            EnemyDecision decision = brain.Tick(senses, Time.time);

            heardAlert = false;
            tookDamage = false;

            Act(decision, senses);
        }

        private EnemySenses ReadSenses()
        {
            bool hasTarget = target != null;
            Vector3 targetPosition = hasTarget ? target.position : Vector3.zero;

            return new EnemySenses
            {
                Position = transform.position,
                HomePosition = home != null ? home.Position : homePosition,
                HasTarget = hasTarget,
                TargetPosition = targetPosition,
                CanSeeTarget = canSeeTarget,
                HeardAlert = heardAlert,
                TookDamage = tookDamage,
                TargetInsideBase = hasTarget && home != null && home.Defends(targetPosition),
                WanderTarget = wanderTarget,
                HasWanderTarget = hasWanderTarget,
                ReachedWanderTarget = hasWanderTarget && Arrived(wanderTarget),
            };
        }

        private void Act(in EnemyDecision decision, in EnemySenses senses)
        {
            if (decision.ShoutAlert) EnemyAlert.Shout(this, transform.position, shoutRadius);

            if (decision.WantsNewWanderTarget) PickWanderTarget(senses.HomePosition);

            if (decision.HasDestination)
            {
                navAgent.isStopped = false;
                navAgent.SetDestination(decision.Destination);
            }
            else if (navAgent.hasPath)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
            }

            // Navigation steers by facing where it is going, which is no use to something standing
            // still. Handed back the moment it starts moving again.
            navAgent.updateRotation = !decision.FaceTarget;
            if (decision.FaceTarget && senses.HasTarget) FaceTowards(senses.TargetPosition);

            if (decision.Attack && strike != null)
            {
                strike.Swing();
                if (enemyAnimator != null) enemyAnimator.PlayAttack();
            }

            if (enemyAnimator != null) enemyAnimator.SetMovement(navAgent.velocity, navAgent.speed);
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (flat.sqrMagnitude < 1e-4f) return;

            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                                                          Quaternion.LookRotation(flat, Vector3.up),
                                                          turnSpeed * Time.deltaTime);
        }

        private bool Arrived(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            return flat.sqrMagnitude <= arrivalRadius * arrivalRadius;
        }

        /// <summary>
        /// A spot on the navmesh somewhere in camp. Sampled rather than trusted: a random point in a
        /// circle is very often inside a rock or off the edge of the walkable ground, and sending an
        /// agent to one is how you get an enemy that walks into a wall and stays there.
        /// </summary>
        private void PickWanderTarget(Vector3 origin)
        {
            float radius = home != null ? home.WanderRadius : EnemySettings.Default.WanderRadius;
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);

            hasWanderTarget = NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas);
            if (hasWanderTarget) wanderTarget = hit.position;
        }

        // Re-resolved on an interval rather than cached once, because the player is destroyed and
        // respawned between deaths and the old reference does not survive it.
        private void ResolveTarget()
        {
            if (target != null && target.gameObject.activeInHierarchy) return;
            if (Time.time < nextTargetSearch) return;

            nextTargetSearch = Time.time + sightInterval;

            GameObject player = GameObject.FindGameObjectWithTag(SpawnClearance.PlayerTag);
            target = player != null ? player.transform : null;

            if (target == null) canSeeTarget = false;
        }

        private void UpdateSight()
        {
            if (Time.time < nextSightCheck) return;
            nextSightCheck = Time.time + sightInterval;

            canSeeTarget = target != null && HasLineOfSight();
        }

        private bool HasLineOfSight()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 chest = target.position + Vector3.up * eyeHeight;

            if (!Cone.Contains(eye, transform.forward, chest, sightRange, sightHalfAngle)) return false;

            Vector3 toTarget = chest - eye;
            if (!Physics.Raycast(eye, toTarget.normalized, out RaycastHit hit, toTarget.magnitude,
                                 sightBlockers, QueryTriggerInteraction.Ignore))
                return true;

            // Whatever the ray stopped on is the target's own body. That is not an obstruction —
            // it IS the thing being looked at, and treating it as one would blind every enemy to a
            // player standing in the open on the same layer as the scenery.
            return hit.transform.root == target.root;
        }

        // Aggro rather than a hit reaction: the reaction is the animator's job, and this is only
        // interested in the fact that something out there is worth turning round for.
        private void OnDamaged(int amount) => tookDamage = true;

        private void OnDied()
        {
            if (strike != null) strike.Cancel();
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh) navAgent.ResetPath();

            // Switched off rather than left ticking a brain for a corpse. Anything that revives the
            // enemy turns this back on.
            enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, -sightHalfAngle, 0f) * transform.forward * sightRange);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, sightHalfAngle, 0f) * transform.forward * sightRange);

            Gizmos.color = new Color(0.6f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, shoutRadius);
        }
    }
}
