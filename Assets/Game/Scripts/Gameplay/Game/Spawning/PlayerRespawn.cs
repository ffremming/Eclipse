using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Core;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Stands the player back up: same body, moved to open ground, lantern part-filled.
    ///
    /// <para>
    /// A state change on a living object rather than a new one — the half of respawning
    /// <see cref="SpawnManager.TryGetRespawnPosition(Vector3, out Vector3)"/> deliberately does not
    /// do, since that only answers where. Keeping the body is what lets everything hanging off it
    /// survive the death: the keys on <c>PlayerKeyRing</c>, the equipped weapon, the hotbar, the
    /// camera the player had just aimed.
    /// </para>
    /// <para>
    /// The world is not rewound. Camps that were cleared stay cleared, doors that were opened stay
    /// open, and the orbs still lying on the floor are still lying there — including, usefully, the
    /// ones knocked out of whatever killed you. What death costs is the light in the lantern and
    /// the walk back, which is the only cost this game has to charge.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [Tooltip("How much of the lantern is lit on standing up, as a share of its maximum. The " +
                 "whole price of dying: the rest of the light is gone, and the dark is that much " +
                 "closer.")]
        [Range(0f, 1f)]
        [SerializeField] private float lightOnReturn = 0.34f;

        private PlayerController player;
        private HealthComponent health;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            health = GetComponent<HealthComponent>();
        }

        /// <summary>
        /// Puts the player back in the world. Does nothing to a player who is not dead, so the
        /// button that calls it cannot be used as a free teleport home.
        ///
        /// <para>
        /// Refusing is a real outcome: with no spawn position and no ground under where the body
        /// fell, standing up would mean dropping the player through the world. They stay down and
        /// the other words on the death screen — starting again, leaving — still work.
        /// </para>
        /// </summary>
        public void Respawn()
        {
            if (!player.IsDead) return;

            if (SpawnManager.Instance == null)
            {
                Debug.LogError("[PlayerRespawn] no SpawnManager in the scene, so there is nowhere " +
                               "to stand up. The player stays down.", this);
                return;
            }

            if (!SpawnManager.Instance.TryGetRespawnPosition(transform.position,
                                                             out Vector3 position))
            {
                Debug.LogError("[PlayerRespawn] no valid spawn position and no ground under where " +
                               "the body fell. The player stays down.", this);
                return;
            }

            // Moved before the light goes back in: restoring the health is what hands control back,
            // and a player given control first would spend a frame standing at the place that just
            // killed them.
            Teleport.Move(gameObject, position,
                          Quaternion.Euler(0f, transform.eulerAngles.y, 0f));

            // RestoreHealth rather than Heal or ResetToFull. Heal refuses to raise the dead;
            // ResetToFull hands back the whole lantern, which would make dying free. This assigns,
            // which is also the only one that copes with overkill — a blow worth more than the
            // light left drives the count below zero, and healing from there comes back short.
            health.RestoreHealth(Rekindle.OnReturn(health.GetMaxHealth, lightOnReturn));
        }
    }
}
