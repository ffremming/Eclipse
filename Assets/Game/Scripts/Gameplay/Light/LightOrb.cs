using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// A small orb of light: thrown out of something that was hurt, falls to the ground, then hangs
    /// just above it rising and sinking until the player walks into it and takes it back.
    /// <para>
    /// Moved by hand rather than by a rigidbody. It has to do exactly two things — arc to the floor,
    /// then bob — and a physics body would add a way for it to roll off, wedge into a rock or wake
    /// up a sleeping stack of its siblings, none of which anyone wants from a pickup.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class LightOrb : MonoBehaviour
    {
        /// <summary>Most colliders the ground probe looks through. A crowd deeper than this is not ground.</summary>
        private const int ProbeBufferSize = 8;

        [Header("Pickup")]
        [Tooltip("How much light the orb restores when taken.")]
        [SerializeField] private int lightValue = 1;

        [Tooltip("Seconds after being thrown before it can be taken. Without it the orbs a hit throws " +
                 "out of the player land on the player and are taken straight back.")]
        [SerializeField] private float pickupDelay = 0.8f;

        [Header("Fall")]
        [Tooltip("How far above the ground the orb hangs, in metres, at the middle of its bob.")]
        [SerializeField] private float hoverHeight = 0.45f;

        [Tooltip("Fraction of its sideways speed the orb keeps each second while airborne. Lower stops " +
                 "the scatter sooner.")]
        [SerializeField, Range(0f, 1f)] private float sidewaysRetention = 0.2f;

        [Tooltip("Seconds in the air after which an orb that never found ground is given up on.")]
        [SerializeField] private float fallTimeout = 6f;

        [Tooltip("How far down the ground probe looks, in metres.")]
        [SerializeField] private float groundSearchDistance = 60f;

        [Header("Bob")]
        [Tooltip("How far it rises and sinks either side of its resting height, in metres.")]
        [SerializeField] private float bobAmplitude = 0.12f;

        [Tooltip("Seconds for one rise and sink.")]
        [SerializeField] private float bobPeriod = 1.8f;

        private readonly RaycastHit[] probeHits = new RaycastHit[ProbeBufferSize];

        private Vector3 velocity;
        private float restHeight;
        private float bobPhase;
        private float age;
        private bool resting;

        /// <summary>
        /// Throw the orb with <paramref name="launchVelocity"/>. Called once, straight after the
        /// orb is made.
        /// </summary>
        public void Launch(Vector3 launchVelocity)
        {
            velocity = launchVelocity;

            // Each orb starts at its own point in the bob, so a handful dropped together drift
            // apart in time instead of pulsing as one block.
            bobPhase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            age += Time.deltaTime;

            if (resting) Bob();
            else Fall();
        }

        private void Fall()
        {
            if (age > fallTimeout)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;
            velocity += Physics.gravity * dt;

            // Frame-rate independent decay of the sideways speed, so a slow frame does not carry the
            // orb further than a fast one.
            float retention = Mathf.Pow(sidewaysRetention, dt);
            velocity.x *= retention;
            velocity.z *= retention;

            Vector3 next = transform.position + velocity * dt;

            if (velocity.y <= 0f && TryFindGround(next, out float groundY) && next.y <= groundY + hoverHeight)
            {
                restHeight = groundY + hoverHeight;
                transform.position = new Vector3(next.x, restHeight, next.z);
                resting = true;
                return;
            }

            transform.position = next;
        }

        private void Bob()
        {
            float offset = Mathf.Sin(age / bobPeriod * Mathf.PI * 2f + bobPhase) * bobAmplitude;
            Vector3 position = transform.position;
            transform.position = new Vector3(position.x, restHeight + offset, position.z);
        }

        // The highest surface below the point that is not somebody's body. A goblin's head is under a
        // falling orb as often as the floor is, and an orb that came to rest on one would ride away on it.
        private bool TryFindGround(Vector3 from, out float groundY)
        {
            int count = Physics.RaycastNonAlloc(from, Vector3.down, probeHits, groundSearchDistance,
                                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            groundY = float.NegativeInfinity;

            for (int i = 0; i < count; i++)
            {
                if (probeHits[i].collider.GetComponentInParent<HealthComponent>() != null) continue;
                groundY = Mathf.Max(groundY, probeHits[i].point.y);
            }

            return groundY > float.NegativeInfinity;
        }

        // Stay rather than enter: an orb the player is standing in while already full has to be
        // taken the moment there is room, not lost because the moment of touching has passed.
        private void OnTriggerStay(Collider other)
        {
            if (!resting || age < pickupDelay) return;

            HealthComponent light = other.GetComponentInParent<HealthComponent>();
            if (light == null || !light.CompareTag(SpawnClearance.PlayerTag)) return;
            if (!light.Alive || light.GetHealth >= light.GetMaxHealth) return;

            light.Heal(lightValue);
            Destroy(gameObject);
        }
    }
}
