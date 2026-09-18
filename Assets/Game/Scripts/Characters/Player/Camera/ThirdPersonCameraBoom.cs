// The on-foot camera: a boom hanging off the eye pivot, looking the way the pivot looks.
//
// The pivot is what PlayerLook pitches, what Interactor and PlayerStance treat as the eye, and
// what remote copies pose their aim from — the third-person change moved none of that. Only
// the rendering camera moved: it now rides a short boom behind and beside the pivot, so the
// player watches their own body from over the shoulder and pitching the view orbits it.
//
// Nothing here decides where the player looks. It answers one question per frame: how far
// out along the boom can the camera sit before it is inside a cliff, and it keeps the camera
// there — snapping in when rock arrives (a frame inside a wall is a frame of sky through the
// floor) and easing back out once it is gone.
//
// The helmet visor is a first-person effect and never belongs on a view of your own back, so
// enabling this view switches the visor off; MountModule hands the flag back in the state it
// found it when a ride ends.
using UnityEngine;

namespace SpaceGame.Characters
{
    [DisallowMultipleComponent]
    public class ThirdPersonCameraBoom : MonoBehaviour
    {
        [Tooltip("The rendering camera. A child of this pivot, so activating the pivot activates it.")]
        [SerializeField] private Transform boomCamera;

        [Tooltip("Where the camera rests when nothing is in the way, in this pivot's local space: " +
                 "right of the shoulder, a little above the eye, a few metres back.")]
        [SerializeField] private Vector3 restOffset = new(0.45f, 0.35f, -3.6f);

        [Tooltip("Radius of the probe swept from the eye to the rest position. Larger keeps the " +
                 "near plane further from rock at the cost of pulling in sooner.")]
        [SerializeField] private float collisionRadius = 0.25f;

        [Tooltip("The boom never collapses closer than this, even inside a crevice, so the view " +
                 "cannot end up inside the character's own head.")]
        [SerializeField] private float minDistance = 0.6f;

        [Tooltip("What the boom must stay out of. Excludes the player's own layer, or the boom " +
                 "would collapse onto their own collider every frame.")]
        [SerializeField] private LayerMask collisionMask = ~0;

        [Tooltip("How fast the boom re-extends once the obstacle is gone, m/s. Deliberately slow " +
                 "next to the instant pull-in: the camera should back away, not spring.")]
        [SerializeField] private float easeOutSpeed = 6f;

        private float currentDistance;

        private void OnEnable()
        {
            currentDistance = restOffset.magnitude;
        }

        private void LateUpdate()
        {
            if (boomCamera == null) return;

            Vector3 origin = transform.position;
            Vector3 toRest = transform.TransformPoint(restOffset) - origin;
            float restDistance = toRest.magnitude;
            if (restDistance < 1e-4f) return;

            Vector3 direction = toRest / restDistance;
            float target = restDistance;

            if (Physics.SphereCast(origin, collisionRadius, direction, out RaycastHit hit,
                                   restDistance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                target = Mathf.Max(minDistance, hit.distance);
            }

            currentDistance = target < currentDistance
                ? target
                : Mathf.MoveTowards(currentDistance, target, easeOutSpeed * Time.deltaTime);

            boomCamera.SetPositionAndRotation(origin + direction * currentDistance, transform.rotation);
        }
    }
}
