using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.Presentation.Menu
{
    /// <summary>
    /// Drags the main menu's spotlight around the floor with the cursor.
    /// <para>
    /// Deliberately thin: it reads the mouse, hands the arithmetic to <see cref="SpotlightAim"/>,
    /// and writes the result to the transform. Nothing here decides anything, which is why the
    /// parts that do are testable.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class CursorSpotlight : MonoBehaviour
    {
        [Tooltip("The camera the cursor is pointing through. The menu camera, not the gameplay one.")]
        [SerializeField] private Camera menuCamera;

        [Tooltip("Height of the floor the light pools on, in world units.")]
        [SerializeField] private float floorHeight;

        [Tooltip("How far above that floor the light itself hangs.")]
        [SerializeField] private float lightHeight = 9f;

        [Tooltip("How far from its starting point the light may roam. Keeps it on the set when the " +
                 "cursor sweeps towards the horizon.")]
        [SerializeField] private float maxRadius = 12f;

        [Tooltip("Seconds to cover most of the gap to the cursor. Low enough to read as attached to " +
                 "the hand; a slack value here is indistinguishable from input lag.")]
        [SerializeField] private float followTimeConstant = 0.06f;

        private Vector3 setCentre;
        private Vector3 aim;

        private void Awake()
        {
            if (menuCamera == null)
            {
                Debug.LogError("[CursorSpotlight] No menu camera assigned, so the cursor cannot be " +
                               "aimed at the floor.", this);
                enabled = false;
                return;
            }

            // Captured once rather than read each frame: this component moves its own transform,
            // so a live read would let the circle the light is allowed to roam drift along behind
            // the light itself until both had wandered off the set.
            setCentre = new Vector3(transform.position.x, floorHeight, transform.position.z);
            aim = setCentre;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;

            // No mouse attached. Holding the last position is the quiet failure: snapping the light
            // back to the middle of the set every frame would look like a fault rather than a
            // missing device.
            if (mouse == null) return;

            Ray cursorRay = menuCamera.ScreenPointToRay(mouse.position.ReadValue());
            Vector3 target = SpotlightAim.Resolve(cursorRay, floorHeight, setCentre, maxRadius);

            // Unscaled, because a menu should not care what the game did to timeScale.
            aim = SpotlightAim.Follow(aim, target, followTimeConstant, Time.unscaledDeltaTime);

            transform.position = aim + Vector3.up * lightHeight;
            transform.LookAt(aim);
        }
    }
}
