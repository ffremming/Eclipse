using System.Linq;
using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Delegates interaction to another IInteractable component on a different GameObject.
    /// Useful for when you want to have an interactable that is not on the same GameObject as the collider that detects the interaction.
    ///
    /// It is a redirect and nothing else: it hands the same press to the same interactor, one
    /// GameObject further away. The target decides what the press means — that is the IInteractable
    /// contract, and it is why DoorInteraction reached through a proxy behaves identically to one
    /// reached directly. A test here would be a second, invisible one in front of the target's own,
    /// and the two would eventually disagree.
    /// </summary>
    public class InteractableProxy : MonoBehaviour, IInteractable
    {
        [SerializeField] Transform target;
        private IInteractable targetInteractable;

        private void Awake()
        {
            if (target == null)
            {
                Debug.LogWarning($"[InteractableProxy] target not assigned on {name}, searching children.", this);
                foreach (var c in GetComponentsInChildren<IInteractable>(true))
                {
                    if (c is not InteractableProxy)
                    {
                        targetInteractable = c;
                        return;
                    }
                }
                return;
            }
            targetInteractable = target.GetComponent<IInteractable>();
        }

        public bool CanInteract()
        {
            return targetInteractable != null && targetInteractable.CanInteract();
        }

        public void Interact(Interactor interactor)
        {
            targetInteractable?.Interact(interactor);
        }
    }
}
