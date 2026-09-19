using System;
using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Gameplay;
using SpaceGame.Items;

namespace SpaceGame.Castle
{
    /// <summary>
    /// A door that wants a key: the castle's outer gate, and the keep's main entrance.
    /// <para>
    /// The two behave differently once they are open, which is <see cref="Opens"/>. The gate is a
    /// gate — it swings and the player walks through. The keep's door never opens at all: the keep
    /// is solid, and putting the tower key in it carries the player to the top of the tower. That
    /// is not a shortcut around a missing interior, it is the point of the tower key.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyedEntrance : MonoBehaviour, IInteractable, IContextualInteractable
    {
        /// <summary>What putting the key in does.</summary>
        public enum Answer
        {
            /// <summary>Swings out of the way and stays open.</summary>
            Swing,

            /// <summary>Carries whoever opened it to <see cref="destination"/>.</summary>
            Carry,
        }

        [Header("The lock")]
        [Tooltip("The key this door wants. Left empty the door is unlocked, which is a legitimate " +
                 "way to leave a gate open while the keys are still being made.")]
        [SerializeField] private InventoryItem requiredKey;

        [Header("What it does")]
        [SerializeField] private Answer answer = Answer.Swing;

        [Tooltip("The part that swings. Left empty the door opens without moving, which is only " +
                 "right for a doorway with no leaf in it.")]
        [SerializeField] private Transform leaf;

        [Tooltip("Where the leaf is hinged, in its parent's space. A door turns about its edge, " +
                 "and the leaf's own origin is its middle — left at zero the door pivots like a " +
                 "revolving panel and comes to rest standing edge-on across its own gateway.")]
        [SerializeField] private Vector3 hingePivot;

        [Tooltip("Degrees the leaf swings.")]
        [SerializeField] private float swingAngle = 96f;

        [Tooltip("Seconds the leaf takes to swing.")]
        [SerializeField] private float swingSeconds = 1.4f;

        [Tooltip("Where a Carry door puts the player. Required for Carry, ignored for Swing.")]
        [SerializeField] private Transform destination;

        /// <summary>Raised the moment the door accepts the key, before it starts moving.</summary>
        public event Action Opened;

        /// <summary>
        /// Raised when the door turns someone away. Listened to by <see cref="EntranceSignal"/>,
        /// which is the only thing telling the player it happened — see that class.
        /// </summary>
        public event Action Refused;

        /// <summary>Whether this door has already been opened.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>The key this door wants, so the signal can ask whether the player has it.</summary>
        public InventoryItem RequiredKey => requiredKey;

        private Quaternion closedRotation;
        private Vector3 closedPosition;
        private float swingElapsed = -1f;

        private void Awake()
        {
            if (leaf != null)
            {
                closedRotation = leaf.localRotation;
                closedPosition = leaf.localPosition;
            }

            if (answer == Answer.Carry && destination == null)
                Debug.LogError("[KeyedEntrance] " + name + " carries the player nowhere: its " +
                               "destination is empty, so using it would drop them at the origin.", this);
        }

        public bool CanInteract() => !IsOpen;

        /// <summary>
        /// Always true for a door that is still shut — even when the player has no key.
        /// <para>
        /// This deliberately does NOT answer "can they open it". <see cref="Interactor"/> only
        /// calls <see cref="Interact"/> on something that says yes here, so a door that answered
        /// honestly would be a door that does nothing whatsoever when a player without the key
        /// presses Use. In a game with a prompt that is fine, because the prompt never appeared.
        /// Eclipse has no prompt, no crosshair and no message line, so "nothing happens" is
        /// indistinguishable from a door that is not interactive at all, a broken key check, or an
        /// input that never arrived.
        /// </para>
        /// <para>
        /// So the door takes every press and answers it, and <see cref="Refused"/> is how it says
        /// no. See <see cref="LockSignal"/>, and <c>GDC-L1-UX-0004</c>, whose note is that a
        /// signified action must also respond.
        /// </para>
        /// </summary>
        public bool CanInteract(Interactor interactor) => !IsOpen;

        public void Interact(Interactor interactor)
        {
            if (IsOpen || interactor == null) return;

            // Nothing is taken from the ring. A key is a permanent unlock rather than a
            // consumable — see KeyRing — so a door stays openable however many times the player
            // comes back to it, and a player who dies past one is not locked out behind it.
            var ring = interactor.GetComponentInParent<PlayerKeyRing>();
            if (requiredKey != null && (ring == null || !ring.Keys.Holds(requiredKey)))
            {
                Refused?.Invoke();
                return;
            }

            Open(interactor.transform.root.gameObject);
        }

        /// <summary>
        /// Opens the door on <paramref name="user"/>'s behalf, the key already checked. Split out
        /// of <see cref="Interact"/> so the two questions stay apart: whether this press is allowed,
        /// and what opening actually does.
        /// </summary>
        private void Open(GameObject user)
        {
            IsOpen = true;
            Opened?.Invoke();

            if (answer == Answer.Carry)
            {
                Carry(user);
                return;
            }

            // Nothing to swing is not a failure — a cut doorway with no leaf in it is simply open
            // the moment it is unlocked.
            if (leaf != null) swingElapsed = 0f;
        }

        private void Carry(GameObject user)
        {
            if (user == null || destination == null) return;

            // Teleport.Move rather than assigning the transform: the player is a CharacterController,
            // which caches its own position and writes it back over the transform within a frame.
            // See that class — it is the one function in the project that moves a body instantly.
            Teleport.Move(user, destination.position, destination.rotation);
        }

        private void Update()
        {
            if (swingElapsed < 0f) return;

            swingElapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(swingElapsed / Mathf.Max(swingSeconds, 0.0001f)));

            // Turned about the hinge rather than about the leaf's own origin. Rotating the
            // transform alone would be enough only if the origin WERE the hinge, and it is the
            // middle of the leaf — so the position has to swing round the pivot with it.
            Quaternion turn = Quaternion.AngleAxis(swingAngle * t, Vector3.up);
            leaf.localRotation = turn * closedRotation;
            leaf.localPosition = hingePivot + turn * (closedPosition - hingePivot);

            if (t >= 1f) swingElapsed = -1f;
        }
    }
}
