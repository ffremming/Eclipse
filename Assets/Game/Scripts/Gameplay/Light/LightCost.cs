using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// How many orbs of light a blow is worth: one, two or three, by how hard it hits.
    /// <para>
    /// One asset shared by everything that counts blows in orbs — the player's health, which loses
    /// that many, and every creature that drops them when hurt. Two copies of the thresholds would
    /// let a goblin shed orbs for a hit the player's lantern counts as smaller, and nobody would see
    /// why the two disagreed.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Eclipse/Light Cost", fileName = "LightCost")]
    public sealed class LightCost : ScriptableObject
    {
        [Tooltip("A blow doing at least this much is worth two orbs. Anything lighter is worth one.")]
        [SerializeField] private int twoOrbsFromDamage = 20;

        [Tooltip("A blow doing at least this much is worth three orbs.")]
        [SerializeField] private int threeOrbsFromDamage = 30;

        /// <summary>Orbs a blow of <paramref name="damage"/> is worth. Nothing for a blow that does none.</summary>
        public int OrbsFor(int damage)
        {
            if (damage <= 0) return 0;
            if (damage >= threeOrbsFromDamage) return 3;
            return damage >= twoOrbsFromDamage ? 2 : 1;
        }

        // A three-orb threshold below the two-orb one would make the middle tier unreachable, and the
        // damage that should be worth two would silently be worth three.
        private void OnValidate()
        {
            twoOrbsFromDamage = Mathf.Max(twoOrbsFromDamage, 2);
            threeOrbsFromDamage = Mathf.Max(threeOrbsFromDamage, twoOrbsFromDamage + 1);
        }
    }
}
