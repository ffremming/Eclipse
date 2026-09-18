using UnityEngine;

namespace SpaceGame.Items
{
    /// <summary>
    /// What the holder reported about one use of an item: which slot it came from, where it starts
    /// and where they were aiming.
    ///
    /// <para>
    /// One struct for every item rather than a type per item. Every use site in the game fits in
    /// these fields, a struct costs no allocation on a path that runs once per shot, and it keeps
    /// adding an item to a matter of overriding <see cref="UsableItem.OnRequestUse"/>.
    /// </para>
    /// </summary>
    public struct UseContext
    {
        /// <summary>The hotbar slot the use came from, or -1 when the holder has no hotbar.</summary>
        public int SlotIndex;

        /// <summary>Where the use starts — a muzzle, a hand, a hook point.</summary>
        public Vector3 Origin;

        /// <summary>Where the holder was aiming.</summary>
        public Quaternion Aim;

        /// <summary>
        /// True when <see cref="Aim"/> carries a real orientation.
        ///
        /// A default-constructed context leaves it all-zero, which is not a rotation — so this is
        /// how an item tells "the holder said where they were aiming" from "nobody filled this in"
        /// and falls back to working the aim out for itself.
        /// </summary>
        public readonly bool HasAim =>
            Aim.x * Aim.x + Aim.y * Aim.y + Aim.z * Aim.z + Aim.w * Aim.w > 1e-4f;

        /// <summary>The reported aim as a direction. Only meaningful when <see cref="HasAim"/>.</summary>
        public readonly Vector3 AimDirection => Aim * Vector3.forward;
    }
}
