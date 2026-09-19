using UnityEngine;

namespace SpaceGame.Castle
{
    /// <summary>
    /// The player's key ring, as a thing in the scene. Sits on the player and holds a
    /// <see cref="KeyRing"/>; doors and pickups find it with
    /// <see cref="Component.GetComponentInParent{T}()"/> from whatever touched them.
    /// <para>
    /// Thin on purpose — the set and its rules are in <see cref="KeyRing"/>, a plain class the
    /// EditMode suite can reach without building a player.
    /// </para>
    /// <para>
    /// In its own file because it is a <see cref="MonoBehaviour"/>: Unity only recognises one whose
    /// class name matches the file it is in. Sharing <c>KeyRing.cs</c> with the plain class left it
    /// attachable in memory and unserialisable on disk, so the player prefab saved with a missing
    /// script instead of a key ring — silently, until the next save refused.
    /// </para>
    /// <para>
    /// It outlives death. Respawning in this game is a state change on the living body rather than
    /// a new one (see <c>SpawnManager.TryGetRespawnPosition</c>), so the ring is still here
    /// afterwards and a player who dies on the way to the tower does not have to fight the garrison
    /// again for a key they already found.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKeyRing : MonoBehaviour
    {
        /// <summary>What this player has found.</summary>
        public KeyRing Keys { get; } = new KeyRing();
    }
}
