using UnityEngine;

namespace SpaceGame.Castle
{
    /// <summary>
    /// A flat shelf stamped into the island's heightmap for a castle to stand on, and the hillside
    /// that carries the land up to it.
    /// <para>
    /// A castle is a rigid mesh and the island is rolling noise, so one of the two has to give. It
    /// is the island: a castle tilted to match a slope looks wrong from every angle, and one left
    /// level over a slope floats at one corner and buries its wall at the other. Flattening the
    /// ground under it is the only version where the outer wall meets the ground all the way round,
    /// which is the thing the player actually notices.
    /// </para>
    /// <para>
    /// The shelf sits ABOVE the land around it rather than being cut into it, because a castle on a
    /// hill is the map's landmark — the thing a player orients by from anywhere on the island
    /// (<c>GDC-L1-LEVEL-0002</c>) and wants to reach long before they can (<c>GDC-L1-LEVEL-0006</c>).
    /// A castle in a hollow is neither.
    /// </para>
    /// <para>
    /// Heights here are the heightmap's own 0..1, not metres, because that is what
    /// <c>TerrainShape</c> works in and converting in both directions around a single multiply
    /// would only add a place for the two to disagree.
    /// </para>
    /// </summary>
    public readonly struct CastleSite
    {
        /// <summary>Where the castle stands, in terrain-local metres from the terrain's corner.</summary>
        public readonly Vector2 Centre;

        /// <summary>Dead flat out to here, in metres. Must cover the whole footprint.</summary>
        public readonly float PlateauRadius;

        /// <summary>Metres beyond the plateau over which the land returns to its own shape.</summary>
        public readonly float SkirtWidth;

        /// <summary>Height of the shelf, in the heightmap's 0..1.</summary>
        public readonly float Height;

        public CastleSite(Vector2 centre, float plateauRadius, float skirtWidth, float height)
        {
            Centre = centre;

            // Guarded rather than trusted: a zero skirt is a division by zero in Reshape, and it
            // arrives as a cliff of NaN heights rather than as anything anyone would read as a
            // bad argument.
            PlateauRadius = Mathf.Max(plateauRadius, 0f);
            SkirtWidth = Mathf.Max(skirtWidth, 0.01f);
            Height = Mathf.Clamp01(height);
        }

        /// <summary>How far out the site changes the land at all.</summary>
        public float Reach => PlateauRadius + SkirtWidth;

        /// <summary>
        /// The height the land should have at this point: the shelf inside the plateau, the land's
        /// own height outside the skirt, and a smooth hillside between.
        /// </summary>
        /// <param name="existing">The height the noise gave this point.</param>
        public float Reshape(float x, float z, float existing)
        {
            float distance = Vector2.Distance(new Vector2(x, z), Centre);
            if (distance <= PlateauRadius) return Height;
            if (distance >= Reach) return existing;

            // SmoothStep rather than a straight ramp so the hillside meets both the shelf and the
            // land at zero gradient. A linear blend leaves a crease at each end, and a crease that
            // runs all the way round the castle reads as a construction line.
            float t = Mathf.SmoothStep(0f, 1f, (distance - PlateauRadius) / SkirtWidth);
            return Mathf.Lerp(Height, existing, t);
        }

        /// <summary>Whether this site changes the land at this point at all.</summary>
        public bool Touches(float x, float z) =>
            Vector2.SqrMagnitude(new Vector2(x, z) - Centre) < Reach * Reach;
    }
}
