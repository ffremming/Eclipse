// Lets an NPC hold and fire the same artifacts the player does.
//
// The items are not a parallel implementation — they are the very same UsableItem prefabs off the
// very same InventoryItem assets, taken from this entity's EntityInventoryComponent. A gun an NPC
// carries is the gun you can loot off it and fire yourself, because it is the same prefab and the
// same code path.
//
// Two things had to be true for that to work, and this class is those two things:
//
//   AIM. Weapon reads its fire direction from the UseContext the holder fills in rather than from
//   a camera, so an NPC aims by filling in the same field. Every weapon ever written against the
//   base class aims correctly for it without knowing NPCs exist.
//
//   ORIENTATION. Weapon.UpdateWeaponRotation swings a weapon to Camera.main for whoever is holding
//   it, which for an NPC is every creature in the world pointing at the player. ExternallyAimed
//   switches that off and this class points the weapon instead.
using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Items;
using SpaceGame.Weapons;

namespace SpaceGame.Agents
{
    public class EntityEquipmentController : MonoBehaviour
    {
        [Header("Socket")]
        [Tooltip("Where held items are parented. Leave empty and it is resolved automatically: " +
                 "humanoid rigs use Animator.GetBoneTransform(handBone), generic rigs fall back to a " +
                 "name search using handBoneNameHints.")]
        [SerializeField] private Transform handSocket;

        [Tooltip("Which hand bone to use when auto-resolving from a humanoid rig.")]
        [SerializeField] private HumanBodyBones handBone = HumanBodyBones.RightHand;

        [Tooltip("Substring hints for auto-resolving on a non-humanoid rig (case-insensitive). The " +
                 "first child Transform whose name contains any of these wins.")]
        [SerializeField] private string[] handBoneNameHints =
            { "RightHand", "Hand_R", "R_Hand", "hand.R", "mixamorig:RightHand" };

        [Header("Startup")]
        [Tooltip("Which inventory slot to hold at spawn. -1 to start empty-handed.")]
        [SerializeField] private int startingSlot = 0;

        [Header("Auto-use")]
        [Tooltip("Fire the held item on a fixed timer regardless of target. Legacy behaviour, kept " +
                 "for prefabs that relied on it — prefer NpcItemUseModule, which fires when there " +
                 "is something to fire AT.")]
        [SerializeField] private bool autoUse = false;

        [SerializeField] private float autoUseInterval = 1f;

        [Header("Aiming")]
        [Tooltip("Point the held object at whatever it is being used on. Turn off for items held in " +
                 "a fixed pose by an animation.")]
        [SerializeField] private bool aimHeldItem = true;

        [Tooltip("How fast the held item swings onto a new aim, in degrees per second. 0 snaps.")]
        [SerializeField] private float aimTurnSpeed = 540f;

        [Header("Grip")]
        [Tooltip("Multiplies the size held items are scaled to. Raise it for an outsized creature so " +
                 "the gun in its fist stays in proportion to the fist.")]
        [SerializeField] private float holdScaleMultiplier = 1f;

        [Tooltip("Height above this entity's origin that shots are measured from when the item has " +
                 "no muzzle of its own.")]
        [SerializeField] private float eyeHeight = 1.5f;

        private EntityInventoryComponent entityInventory;
        private EquipItemSocket socket;
        private GameObject equippedObject;
        private UsableItem equippedUsable;
        private int equippedSlotIndex = -1;
        private float autoUseTimer;

        private bool hasAimPoint;
        private Vector3 aimPoint;

        // ── Published state ──────────────────────────────────────────────────────

        public int EquippedSlotIndex => equippedSlotIndex;
        public GameObject EquippedObject => equippedObject;
        public UsableItem HeldUsable => equippedUsable;
        public bool HasItem => equippedUsable != null;

        public InventoryItem EquippedItem
        {
            get
            {
                InventorySlot slot = entityInventory != null ? entityInventory.GetSlot(equippedSlotIndex) : null;
                return slot != null && !slot.IsEmpty ? slot.Item : null;
            }
        }

        /// <summary>
        /// Where this entity's shots come from: the weapon's own muzzle when it has one, otherwise
        /// eye height above the body. Read by the use path and by anything checking line of fire.
        /// </summary>
        public Vector3 FireOrigin
        {
            get
            {
                // The palm, not the wrist. The item is seated at the grip, so a shot measured from
                // the bone origin starts a hand's width behind the thing that fired it.
                if (equippedObject != null && socket != null && socket.Socket != null)
                    return socket.GripPosition;

                return transform.position + Vector3.up * eyeHeight;
            }
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            entityInventory = GetComponent<EntityInventoryComponent>();

            // Prefer the actual armature bone over anything serialized, the same way the player's
            // EquipmentController does. A hand bone dragged into the inspector is a reference into a
            // rig that gets re-exported regularly, and the FBX re-import that renames or re-parents
            // it leaves a null here with nothing to say why the NPC is holding nothing.
            Transform resolved = ResolveHandSocket();
            if (resolved != null)
                handSocket = resolved;
            else if (handSocket == null)
                Debug.LogError($"{name}: EntityEquipmentController could not resolve a hand bone. " +
                               "Assign handSocket manually or add a hint to handBoneNameHints.", this);

            // The grip frame is worked out from the rig's own fingers rather than taken from the
            // bone's rotation — see HandGripFrame. NPC skeletons vary far more than the player's
            // does, so a frame that survives a different export matters more here, not less.
            Animator rigAnimator = GetComponentInChildren<Animator>(true);
            HandGripFrame frame = HandGripFrame.Derive(rigAnimator, handSocket,
                                                       isRightHand: handBone != HumanBodyBones.LeftHand);

            socket = new EquipItemSocket(handSocket, frame, holdScaleMultiplier);
        }

        /// <summary>
        /// The bone to hang items off. Humanoid avatar first, then a substring search by name.
        ///
        /// The name search is not a fallback for tidiness — several rigs in this project import with
        /// <c>avatar.isHuman = false</c> after a re-export and Unity says nothing about it, so the
        /// humanoid path can silently stop working on a character that used it yesterday.
        /// </summary>
        private Transform ResolveHandSocket()
        {
            Animator animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                Transform bone = animator.GetBoneTransform(handBone);
                if (bone != null) return bone;
            }

            if (handBoneNameHints == null || handBoneNameHints.Length == 0) return null;

            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                foreach (string hint in handBoneNameHints)
                {
                    if (string.IsNullOrEmpty(hint)) continue;
                    if (t.name.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return t;
                }
            }

            return null;
        }

        private void Start()
        {
            // The starting slot is the AUTHORED answer to "what is this NPC holding". A restore is
            // the recorded one, and it wins: an NPC that picked up and equipped a looted rifle must
            // not come back holding the pistol its prefab ships with. Not consumed, unlike the
            // OnEnable latches elsewhere — Start runs once, so there is no later genuine start for
            // the flag to have to yield to.
            if (!equipmentRestored && startingSlot >= 0)
                EquipSlot(startingSlot);

            if (entityInventory)
                entityInventory.OnSlotChanged += OnInventorySlotChanged;
        }

        // ── Save/restore ──────────────────────────────────────────────────────────
        //
        // <c>EntityInventorySaveable</c> already persists WHAT is in the bag. This is WHICH of it is
        // in the hand, which is a different question with a different answer: the slot index is
        // runtime state written by <see cref="EquipSlot"/>, <see cref="EquipFirstAvailable"/> and by
        // an item running dry, and none of those leave a trace anything captured.
        //
        // `equippedObject` and `equippedUsable` are deliberately not stored. They are a spawned
        // instance and a component on it — rebuilt by <see cref="EquipSlot"/> from the slot, so
        // naming the slot names them too, and a saved reference to a scene object would be
        // meaningless in a file anyway.
        private bool equipmentRestored;

        public float AutoUseTimer => autoUseTimer;
        public bool HasAimPoint => hasAimPoint;
        public Vector3 AimPoint => aimPoint;

        /// <summary>
        /// Restore-only. Called by the save system; do not call from gameplay.
        ///
        /// Idempotent, because it is applied twice on purpose — once as the record lands and once
        /// after the load settles — so that it is correct whichever order this saver and the
        /// inventory's saver happen to run in. <see cref="EquipSlot"/> returns early when the slot
        /// asked for is already in hand, so the second call costs nothing and re-spawns nothing.
        /// </summary>
        public void RestoreEquipment(int slotIndex, float autoTimer, bool aiming, Vector3 aimAt)
        {
            equipmentRestored = true;

            if (slotIndex < 0) Unequip();
            else EquipSlot(slotIndex);

            autoUseTimer = autoTimer;

            // After the equip, never before: Unequip clears the aim, so an aim written first would
            // be thrown away on the empty-handed path.
            if (aiming) AimAt(aimAt);
            else ClearAim();
        }

        private void OnDestroy()
        {
            if (entityInventory)
                entityInventory.OnSlotChanged -= OnInventorySlotChanged;
        }

        private void Update()
        {
            if (!autoUse || equippedUsable == null)
                return;

            autoUseTimer -= Time.deltaTime;
            if (autoUseTimer > 0f) return;

            autoUseTimer = autoUseInterval;
            TryUseForward();
        }

        // Aim in LateUpdate, not Update. The held item is parented to a hand bone, and the Animator
        // writes that bone after Update — so a rotation applied in Update is composed with whatever
        // the animation does next and the weapon ends up pointing wherever the walk cycle left the
        // hand. Everything that poses something attached to a rig has to run after the rig.
        private void LateUpdate()
        {
            if (aimHeldItem) UpdateHeldItemAim();
        }

        // ── Equipping ────────────────────────────────────────────────────────────

        public void EquipSlot(int slotIndex)
        {
            if (entityInventory == null) return;

            InventorySlot slot = entityInventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                Unequip();
                return;
            }

            if (slotIndex == equippedSlotIndex && equippedObject != null)
                return;

            Unequip();

            equippedSlotIndex = slotIndex;
            equippedObject = socket.Equip(slot.Item.itemPrefab);

            if (equippedObject == null)
            {
                equippedSlotIndex = -1;
                return;
            }

            equippedUsable = equippedObject.GetComponent<UsableItem>();

            if (equippedUsable != null)
            {
                equippedUsable.OnItemDepleted += OnItemDepleted;

                // The same lifecycle hook the player's EquipmentController fires. Skipping it — as
                // the previous version of this class did — means an NPC's weapon never learns who
                // is holding it, so anything reading the holder to describe a use finds null.
                equippedUsable.OnEquipped(gameObject);
            }

            // See Weapon.ExternallyAimed. Set for every weapon on the object, not just the root,
            // so a multi-part prefab does not leave one barrel tracking the player's camera.
            foreach (Weapon weapon in equippedObject.GetComponentsInChildren<Weapon>(true))
                weapon.ExternallyAimed = aimHeldItem;
        }

        public void Unequip()
        {
            if (equippedUsable != null)
            {
                equippedUsable.OnItemDepleted -= OnItemDepleted;
                equippedUsable.OnUnequipped(gameObject);
            }

            socket.Unequip();
            equippedObject = null;
            equippedUsable = null;
            equippedSlotIndex = -1;
            hasAimPoint = false;
        }

        /// <summary>
        /// Hold the first slot containing an item, or nothing if the inventory is empty. Used when
        /// an NPC picks something up mid-session and should start carrying it visibly.
        /// </summary>
        public void EquipFirstAvailable()
        {
            if (entityInventory == null) return;

            for (int i = 0; i < entityInventory.Size; i++)
            {
                InventorySlot slot = entityInventory.GetSlot(i);
                if (slot != null && !slot.IsEmpty)
                {
                    EquipSlot(i);
                    return;
                }
            }

            Unequip();
        }

        private void OnInventorySlotChanged(int index, InventorySlot slot)
        {
            if (index != equippedSlotIndex) return;

            if (slot == null || slot.IsEmpty)
            {
                Unequip();
                return;
            }

            // Force a rebuild — the slot holds a different item than the one in hand.
            int wanted = equippedSlotIndex;
            Unequip();
            EquipSlot(wanted);
        }

        private void OnItemDepleted(UsableItem item)
        {
            int slot = equippedSlotIndex;
            Unequip();

            // Take the spent item out of the NPC's bag as well, or the next equip hands it straight
            // back and the NPC stands there re-drawing an empty weapon forever.
            entityInventory?.TryRemoveItem(slot);
            EquipFirstAvailable();
        }

        // ── Using ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Use the held item at a world position.
        ///
        /// <para>
        /// This is the NPC counterpart of the player pressing the use button, and it goes through
        /// the identical two-halves contract: <see cref="UsableItem.PlayUse"/> for the look and
        /// sound, <see cref="UsableItem.TryUse"/> for the effect.
        /// </para>
        /// <para>
        /// Returns false when there is nothing held.
        /// </para>
        /// </summary>
        public bool TryUseAt(Vector3 worldAimPoint)
        {
            if (equippedUsable == null) return false;

            aimPoint = worldAimPoint;
            hasAimPoint = true;

            Vector3 origin = FireOrigin;
            Vector3 direction = worldAimPoint - origin;

            var context = new UseContext
            {
                SlotIndex = equippedSlotIndex,
                Origin = origin,
                Aim = direction.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                    : transform.rotation,
            };

            // The item's own hook first, exactly as the player path does it. An item that wants to
            // describe its own use — a grapple reporting where it is hooking — gets the chance,
            // and anything it writes overrides what was filled in above.
            equippedUsable.OnRequestUse(ref context);

            // Presentation before effect, matching EquipmentController.
            equippedUsable.PlayUse(gameObject, context);
            equippedUsable.TryUse(gameObject, context);

            return true;
        }

        /// <summary>Use the held item at a point straight ahead. For items that need no target.</summary>
        public bool TryUseForward() =>
            TryUseAt(FireOrigin + transform.forward * 100f);

        /// <summary>
        /// Use the held item on this entity itself — a stim, a shield, an effect artifact.
        ///
        /// Aimed at the ground under its own feet rather than at nothing, because an aimed item
        /// used with a degenerate direction falls back to its holder's forward, and a healing item
        /// that happens to also raycast would otherwise hit whatever is in front of the NPC.
        /// </summary>
        public bool TryUseOnSelf() =>
            TryUseAt(transform.position + Vector3.down * 0.5f);

        // ── Aiming ───────────────────────────────────────────────────────────────

        /// <summary>Point the held item at a world position, without using it.</summary>
        public void AimAt(Vector3 worldPoint)
        {
            aimPoint = worldPoint;
            hasAimPoint = true;
        }

        public void ClearAim() => hasAimPoint = false;

        private void UpdateHeldItemAim()
        {
            if (equippedObject == null || !hasAimPoint) return;

            Transform item = equippedObject.transform;

            Vector3 direction = aimPoint - item.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);

            item.rotation = aimTurnSpeed <= 0f
                ? wanted
                : Quaternion.RotateTowards(item.rotation, wanted, aimTurnSpeed * Time.deltaTime);

            // Re-seat the grip. The socket seats the item's grip point in the palm once, at equip
            // time, by offsetting the object's ROOT — so any rotation after that swings the item
            // about its own origin and the grip leaves the hand. Putting it back each frame keeps
            // the item turning about the point it is actually held by.
            //
            // Asking the socket rather than doing the arithmetic here is what extends this to
            // artifacts: it re-seats whatever the grip point turned out to be, which for a weapon
            // is still Handle1 and for everything else is its ItemGrip or its own centre.
            socket?.ReseatGrip();
        }

        private void OnValidate()
        {
            autoUseInterval = Mathf.Max(0.05f, autoUseInterval);
            aimTurnSpeed = Mathf.Max(0f, aimTurnSpeed);
            eyeHeight = Mathf.Max(0f, eyeHeight);
            holdScaleMultiplier = Mathf.Max(0.01f, holdScaleMultiplier);
        }
    }
}
