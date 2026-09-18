using UnityEngine;
using SpaceGame.Items;

namespace SpaceGame.Core
{
    /// <summary>
    /// Puts an item into the world as a physical object — a player emptying a hotbar slot, or a
    /// dead agent shedding its loot.
    /// </summary>
    public class PlayerDropService : IItemDropService
    {
        public void DropItem(Transform origin, InventoryItem item)
        {
            if (origin == null || item == null || item.itemPrefab == null) return;

            GameObject obj = GameServices.World.Spawn(item.itemPrefab, origin.position, Quaternion.identity);
            if (obj == null) return;

            ApplyForce(origin.forward, obj);
        }

        private void ApplyForce(Vector3 direction, GameObject droppedItem)
        {
            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();

            if (rb == null)
                return;

            rb.isKinematic = false;

            Vector3 force =
                direction * 1.5f +
                Vector3.up * 1.0f;

            rb.AddForce(force, ForceMode.Impulse);
        }
    }
}
