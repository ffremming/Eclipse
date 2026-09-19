using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Gameplay;
using SpaceGame.Items;

namespace SpaceGame.Castle
{
    /// <summary>
    /// One castle, start to finish: its beacon, what lighting it does to the world, where the
    /// player is put down afterwards and what they are left holding.
    /// <para>
    /// The chain is beacon → lightfall → return → reward, and it is wired here rather than by each
    /// piece knowing the next one (<c>GDC-L1-ARCH-0003</c>). That is what lets the same three
    /// components build both castles: the keep alone lights its own grounds and pays out the wall
    /// key, and the full castle lights the island and pays out nothing, because there is nothing
    /// after it.
    /// </para>
    /// <para>
    /// The return is not a flourish. The tower is reached by putting the tower key in the keep's
    /// door, which carries the player to the top of it — and there is no stair back down, so a
    /// player who had lit the beacon was left standing on the deck with nowhere to go. Carrying
    /// them back down is what closes the loop and puts them at the foot of the castle looking up at
    /// what they just did.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastleEncounter : MonoBehaviour
    {
        [Header("The chain")]
        [SerializeField] private TowerBeacon beacon;
        [SerializeField] private Lightfall lightfall;

        [Header("After")]
        [Tooltip("Where the player is set down once the light has arrived, normally the ground " +
                 "outside the keep's door. Left empty they are left on the tower, which for a " +
                 "tower with no stair means stranded.")]
        [SerializeField] private Transform returnTo;

        [Header("Reward")]
        [Tooltip("What taking this castle gives the player — for the first castle, the key to the " +
                 "second one's outer gate. Left empty the castle pays out nothing, which is right " +
                 "for the last one.")]
        [SerializeField] private InventoryItem reward;

        [Tooltip("Where the reward appears. On the ground the player is returned to rather than on " +
                 "the deck they were standing on, so they are not carried away from it.")]
        [SerializeField] private Transform rewardAnchor;

        [Tooltip("The pickup the reward appears as. Must carry a PickupableItem holding the same " +
                 "item asset — this only places it.")]
        [SerializeField] private GameObject rewardPrefab;

        private void Awake()
        {
            if (beacon == null) beacon = GetComponentInChildren<TowerBeacon>(true);
            if (lightfall == null) lightfall = GetComponentInChildren<Lightfall>(true);

            if (beacon == null || lightfall == null)
            {
                Debug.LogError("[CastleEncounter] " + name + " has no beacon or no lightfall, so " +
                               "lighting it would do nothing.", this);
                enabled = false;
                return;
            }

            if (reward != null && rewardPrefab == null)
                Debug.LogError("[CastleEncounter] " + name + " has a reward but no prefab to put " +
                               "it in the world as, so taking this castle pays out nothing.", this);
        }

        private void OnEnable()
        {
            beacon.Lit += OnBeaconLit;
            lightfall.Complete += OnLightfallComplete;
        }

        private void OnDisable()
        {
            beacon.Lit -= OnBeaconLit;
            lightfall.Complete -= OnLightfallComplete;
        }

        private void OnBeaconLit() => lightfall.Light();

        /// <summary>
        /// Both of these happen when the light has finished arriving, not when the beacon is
        /// struck. Moving the player mid-rise would take them away from the one moment the game
        /// asks them to just watch, and the reward would land somewhere they no longer are.
        /// </summary>
        private void OnLightfallComplete()
        {
            SendThePlayerBack();

            if (reward == null || rewardPrefab == null) return;

            Transform anchor = rewardAnchor != null ? rewardAnchor : transform;
            Instantiate(rewardPrefab, anchor.position, anchor.rotation);
        }

        /// <summary>
        /// Carries the player down off the tower. By tag rather than by remembering whoever put the
        /// key in the door: the beacon is struck through <c>IDamageable.Damage</c>, which carries no
        /// attacker, and a player who died to the garrison and respawned between the two is a
        /// different body from the one the door let in.
        /// </summary>
        private void SendThePlayerBack()
        {
            if (returnTo == null) return;

            GameObject player = GameObject.FindGameObjectWithTag(SpawnClearance.PlayerTag);
            if (player == null) return;

            Teleport.Move(player, returnTo.position, returnTo.rotation);
        }
    }
}
