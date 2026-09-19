using UnityEngine;
using SpaceGame.Gameplay;
using SpaceGame.Items;

namespace SpaceGame.Castle
{
    /// <summary>
    /// A key lying in the world. Walk into it and it is yours — there is no prompt and no button,
    /// the same as an orb of light.
    /// <para>
    /// Taken by walking rather than by pressing E because a key is not a choice: see
    /// <see cref="KeyRing"/>. It is also the same gesture the player already learned from the orbs,
    /// which are the only other thing in this game you collect off the ground
    /// (<c>GDC-L1-UX-0004</c> — honour the convention the game has already taught).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyPickup : MonoBehaviour
    {
        [Tooltip("Which key this is. The same asset the doors it opens are asking for.")]
        [SerializeField] private InventoryItem key;

        [Tooltip("Seconds after appearing before it can be taken. Without it a key shed by an " +
                 "enemy the player is stood against is gone before it has visibly left the body, " +
                 "and the one moment the garrison was fought for is never seen.")]
        [SerializeField] private float pickupDelay = 0.8f;

        /// <summary>What this pickup is, so a builder can check it against the door it belongs to.</summary>
        public InventoryItem Key => key;

        private float age;

        private void Awake()
        {
            if (key == null)
                Debug.LogError("[KeyPickup] " + name + " is a key to nothing: no key asset is set, " +
                               "so walking into it would open no door.", this);
        }

        private void Update() => age += Time.deltaTime;

        /// <summary>
        /// Stay rather than Enter, for the case Enter cannot see: a key that lands on a player who
        /// is already standing still over it. Enter fires on the crossing, and there is no crossing
        /// — the player would have to step off it and back on to collect a key dropped at their own
        /// feet, which is exactly where a killed enemy drops one.
        /// </summary>
        private void OnTriggerStay(Collider other)
        {
            if (age < pickupDelay || key == null) return;

            var ring = other.GetComponentInParent<PlayerKeyRing>();
            if (ring == null || !ring.CompareTag(SpawnClearance.PlayerTag)) return;

            // Consumed whether or not it was new. A duplicate key is still picked up rather than
            // left lying there refusing to be collected, which would read as a broken pickup.
            ring.Keys.Take(key);
            Destroy(gameObject);
        }
    }
}
