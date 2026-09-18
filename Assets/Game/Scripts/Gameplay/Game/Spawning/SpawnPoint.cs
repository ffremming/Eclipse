using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Scatters spawn positions around itself and vouches for them.
    ///
    /// The answer is allowed to be "not yet": a caller that gets false must wait and ask again
    /// rather than spawn blind. There is no position this can return that is better than waiting a
    /// frame for the arena's colliders to exist — an unvalidated fallback is how a body ends up
    /// inside the floor.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private float spawnRadius = 10f;
        [SerializeField] private LayerMask blockingLayers;

        [Tooltip("Lift above the sampled ground point. The player capsule's bottom sits ~1m below " +
                 "the prefab pivot, so spawning exactly on the surface buries half the collider and " +
                 "PhysX sometimes resolves that penetration downwards, dropping the body through " +
                 "the floor.")]
        [SerializeField] private float groundClearance = 1.2f;

        [Tooltip("How many scattered positions to try before falling back to this point's own X/Z.")]
        [SerializeField] private int attempts = 20;

        [Tooltip("The volume a spawned body needs free above the ground to not be standing inside " +
                 "something. Mirrors the player capsule, which is 3 m tall.")]
        [SerializeField] private float standingHeight = 3f;

        [Tooltip("Radius of that volume. Slightly under the player capsule's 0.5 m so brushing a " +
                 "wall or a crate is not counted as being stuck in it.")]
        [SerializeField] private float standingRadius = 0.45f;

        [Tooltip("How far above this point the ground probe starts. Lower it below the ceiling for " +
                 "a spawn point indoors: the probe takes the first collider it meets, so a ray " +
                 "starting above the roof lands the body on the roof instead of on the floor.")]
        [SerializeField] private float probeHeight = 50f;

        private const float ProbeDistance = 100f;
        private const float ClearRadius = 1.5f;

        /// <summary>
        /// This point's authored position. Never a spawn position: nothing has verified there is
        /// ground at it, which is the whole reason <see cref="TryGetSpawnPoint"/> exists.
        /// </summary>
        public Vector3 Anchor => transform.position;

        /// <summary>
        /// A ground-backed position near this point, or false when the scene here cannot yet vouch
        /// for one.
        /// </summary>
        public bool TryGetSpawnPoint(out Vector3 spawnPosition) =>
            TryGetSpawnPoint(null, 0f, out spawnPosition, out _);

        /// <summary>
        /// As above, but preferring a position clear of everyone already placed.
        ///
        /// <para>
        /// Separation is a PREFERENCE layered over validity, never a new way to fail. A candidate
        /// that clears everyone is taken immediately; a valid but crowded one is remembered, and the
        /// roomiest of those is the answer when nothing better turns up. Refusing instead would be
        /// read by the caller as "the scene is not ready" — the one thing false means here.
        /// </para>
        /// </summary>
        /// <param name="occupied">Positions to stay away from — bodies already standing, and
        /// positions handed out to fighters whose bodies do not exist yet. May be null.</param>
        /// <param name="separation">How far away is far enough to stop looking.</param>
        /// <param name="clearance">Distance from the answer to the nearest occupant, or infinity
        /// when there are none. Lets a caller choose between several spawn points.</param>
        public bool TryGetSpawnPoint(IReadOnlyList<Vector3> occupied, float separation,
                                     out Vector3 spawnPosition, out float clearance)
        {
            bool found = false;
            spawnPosition = Vector3.zero;
            clearance = 0f;

            // One attempt past the scattered ones, on this point's own X/Z. Scattering exists so a
            // group does not spawn stacked, but it is a preference, not a requirement, and the
            // authored spot is the one position somebody actually looked at.
            for (int attempt = 0; attempt <= attempts; attempt++)
            {
                Vector3 origin = attempt < attempts
                    ? GetRandomPoint(transform.position, spawnRadius)
                    : transform.position + Vector3.up * probeHeight;

                if (!TryGetGroundPoint(origin, out Vector3 groundPoint)) continue;
                if (!IsSpawnPointClear(groundPoint, ClearRadius, blockingLayers)) continue;

                // The check that makes "spawned inside the floor" impossible rather than unlikely,
                // and the only one here that measures the body being placed instead of the ground
                // under it.
                if (!SpawnClearance.HasRoomToStand(groundPoint, standingHeight, standingRadius))
                    continue;

                Vector3 candidate = groundPoint + Vector3.up * groundClearance;
                float gap = DistanceToNearest(candidate, occupied);

                // Clear of everybody. Nothing later in the loop can beat that, so stop.
                if (gap >= separation)
                {
                    spawnPosition = candidate;
                    clearance = gap;
                    return true;
                }

                // Valid but crowded: kept as the best answer so far rather than returned, and never
                // discarded — a position on top of somebody is still a position.
                if (!found || gap > clearance)
                {
                    found = true;
                    spawnPosition = candidate;
                    clearance = gap;
                }
            }

            if (found) return true;

            // Every probe missed: there is nothing to stand on here yet. Say so.
            spawnPosition = Vector3.zero;
            clearance = 0f;
            return false;
        }

        /// <summary>
        /// How far <paramref name="position"/> is from the nearest occupant, or infinity when there
        /// are none — so an empty arena reads as "as clear as it is possible to be" and every
        /// separation test passes without a special case.
        ///
        /// Measured in three dimensions rather than on the floor plane, so somebody on the gallery
        /// above is not counted as standing on top of you.
        /// </summary>
        private static float DistanceToNearest(Vector3 position, IReadOnlyList<Vector3> occupied)
        {
            if (occupied == null || occupied.Count == 0) return float.PositiveInfinity;

            float nearest = float.PositiveInfinity;

            for (int i = 0; i < occupied.Count; i++)
            {
                float distance = Vector3.Distance(position, occupied[i]);
                if (distance < nearest) nearest = distance;
            }

            return nearest;
        }

        private void OnValidate()
        {
            standingHeight = Mathf.Max(0.1f, standingHeight);
            standingRadius = Mathf.Max(0.05f, standingRadius);
            groundClearance = Mathf.Max(0f, groundClearance);
            attempts = Mathf.Max(1, attempts);
        }

        private Vector3 GetRandomPoint(Vector3 center, float radius)
        {
            Vector2 randomCircle = Random.insideUnitCircle * radius;
            return new Vector3(center.x + randomCircle.x, center.y + probeHeight, center.z + randomCircle.y);
        }

        /// <summary>
        /// Ignores triggers. Without that an interaction volume, a pickup radius or a damage zone
        /// counts as ground, and the spawn is placed on a surface that does not exist.
        /// </summary>
        private static bool TryGetGroundPoint(Vector3 origin, out Vector3 hitPoint)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, ProbeDistance,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                return true;
            }

            hitPoint = Vector3.zero;
            return false;
        }

        private static bool IsSpawnPointClear(Vector3 position, float radius, LayerMask blockingLayers)
        {
            return !Physics.CheckSphere(position, radius, blockingLayers);
        }
    }
}
