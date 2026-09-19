using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// What a creature standing here would be standing on.
    /// <para>
    /// A raycast rather than <c>Terrain.SampleHeight</c> alone, because the island has things built
    /// on it: a creature placed at the terrain's height inside a castle's plinth is inside solid
    /// geometry and never finds the navmesh. The terrain height is the fallback for the ray missing
    /// everything, which happens over water and off the edge of the world.
    /// </para>
    /// </summary>
    public static class TerrainGround
    {
        /// <summary>How far above the query point the ray starts, in metres.</summary>
        private const float RayStart = 30f;

        /// <summary>How far it travels. Twice the start, so it reaches the same distance below.</summary>
        private const float RayLength = 60f;

        public static float Under(Terrain terrain, Vector3 world)
        {
            Vector3 above = new Vector3(world.x, world.y + RayStart, world.z);

            if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, RayLength, ~0,
                                QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return Surface(terrain, world);
        }

        /// <summary>
        /// The land itself, ignoring anything standing on it.
        /// <para>
        /// What to ask out in the wild, where the only things a ray can hit are boulders and tree
        /// canopies — and a creature placed on top of one of those is a creature standing in the
        /// air above the ground everything else walks on.
        /// </para>
        /// </summary>
        public static float Surface(Terrain terrain, Vector3 world)
            => terrain.SampleHeight(world) + terrain.transform.position.y;
    }
}
