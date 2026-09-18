// What a swing hits, for whoever is swinging.
//
// PlayerMeleeSwing has always ended on a note saying the hit window, the hitbox and the damage call
// were "a separate piece of work, and this is the seam they will hang off". This is that work. It
// is shared rather than written once for the player and once for the enemies, because there is only
// one question here — who was in front of the blade while it was moving — and two answers to it
// would drift apart the first time either was tuned.
//
// The split either side of this component: the CALLER decides when a swing happens (the player on a
// button, an enemy when its brain says so), and this decides what that swing connects with.
//
// Timing rather than an animation event, for two reasons. An animation event lives in a clip, so
// retiming a swing means reopening the FBX import — and the goblin clips are shared by the player
// and every enemy, so there is nowhere to put a windup that differs between them. Serialized
// seconds can be tuned per prefab in the Inspector.
using System.Collections.Generic;
using SpaceGame.Gameplay;
using UnityEngine;

namespace SpaceGame.Enemies
{
    [DisallowMultipleComponent]
    public class MeleeStrike : MonoBehaviour
    {
        [Tooltip("Where the swing is measured from. Leave empty to use this transform — set it to a " +
                 "shoulder or chest bone if the origin should sit above the feet.")]
        [SerializeField] private Transform origin;

        [Header("Reach")]
        [Tooltip("How far the blade reaches. Should sit slightly above the attacker's attack range " +
                 "so a target that steps back during the windup can still be caught.")]
        [SerializeField] private float range = 2.6f;

        [Tooltip("Half the width of the swing arc, in degrees. 60 is a generous forward sweep that " +
                 "clips anyone roughly in front; drop it for a precise thrust.")]
        [SerializeField] private float halfAngle = 60f;

        [Tooltip("Raised so the origin sits around chest height rather than at the feet, where a " +
                 "flat arc would pass under a target standing on a step.")]
        [SerializeField] private float originHeight = 1.2f;

        [Header("Timing")]
        [Tooltip("Seconds between the swing starting and the blade becoming dangerous. This is the " +
                 "player's window to read the attack and get out of it, so it is the single most " +
                 "important number here for whether the fight feels fair.")]
        [SerializeField] private float windup = 0.3f;

        [Tooltip("Seconds the blade stays dangerous. Long enough to catch a moving target, short " +
                 "enough that walking away mid-swing works.")]
        [SerializeField] private float hitWindow = 0.15f;

        [Header("Damage")]
        [SerializeField] private int damage = 12;

        [Tooltip("What the swing can connect with. Leave as Everything and the sweep still ignores " +
                 "anything without health, but narrowing it saves the overlap query some work.")]
        [SerializeField] private LayerMask hittable = ~0;

        // Sized for the worst honest case — a crowd pressed into one swing — and reused, because an
        // allocating overlap query per frame of every enemy's hit window is a steady drip of garbage.
        private readonly Collider[] overlapBuffer = new Collider[32];

        // Instance ids rather than GameObjects: a body is several colliders, and without this a
        // single swing would land once per limb it happened to clip.
        private readonly HashSet<int> alreadyHit = new HashSet<int>();

        private float windowOpensAt = float.PositiveInfinity;
        private float windowClosesAt = float.NegativeInfinity;
        private bool swinging;

        private Transform Origin => origin != null ? origin : transform;

        /// <summary>True while a swing is underway, so a caller can avoid starting a second one.</summary>
        public bool IsSwinging => swinging;

        /// <summary>
        /// Start a swing. The blade turns dangerous after the windup and stays so for the hit
        /// window; a swing already underway is left alone rather than restarted.
        /// </summary>
        public void Swing()
        {
            if (swinging) return;

            swinging = true;
            windowOpensAt = Time.time + windup;
            windowClosesAt = windowOpensAt + hitWindow;
            alreadyHit.Clear();
        }

        /// <summary>Drop a swing in progress — used when the swinger is knocked down or killed.</summary>
        public void Cancel()
        {
            swinging = false;
            alreadyHit.Clear();
        }

        private void OnDisable() => Cancel();

        private void Update()
        {
            if (!swinging) return;

            float now = Time.time;
            if (now < windowOpensAt) return;

            if (now > windowClosesAt)
            {
                Cancel();
                return;
            }

            // Swept every frame the window is open rather than once when it opens: a single sample
            // misses a target that walks into the arc a frame later, which reads as a swing passing
            // straight through someone.
            SweepForTargets();
        }

        private void SweepForTargets()
        {
            Vector3 sweepOrigin = Origin.position + Vector3.up * originHeight;
            Vector3 forward = Origin.forward;
            Transform self = transform.root;

            int found = Physics.OverlapSphereNonAlloc(sweepOrigin, range, overlapBuffer,
                                                      hittable, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < found; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit == null) continue;

                Transform root = hit.transform.root;
                if (root == self) continue;

                int id = root.GetInstanceID();
                if (alreadyHit.Contains(id)) continue;

                // The collider's own position, not the root's: on a humanoid the root sits between
                // the feet, and a chest collider is the honest thing to ask "were you in the arc".
                if (!Cone.Contains(sweepOrigin, forward, hit.bounds.center, range, halfAngle))
                    continue;

                // Recorded before the damage lands, so a target that dies to this swing still
                // cannot be hit twice by the rest of the window.
                alreadyHit.Add(id);

                if (!HasHealth(hit)) continue;

                Damage.Apply(hit.gameObject, damage, transform);
            }
        }

        // Asked so that scenery inside the arc is marked as hit — and so skipped for the rest of the
        // window — without a sound or a damage call being spent on it.
        private static bool HasHealth(Component target)
            => target.GetComponentInParent<IDamageable>() != null;

        private void OnDrawGizmosSelected()
        {
            Vector3 sweepOrigin = Origin.position + Vector3.up * originHeight;

            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
            Gizmos.DrawLine(sweepOrigin, sweepOrigin + Quaternion.Euler(0f, -halfAngle, 0f) * Origin.forward * range);
            Gizmos.DrawLine(sweepOrigin, sweepOrigin + Quaternion.Euler(0f, halfAngle, 0f) * Origin.forward * range);
            Gizmos.DrawLine(sweepOrigin, sweepOrigin + Origin.forward * range);
        }
    }
}
