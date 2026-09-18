using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SpaceGame.Teleporting;

namespace SpaceGame.Core
{
    /// <summary>
    /// Moves an object instantly without the movement systems undoing it.
    ///
    /// Assigning <c>transform.position</c> is not enough for anything the player or an agent drives.
    /// A CharacterController caches its own internal position and writes it back over the transform
    /// on its next Move, so the object snaps home within a frame. A NavMeshAgent does the same from
    /// its own agent position, and additionally has to be told to re-find the mesh — an agent moved
    /// off its current polygon simply stops. A Rigidbody keeps whatever momentum it had, so a body
    /// placed mid-air carries the fall it was in.
    ///
    /// This is the ONE function in the project that moves an object instantly — respawn, ring-outs,
    /// arena transitions — which is what makes <see cref="ITeleportAware"/> worth having and why it
    /// is raised from here rather than from any caller.
    /// </summary>
    public static class Teleport
    {
        /// <param name="zeroVelocity">
        /// Whether to stop the body on arrival. True for placement — a body teleported while
        /// carrying the velocity it had at its old position keeps travelling in a direction that no
        /// longer means anything.
        /// </param>
        public static void Move(GameObject target, Vector3 position, Quaternion rotation,
                                bool zeroVelocity = true)
        {
            if (target == null) return;

            // Read before anything writes: the transfer handed to listeners is derived from where
            // the object actually was and where it actually ended up, which is the only description
            // of the move that cannot disagree with the move.
            Transform self = target.transform;
            Vector3 from = self.position;
            Quaternion fromRotation = self.rotation;

            var controller = target.GetComponent<CharacterController>();
            var agent = target.GetComponent<NavMeshAgent>();

            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false;

            // Warp, not transform assignment: it moves the agent's own position too, which is what
            // the agent actually navigates from. Its RETURN VALUE is the whole story — Warp refuses
            // a destination it cannot map onto a navigation polygon, and refuses it in silence,
            // leaving the agent exactly where it was.
            bool placed = agent != null && agent.enabled && agent.isOnNavMesh && agent.Warp(position);

            if (placed)
                self.rotation = rotation;
            else
                self.SetPositionAndRotation(position, rotation);

            if (controllerWasEnabled) controller.enabled = true;

            PlaceBodies(target, zeroVelocity);
            Announce(target, from, fromRotation);
        }

        /// <summary>
        /// Tell everything under <paramref name="target"/> that keeps world-space state where that
        /// state has to move to. See <see cref="ITeleportAware"/>.
        ///
        /// Last, and deliberately so: by the time this runs the transform is final and every body
        /// under it has been resynced, so a listener that reads its own transform reads the truth.
        /// </summary>
        private static void Announce(GameObject target, Vector3 from, Quaternion fromRotation)
        {
            Transform self = target.transform;
            Vector3 to = self.position;
            Quaternion toRotation = self.rotation;

            // A resync is not a teleport: callers place an object at the pose it already has purely
            // to push it into PhysX. Waking every listener to rebase world state by an identity
            // transform would be pure cost, and the float noise in "identity" is exactly the kind of
            // thing that walks a foothold a millimetre at a time.
            if ((to - from).sqrMagnitude < StillThere * StillThere &&
                Quaternion.Angle(fromRotation, toRotation) < StillFacing)
                return;

            // A fresh list rather than a shared scratch buffer: a listener is entitled to teleport
            // something else, and a static buffer turns that into a silently truncated notification
            // list two levels up. Teleports are rare events, not a per-frame path.
            var listeners = new List<ITeleportAware>();
            target.GetComponentsInChildren(true, listeners);
            if (listeners.Count == 0) return;

            var move = new TeleportMove(from, fromRotation, to, toRotation);
            for (int i = 0; i < listeners.Count; i++) listeners[i].OnTeleported(in move);
        }

        /// <summary>Below this much movement, and this much turn, a Move is a resync rather than a teleport.</summary>
        private const float StillThere = 0.0001f;
        private const float StillFacing = 0.01f;

        /// <summary>
        /// Puts every Rigidbody under <paramref name="target"/> where its transform now is.
        ///
        /// Every body, not just the root's own: moving a transform moves its children, but PhysX
        /// holds each body's pose independently. On an articulated one — a fighter's ragdoll — only
        /// resyncing the root drags the torso away from limbs that stayed behind and pulls the
        /// joints apart.
        /// </summary>
        private static void PlaceBodies(GameObject target, bool zeroVelocity)
        {
            foreach (Rigidbody body in target.GetComponentsInChildren<Rigidbody>(true))
            {
                if (body == null) continue;

                // Interpolation is switched off across the write and put back after it. An
                // interpolated body blends the transform from the poses it has already simulated,
                // so leaving it on means the frame after a teleport is spent travelling back toward
                // where the body came from. Restoring the setting matters as much as clearing it —
                // a body left on None never smooths again.
                RigidbodyInterpolation interpolation = body.interpolation;
                if (interpolation != RigidbodyInterpolation.None)
                    body.interpolation = RigidbodyInterpolation.None;

                body.position = body.transform.position;
                body.rotation = body.transform.rotation;

                if (interpolation != RigidbodyInterpolation.None)
                    body.interpolation = interpolation;

                if (zeroVelocity && !body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }
    }
}
