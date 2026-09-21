using UnityEngine;

namespace SpaceGame.Gameplay
{
    /// <summary>
    /// How much light a player comes back with.
    ///
    /// <para>
    /// The whole cost of dying in Eclipse, in one number. Light is the health, the currency and the
    /// only thing holding the dark off, so the lantern you stand up holding is the entire penalty —
    /// there is nothing else to take. Coming back full would make death free; coming back empty
    /// would kill you again on the next blow, which is a loop rather than a punishment.
    /// </para>
    /// <para>
    /// A share of the maximum rather than a fixed count, so retuning the lantern's size cannot
    /// silently retune what dying costs. Kept out of the MonoBehaviour because it is the one
    /// decision in a respawn worth arguing about, and an argument buried in an <c>Update</c> is an
    /// argument nothing can test.
    /// </para>
    /// </summary>
    public static class Rekindle
    {
        /// <summary>
        /// The light left in the lantern on standing up: <paramref name="share"/> of
        /// <paramref name="maxLight"/>, never none and never more than the lantern holds.
        ///
        /// <para>
        /// Never none because zero light is death, and a respawn that hands back a dead body would
        /// raise the death screen again from inside the standing up that was meant to close it.
        /// </para>
        /// </summary>
        public static int OnReturn(int maxLight, float share)
        {
            if (maxLight <= 0) return 0;

            return Mathf.Clamp(Mathf.RoundToInt(maxLight * Mathf.Clamp01(share)), 1, maxLight);
        }
    }
}
