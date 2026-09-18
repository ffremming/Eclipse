using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Gameplay;

namespace SpaceGame.Items
{
    /// <summary>
    /// Script to be attached to pickupable items in the world.
    /// When interacted with, it will attempt to add the item to the player's inventory and destroy itself if successful.
    /// </summary>
    public class PickupableItem : MonoBehaviour, IInteractable
    {
       [SerializeField] private InventoryItem item;

       public bool CanInteract()
       {
          return true;
       }

       public void Interact(Interactor interactor)
       {
          if (interactor == null) return;

          Pickup(interactor);
       }

       private void Pickup(Interactor interactor)
       {
          IPlayerInventory inventory = interactor.GetComponentInParent<IPlayerInventory>();
          if (inventory == null) return;

          if (inventory.TryAddItem(item))
          {
             GameServices.World.Despawn(gameObject);
          }
       }
    }
}
