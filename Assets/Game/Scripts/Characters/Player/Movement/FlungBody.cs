using FirstGearGames.SmoothCameraShaker;
using UnityEngine;

namespace SpaceGame.Characters
{
    /// <summary>
    /// Throws this player's body: a blast, a launcher, anything that hands them a velocity they
    /// did not ask for.
    ///
    /// Execution order 200 (the value LeashedBody uses, for the same reason): PlayerMovement's
    /// FixedUpdate ASSIGNS horizontal velocity, so anything written before it runs is deleted the
    /// same tick. Latch here, drain after.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class FlungBody : MonoBehaviour
    {
        [Tooltip("Shake played on the victim's own machine when the fling lands. Optional.")]
        [SerializeField] private ShakeData flungShake;

        [Tooltip("Brief FOV kick (degrees) on the victim when flung. 0 disables.")]
        [SerializeField] private float fovKick = 5f;

        [Tooltip("Seconds the FOV kick holds before easing back.")]
        [SerializeField] private float fovKickDuration = 0.25f;

        private Rigidbody body;
        private PlayerMovement movement;
        private PlayerLook look;
        private Vector3 pending;
        private float fovKickUntil = float.NegativeInfinity;
        private bool fovKickArmed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            movement = GetComponent<PlayerMovement>();
            look = GetComponent<PlayerLook>();
        }

        private void OnDisable()
        {
            // A latched impulse that hasn't fired yet (component disabled mid-flight, e.g. on
            // death) must not carry over to the next enable — it would fire stale on respawn.
            pending = Vector3.zero;

            // SetFovOffset's contract (PlayerLook.cs) is that whoever sets it must clear it.
            // Without this, disabling mid-kick (death, dismount) leaves the camera permanently
            // kicked out, since Update stops ticking and the 0f reset in it never runs.
            if (fovKickArmed)
            {
                fovKickArmed = false;
                if (look != null) look.SetFovOffset(0f);
            }
        }

        /// <summary>
        /// Throw this player. Latched rather than applied here, because PlayerMovement ASSIGNS
        /// horizontal velocity in its own FixedUpdate and would delete anything written first.
        /// </summary>
        public void Fling(Vector3 impulse)
        {
            pending += impulse;
        }

        private void FixedUpdate()
        {
            if (pending == Vector3.zero) return;
            Vector3 impulse = pending;
            pending = Vector3.zero;

            // Called here too, not only relied on from PlayerMovement's own FixedUpdate: the
            // fling must not depend on movement having already run this tick — e.g. right after
            // a load, while movement is still disabled.
            if (movement != null) movement.EnsureMovableBody();

            // A kinematic body is kinematic on purpose. Dropping the impulse here is intentional
            // — banking it to fire once the body becomes dynamic again would land the fling at the
            // wrong time, on top of whatever is happening then.
            if (body == null || body.isKinematic) return;

            body.linearVelocity += impulse;
            // Without the latch, air control lerps the horizontal half back to walk speed in ~0.2 s.
            if (movement != null) movement.CarryMomentum();

            if (flungShake != null) CameraShakerHandler.Shake(flungShake);
            if (look != null && fovKick > 0f)
            {
                look.SetFovOffset(fovKick);
                fovKickUntil = Time.time + fovKickDuration;
                fovKickArmed = true;
            }
        }

        private void Update()
        {
            if (fovKickArmed && Time.time >= fovKickUntil)
            {
                fovKickArmed = false;
                if (look != null) look.SetFovOffset(0f);
            }
        }
    }
}
