// A place that has its own music.
//
// A registry and a radius rather than a trigger collider, for the same reason EnemyAlert is a
// registry: the thing being searched is always a music zone, there are two of them in the whole
// world, and walking a list of two is cheaper than adding two more colliders to a physics scene
// that the player, the enemies and every swing already query. It also cannot be entered by a
// falling orb or left behind by a teleport that skipped the trigger.
using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MusicZone : MonoBehaviour
    {
        [Tooltip("What the score plays inside this place.")]
        [SerializeField] private MusicMood mood = MusicMood.Castle;

        [Tooltip("How far out from this object the place reaches, in metres. For a castle this is " +
                 "the shelf it stands on plus half the hillside, so the music arrives while the " +
                 "player is still climbing towards it rather than as they step over a line.")]
        [Min(0f)]
        [SerializeField] private float radius = 60f;

        [Tooltip("Keep this music through a fight instead of handing over to combat. True for a " +
                 "castle: its garrison is most of the fighting in the game, and a castle that " +
                 "sounded like every other fight the moment it defended itself would never be " +
                 "heard as a castle at all.")]
        [SerializeField] private bool holdsThroughFights = true;

        private static readonly List<MusicZone> Zones = new List<MusicZone>();

        public MusicMood Mood => mood;
        public bool HoldsThroughFights => holdsThroughFights;

        private void OnEnable() => Zones.Add(this);

        private void OnDisable() => Zones.Remove(this);

        /// <summary>
        /// The zone <paramref name="position"/> is standing in, or null for open ground. Where two
        /// overlap the tighter one wins — measured as a fraction of each one's radius, so a small
        /// place inside a large one is heard as the small place rather than as whichever happens to
        /// have been registered first.
        /// </summary>
        public static MusicZone At(Vector3 position)
        {
            MusicZone best = null;
            float bestDepth = float.PositiveInfinity;

            for (int i = Zones.Count - 1; i >= 0; i--)
            {
                MusicZone zone = Zones[i];

                // Destroyed without disabling — a castle removed by a rebuild. Dropped here rather
                // than left to accumulate as null entries.
                if (zone == null)
                {
                    Zones.RemoveAt(i);
                    continue;
                }

                if (zone.radius <= 0f) continue;

                float depth = Vector3.Distance(position, zone.transform.position) / zone.radius;
                if (depth > 1f || depth >= bestDepth) continue;

                bestDepth = depth;
                best = zone;
            }

            return best;
        }

        // A static list outlives a play session when Enter Play Mode Options are on, and one still
        // holding last session's zones would put the player in a castle that is no longer there.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Zones.Clear();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.4f, 0.9f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
