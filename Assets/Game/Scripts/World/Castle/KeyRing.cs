using System;
using System.Collections.Generic;
using SpaceGame.Items;

namespace SpaceGame.Castle
{
    /// <summary>
    /// The keys the player has found. Walking over a key puts it here for good, and every door that
    /// wants it is open from that moment on.
    /// <para>
    /// A key is NOT an inventory item, and that is the whole design. It used to be: the player
    /// pressed E to pick one up, it took a hotbar slot, and pressing E at the door spent a second
    /// action on a decision nobody ever makes — you never stand at a locked door choosing not to
    /// use the key you are carrying. That is incidental friction rather than meaningful friction
    /// (<c>GDC-L1-UX-0007</c>): a tax on the path back to the fighting with no decision inside it.
    /// It also cost a slot on a ten-slot hotbar that already holds seven things, and gave the key
    /// an <c>ItemGrip</c> — so the game was prepared to let you draw a key and swing it.
    /// </para>
    /// <para>
    /// So picking a key up IS unlocking its doors. The key has no other use, cannot be dropped and
    /// cannot be spent, which is what lets a door be re-opened after a death without the game
    /// having to remember which doors were opened.
    /// </para>
    /// <para>
    /// The identity is the <see cref="InventoryItem"/> asset, compared by reference. That type is
    /// carried over from when keys really were inventory items, and it survives here because what
    /// it provides is exactly what is still needed — one asset per key, and a prefab to put in the
    /// world — not because a key belongs in a hotbar. Nothing adds one to an inventory any more.
    /// </para>
    /// </summary>
    public sealed class KeyRing
    {
        private readonly HashSet<InventoryItem> held = new HashSet<InventoryItem>();

        /// <summary>Raised when a key is taken that was not already on the ring.</summary>
        public event Action<InventoryItem> Taken;

        /// <summary>How many different keys have been found.</summary>
        public int Count => held.Count;

        /// <summary>Whether this door's key has been found.</summary>
        public bool Holds(InventoryItem key) => key != null && held.Contains(key);

        /// <summary>
        /// Adds <paramref name="key"/>. False when it was null or already held — a second copy of a
        /// key is not an error and not a thing, it is simply nothing happening, which is what lets
        /// the caller decide whether the pickup on the ground should still be consumed.
        /// </summary>
        public bool Take(InventoryItem key)
        {
            if (key == null || !held.Add(key)) return false;

            Taken?.Invoke(key);
            return true;
        }
    }
}
