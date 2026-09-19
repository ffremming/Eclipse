using System.Collections.Generic;
using UnityEngine;
using SpaceGame.Characters;
using SpaceGame.Core;
using SpaceGame.Presentation;

namespace SpaceGame.Items
{
    /// <summary>
    /// The weapon wheel: hold the key, point at a weapon, let go to draw it.
    ///
    /// <para>
    /// While the key is down the game slows, the camera stops turning and the look input steers a
    /// virtual pointer round the dial instead. Releasing over a wedge equips that hotbar slot;
    /// releasing with the pointer still in the hub changes nothing, so tapping the key and letting go
    /// is a way to look at the hotbar without committing to anything.
    /// </para>
    /// <para>
    /// Thin by design. Which wedge a pointer is over is <see cref="RadialSelection"/>, what gets
    /// drawn is <see cref="WeaponWheelView"/>, and how a slot becomes the held item is the
    /// inventory's own <see cref="IPlayerInventory.SelectSlot"/> — the same call the number keys
    /// make — so the wheel is a second way of asking, not a second way of equipping.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class WeaponWheel : MonoBehaviour
    {
        [Tooltip("Game speed while the wheel is up. 1 leaves time alone; the lower it is, the more " +
                 "the world hangs while the player chooses.")]
        [SerializeField, Range(0.02f, 1f)] private float openTimeScale = 0.2f;

        [Tooltip("Reference pixels the pointer travels per unit of look delta. Raise it if the " +
                 "pointer feels heavy, lower it if it overshoots a wedge.")]
        [SerializeField] private float pointerSensitivity = 1.5f;

        [SerializeField] private WeaponWheelStyle style = new();

        private PlayerInputManager input;
        private IPlayerInventory inventory;
        private WeaponWheelView view;

        private readonly List<WheelEntry> entries = new();

        private bool isOpen;
        private float timeScaleBeforeOpen;
        private Vector2 pointer;
        private int hoveredSlot = RadialSelection.NoSelection;

        private void Start()
        {
            var player = GetComponent<PlayerController>();
            input = player.Input;
            inventory = player.PlayerInventory;

            view = new GameObject("Weapon Wheel").AddComponent<WeaponWheelView>();
            view.transform.SetParent(transform, false);
            view.Build(style, inventory.GetInventorySize());

            input.OnWeaponWheelPressed += Open;
            input.OnWeaponWheelReleased += OnKeyReleased;
        }

        private void OnDestroy()
        {
            if (input == null) return;

            input.OnWeaponWheelPressed -= Open;
            input.OnWeaponWheelReleased -= OnKeyReleased;
        }

        private void OnDisable()
        {
            // A wheel still open when this component goes away would leave the game running slowed
            // with nothing left to speed it back up.
            if (isOpen) Close(commit: false);
        }

        private void Update()
        {
            if (!isOpen) return;

            pointer = RadialSelection.MovePointer(pointer, input.WheelPointerDelta,
                                                  pointerSensitivity, style.outerRadius);

            // The hub is the deadzone: a pointer resting on it is not choosing anything.
            hoveredSlot = RadialSelection.IndexAt(pointer, inventory.GetInventorySize(), style.innerRadius);
            view.SetPointer(pointer, hoveredSlot);
        }

        private void Open()
        {
            if (isOpen) return;
            isOpen = true;

            pointer = Vector2.zero;
            hoveredSlot = RadialSelection.NoSelection;

            timeScaleBeforeOpen = Time.timeScale;
            Time.timeScale = openTimeScale;

            view.Show(SnapshotHotbar());
        }

        /// <summary>
        /// The key came up — or the input was switched off under it, which the Input System reports
        /// the same way. Only the first is a choice: dying or a cutscene taking control while the
        /// wheel is up must not draw whatever the pointer happened to be resting on.
        /// </summary>
        private void OnKeyReleased()
        {
            Close(commit: input.isActiveAndEnabled);
        }

        private void Close(bool commit)
        {
            if (!isOpen) return;
            isOpen = false;

            Time.timeScale = timeScaleBeforeOpen;
            view.Hide();

            if (commit) Equip(hoveredSlot);
        }

        private void Equip(int slotIndex)
        {
            if (slotIndex == RadialSelection.NoSelection) return;

            InventorySlot slot = inventory.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            // SelectSlot reads a repeat of the current slot as "put it away", so choosing what is
            // already in hand must do nothing rather than empty the player's hands.
            if (slotIndex == inventory.SelectedSlotIndex) return;

            inventory.SelectSlot(slotIndex);
        }

        private IReadOnlyList<WheelEntry> SnapshotHotbar()
        {
            entries.Clear();

            for (int i = 0; i < inventory.GetInventorySize(); i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                bool isEmpty = slot == null || slot.IsEmpty;

                entries.Add(new WheelEntry(
                    isEmpty ? null : ItemDisplayName.From(slot.Item.itemName),
                    isEmpty ? null : slot.Item.icon,
                    isEmpty,
                    i == inventory.SelectedSlotIndex));
            }

            return entries;
        }
    }
}
