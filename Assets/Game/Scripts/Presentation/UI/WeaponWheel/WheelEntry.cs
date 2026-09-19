using UnityEngine;

namespace SpaceGame.Presentation
{
    /// <summary>What one wedge of the weapon wheel shows. A snapshot: the wheel never holds the inventory.</summary>
    public readonly struct WheelEntry
    {
        public readonly string Label;

        /// <summary>Null for an item that has no icon authored, which is every item today.</summary>
        public readonly Sprite Icon;

        public readonly bool IsEmpty;

        /// <summary>Is this the item in the player's hand right now?</summary>
        public readonly bool IsEquipped;

        public WheelEntry(string label, Sprite icon, bool isEmpty, bool isEquipped)
        {
            Label = label;
            Icon = icon;
            IsEmpty = isEmpty;
            IsEquipped = isEquipped;
        }
    }
}
