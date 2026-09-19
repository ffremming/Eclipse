using System.Collections.Generic;
using SpaceGame.Castle;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// Where the two castles stand, and how big a shelf each needs under it.
    /// <para>
    /// One table read by two builders. <see cref="NatureWorldBuilder"/> needs it to flatten the
    /// ground before the terrain is written, and <see cref="CastleBuilder"/> needs it to stand the
    /// castle on that ground afterwards. If each kept its own copy, moving a castle would move the
    /// building without moving the hill it is on — and the failure is a castle floating a few
    /// metres over a hillside, which is only visible from inside the scene.
    /// </para>
    /// </summary>
    public static class CastlePlacement
    {
        /// <summary>
        /// The keep alone: the first castle, six of them, one locked door. Placed inland and well
        /// inside the island, because it is the one the player is meant to find first.
        /// </summary>
        public static readonly CastleStand Keep = new CastleStand(
            name: "Watchkeep",
            seedOffset: 101,
            centre: new Vector2(75f, -70f),
            plateauRadius: 26f,
            skirtWidth: 30f,
            lift: 0.055f,
            roster: new[]
            {
                new CampRoster(SculptEnemies.Crumpy, 5),
                new CampRoster(SculptEnemies.Alien, 1),
            },
            garrisonInner: 17f,
            garrisonOuter: 24f,
            towerKeyBearers: 1);

        /// <summary>
        /// The whole castle: fifteen of them, the outer gate and the keep's door. Out towards the
        /// shore, on the highest ground it can find there, so its tower is the thing on the
        /// horizon the player wants to reach long before they can (<c>GDC-L1-LEVEL-0006</c>) and
        /// the landmark they orient by afterwards (<c>GDC-L1-LEVEL-0002</c>).
        /// </summary>
        public static readonly CastleStand Full = new CastleStand(
            name: "Castle",
            seedOffset: 202,
            centre: new Vector2(-145f, 120f),
            plateauRadius: 46f,
            skirtWidth: 34f,
            lift: 0.085f,
            roster: new[]
            {
                new CampRoster(SculptEnemies.Crumpy, 12),
                new CampRoster(SculptEnemies.Alien, 3),
            },
            garrisonInner: 16f,
            garrisonOuter: 29f,
            towerKeyBearers: 0);

        public static IReadOnlyList<CastleStand> All => new[] { Keep, Full };

        /// <summary>
        /// The shelves to stamp into the heightmap, in the terrain-local metres
        /// <see cref="TerrainShape.Heights"/> works in.
        /// </summary>
        /// <param name="sizeMetres">Side length of the terrain.</param>
        public static List<CastleSite> Sites(float sizeMetres, int seed)
        {
            var sites = new List<CastleSite>();
            foreach (CastleStand stand in All) sites.Add(stand.Site(sizeMetres, seed));
            return sites;
        }
    }

    /// <summary>One castle's place in the world.</summary>
    public readonly struct CastleStand
    {
        /// <summary>What the castle's root object is called in the scene.</summary>
        public readonly string Name;

        /// <summary>
        /// Added to the world seed so the two castles lay their garrisons out differently.
        /// <para>
        /// A number rather than a hash of the name. <c>string.GetHashCode</c> is not promised to be
        /// stable between runtimes or runs, and a seed that is not stable is a castle laid out
        /// differently every time it is built — which no test can catch, because the test would be
        /// computing the same unstable number.
        /// </para>
        /// </summary>
        public readonly int SeedOffset;

        /// <summary>Where it stands, in world metres on the XZ plane.</summary>
        public readonly Vector2 Centre;

        public readonly float PlateauRadius;
        public readonly float SkirtWidth;

        /// <summary>
        /// How far above the ground around it the shelf sits, in the heightmap's 0..1. This is
        /// what makes it a castle ON a hill rather than a castle in a clearing — see
        /// <see cref="CastleSite"/>.
        /// </summary>
        public readonly float Lift;

        /// <summary>
        /// Who holds this castle. A roster rather than a count since the goblins left: the same
        /// mix the island's camps use, crumpies with a few aliens among them, so a garrison is
        /// read the same way a camp is and the alien stays the thing you were not hoping to see.
        /// </summary>
        public readonly CampRoster[] Roster;

        /// <summary>The ring the garrison stands in, from the castle's centre.</summary>
        public readonly float GarrisonInner;
        public readonly float GarrisonOuter;

        /// <summary>
        /// How many of this castle's garrison are carrying the tower key.
        /// <para>
        /// One at the first castle, whose garrison stands on open ground that needs no key to
        /// reach — so the key that opens its keep is behind a fight the player can always start.
        /// None at the full castle: by the time its gate opens the player is already carrying the
        /// tower key, and a second copy would only take a hotbar slot.
        /// </para>
        /// </summary>
        public readonly int TowerKeyBearers;

        public CastleStand(string name, int seedOffset, Vector2 centre, float plateauRadius,
                           float skirtWidth, float lift, CampRoster[] roster, float garrisonInner,
                           float garrisonOuter, int towerKeyBearers)
        {
            TowerKeyBearers = towerKeyBearers;
            Name = name;
            SeedOffset = seedOffset;
            Centre = centre;
            PlateauRadius = plateauRadius;
            SkirtWidth = skirtWidth;
            Lift = lift;
            Roster = roster;
            GarrisonInner = garrisonInner;
            GarrisonOuter = garrisonOuter;
        }

        /// <summary>Everyone in the garrison, in the order they are placed around the courtyard.</summary>
        public IReadOnlyList<CampMember> Garrison => CampRosters.Expand(Roster);

        /// <summary>Terrain-local metres, which is where the heightmap's origin is.</summary>
        public Vector2 TerrainLocalCentre(float sizeMetres) =>
            Centre + new Vector2(sizeMetres, sizeMetres) * 0.5f;

        /// <summary>
        /// The shelf, sat on top of the highest ground the noise made nearby.
        /// <para>
        /// Taken from the highest point rather than the average so the shelf raises the hill that
        /// is already there instead of slicing through its side. Sampled over the plateau plus
        /// half the skirt, because a hill whose summit is just outside the footprint is still the
        /// hill this castle is standing on.
        /// </para>
        /// </summary>
        public CastleSite Site(float sizeMetres, int seed)
        {
            Vector2 local = TerrainLocalCentre(sizeMetres);
            float sampled = TerrainShape.HighestWithin(local, PlateauRadius + SkirtWidth * 0.5f,
                                                       sizeMetres, seed);

            return new CastleSite(local, PlateauRadius, SkirtWidth, sampled + Lift);
        }

        /// <summary>
        /// Where the castle's courtyard actually sits in the world, once the shelf is stamped.
        /// </summary>
        public Vector3 GroundPosition(float sizeMetres, float heightMetres, int seed) =>
            new Vector3(Centre.x, Site(sizeMetres, seed).Height * heightMetres, Centre.y);

        /// <summary>
        /// Which way the castle faces: its gate towards the middle of the island, so the player
        /// meets the front of it on the way out rather than walking round the back.
        /// </summary>
        public Quaternion Rotation()
        {
            Vector3 inward = new Vector3(-Centre.x, 0f, -Centre.y);
            return inward.sqrMagnitude < 1e-4f
                ? Quaternion.identity
                : Quaternion.LookRotation(inward.normalized, Vector3.up);
        }
    }
}
