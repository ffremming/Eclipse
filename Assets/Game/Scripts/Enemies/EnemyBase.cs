// A camp: the place a group of enemies belongs to.
//
// Not a spawner. It does not create anything and it does not own a roster — the enemies are placed
// in the scene and each one points at its camp. That keeps the camp a piece of level design you can
// drag around in the Editor and see the effect of, rather than a system with a lifecycle.
//
// It answers two questions, and nothing else asks it anything: where is the middle of camp, and is
// the player inside it.
using UnityEngine;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    public class EnemyBase : MonoBehaviour
    {
        [Tooltip("How far from here its enemies are willing to stroll while nothing is happening.")]
        [SerializeField] private float wanderRadius = 12f;

        [Tooltip("Walk inside this and every enemy that calls this camp home comes for you, whether " +
                 "or not any of them can actually see you. This is what makes a camp feel defended " +
                 "rather than like a group of individuals who happen to be standing together.")]
        [SerializeField] private float defendRadius = 16f;

        public Vector3 Position => transform.position;

        public float WanderRadius => wanderRadius;

        /// <summary>True when <paramref name="point"/> is inside the defended ground.</summary>
        public bool Defends(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            return flat.sqrMagnitude <= defendRadius * defendRadius;
        }

        // Both radii drawn, because the gap between them IS the design: enemies stroll the inner
        // circle and defend the outer one, so a camp whose radii are equal has no approach.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
            DrawCircle(wanderRadius);

            Gizmos.color = new Color(1f, 0.4f, 0.3f, 0.9f);
            DrawCircle(defendRadius);
        }

        private void DrawCircle(float radius)
        {
            const int Segments = 48;
            Vector3 previous = transform.position + Vector3.forward * radius;

            for (int i = 1; i <= Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                Vector3 next = transform.position
                               + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
