using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Puts bodies into the arena: the player at the start of a fight, and fighters coming back
    /// after one goes down.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance;

        [Tooltip("The body the player gets.")]
        [SerializeField] private GameObject playerPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// Spawn points are re-scanned on every call rather than cached, since this manager lives in
        /// the persistent scene and activates before scenes loaded additively on top of it exist.
        /// Prefers a SpawnPoint in the active scene, falling back to any loaded one.
        /// </summary>
        private SpawnPoint[] FindSpawnPoints()
        {
            var all = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (all.Length == 0) return all;

            Scene active = SceneManager.GetActiveScene();
            var inActiveScene = Array.FindAll(all, sp => sp.gameObject.scene == active);
            return inActiveScene.Length > 0 ? inActiveScene : all;
        }

        public bool SpawnPointsAvailable() => FindSpawnPoints().Length > 0;

        /// <summary>
        /// How far apart two bodies placed in the same breath should be, when the room allows it.
        /// Five capsule radii: enough that nobody is inside anybody.
        /// </summary>
        private const float SpawnSeparation = 2.5f;

        /// <summary>Reused between calls; spawning is never concurrent.</summary>
        private readonly List<Vector3> occupied = new();

        /// <summary>
        /// A validated spawn position, or false when no spawn point can vouch for one — which means
        /// the scene's colliders are not up yet. Callers must wait and ask again rather than
        /// substitute a position of their own.
        ///
        /// <para>
        /// With several spawn points it takes the roomiest answer rather than the first, so two
        /// fighters entering together are put at opposite ends of the arena.
        /// </para>
        /// </summary>
        public bool TryGetSpawnPoint(out Vector3 spawnPosition)
        {
            var spawnPoints = FindSpawnPoints();
            if (spawnPoints.Length == 0)
            {
                Debug.LogError("No SpawnPoint found in scene!");
                spawnPosition = Vector3.zero;
                return false;
            }

            CollectOccupied(occupied);

            bool found = false;
            float bestClearance = float.NegativeInfinity;
            spawnPosition = Vector3.zero;

            foreach (SpawnPoint point in spawnPoints)
            {
                if (!point.TryGetSpawnPoint(occupied, SpawnSeparation,
                                            out Vector3 candidate, out float clearance))
                    continue;

                if (!found || clearance > bestClearance)
                {
                    found = true;
                    bestClearance = clearance;
                    spawnPosition = candidate;
                }

                // Already clear of everybody, so no other point can do better.
                if (clearance >= SpawnSeparation) break;
            }

            return found;
        }

        /// <summary>Where bodies already are. Rebuilt on every ask, because they walk.</summary>
        private void CollectOccupied(List<Vector3> into)
        {
            into.Clear();

            // The tag rather than a component, matching SpawnClearance — nothing but a player
            // character carries it, and it is the same question asked from the other side.
            foreach (GameObject player in GameObject.FindGameObjectsWithTag(SpawnClearance.PlayerTag))
                if (player != null) into.Add(player.transform.position);
        }

        /// <summary>
        /// A validated position to put a fighter back on their feet at.
        ///
        /// Respawning deliberately does not replace the body: it is a state change on a living
        /// object, not a new object — see <c>PlayerRespawn</c>. This only answers where.
        /// </summary>
        public bool TryGetRespawnPosition(out Vector3 respawnPosition) =>
            TryGetSpawnPoint(out respawnPosition);

        /// <summary>
        /// As above, falling back to <paramref name="near"/> — usually where the body fell, which
        /// is ground that has already held somebody up, the one thing a spawn point cannot promise
        /// about itself.
        ///
        /// <para>
        /// The fallback is checked rather than trusted. A body can fall out of the arena, and
        /// "wherever they were last" is then a place with nothing under it — dropping them back
        /// into that is worse than making them press the button again.
        /// </para>
        /// </summary>
        public bool TryGetRespawnPosition(Vector3 near, out Vector3 respawnPosition)
        {
            if (TryGetSpawnPoint(out respawnPosition)) return true;

            if (SpawnClearance.StandsOnStructure(near, FallbackFloorReach) &&
                SpawnClearance.HasRoomToStand(near, StandingHeight, StandingRadius))
            {
                respawnPosition = near;
                Debug.LogWarning("[SpawnManager] no spawn point could vouch for a position — " +
                                 $"respawning where the body fell, at {respawnPosition}.");
                return true;
            }

            respawnPosition = Vector3.zero;
            return false;
        }

        /// <summary>How far under the fallback position to look for something holding it up.</summary>
        private const float FallbackFloorReach = 2f;

        /// <summary>Mirrors SpawnPoint's standing volume — the player capsule, 3 m tall.</summary>
        private const float StandingHeight = 3f;
        private const float StandingRadius = 0.45f;

        /// <summary>Puts the player's body into the arena at a position this manager resolves.</summary>
        public GameObject SpawnPlayer()
        {
            if (!TryGetSpawnPoint(out Vector3 spawnPosition))
            {
                Debug.LogError("Cannot spawn the player: no valid SpawnPoint position!");
                return null;
            }

            return SpawnPlayer(spawnPosition, Quaternion.identity);
        }

        /// <summary>
        /// Spawns at a position the caller already resolved.
        ///
        /// This overload exists because resolving twice is a bug, not a convenience: a spawn point
        /// scatters its result inside a radius, so two calls return two different positions.
        /// </summary>
        public GameObject SpawnPlayer(Vector3 spawnPosition) =>
            SpawnPlayer(spawnPosition, Quaternion.identity);

        /// <summary>As above, but facing a specific direction.</summary>
        public GameObject SpawnPlayer(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[SpawnManager] no player prefab assigned.", this);
                return null;
            }

            // No faction registration: enemies find the player by the "Player" tag on the prefab,
            // so a spawned player is targetable from its first frame with nothing to wire up.
            return Instantiate(playerPrefab, spawnPosition, spawnRotation);
        }
    }
}
