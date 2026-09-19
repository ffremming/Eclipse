using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>What <see cref="ModelMount.Mount"/> leaves behind for the builder to wire up.</summary>
    public readonly struct MountedModel
    {
        /// <summary>Empty transform at the far end, away from the handle.</summary>
        public readonly Transform Tip;

        /// <summary>Where the hand closes, in the parent's space: on the handle's own axis.</summary>
        public readonly Vector3 GripPoint;

        public MountedModel(Transform tip, Vector3 gripPoint)
        {
            Tip = tip;
            GripPoint = gripPoint;
        }
    }
}
