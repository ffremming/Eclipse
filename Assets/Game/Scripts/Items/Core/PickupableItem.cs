using FMODUnity;
using UnityEngine;
using SpaceGame.Audio;
using SpaceGame.Core;
using SpaceGame.Gameplay;

namespace SpaceGame.Items
{
    /// <summary>
    /// Script to be attached to pickupable items in the world.
    /// When interacted with, it will attempt to add the item to the player's inventory and destroy itself if successful.
    /// </summary>
    class PickupableItem : MonoBehaviour, IInteractable
    {
       [SerializeField] private InventoryItem item;

       [Header("Audio")]
       [SerializeField] private SfxId pickupId = SfxId.InteractPickup;
       [SerializeField] private EventReference pickupSound;

       public bool CanInteract()
       {
          return true;
       }

       public void Interact(Interactor interactor)
       {
          if (interactor == null) return;

          // Played before the pickup is decided, so the click is feedback that the interact
          // registered. The cost is that a pickup refused for a full inventory still clicks.
          Sfx.Play(pickupId, transform.position, pickupSound, GetInstanceID());

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
