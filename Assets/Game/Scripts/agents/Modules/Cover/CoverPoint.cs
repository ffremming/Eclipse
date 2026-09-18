// Marker placed on a rock, wall, or ruin that entities can hide behind.
// Self-registers in CoverPointRegistry on enable so CoverModule never calls FindObjectsByType.
// Just drop this on any cover object in the scene — no configuration needed.
using System.Collections.Generic;
using UnityEngine;
using SpaceGame.Presentation;

namespace SpaceGame.Agents
{
    public class CoverPoint : MonoBehaviour
    {
        [Tooltip("How many entities can use this cover simultaneously.")]
        [SerializeField] private int maxOccupants = 1;

        private int currentOccupants;

        public bool IsAvailable => currentOccupants < maxOccupants;
        public Vector3 Position => transform.position;

        /// <summary>
        /// How many agents currently claim this point. Exposed for the save system, which does not
        /// store the count — it is rebuilt from the agents that re-claim their reservation on load,
        /// which is the only reading that cannot drift out of step with them.
        /// </summary>
        public int CurrentOccupants => currentOccupants;

        private string stableId;

        /// <summary>
        /// An identity that survives a reload of the scene, so an agent can name the cover it holds.
        ///
        /// Derived from where the point sits in its scene rather than stored, because a cover point
        /// is a bare marker and nothing authors an id on it. Stable across sessions, not across
        /// edits to the scene — a cover point that moves in the hierarchy is a new point, and the
        /// agent that had claimed it simply picks a fresh one.
        ///
        /// Cached: the derivation allocates and it is asked for once per lookup.
        /// </summary>
        public string StableId =>
            !string.IsNullOrEmpty(stableId) ? stableId : stableId = DeriveId(gameObject);

        /// <summary>
        /// The scene path of <paramref name="go"/>, hashed. Includes each step's sibling index so
        /// two identically named children of one parent stay distinguishable.
        /// </summary>
        private static string DeriveId(GameObject go)
        {
            if (go == null) return string.Empty;

            var steps = new List<string>();
            for (Transform t = go.transform; t != null; t = t.parent)
                steps.Add($"{t.GetSiblingIndex()}:{t.name}");

            var path = new System.Text.StringBuilder(go.scene.name);
            for (int i = steps.Count - 1; i >= 0; i--)
                path.Append('/').Append(steps[i]);

            return "cover" + Hash(path.ToString());
        }

        /// <summary>
        /// FNV-1a, written out rather than taken from <c>string.GetHashCode</c>, which Unity does
        /// not guarantee to be stable between runs or platforms — and an identity that changes
        /// between runs is not an identity.
        /// </summary>
        private static string Hash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }

        private void OnEnable()  => CoverPointRegistry.Register(this);
        private void OnDisable() => CoverPointRegistry.Unregister(this);

        public bool TryOccupy()
        {
            if (!IsAvailable)
                return false;
            currentOccupants++;
            return true;
        }

        public void Vacate()
        {
            currentOccupants = Mathf.Max(0, currentOccupants - 1);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsAvailable ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);
        }
    }

    // O(1) registry — mirrors EntityTargetRegistry and HerdModule patterns.
    public static class CoverPointRegistry
    {
        private static readonly List<CoverPoint> s_all = new();

        public static void Register(CoverPoint cp)
        {
            if (!s_all.Contains(cp))
                s_all.Add(cp);
        }

        public static void Unregister(CoverPoint cp) => s_all.Remove(cp);

        /// <summary>
        /// The registered point with this <see cref="CoverPoint.StableId"/>, or null if its scene is
        /// not loaded. Null is an ordinary answer during a load — a chunk may still be streaming —
        /// so a caller should retry rather than give up.
        ///
        /// A linear scan on purpose: this runs a handful of times per load, never per frame, and a
        /// dictionary keyed on a derived id would have to be invalidated every time a point moved.
        /// </summary>
        public static CoverPoint FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (CoverPoint cp in s_all)
            {
                if (cp && cp.StableId == id)
                    return cp;
            }

            return null;
        }

        // Returns the best available cover relative to 'self' within 'searchRadius', given 'threatPos'.
        // Returns null if nothing is available.
        public static CoverPoint FindBest(Vector3 self, Vector3 threatPos, float searchRadius)
        {
            CoverPoint best = null;
            float bestScore = float.MinValue;

            foreach (CoverPoint cp in s_all)
            {
                if (!cp || !cp.IsAvailable)
                    continue;

                float distFromSelf = Vector3.Distance(self, cp.Position);
                if (distFromSelf > searchRadius)
                    continue;

                float distFromThreat = Vector3.Distance(threatPos, cp.Position);
                float score = distFromThreat - distFromSelf * 0.5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = cp;
                }
            }

            return best;
        }
    }
}
