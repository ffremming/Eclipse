using System.Collections.Generic;

namespace SpaceGame.Vegetation
{
    /// <summary>
    /// The points a layout has already placed, in a uniform grid, so asking "is anything within r
    /// of here" costs a look at nine cells instead of a scan over everything placed so far.
    /// <para>
    /// A grid rather than a linear scan because a field of tens of thousands of plants is rebuilt
    /// every time a number in the inspector changes, and the scan is quadratic in the count.
    /// </para>
    /// <para>
    /// <see cref="AnyWithin"/> is exact rather than approximate: a grid that answered "probably"
    /// would change the shape of a field every time the cell size did.
    /// </para>
    /// </summary>
    public sealed class SpatialHash
    {
        private readonly float cellSize;

        /// <summary>
        /// Cell coordinate pair to the indices of the points standing in that cell. Indices rather
        /// than coordinates so a point costs four bytes per cell it is filed under, and so the
        /// coordinates live in one flat pair of lists the sweep can read straight through.
        /// </summary>
        private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();

        private readonly List<float> xs = new List<float>();
        private readonly List<float> zs = new List<float>();

        /// <param name="cellSize">
        /// Ideally at least the largest radius that will ever be queried, because that is the size
        /// at which a query sweeps nine cells and no more. A smaller cell is not wrong — see
        /// <see cref="AnyWithin"/>, which widens its sweep to cover the radius — only slower. Zero
        /// or negative is floored to a hair above zero, since dividing by it would file every point
        /// into one bucket and turn the grid back into the scan it exists to replace.
        /// </param>
        public SpatialHash(float cellSize)
        {
            this.cellSize = cellSize > 0.0001f ? cellSize : 0.0001f;
        }

        /// <summary>How many points have been added. Nothing is ever removed.</summary>
        public int Count => xs.Count;

        /// <summary>Files a point at (<paramref name="x"/>, <paramref name="z"/>) into its cell.</summary>
        public void Add(float x, float z)
        {
            int index = xs.Count;
            xs.Add(x);
            zs.Add(z);

            long key = Key(Cell(x), Cell(z));
            if (!cells.TryGetValue(key, out List<int> bucket))
            {
                // Four is what a well-spaced layer puts in a cell sized to its own spacing, so the
                // common case never grows the list and the rare crowded cell only doubles once.
                bucket = new List<int>(4);
                cells[key] = bucket;
            }

            bucket.Add(index);
        }

        /// <summary>
        /// Whether any added point stands strictly within <paramref name="radius"/> of
        /// (<paramref name="x"/>, <paramref name="z"/>). Strictly, matching the scan it replaces:
        /// a point at exactly the radius does not count as too close.
        /// </summary>
        public bool AnyWithin(float x, float z, float radius)
        {
            if (radius <= 0f) return false;

            // A radius larger than the cell would need a wider sweep than nine cells. Widen it
            // rather than answering wrongly.
            int reach = (int)(radius / cellSize) + 1;
            int centreX = Cell(x);
            int centreZ = Cell(z);
            float radiusSquared = radius * radius;

            for (int cx = centreX - reach; cx <= centreX + reach; cx++)
            {
                for (int cz = centreZ - reach; cz <= centreZ + reach; cz++)
                {
                    if (!cells.TryGetValue(Key(cx, cz), out List<int> bucket)) continue;

                    for (int i = 0; i < bucket.Count; i++)
                    {
                        int index = bucket[i];
                        float dx = xs[index] - x;
                        float dz = zs[index] - z;
                        if (dx * dx + dz * dz < radiusSquared) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The cell coordinate a world coordinate falls in, on one axis.</summary>
        private int Cell(float value)
        {
            // Floor, not truncate: truncation folds -0.5 and 0.5 into the same cell and every
            // point left of the origin then collides with one right of it.
            float scaled = value / cellSize;
            int floored = (int)scaled;
            return scaled < floored ? floored - 1 : floored;
        }

        /// <summary>
        /// The two cell coordinates packed into one dictionary key. Both halves are kept whole, so
        /// two different cells can never share a key and no query has to guard against a collision.
        /// </summary>
        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
