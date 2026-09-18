// Where the player's head is pointing, as a transform anything can hang off.
//
// Yaw lives on the body, but pitch is a private float on PlayerLook that is spent on a child
// camera — so anything outside the camera rig that wants to point at what the player is looking at
// (a held weapon, an aim rig) has nothing to read. AimPivot is that missing transform: a runtime
// child of the player carrying the camera's local position and the current pitch.
using UnityEngine;

namespace SpaceGame.Characters
{
    [DisallowMultipleComponent]
    public class PlayerView : MonoBehaviour
    {
        private PlayerController controller;
        private PlayerLook look;

        private Transform aimPivot;
        private float pitch;

        /// <summary>
        /// Where this player is looking: their body's yaw plus their pitch.
        ///
        /// <para>
        /// Deliberately NOT wired into <see cref="AimProvider"/>: an item's aim travels in its use
        /// message so that the shot and the effect agree. This is for things that only have to
        /// LOOK right.
        /// </para>
        /// </summary>
        public Transform AimPivot => aimPivot;

        /// <summary>Is this player aiming down their weapon?</summary>
        public bool Aiming { get; private set; }

        /// <summary>Called by <see cref="PlayerAimRig"/> once its own decision has been made.</summary>
        public void PublishAiming(bool aiming) => Aiming = aiming;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            look = GetComponent<PlayerLook>();

            aimPivot = new GameObject("AimPivot").transform;
            aimPivot.SetParent(transform, worldPositionStays: false);
        }

        // LateUpdate, so the pitch read here is the one PlayerLook wrote in Update this frame
        // rather than last frame's.
        private void LateUpdate()
        {
            if (look != null) pitch = look.Pitch;

            if (aimPivot == null) return;

            // Read every frame rather than cached: the camera's local position is the eye height,
            // and crouching or a rig change is free to move it.
            Transform camera = controller != null ? controller.PlayerCameraTransform : null;
            if (camera != null) aimPivot.localPosition = camera.localPosition;

            aimPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
