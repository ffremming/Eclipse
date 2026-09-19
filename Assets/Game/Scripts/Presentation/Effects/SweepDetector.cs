// Decides whether a point is being swung, from how fast it is moving.
//
// A swing animation is mostly not a swing: the windup lifts the weapon slowly, the follow-through
// lets it settle, and only the strike between them is fast. Trailing all of it would draw a smear
// over the whole clip. Gating on speed instead means the light appears exactly where the blade is
// travelling, whatever clip is playing and however long its windup is — nothing in it needs the
// clip's timing, so a retimed or replaced animation keeps working.
//
// The threshold has two edges. It takes a higher speed to START sweeping than to KEEP sweeping,
// because the speed of a swung hand is noisy and a single threshold makes the ribbon flicker on
// and off, and break into confetti, wherever the speed hovers around it.
using UnityEngine;

namespace SpaceGame.Presentation
{
    public class SweepDetector
    {
        private readonly float startSpeed;
        private readonly float holdSpeed;

        private Vector3 previous;
        private bool hasPrevious;

        /// <param name="startSpeed">Speed, in metres a second, that begins a sweep.</param>
        /// <param name="holdRatio">
        /// The fraction of <paramref name="startSpeed"/> a sweep survives down to. 1 is a single
        /// threshold; lower values make a sweep stickier once it has begun.
        /// </param>
        public SweepDetector(float startSpeed, float holdRatio)
        {
            this.startSpeed = Mathf.Max(0f, startSpeed);
            holdSpeed = this.startSpeed * Mathf.Clamp01(holdRatio);
        }

        public bool Sweeping { get; private set; }

        /// <summary>
        /// Feed the point's position for this frame. Positions must be in the swinger's own
        /// frame, or walking forward would look like swinging the weapon at walking pace.
        /// </summary>
        public bool Update(Vector3 point, float deltaTime)
        {
            float speed = hasPrevious && deltaTime > 0f ? (point - previous).magnitude / deltaTime : 0f;
            previous = point;
            hasPrevious = true;

            Sweeping = speed >= (Sweeping ? holdSpeed : startSpeed);
            return Sweeping;
        }
    }
}
