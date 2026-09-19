// Keeps the masked attack layer out of the way except while it is actually swinging.
//
// An Override layer left at weight 1 can never be transparent, and the attack layer is masked to
// the torso, both arms and both hands. Whatever its idle state does, it does to the upper body all
// the time:
//
//   Write Defaults ON  - writes the bind pose every frame, so the arms never swing while running.
//   Write Defaults OFF - writes nothing, so the bones hold whatever the layer last put there,
//                        which is the final frame of the previous attack. The character stands
//                        around frozen mid-swing.
//
// Neither is a setting problem; the weight is. So the layer sits at zero and is raised only for
// the length of a swing, which lets the base layer's own idle and run own the upper body the rest
// of the time.
//
// Shared by the player and the goblins because they wear the same controller.
using UnityEngine;

namespace SpaceGame.Characters
{
    [DisallowMultipleComponent]
    public class AttackLayerWeight : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("Name of the masked layer that holds the swing clips.")]
        [SerializeField] private string layerName = "Attack";

        [Tooltip("Name of that layer's do-nothing state. While this is the current state the " +
                 "layer is faded out and the base layer owns the upper body.")]
        [SerializeField] private string idleStateName = "No Attack";

        [Tooltip("How long the layer takes to hand the upper body back after a swing. Raising it " +
                 "is instant by contrast, so an attack never starts late.")]
        [SerializeField] private float releaseTime = 0.15f;

        private int layer = -1;
        private float weight;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) layer = animator.GetLayerIndex(layerName);

            if (layer < 0)
            {
                Debug.LogError($"{nameof(AttackLayerWeight)} on '{name}' found no layer called " +
                               $"'{layerName}'; the upper body will stay stuck in its last swing.", this);
                enabled = false;
                return;
            }

            animator.SetLayerWeight(layer, 0f);
        }

        /// <summary>
        /// True while the layer is playing, or about to play, something other than its idle state.
        /// The transition is checked as well so the weight is already up on the frame a swing
        /// starts blending in, rather than a frame later.
        /// </summary>
        private bool Swinging()
        {
            if (!animator.GetCurrentAnimatorStateInfo(layer).IsName(idleStateName)) return true;
            return animator.IsInTransition(layer)
                   && !animator.GetNextAnimatorStateInfo(layer).IsName(idleStateName);
        }

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            float target = Swinging() ? 1f : 0f;

            // Up instantly, down on a fade: a swing that arrives late reads as unresponsive,
            // while one that snaps away reads as a glitch.
            weight = target > weight
                ? target
                : Mathf.MoveTowards(weight, target, Time.deltaTime / Mathf.Max(0.01f, releaseTime));

            animator.SetLayerWeight(layer, weight);
        }
    }
}
