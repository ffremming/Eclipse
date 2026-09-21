// "He's over here!"
//
// The one piece of shared state the enemies have. An enemy that notices the player shouts, and
// every enemy within earshot goes looking for a fight — which is what turns four creatures standing
// near each other into a group that reacts as one.
//
// A registry rather than a Physics.OverlapSphere because the thing being searched for is always an
// enemy: walking a list of a dozen agents is cheaper than asking the physics scene, and it cannot
// pick up a crate.
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Enemies
{
    public static class EnemyAlert
    {
        private static readonly List<EnemyAgent> Listeners = new List<EnemyAgent>();

        public static void Register(EnemyAgent agent)
        {
            if (agent != null && !Listeners.Contains(agent)) Listeners.Add(agent);
        }

        public static void Unregister(EnemyAgent agent) => Listeners.Remove(agent);

        /// <summary>
        /// Tell every enemy within <paramref name="radius"/> of <paramref name="position"/> that
        /// there is something to fight. The shouter is skipped: it already knows.
        /// </summary>
        public static void Shout(EnemyAgent source, Vector3 position, float radius)
        {
            Prune();
            float sqrRadius = radius * radius;

            foreach (EnemyAgent listener in Listeners)
            {
                if (listener == source) continue;
                if ((listener.transform.position - position).sqrMagnitude > sqrRadius) continue;

                listener.HearAlert();
            }
        }

        /// <summary>
        /// Whether anything within <paramref name="radius"/> of <paramref name="position"/> is
        /// currently closing on something or swinging at it — which is this game's definition of
        /// "a fight is happening here".
        /// <para>
        /// Asked of the same roster the shout walks, rather than of the physics scene, and for the
        /// same reason: the answer is always about enemies, and there are only ever a dozen of them.
        /// </para>
        /// </summary>
        public static bool AnyHunting(Vector3 position, float radius)
        {
            Prune();
            float sqrRadius = radius * radius;

            foreach (EnemyAgent listener in Listeners)
            {
                if (!listener.IsHunting) continue;
                if ((listener.transform.position - position).sqrMagnitude > sqrRadius) continue;

                return true;
            }

            return false;
        }

        // Destroyed without unregistering — a body removed by something other than its own
        // OnDisable. Dropped here rather than left to accumulate as null entries.
        private static void Prune()
        {
            for (int i = Listeners.Count - 1; i >= 0; i--)
                if (Listeners[i] == null) Listeners.RemoveAt(i);
        }

        // Statics outlive a play session when Enter Play Mode Options are on, and a list still
        // holding last session's destroyed agents would have every shout walking over corpses.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Listeners.Clear();
    }
}
