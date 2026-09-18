using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// Puts the player into the world once the scene can vouch for a spawn position.
    ///
    /// SpawnManager answering false means "not yet" — the terrain collider has not settled the
    /// frame the scene loads — so this asks again every frame rather than spawning blind. See
    /// SpawnPoint's own doc comment for why there is no fallback position better than waiting.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        private void Update()
        {
            if (SpawnManager.Instance == null) return;
            if (!SpawnManager.Instance.TryGetSpawnPoint(out Vector3 position)) return;

            SpawnManager.Instance.SpawnPlayer(position);
            enabled = false;
        }
    }
}
