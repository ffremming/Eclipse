using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SpaceGame.Core;
using SpaceGame.Gameplay;
using PlayerInputManager = SpaceGame.Core.PlayerInputManager;

namespace SpaceGame.Characters
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement : MonoBehaviour
    {
        private PlayerInputManager inputs; 
    
        [Header("Movement")]
        [Tooltip("Ordinary walking speed, and the one every other stance is measured against.")]
        [SerializeField] private float moveSpeed = 6f;

        [Tooltip("Speed while sprinting — double-tap forward and hold. See PlayerStance.")]
        [SerializeField] private float sprintSpeed = 9f;

        [Tooltip("Speed while crouched.")]
        [SerializeField] private float crouchSpeed = 2.6f;

        [SerializeField, Range(0f, 1f)] private float airControl = 0.3f;

        [Tooltip("Upward speed, in m/s, above which a carried fling still counts as in flight — " +
                 "see SteerWithoutBraking. Big enough to ignore the jitter of standing on a " +
                 "collider, small enough that any real launch clears it.")]
        [SerializeField] private float momentumRiseThreshold = 0.5f;

        [Tooltip("Sideways acceleration available while hanging from a rope, in m/s². Steering " +
                 "only — see SteerTether. It can turn a swing and pump it, never slow one.")]
        [SerializeField] private float tetherAcceleration = 22f;

        [Header("Jumping")]
        [SerializeField] private float jumpForce = 7f;
        [SerializeField] private float jumpCooldown = 0.6f;
        [SerializeField] private float groundCheckDistance = 0.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Animation matching")]
        [Tooltip("Ground speed the Move tree's run clip was authored to travel at. Above this " +
                 "the whole cycle is played proportionally faster, so a sprint puts down more " +
                 "steps instead of skating on the same ones. Measured from the clip's stride, so " +
                 "it must match the run anchor in the Move tree.")]
        [SerializeField] private float runClipSpeed = 4.92f;

        [Tooltip("The same figure for the Crouch tree's walk clip.")]
        [SerializeField] private float crouchClipSpeed = 1.29f;

        [Header("Dash")]
        [Tooltip("Launch speed of a plain sidestep or backstep. The evasive clips play in place " +
                 "and this is what actually moves the body, so raising it past what the clip's " +
                 "own stride covers is what makes a dodge look like it is on ice.")]
        [SerializeField] private float dashSpeed = 6f;

        [Tooltip("Launch speed of a sprinting roll. Faster than a dodge because the roll is " +
                 "spending momentum the player already had.")]
        [SerializeField] private float rollSpeed = 9f;

        [Tooltip("Floor under a slide's launch speed. A slide entered from a faster sprint keeps " +
                 "the sprint's speed instead, so sliding can never be a way of slowing down.")]
        [SerializeField] private float slideSpeed = 11f;

        [Tooltip("How long a slide holds its own speed before ordinary crouch movement takes " +
                 "over. The ground lerp is suppressed for this whole window, which is what stops " +
                 "crouchSpeed from erasing the slide on the next physics tick.")]
        [SerializeField] private float slideDuration = 0.8f;

        [Tooltip("How far back the stick must be held for a dash to become a backstep rather " +
                 "than a sidestep.")]
        [SerializeField] private float dodgeBackThreshold = 0.5f;

        [SerializeField] private GameObject playerCamera;

        [SerializeField] private Rigidbody rb;
        [SerializeField] private Animator animator;
        [SerializeField] private CapsuleCollider playerCollider;
        private PlayerStance stance;
        private Vector2 moveInput;
        private float jumpCooldownTimer;
        private bool jumpOnCooldown;
        private bool groundSnapEnabled = true;
    
        [Header("Fall Damage")]
        [SerializeField] private float minFallSpeed = -5f;
        [SerializeField] private float maxFallSpeed = -30f;
        [SerializeField] private int maxFallDamage = 100;

        private float lastYVelocity;
        private bool wasGrounded;

        // Movement already computes the exact edges audio needs — the grounded transition, the
        // successful jump, the dash — and all of it from private state. Exposing them as events is
        // cheaper and far less brittle than a second component re-deriving grounded-ness with its
        // own raycast, which would drift from this one the moment either is tuned.
        /// <summary>Raised when a jump actually leaves the ground, not merely when the key is pressed.</summary>
        public event Action OnJumped;

        /// <summary>Raised on touchdown, carrying the vertical speed at impact (negative when falling).</summary>
        public event Action<float> OnLanded;

        /// <summary>Raised on a dash that was allowed to happen.</summary>
        public event Action OnDashed;

        /// <summary>Horizontal speed in m/s. Used to pace footsteps.</summary>
        public float HorizontalSpeed
        {
            get
            {
                if (rb == null) return 0f;
                Vector3 v = rb.linearVelocity;
                return new Vector3(v.x, 0f, v.z).magnitude;
            }
        }

        /// <summary>Whether the player was on the ground as of the last physics step.</summary>
        public bool IsOnGround => wasGrounded;

        /// <summary>
        /// The body, resolved on demand. <see cref="rb"/> is the authored reference; this is here so
        /// <see cref="EnsureMovableBody"/> can be called on a bare GameObject — a test, a
        /// script-built player — without the serialized field having been filled in.
        /// </summary>
        private Rigidbody Body => rb != null ? rb : rb = GetComponent<Rigidbody>();

        /// <summary>Logged once per player, not once per physics step. See EnsureMovableBody.</summary>
        private bool warnedAboutKinematicBody;

        /// <summary>
        /// Insists that a player who is meant to be walking has a body physics can move.
        ///
        /// A kinematic Rigidbody silently discards every <c>linearVelocity</c> write, so a player in
        /// that state stands still while everything upstream looks perfect: input arrives, this
        /// component runs to the end of FixedUpdate, the animator plays a walk. Only the body is
        /// missing from the conversation. That is not a state worth being tolerant of — there is no
        /// reading of it in which the player is having a good time — so it is corrected here rather
        /// than diagnosed later.
        ///
        /// One thing is allowed to hold the body and is left alone: a carrier that has parented
        /// the player into itself and made the body kinematic on purpose. Freeing it would drop
        /// them out of whatever is carrying them. The question is asked as "has a parent" rather
        /// than as a lookup of one named carrier, so the next one is covered without being named.
        ///
        /// It warns the first time it fires, because a body that reaches this state has come from a
        /// bug somewhere else and a silent repair would hide it; the warning is what makes that bug
        /// findable.
        /// </summary>
        public void EnsureMovableBody()
        {
            if (Body == null || !Body.isKinematic) return;
            if (transform.parent != null) return;

            Body.isKinematic = false;

            if (warnedAboutKinematicBody) return;
            warnedAboutKinematicBody = true;

            Debug.LogWarning(
                $"[PlayerMovement] {name} was driving a kinematic body, so nothing it was told to " +
                "do could move it. Released it. Something handed this player a body it does not " +
                "own — check whatever last touched isKinematic.", this);
        }

        /// <summary>
        /// How fast the player may travel right now.
        ///
        /// <para>
        /// The stance is asked rather than tracked, so there is exactly one component that decides
        /// whether the player is crouched — and it is the one that also shortened the capsule and
        /// dropped the camera. A player with no PlayerStance on them simply walks, which is what
        /// every caller wants from a body that has no stance to be in.
        /// </para>
        /// </summary>
        private float CurrentMoveSpeed
        {
            get
            {
                // Crouching outranks aiming: a crouched player is already slow, and testing it
                // first means the order of these branches stops being something anyone has to
                // think about.
                if (stance != null && stance.IsCrouching) return crouchSpeed;
                if (stance == null) return moveSpeed;
                return stance.IsSprinting ? sprintSpeed : moveSpeed;
            }
        }

        private void Awake()
        {
            stance = GetComponent<PlayerStance>();
        }

        private void Start()
        {
            inputs = GetComponent<PlayerController>().Input;
            inputs.OnJumpPressed += OnJump;
            inputs.OnDashPressed += OnDash;

            var health = GetComponent<HealthComponent>();
            if (health != null)
            {
                health.OnDamage += _ => TriggerAnimator("Hurt");
                health.OnDeath += () => TriggerAnimator("Die");
            }
        }

        private void FixedUpdate()
        {
            // Before anything is computed, because everything below is written into this body and a
            // kinematic one throws all of it away without complaining.
            EnsureMovableBody();

            moveInput = inputs.MoveInput;
            HandleJumpCooldown();

            if (!groundSnapEnabled)
            {
                return;
            }
        
            bool grounded = IsGrounded();

            // Deliberately skipped while on a rope. A swing on a 20 m tether passes the bottom of
            // its arc at around 19 m/s downward under this project's -18 gravity, which the fall
            // table prices at over half the player's health — so a grapple used to survive a drop
            // would bill them for the swing that saved them. The edge is still consumed: wasGrounded
            // is written below either way, so releasing over ground does not then fire a phantom
            // landing for a fall that already finished.
            if (!tethered) HandleFallDamage(grounded);

            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            move = Vector3.ClampMagnitude(move, 1f);
            Vector3 desiredHorizontal = move * CurrentMoveSpeed;

            Vector3 velocity = rb.linearVelocity;
            Vector3 currentHorizontal = new Vector3(velocity.x, 0f, velocity.z);

            Vector3 newHorizontal;
            if (tethered)
            {
                newHorizontal = SteerTether(currentHorizontal, move);
            }
            else if (IsSliding && grounded)
            {
                // The grounded lerp below is a hard set, not an ease: with control at 1 it replaces
                // the velocity outright with crouchSpeed, which erased the slide one tick after it
                // started. A slide steers its own speed until its window is up.
                newHorizontal = slideDirection * SlideDecay.SpeedAt(
                    Time.time - slideStartTime, slideLaunchSpeed, crouchSpeed, slideDuration);
            }
            else
            {
                float control = grounded ? 1f : airControl;
                newHorizontal = Vector3.Lerp(currentHorizontal, desiredHorizontal, control);
            }
            newHorizontal = SteerWithoutBraking(currentHorizontal, newHorizontal, grounded);

            velocity.x = newHorizontal.x;
            velocity.z = newHorizontal.z;
            rb.linearVelocity = velocity;
        
            lastYVelocity = rb.linearVelocity.y;
            wasGrounded = grounded;

            UpdateAnimatorParameters(velocity, grounded);
        }
    
        /// <summary>
        /// Momentum this component did not produce and must not throw away.
        ///
        /// Set by <see cref="CarryMomentum"/> and cleared the moment the player
        /// lands or slows to a walk, so it is off for all of ordinary movement.
        /// "Lands" is stricter than the ground probe — see <see cref="SteerWithoutBraking"/>.
        /// </summary>
        private bool carryingMomentum;

        /// <summary>
        /// The live slide. Deliberately not reusing <see cref="carryingMomentum"/>: that one ends
        /// the moment the body is grounded and not rising, which is every frame of a ground slide.
        /// </summary>
        private float slideStartTime = float.NegativeInfinity;
        private float slideLaunchSpeed;
        private Vector3 slideDirection;

        /// <summary>Whether a slide is still inside its window and steering its own speed.</summary>
        public bool IsSliding => Time.time - slideStartTime < slideDuration;

        /// <summary>
        /// Keep whatever horizontal speed the body has until it lands.
        ///
        /// Called by anything that flings the player faster than they can run —
        /// today that is coming out of a portal. Without it the aim of
        /// "speedy thing goes in, speedy thing comes out" cannot survive
        /// <see cref="FixedUpdate"/>: the lerp below pulls horizontal velocity
        /// 30% of the way toward a 6 m/s walk fifty times a second, which turns
        /// a 40 m/s exit into a stroll in about a fifth of a second. The player
        /// sees the fling start and then be visibly confiscated.
        /// </summary>
        public void CarryMomentum() => carryingMomentum = true;

        /// <summary>
        /// True while something else is doing the moving on a rope — today the grappling hook.
        /// Unlike <see cref="carryingMomentum"/> this never expires on its own: the thing holding
        /// the rope is the only one that knows when it let go.
        /// </summary>
        private bool tethered;

        /// <summary>
        /// Hand the body over to a rope, or take it back.
        ///
        /// <para>
        /// This replaces what the grappling hook used to do, which was call
        /// <see cref="DisableGroundSnap"/> for 999 seconds. That name undersells it — a disabled
        /// ground snap makes <see cref="FixedUpdate"/> return before it does anything at all, so a
        /// grappling player had no steering, no animator updates and no grounded state for the whole
        /// swing. It also began at the press rather than at the hit, which is why firing the hook
        /// felt like being dragged before it had caught anything: control was gone the moment the
        /// trigger came down, while the rope was still in the air.
        /// </para>
        /// <para>
        /// A tether keeps every one of those running and changes only how the move input is applied.
        /// The caller MUST clear it — see the grappling hook's StopGrapple, which is reached from
        /// its release, its arrival, and its teardown alike.
        /// </para>
        /// </summary>
        public void SetTethered(bool value) => tethered = value;

        /// <summary>Whether a rope currently owns this body's horizontal motion.</summary>
        public bool IsTethered => tethered;

        /// <summary>
        /// True while the player is riding something sprung — today the jumping rod.
        ///
        /// <para>
        /// Deliberately much narrower than <see cref="tethered"/>, and the narrowness is the point.
        /// A tether takes over horizontal motion; this changes nothing about how the player moves.
        /// It suppresses <b>fall damage only</b>, because the whole business of a pogo stick is
        /// arriving hard and leaving harder: at this project's -18 gravity a three-metre hop lands
        /// at about -11 m/s, which the fall table prices at a fifth of the player's health — so a
        /// rod that bounced you well would kill you in five bounces.
        /// </para>
        /// <para>
        /// The rod is left to write <c>linearVelocity.y</c> directly rather than being given a
        /// method here, because <see cref="FixedUpdate"/> only ever writes x and z: the vertical
        /// axis is already free for anything that wants it, and a second jump API would be a second
        /// thing to keep in step with this one.
        /// </para>
        /// </summary>
        public void SetBouncing(bool value) => bouncing = value;

        /// <summary>Whether something sprung is absorbing this player's landings.</summary>
        public bool IsBouncing => bouncing;

        /// <summary>See <see cref="SetBouncing"/>.</summary>
        private bool bouncing;

        /// <summary>
        /// Air steering for a player hanging on a rope.
        ///
        /// The ordinary air lerp cannot be used here, for the same reason
        /// <see cref="CarryMomentum"/> had to exist: it pulls horizontal velocity 30% of the way
        /// toward a 6 m/s walk fifty times a second, so a 25 m/s swing is confiscated in about a
        /// fifth of a second and the pendulum dies before it completes one pass.
        ///
        /// So this pushes instead of blending toward a target. The player can turn the arc and pump
        /// it, and nothing they press can brake it. The ceiling is whichever is greater of the speed
        /// they already had and a walk — steering can never itself become a source of speed, and a
        /// slow hang near the anchor is still nudgeable at walking pace.
        ///
        /// <para>
        /// Used whether or not the player is on the ground. Excluding the grounded case was the
        /// obvious-looking call — on your feet you should walk normally — and it was wrong. On the
        /// ground the ordinary branch runs at <c>control = 1</c>, which sets horizontal velocity
        /// straight to the input target, and with no input that target is ZERO. So a winch pulling
        /// toward anything near horizontal had its entire effect deleted fifty times a second while
        /// the player stood there; and because the distance to the anchor then never changed, the
        /// hook's own stall guard dropped the rope a moment later. Standing on the ground was a hard
        /// counter to the grappling hook.
        /// </para>
        /// <para>
        /// Nothing is given up by including it: with no move input this returns the current velocity
        /// unchanged, so ground friction, gravity and the rope all still do exactly what they did.
        /// </para>
        /// </summary>
        private Vector3 SteerTether(Vector3 current, Vector3 move)
        {
            Vector3 steered = current + move * (tetherAcceleration * Time.fixedDeltaTime);

            float ceiling = Mathf.Max(current.magnitude, CurrentMoveSpeed);
            return steered.magnitude > ceiling ? steered.normalized * ceiling : steered;
        }

        /// <summary>
        /// While momentum is being carried, air control may TURN the flight but
        /// never slow it.
        ///
        /// Preserving the magnitude rather than skipping the lerp is what keeps
        /// the player steerable in mid-air, which is the half of air control
        /// that was always wanted. It ends by itself: on touchdown, because the
        /// ground is where speed is supposed to be given back, and at walking
        /// pace, because below that there is no fling left to protect.
        ///
        /// <para>
        /// "On touchdown" cannot be read off <see cref="IsGrounded"/> alone, which is why
        /// <paramref name="grounded"/> is qualified by whether the body is still RISING. That probe
        /// sphere-casts a 0.45 m sphere from the capsule's centre over the full half-height plus
        /// the ground check distance, so with the authored capsule it keeps answering "grounded"
        /// for roughly the first 0.6 m of clearance. A fling leaves at up to ~10 m/s of vertical,
        /// which is 0.2 m of rise per physics step — so the launch is still "grounded" for the
        /// next several ticks, and the unqualified clause cleared the latch on the very first one.
        /// The horizontal half was then handed to the ordinary <c>control = 1</c> lerp, whose
        /// target with no input is ZERO: a standing victim popped straight up and landed on the
        /// spot, and the gauntlet's own recoil died the same way.
        /// </para>
        /// <para>
        /// Rising is not an escape hatch. Gravity spends the launch in well under a second, after
        /// which a grounded body clears the latch exactly as before; and the walking-pace clause
        /// is untouched, so it still ends a carry that has nothing left to protect.
        /// </para>
        /// </summary>
        private Vector3 SteerWithoutBraking(Vector3 current, Vector3 steered, bool grounded)
        {
            if (!carryingMomentum) return steered;

            float carried = current.magnitude;
            bool rising = rb != null && rb.linearVelocity.y > momentumRiseThreshold;
            if (ShouldEndCarry(grounded, rising, carried, CurrentMoveSpeed))
            {
                carryingMomentum = false;
                return steered;
            }

            return steered.sqrMagnitude > 1e-6f ? steered.normalized * carried : current;
        }

        /// <summary>
        /// Whether a carried fling is finished — the decision <see cref="SteerWithoutBraking"/>
        /// makes, pulled out as pure arithmetic so it can be pinned by a test without a physics
        /// scene, and so the reasoning above lives next to something checkable.
        ///
        /// <para>
        /// A body that is still rising has not landed, whatever the ground probe says; and a body
        /// down to walking pace has no fling left to protect, whether or not it is in the air.
        /// </para>
        /// </summary>
        public static bool ShouldEndCarry(bool grounded, bool rising, float carriedSpeed, float moveSpeed)
            => (grounded && !rising) || carriedSpeed <= moveSpeed;

        private void HandleFallDamage(bool grounded)
        {
            // Detect landing (was in air, now grounded)
            if (!wasGrounded && grounded)
            {
                // Fired for every landing, including harmless ones — audio wants the soft touchdowns
                // too, and the impact speed lets a listener pick between a step and a thud.
                OnLanded?.Invoke(lastYVelocity);

                // A sprung landing costs nothing. The event above still fires — the landing did
                // happen and audio still wants it — but the arrival was absorbed by something the
                // player is deliberately standing on rather than by their legs.
                if (bouncing) return;

                // Only apply if falling fast enough
                if (lastYVelocity < minFallSpeed)
                {
                    float t = Mathf.InverseLerp(minFallSpeed, maxFallSpeed, lastYVelocity);
                    int damage = Mathf.RoundToInt(t * maxFallDamage);

                    ApplyFallDamage(damage);
                }
            }
        }
    
        private void ApplyFallDamage(int damage)
        {
            var health = GetComponent<HealthComponent>();
            if (health)
            {
                // Through Damage rather than straight at the HealthComponent, so a fall is
                // recorded the same way a bullet is.
                Damage.Apply(health.gameObject, damage);
            }
        }

        private void UpdateAnimatorParameters(Vector3 velocity, bool grounded)
        {
            if (!animator || animator.runtimeAnimatorController == null) return;

            Vector3 localVelocity = transform.worldToLocalMatrix.MultiplyVector(velocity);
            bool crouching = stance != null && stance.IsCrouching;

            // Both blend trees anchor each clip at the ground speed that clip's stride actually
            // covers, so both read SpeedX/SpeedY as metres per second and neither needs scaling.
            animator.SetFloat("SpeedX", localVelocity.x, .1f, Time.deltaTime);
            animator.SetFloat("SpeedY", localVelocity.z, .1f, Time.deltaTime);
            animator.SetFloat("FallSpeed", velocity.y, .1f, Time.deltaTime);
            animator.SetFloat("MoveAnimSpeed", StrideRate.For(localVelocity, crouching ? crouchClipSpeed : runClipSpeed));
            animator.SetBool("IsGrounded", grounded);
            animator.SetBool("IsImmobalized", !groundSnapEnabled);
        }

        private void TriggerAnimator(string triggerName)
        {
            if (animator && animator.runtimeAnimatorController != null)
                animator.SetTrigger(triggerName);
        }

        public void ForceIdleAnimation()
        {
            if (!animator)
            {
                return;
            }

            animator.SetFloat("SpeedX", 0f);
            animator.SetFloat("SpeedY", 0f);
            animator.SetFloat("FallSpeed", 0f);
            animator.SetFloat("MoveAnimSpeed", 1f);
            animator.SetBool("IsGrounded", IsGrounded());
            animator.SetBool("IsImmobalized", true);
        }

        public void OnJump()
        {
            if (rb == null || !isActiveAndEnabled || rb.isKinematic)
            {
                return;
            }

            if (IsGrounded() && !jumpOnCooldown)
            {
                Vector3 v = rb.linearVelocity;
                if (v.y > 0f) v.y = 0f;
                rb.linearVelocity = v;
                rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
                jumpOnCooldown = true;

                // A jump out of a sprint becomes a flip. Nothing about the physics changes — the
                // extra height a salto looks like it should have is already in the sprint's
                // forward speed, and giving it real extra lift would make the agile option also
                // the strictly better one.
                if (animator && animator.runtimeAnimatorController != null
                    && stance != null && stance.IsSprinting)
                {
                    animator.SetTrigger("Salto");
                }

                OnJumped?.Invoke();
            }
        }

        /// <summary>
        /// One button, four evasive moves: what comes out is read off what the player is already
        /// doing, which <see cref="DodgeSelector"/> decides. The clips play in place, so the
        /// displacement below is the whole of the movement.
        /// </summary>
        public void OnDash()
        {
            if (rb == null || !isActiveAndEnabled || rb.isKinematic)
            {
                return;
            }

            bool sprinting = stance != null && stance.IsSprinting;
            PlayDodge(DodgeSelector.Choose(moveInput, sprinting, dodgeBackThreshold));

            OnDashed?.Invoke();
        }

        /// <summary>
        /// Launch one evasive move: the clip plays in place, so this displacement is the whole of
        /// the movement. Public because the slide is taken off the crouch press rather than the
        /// dash — see <see cref="DodgeSelector"/> for why it cannot be decided with the others.
        /// </summary>
        public void PlayDodge(DodgeMove dodge)
        {
            if (rb == null || !isActiveAndEnabled || rb.isKinematic) return;

            Vector3 velocity = rb.linearVelocity;
            Vector3 direction = DodgeDirection(dodge);

            float speed = dodge switch
            {
                DodgeMove.Roll => rollSpeed,
                DodgeMove.Slide => slideSpeed,
                _ => dashSpeed,
            };

            if (dodge == DodgeMove.Slide)
            {
                // Whichever is faster: the sprint that earned the slide, or the slide's own floor.
                float carried = new Vector3(velocity.x, 0f, velocity.z).magnitude;
                slideLaunchSpeed = Mathf.Max(carried, slideSpeed);
                slideDirection = direction;
                slideStartTime = Time.time;
                speed = slideLaunchSpeed;
            }

            rb.linearVelocity = direction * speed + Vector3.up * velocity.y;

            if (animator && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger("DodgeIndex", (int)dodge);
                animator.SetTrigger("Dodge");
            }
        }

        /// <summary>
        /// Where a dodge goes. The stick wins when the player is pushing one, so a dodge is aimed
        /// rather than always forward; with no input it falls back to where they are looking, which
        /// is what the dash did before there were four of these.
        /// </summary>
        private Vector3 DodgeDirection(DodgeMove dodge)
        {
            Vector3 facing = playerCamera ? playerCamera.transform.forward : transform.forward;
            facing.y = 0f;
            facing.Normalize();

            if (dodge == DodgeMove.DodgeBack) return -facing;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 aimed = transform.right * moveInput.x + transform.forward * moveInput.y;
                aimed.y = 0f;
                if (aimed.sqrMagnitude > 1e-6f) return aimed.normalized;
            }

            return facing;
        }

        public void DisableGroundSnap(float duration = 0.2f)
        {
            groundSnapEnabled = false;
            CancelInvoke(nameof(EnableGroundSnap));
            Invoke(nameof(EnableGroundSnap), duration);
        }

        private void EnableGroundSnap()
        {
            groundSnapEnabled = true;
        }

        private bool IsGrounded()
        {
            CapsuleCollider colliderToUse = playerCollider != null ? playerCollider : GetComponentInChildren<CapsuleCollider>();
            if (colliderToUse == null)
            {
                Vector3 rayOrigin = transform.position;
                return Physics.Raycast(rayOrigin, Vector3.down, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
            }

            Bounds bounds = colliderToUse.bounds;
            float radius = Mathf.Max(0.05f, bounds.extents.x * 0.9f);
            Vector3 origin = bounds.center + Vector3.up * 0.05f;
            float distance = bounds.extents.y + groundCheckDistance;

            return Physics.SphereCast(origin, radius, Vector3.down, out _, distance, groundMask, QueryTriggerInteraction.Ignore);
        }

        private void HandleJumpCooldown()
        {
            if (!jumpOnCooldown) return;

            jumpCooldownTimer += Time.deltaTime;
            if (jumpCooldownTimer >= jumpCooldown)
            {
                jumpOnCooldown = false;
                jumpCooldownTimer = 0f;
            }
        }
    }
}
