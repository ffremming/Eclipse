using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Bursts into light orbs when its health runs out, and is gone. Sits on the light mushroom beside
    /// its <see cref="HealthComponent"/>: a hit is an ordinary <c>Damage.Apply</c>, so a bare-handed
    /// swing or any weapon hurts it with nothing extra here — this only answers what happens when it
    /// dies.
    /// <para>
    /// The orbs are <see cref="LightOrb"/>s, so what each is worth and who may take it is decided
    /// there. <see cref="LightDropper"/> is the other way orbs come out of the world: shed a few at a
    /// time by something that is hurt and lives on. This is all of them at once, from something that
    /// does not.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class OrbBurstOnDeath : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;

        [Tooltip("The orb dropped.")]
        [SerializeField] private LightOrb orbPrefab;

        [Tooltip("Fewest orbs a burst throws.")]
        [SerializeField, Min(1)] private int minOrbs = 3;

        [Tooltip("Most orbs a burst throws. The count is picked evenly between the two, inclusive.")]
        [SerializeField, Min(1)] private int maxOrbs = 5;

        [Tooltip("Height above the mushroom's base the orbs leave from, in metres.")]
        [SerializeField, Min(0f)] private float releaseHeight = 0.5f;

        [Tooltip("Slowest sideways speed an orb is thrown at, in metres per second.")]
        [SerializeField, Min(0f)] private float minScatterSpeed = 2f;

        [Tooltip("Fastest sideways speed an orb is thrown at, in metres per second.")]
        [SerializeField, Min(0f)] private float maxScatterSpeed = 4f;

        [Tooltip("Upward speed the orbs are thrown at, in metres per second.")]
        [SerializeField, Min(0f)] private float lift = 3.5f;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();
        }

        private void OnEnable() => health.OnDeath += Burst;

        private void OnDisable() => health.OnDeath -= Burst;

        private void Burst()
        {
            int count = Random.Range(minOrbs, maxOrbs + 1);
            Vector3[] throws = OrbBurst.Velocities(count, minScatterSpeed, maxScatterSpeed, lift,
                                                   Random.value * 360f);

            Vector3 origin = transform.position + Vector3.up * releaseHeight;
            foreach (Vector3 launchVelocity in throws)
            {
                Instantiate(orbPrefab, origin, Quaternion.identity).Launch(launchVelocity);
            }

            Destroy(gameObject);
        }
    }
}
