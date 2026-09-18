// Puts one dead player back on their feet.
//
// Respawn is modelled here as what it actually is: a state change on a player who already exists.
// Nothing is destroyed, nothing is re-created, and the save record is never consulted — a respawn
// that rebuilt the body used to restore the corpse's own zero health and death position over the
// top of it, which is a respawn that produces a dead player standing at their own grave.
using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Gameplay;

namespace SpaceGame.Characters
{
    /// <summary>
    /// The player's own respawn. Lives on the player because a respawn has exactly one subject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerRespawn : MonoBehaviour
    {
        private HealthComponent health;

        private void Awake() => health = GetComponent<HealthComponent>();

        /// <summary>Come back. Does nothing for a player who is not dead.</summary>
        public void Request()
        {
            // Already alive means a duplicate request — two clicks on the same button. Healing
            // again would be harmless; moving them would not.
            if (health == null || health.Alive) return;

            // Their own position is passed as the last-resort anchor. A spawn point can refuse —
            // the bay is full of geometry, the ship has been driven somewhere awkward, the chunk
            // under it has not loaded — and a refusal used to end the respawn, leaving the player
            // face down with nothing left to press. It does not any more: SpawnManager falls back
            // to open ground outside, near the spawn point if it can and near the corpse if it
            // cannot, since a dead player is by definition standing on ground that exists.
            if (SpawnManager.Instance == null ||
                !SpawnManager.Instance.TryGetRespawnPosition(transform.position, out Vector3 position))
            {
                Debug.LogError("[Respawn] No valid spawn position — no SpawnPoint could vouch for " +
                               "one and the body's own position was refused, so the player stays " +
                               "down. Is there a SpawnPoint in the arena?", this);
                return;
            }

            // Placement first, healing second, and the order is load-bearing. Healing raises
            // OnRevive, which is what hands the player their controls back; doing that before the
            // move would give them a frame or two of live control standing on their own corpse.
            Teleport.Move(gameObject, position, transform.rotation);

            // ResetToFull rather than Heal(maxHealth): overkill drives currentHealth below zero and
            // Heal caps the restore at the amount passed, so a heavily overkilled player would come
            // back damaged — or still dead.
            health.ResetToFull();
        }
    }
}
