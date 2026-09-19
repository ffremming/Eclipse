using UnityEngine;

namespace SpaceGame.Chain
{
    /// <summary>
    /// What the chain asks of the world it swings through: "if this point moves there, what does it
    /// hit?"
    /// <para>
    /// A seam rather than a call to <c>Physics</c>, so the rope can be stepped in an EditMode test
    /// against a floor made of one line of arithmetic, and so the question of which colliders count
    /// (not the holder's own, for one) is answered by the item that knows its holder and not by
    /// the rope.
    /// </para>
    /// </summary>
    public interface IChainWorld
    {
        /// <summary>
        /// Sweep a sphere of <paramref name="radius"/> from <paramref name="from"/> to
        /// <paramref name="to"/>. On a hit, <paramref name="centre"/> is where the sphere's centre
        /// stopped and <paramref name="normal"/> is the surface it stopped against.
        /// </summary>
        bool Sweep(Vector3 from, Vector3 to, float radius, out Vector3 centre, out Vector3 normal);
    }
}
