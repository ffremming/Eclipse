using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Throws orbs of light out of whoever it sits on each time they are hurt — as many as the blow
    /// was worth.
    /// <para>
    /// The same component on the player and on an enemy. What differs is only where the light comes
    /// from: the player's health is the light and loses those orbs, an enemy keeps its health and
    /// sheds the orbs as a reaction to the blow. Either way the orbs on the ground are the light that
    /// was knocked loose, and the player can take it back.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public sealed class LightDropper : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;

        [Tooltip("Shared with the player's health, so a blow is worth the same number of orbs to the " +
                 "one who drops them and the one who loses them.")]
        [SerializeField] private LightCost lightCost;

        [SerializeField] private LightOrb orbPrefab;

        [Tooltip("Height above the feet the orbs leave from. Around the chest reads as the wound.")]
        [SerializeField] private float releaseHeight = 1.2f;

        [Tooltip("Sideways speed the orbs are thrown at, in metres per second.")]
        [SerializeField] private float scatterSpeed = 3f;

        [Tooltip("Upward speed the orbs are thrown at, in metres per second. The arc they trace is " +
                 "the read that they came out of the body.")]
        [SerializeField] private float lift = 3.5f;

        [Tooltip("How much each orb's throw varies, as a fraction of its speed, so a handful does not " +
                 "land in a neat ring.")]
        [SerializeField, Range(0f, 1f)] private float spread = 0.35f;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();
        }

        private void OnEnable() => health.OnDamage += OnDamaged;

        private void OnDisable() => health.OnDamage -= OnDamaged;

        private void OnDamaged(int amount)
        {
            int orbs = lightCost.OrbsFor(amount);
            Vector3 origin = transform.position + Vector3.up * releaseHeight;

            // Evenly spaced round the circle from a random start: each orb has its own direction, and
            // no two hits throw them the same way.
            float startAngle = Random.value * Mathf.PI * 2f;

            for (int i = 0; i < orbs; i++)
            {
                float angle = startAngle + i * Mathf.PI * 2f / orbs;
                float speed = scatterSpeed * (1f + Random.Range(-spread, spread));
                Vector3 throwVelocity = new Vector3(Mathf.Cos(angle) * speed, lift, Mathf.Sin(angle) * speed);

                Instantiate(orbPrefab, origin, Quaternion.identity).Launch(throwVelocity);
            }
        }
    }
}
