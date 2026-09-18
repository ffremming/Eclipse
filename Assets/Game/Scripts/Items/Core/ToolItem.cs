using UnityEngine;
using SpaceGame.Characters;

namespace SpaceGame.Items
{
    /// <summary>
    /// ToolItem is for one-time use effects that don't need duration management.
    /// Examples: Heal potion, speed boost, teleport, damage, etc.
    /// These items execute their effect once and don't need cleanup.
    /// </summary>
    public abstract class ToolItem : UsableItem
    {
        /// <summary>
        /// Where the holder is pointing.
        ///
        /// Resolved on demand rather than cached in Use(), so it is also available in
        /// <see cref="UsableItem.OnRequestUse"/> — which is where an aim is read, because it runs
        /// before the use and therefore before anything has had a chance to move.
        /// </summary>
        protected AimProvider aimProvider =>
            owner != null ? owner.GetComponent<AimProvider>() : null;

        // Tool items just override Use() (the effect) and/or Present() (the look and sound).
        protected override void Use() { }
    }
}
