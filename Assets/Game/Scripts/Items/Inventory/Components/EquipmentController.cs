using System;
using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Core;

namespace SpaceGame.Items
{
    public class EquipmentController : MonoBehaviour
    {
        [Tooltip("Where equipped items are parented. If left empty, the controller resolves it automatically: humanoid rigs use Animator.GetBoneTransform(handBone); generic rigs fall back to a name search using handBoneNameHints.")]
        [SerializeField] private Transform handSocket;
        [Tooltip("Which hand bone to use when auto-resolving handSocket from a humanoid rig.")]
        [SerializeField] private HumanBodyBones handBone = HumanBodyBones.RightHand;
        [Tooltip("Substring hints used when auto-resolving handSocket on a non-humanoid rig (case-insensitive). The first child Transform whose name contains any of these wins.")]
        [SerializeField] private string[] handBoneNameHints = { "RightHand", "Hand_R", "R_Hand", "hand.R" };

        [Header("Off hand")]
        [Tooltip("Bone used for items whose ItemGrip says they are held in the other hand. Leave the hints empty to keep everything in the main hand.")]
        [SerializeField] private HumanBodyBones offHandBone = HumanBodyBones.LeftHand;
        [SerializeField] private string[] offHandBoneNameHints = { "LeftHand", "Hand_L", "L_Hand", "hand.L" };

        [Header("Grip")]
        [Tooltip("Optional child of the hand bone whose local position and rotation replace the grip frame this controller works out from the rig's finger bones. Only needed for a rig the derivation gets wrong.")]
        [SerializeField] private Transform gripFrameOverride;

        [Tooltip("Multiplies the size every held item is scaled to. 1 means an item's ItemGrip.holdSize is taken literally, which is what you want unless this character is deliberately outsized.")]
        [SerializeField] private float holdScaleMultiplier = 1f;

        private EquipItemSocket equipmentSocket;
        private EquipItemSocket offHandSocket;
        private EquipItemSocket activeSocket;
        private IPlayerInventory inventory;

        private GameObject equippedItemObject;

        /// <summary>
        /// Which hotbar slot the held object came out of, or -1.
        ///
        /// Needed because unequipping has to put the item's state back where it came from, and by
        /// the time <see cref="Unequip"/> runs the selection has usually already moved on — the
        /// inventory raises OnSlotSelected with the NEW slot, and equipping the new item is what
        /// unequips the old one.
        /// </summary>
        private int equippedSlotIndex = -1;

        /// <summary>
        /// How often a held item is asked to keep going. 15 Hz rather than every frame: the aim is
        /// the only thing a tick carries, and an item that wants a finer rate than that is reading
        /// the holder's camera itself.
        /// </summary>
        private const float HoldTickInterval = 1f / 15f;

        /// <summary>Are hold ticks streaming? Not the same as the button being down — see <see cref="useButtonDown"/>.</summary>
        private bool useHeld;

        /// <summary>
        /// Is the use button physically down?
        ///
        /// Tracked apart from <see cref="useHeld"/> because a self-timed item — one whose
        /// <see cref="UsableItem.WantsHold"/> is true — outlives the press, and the stream has to
        /// keep running while it does. Collapsing the two back into one flag is what makes a
        /// three-second burst freeze its aim the moment the player lets go.
        /// </summary>
        private bool useButtonDown;

        private float nextHoldTick;


        /// <summary>
        /// The main hand's grip rotation, in that hand bone's own space.
        ///
        /// <para>
        /// Identity when there is no main hand, which is the right answer for a character with no
        /// rig to speak of: aim the hand at the target and accept that the item sits however the
        /// prefab sits.
        /// </para>
        /// </summary>
        public Quaternion MainHandGripLocalRotation =>
            equipmentSocket != null ? equipmentSocket.FrameLocalRotation : Quaternion.identity;

        private void Awake()
        {
            var anim = GetComponentInChildren<Animator>(true);

            // Always prefer the actual armature bone — the serialized handSocket is
            // only a manual override for rigs the auto-resolver can't handle.
            var resolved = ResolveBone(anim, handBone, handBoneNameHints);
            if (resolved != null)
            {
                handSocket = resolved;
            }
            else if (handSocket == null)
            {
                Debug.LogError("EquipmentController: could not resolve a hand bone. Assign handSocket manually or add hints in handBoneNameHints.", this);
            }
            else
            {
                Debug.LogWarning("EquipmentController: hand bone auto-resolve failed; falling back to the serialized handSocket Transform.", this);
            }

            equipmentSocket = BuildSocket(anim, handSocket, isRightHand: true);

            // The off hand is genuinely optional. A rig without one, or a character that only ever
            // holds things in the main hand, just gets a null here and every item goes to the main
            // socket — no warning, because nothing is wrong.
            var offHand = ResolveBone(anim, offHandBone, offHandBoneNameHints);
            if (offHand != null && offHand != handSocket)
                offHandSocket = BuildSocket(anim, offHand, isRightHand: false);
        }

        /// <summary>
        /// Build the socket for one hand, with the grip frame derived from the rig's own anatomy.
        ///
        /// <para>
        /// Derived rather than serialized on purpose: a hand bone's rotation is whatever the FBX
        /// exporter wrote, it changes when the rig is re-exported, and it differs between every
        /// character. Reading the fingers instead gives a frame that means the same thing on all
        /// of them, so an item tuned once is tuned everywhere.
        /// </para>
        /// </summary>
        private EquipItemSocket BuildSocket(Animator anim, Transform bone, bool isRightHand)
        {
            if (bone == null) return null;

            HandGripFrame frame;

            if (gripFrameOverride != null && isRightHand && gripFrameOverride.IsChildOf(bone))
            {
                frame = new HandGripFrame(gripFrameOverride.localPosition,
                                          gripFrameOverride.localRotation,
                                          gripFrameOverride.localPosition.magnitude,
                                          "serialized gripFrameOverride");
            }
            else
            {
                frame = HandGripFrame.Derive(anim, bone, isRightHand);
            }

            return new EquipItemSocket(bone, frame, holdScaleMultiplier);
        }

        private Transform ResolveBone(Animator anim, HumanBodyBones bone, string[] nameHints)
        {
            // Humanoid rig: ask the Animator for the actual bone Transform.
            if (anim != null && anim.isHuman)
            {
                var mapped = anim.GetBoneTransform(bone);
                if (mapped != null) return mapped;
            }

            // Generic rig: substring-search the hierarchy by bone name.
            if (nameHints != null && nameHints.Length > 0)
            {
                var all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    string n = all[i].name;
                    for (int h = 0; h < nameHints.Length; h++)
                    {
                        var hint = nameHints[h];
                        if (string.IsNullOrEmpty(hint)) continue;
                        if (n.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                            return all[i];
                    }
                }
            }

            return null;
        }

        private void Start()
        {
            var player = GetComponent<PlayerController>();
            inventory = player.PlayerInventory;

            inventory.OnSlotSelected += HandleEquip;
            inventory.OnSlotChanged += OnSlotChanged;
            inventory.OnItemDropped += OnItemDropped;

            player.Input.OnUsePressed += OnUse;
            player.Input.OnUseReleased += OnUseRelease;

            // Equip whatever is already selected, rather than waiting for the next change: a body
            // restored from a save comes up with its hotbar already populated and no change event
            // left to come. Pulled here rather than pushed from the inventory, which fills itself
            // before this Start runs and would push to nobody.
            HandleEquip(inventory.GetSelectedSlot());
        }

        private void OnSlotChanged(int index, InventorySlot slot)
        {
            if (inventory.SelectedSlotIndex != index) return;
            HandleEquip(slot);
        }

        private void HandleEquip(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                Unequip();
                return;
            }
        
            Equip(slot.Item, slot);
        }


        public void Equip(InventoryItem item) => Equip(item, null);

        /// <summary>
        /// Puts an item in the hand, and — when it came from a hotbar slot — hands it back whatever
        /// that slot remembers about it. See <see cref="ItemState"/>.
        /// </summary>
        public void Equip(InventoryItem item, InventorySlot slot)
        {
            Unequip();

            if (item == null || item.itemPrefab == null)
            {
                Debug.LogError("EquipmentController.Equip: InventoryItem or itemPrefab is null!", this);
                return;
            }

            activeSocket = SocketFor(item.itemPrefab);
            equippedItemObject = activeSocket != null ? activeSocket.Equip(item.itemPrefab) : null;

            if (equippedItemObject == null)
            {
                Debug.LogError($"EquipmentController.Equip: Failed to equip {item.name} - prefab instantiation failed!", this);
                return;
            }

            equippedSlotIndex = slot?.Index ?? -1;

            var usableItem = equippedItemObject.GetComponent<UsableItem>();
            if (usableItem)
            {
                usableItem.OnItemDepleted += ItemDepleted;
                usableItem.OnEquipped(gameObject);

                // AFTER OnEquipped, deliberately. Several items reset themselves there — the item
                // scanner switches its own power off, a weapon's OnEnable has just refilled its
                // magazine — so a restore that ran first would be overwritten by the very act of
                // picking the item up.
                if (usableItem is IItemStateCarrier carrier)
                    carrier.RestoreItemState(slot?.State);
            }
        }

        /// <summary>
        /// Copy the held item's live state back into the slot it came from.
        ///
        /// <para>
        /// Called on unequip, and again whenever the slot has to be accurate without the item being
        /// put away — a save, chiefly. Without the second caller, saving while holding a half-empty
        /// gun would store whatever the slot last heard, which is the state the gun was in when it
        /// was last put down.
        /// </para>
        /// </summary>
        public void WriteBackHeldItemState()
        {
            if (equippedItemObject == null || inventory == null) return;
            if (equippedItemObject.GetComponent<UsableItem>() is not IItemStateCarrier carrier) return;

            InventorySlot slot = inventory.GetSlot(equippedSlotIndex);
            if (slot == null || slot.IsEmpty) return;

            var state = new ItemState();
            carrier.CaptureItemState(state);

            // An empty bag is stored as no bag: "at its defaults" is the common case and should not
            // put a dictionary in the save file for every slot in the game.
            slot.State = state.IsEmpty ? null : state;
        }

        /// <summary>
        /// Hand the currently held item the state its slot now holds.
        ///
        /// <para>
        /// For a load, and only for a load. Restoring the hotbar equips the selected item as a side
        /// effect of assigning the selection, which happens BEFORE the saver can put the per-slot
        /// bags back — so the item that ends up in the hand is the one item restored without its
        /// state. This is the second pass that fixes that.
        /// </para>
        /// </summary>
        public void ReapplyHeldItemState()
        {
            if (equippedItemObject == null || inventory == null) return;
            if (equippedItemObject.GetComponent<UsableItem>() is not IItemStateCarrier carrier) return;

            InventorySlot slot = inventory.GetSlot(equippedSlotIndex);
            if (slot == null) return;

            carrier.RestoreItemState(slot.State);
        }

        /// <summary>The item currently in the hand, or null. For savers and tests.</summary>
        public UsableItem HeldUsable => HeldItem();

        /// <summary>
        /// Trigger the held item as though the use button had been pressed.
        ///
        /// The seam a test fires a use through, because the button itself cannot be pressed from
        /// one: <c>PlayerInputManager.OnUsePressed</c> is a C# event, and only the class that
        /// declares one may raise it. Everything from <see cref="UsableItem.OnRequestUse"/> onwards
        /// is the real path, so what this leaves untested is exactly one thing: that the Use action
        /// is still bound to <see cref="OnUse"/>.
        /// </summary>
        public void UseHeldItem() => OnUse();

        private void Unequip()
        {
            // Before anything is destroyed: the slot has to keep what this instance became, or
            // switching hotbar slot and back would refill the magazine and the charges.
            WriteBackHeldItemState();

            // Before anything is cleared, while HeldItem() still answers with the item that is
            // actually burning. Putting a beam away has to put the beam out.
            EndHold(describeAim: true);

            if (equippedItemObject)
            {
                var usable = equippedItemObject.GetComponent<UsableItem>();
                if (usable)
                {
                    usable.OnUnequipped(gameObject);
                    usable.OnItemDepleted -= ItemDepleted;
                }
            }

            // Whichever hand it went into. Unequipping only the main socket would leave an off-hand
            // item welded to the wrist forever, since nothing else ever destroys it.
            equipmentSocket?.Unequip();
            offHandSocket?.Unequip();
            activeSocket = null;
            equippedItemObject = null;
            equippedSlotIndex = -1;
        }

        /// <summary>Which hand this prefab asked for, falling back to the main one.</summary>
        private EquipItemSocket SocketFor(GameObject prefab)
        {
            var grip = prefab.GetComponent<ItemGrip>();
            if (grip != null && grip.HeldIn == ItemGrip.Hand.Left && offHandSocket != null)
                return offHandSocket;

            return equipmentSocket;
        }

        private void ItemDepleted(UsableItem item)
        {
            item.OnItemDepleted -= ItemDepleted;
            inventory.TryRemoveItem(inventory.SelectedSlotIndex);
            Unequip();
        }

        // ─────────── Using the held item ───────────
        //
        // This is the only place an artifact is triggered. A use is three steps in a fixed order —
        // the holder describes it, the item presents it, the item runs it — and every artifact,
        // the eight that exist and every one written after this, goes through them.

        private void OnDisable()
        {
            // A beam left running past this component would keep burning with nothing steering it.
            // The item is on its way out, so it is not asked where it is aiming.
            EndHold(describeAim: false);
        }

        /// <summary>Player pressed use.</summary>
        private void OnUse()
        {
            UsableItem usable = HeldItem();
            if (usable == null) return;

            // The holder describes the use — chiefly where they aimed, which is knowable only here.
            var context = new UseContext { SlotIndex = inventory?.SelectedSlotIndex ?? -1 };
            usable.OnRequestUse(ref context);

            // Presented before it is run, so the report and the muzzle flash lead the effect rather
            // than trail it. Same order as an NPC's EntityEquipmentController.
            usable.PlayUse(gameObject, context);
            usable.TryUse(gameObject, context);

            // A continuous item's press is also the start of its hold. The first tick runs on the
            // next Update rather than here, so that start and sustain go through one code path and
            // cannot describe the aim two different ways.
            if (usable.IsContinuous)
            {
                useHeld = true;
                useButtonDown = true;
                nextHoldTick = 0f;
            }
        }

        /// <summary>
        /// Player let go of use.
        ///
        /// A self-timed item is not finished just because the finger came up. Its hold ends in
        /// <see cref="Update"/>, when the item itself says it is done.
        /// </summary>
        private void OnUseRelease()
        {
            useButtonDown = false;

            UsableItem usable = HeldItem();
            if (usable != null && usable.IsContinuous && usable.WantsHold) return;

            EndHold(describeAim: true);
        }

        /// <summary>
        /// Keep the aim flowing while the button is down.
        ///
        /// Guarded on the item still being the continuous one that started the hold — swapping
        /// hotbar slots mid-beam otherwise leaves this ticking whatever is in the hand now, and
        /// Unequip's EndHold would have nothing left to switch off.
        /// </summary>
        private void Update()
        {
            if (!useHeld) return;

            UsableItem usable = HeldItem();
            if (usable == null || !usable.IsContinuous)
            {
                EndHold(describeAim: true);
                return;
            }

            // The button is up and the item has stopped asking. For an ordinary held item this is
            // never reached — OnUseRelease already ended it — so this is the self-timed item's
            // release, arriving whenever the item decided rather than whenever the finger did.
            if (!useButtonDown && !usable.WantsHold)
            {
                EndHold(describeAim: true);
                return;
            }

            if (Time.time < nextHoldTick) return;
            nextHoldTick = Time.time + HoldTickInterval;

            TickHold(usable, active: true);
        }

        /// <summary>
        /// Stop the beam. Safe to call from anywhere, including twice.
        ///
        /// <paramref name="describeAim"/> is false during teardown — see <see cref="OnDisable"/>,
        /// where an item on its way out is in no state to be asked where it is pointing.
        /// </summary>
        private void EndHold(bool describeAim)
        {
            if (!useHeld) return;
            useHeld = false;
            useButtonDown = false;

            UsableItem usable = HeldItem();
            if (usable == null) return;

            if (describeAim)
            {
                TickHold(usable, active: false);
                return;
            }

            usable.PlayHold(gameObject, default, active: false);
            usable.TryHold(gameObject, default, active: false);
        }

        /// <summary>One tick of a hold, down the same route a press takes.</summary>
        private void TickHold(UsableItem usable, bool active)
        {
            var context = new UseContext { SlotIndex = inventory?.SelectedSlotIndex ?? -1 };
            usable.OnRequestHold(ref context, active);

            usable.PlayHold(gameObject, context, active);
            usable.TryHold(gameObject, context, active);
        }

        private UsableItem HeldItem() =>
            equippedItemObject != null ? equippedItemObject.GetComponent<UsableItem>() : null;

        private void OnItemDropped(InventoryItem item)
        {
            GameServices.ItemDropService.DropItem(handSocket, item);
        }

        private void OnValidate()
        {
            holdScaleMultiplier = Mathf.Max(0.01f, holdScaleMultiplier);
        }
    }
}
