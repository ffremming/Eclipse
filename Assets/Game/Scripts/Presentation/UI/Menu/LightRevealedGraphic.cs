using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.Presentation.Menu
{
    /// <summary>
    /// Fades a piece of the menu in as the cursor's light passes over it.
    /// <para>
    /// Only the alpha moves. A <see cref="CanvasGroup"/>'s alpha does not affect raycasting, so a
    /// button wearing this is still clickable at its darkest — which is the point. The light sets
    /// the mood; it is never allowed to become the thing standing between the player and the only
    /// action on the screen.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LightRevealedGraphic : MonoBehaviour
    {
        [Tooltip("Distance in screen pixels within which this is fully lit.")]
        [SerializeField] private float innerRadius = 80f;

        [Tooltip("Distance in screen pixels beyond which only the floor below remains.")]
        [SerializeField] private float outerRadius = 320f;

        [Tooltip("The dimmest this is ever allowed to get. Zero lets it vanish entirely, which suits " +
                 "decoration; anything the player has to be able to find needs a floor above zero.")]
        [SerializeField, Range(0f, 1f)] private float floorAlpha;

        private CanvasGroup group;
        private RectTransform rect;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // The menu canvas is Screen Space - Overlay, so the canvas has no camera and the rect's
            // world position is already in screen pixels.
            Vector2 here = RectTransformUtility.WorldToScreenPoint(null, rect.position);

            group.alpha = RevealField.Alpha(mouse.position.ReadValue(), here, innerRadius, outerRadius,
                                            floorAlpha);
        }
    }
}
