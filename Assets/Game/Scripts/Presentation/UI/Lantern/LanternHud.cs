using UnityEngine;
using SpaceGame.Gameplay;

namespace SpaceGame.Presentation
{
    /// <summary>
    /// The player's health, shown as a lantern in the bottom right corner: full of light when the
    /// player is, dark when they are not.
    ///
    /// <para>
    /// The player's health <em>is</em> their light — one point per orb, see
    /// <see cref="HealthComponent"/> — so there is nothing here to keep in step with anything else.
    /// The level is read every frame, which cannot miss a change however it happened; the events
    /// are only for the reactions, a jolt when light is lost and a swell when it is taken back.
    /// </para>
    /// </summary>
    public sealed class LanternHud : MonoBehaviour
    {
        [SerializeField] private HealthComponent health;
        [SerializeField] private LanternStyle style = new();

        private LanternView view;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();

            view = new GameObject("Lantern HUD").AddComponent<LanternView>();
            view.transform.SetParent(transform, false);
            view.Build(style);
            view.SetLevel(Level());
        }

        private void OnEnable()
        {
            health.OnDamage += OnLightLost;
            health.OnHeal += OnLightGained;
        }

        private void OnDisable()
        {
            health.OnDamage -= OnLightLost;
            health.OnHeal -= OnLightGained;
        }

        private void Update() => view.SetLevel(Level());

        private void OnLightLost(int amount) => view.Lose();

        private void OnLightGained(int amount) => view.Gain();

        private float Level() => (float)health.GetHealth / Mathf.Max(health.GetMaxHealth, 1);
    }
}
