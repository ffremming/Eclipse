using UnityEngine;
using SpaceGame.Items;

namespace SpaceGame.Castle
{
    /// <summary>
    /// One castle, start to finish: its beacon, what lighting it does to the world, and what the
    /// player is left holding afterwards.
    /// <para>
    /// The chain is beacon → lightfall → reward, and it is wired here rather than by each piece
    /// knowing the next one (<c>GDC-L1-ARCH-0003</c>). That is what lets the same three components
    /// build both castles: the keep alone lights its own grounds and pays out the wall key, and the
    /// full castle lights the island and pays out nothing, because there is nothing after it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastleEncounter : MonoBehaviour
    {
        [Header("The chain")]
        [SerializeField] private TowerBeacon beacon;
        [SerializeField] private Lightfall lightfall;

        [Header("Reward")]
        [Tooltip("What taking this castle gives the player — for the first castle, the key to the " +
                 "second one's outer gate. Left empty the castle pays out nothing, which is right " +
                 "for the last one.")]
        [SerializeField] private InventoryItem reward;

        [Tooltip("Where the reward appears. Usually on the lantern deck, in front of the beacon, " +
                 "so it is the first thing the player sees when the light comes up.")]
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
        /// The reward is placed when the light has finished arriving, not when the beacon is
        /// struck. Handing it over during the transition would put a pickup prompt in the middle of
        /// the one moment the game asks the player to just watch.
        /// </summary>
        private void OnLightfallComplete()
        {
            if (reward == null || rewardPrefab == null) return;

            Transform anchor = rewardAnchor != null ? rewardAnchor : transform;
            Instantiate(rewardPrefab, anchor.position, anchor.rotation);
        }
    }
}
