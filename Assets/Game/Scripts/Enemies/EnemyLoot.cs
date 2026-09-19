using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Gameplay;
using SpaceGame.Items;

namespace SpaceGame.Enemies
{
    /// <summary>
    /// What an enemy was carrying, dropped where it falls.
    /// <para>
    /// Used for the castle keys: one creature in a garrison is holding the key to the tower, and the
    /// only way to find out which is to fight them. That is what makes the garrison worth clearing
    /// rather than worth running past — the key is behind the encounter, not beside it.
    /// </para>
    /// <para>
    /// A component rather than a field on <see cref="EnemyAgent"/>, so that carrying something is a
    /// property of the individual the level designer picked and not of every creature in the game.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    [DisallowMultipleComponent]
    public sealed class EnemyLoot : MonoBehaviour
    {
        [Tooltip("What this one is carrying. Left empty it drops nothing, which is every creature " +
                 "that is not holding a key.")]
        [SerializeField] private InventoryItem carried;

        [SerializeField] private HealthComponent health;

        /// <summary>Whether the loot has already been handed over.</summary>
        private bool dropped;

        private void Awake()
        {
            if (health == null) health = GetComponent<HealthComponent>();
        }

        private void OnEnable() => health.OnDeath += OnDied;

        private void OnDisable() => health.OnDeath -= OnDied;

        private void OnDied()
        {
            // Restoring is not dying. HealthComponent raises OnDeath when a saved value below zero
            // is assigned as well as when a blow lands, and the difference matters here because
            // dropping is not repeatable — see HealthComponent.IsRestoring. Without this check a
            // body that was already dead sheds another key every time the world loads.
            if (dropped || health.IsRestoring || carried == null) return;

            dropped = true;
            GameServices.ItemDropService.DropItem(transform, carried);
        }
    }
}
