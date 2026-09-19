using System.Collections.Generic;
using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// The camps of aliens and crumpies on the island: where they are, who is in them, and so how
    /// often the player meets each creature.
    /// <para>
    /// Rarity is this table and nothing else. There is no spawn chance anywhere in the game — an
    /// enemy is a thing standing in a place (see <c>EnemyBase</c>, which is level design rather
    /// than a spawner), so "the crumpy is common and the alien is rare" means thirteen crumpies in
    /// three camps and three aliens in two. That is a number you can count on a walk across the
    /// island, and it cannot drift with a random roll.
    /// </para>
    /// <para>
    /// The camps sit in the open middle of the island rather than up on its ridges, because that is
    /// the ground the fights want: these two both close to melee, and a brawl on a 40-degree slope
    /// is a brawl with the camera in the dirt (<c>GDC-L1-LEVEL-0008</c>). They are kept well clear
    /// of both castles, so a camp and a garrison are never one fight by accident.
    /// </para>
    /// </summary>
    public static class EnemyCampPlacement
    {
        /// <summary>
        /// Ground below this is the beach or the sea. Matches NatureWorldBuilder's planting floor:
        /// what is too wet to plant on is too wet to camp on.
        /// </summary>
        public const float MinHeight = 8.5f;

        /// <summary>Steepest ground, in degrees, a camp will settle on.</summary>
        public const float MaxSteepness = 18f;

        /// <summary>How far from its nominal centre a camp may wander to find that ground.</summary>
        public const float SearchRadius = 55f;

        /// <summary>
        /// The ring they stand in, from the middle of camp. Wider than a castle garrison's inner
        /// ring and much smaller than its outer one: a camp is a handful of creatures round a spot,
        /// and one whose ring is bigger than the player's sight line reads as several lone enemies
        /// who happen to be nearby.
        /// </summary>
        public const float RingInner = 4f;
        public const float RingOuter = 11f;

        /// <summary>Metres of random offset per creature, so a camp does not read as a pattern.</summary>
        public const float RingJitter = 1.2f;

        /// <summary>How far they will stroll while nothing is happening.</summary>
        public const float WanderRadius = 14f;

        /// <summary>
        /// Walk inside this and the whole camp comes, seen or not. Four metres past the stroll, so
        /// there is an approach where they are visible and not yet coming — which is where the
        /// player gets to choose whether to have this fight.
        /// </summary>
        public const float DefendRadius = 18f;

        /// <summary>
        /// The camp that is a PREFAB rather than a place: both creatures, ready to drag into any
        /// scene, the way <c>GoblinCamp.prefab</c> was before the goblins were cut.
        /// <para>
        /// Deliberately not in <see cref="All"/> — it is not a fifth camp on the island, it is a
        /// camp a designer places by hand. Its centre is therefore unused: a prefab stands wherever
        /// it is dropped, and the ring is laid out around its own origin.
        /// </para>
        /// <para>
        /// Two aliens rather than the one the island's mixed camp has. A camp being dropped
        /// deliberately is a camp someone wants a fight from, and the alien is the half of the
        /// roster that makes it one — the crumpies set the pace, the aliens decide how long it
        /// takes. Change the counts here and rebuild; nothing is authored in the prefab itself.
        /// </para>
        /// </summary>
        public static EnemyCamp Mixed => new EnemyCamp(
            "DarkCamp", seedOffset: 300, centre: Vector2.zero,
            new CampRoster(SculptEnemies.Crumpy, 4),
            new CampRoster(SculptEnemies.Alien, 2));

        public static EnemyCamp[] All => new[]
        {
            // Three crumpy camps, spread so that crossing the island from any direction runs into
            // one. This is the enemy the player learns the game on.
            new EnemyCamp("Fernhollow", seedOffset: 301, centre: new Vector2(-60f, 20f),
                          new CampRoster(SculptEnemies.Crumpy, 5)),

            new EnemyCamp("Driftline", seedOffset: 302, centre: new Vector2(30f, 175f),
                          new CampRoster(SculptEnemies.Crumpy, 4)),

            // The one camp where the two are mixed. Meeting the first alien inside a fight the
            // player already knows how to win is what makes it land as a surprise rather than as a
            // new enemy type being introduced (GDC-L1-LEVEL-0004).
            new EnemyCamp("Sunkenrow", seedOffset: 303, centre: new Vector2(150f, -40f),
                          new CampRoster(SculptEnemies.Crumpy, 4),
                          new CampRoster(SculptEnemies.Alien, 1)),

            // Two aliens and nothing else, in the far south. The hardest fight on the island and
            // the largest pool of light on it, out where the player has to choose to go.
            new EnemyCamp("Longwatch", seedOffset: 304, centre: new Vector2(10f, -165f),
                          new CampRoster(SculptEnemies.Alien, 2)),
        };
    }

    /// <summary>How many of one creature stand in a camp.</summary>
    public readonly struct CampRoster
    {
        public readonly SculptEnemy Species;
        public readonly int Count;

        public CampRoster(SculptEnemy species, int count)
        {
            Species = species;
            Count = count;
        }
    }

    /// <summary>One creature of a roster: which it is, and which of its kind it is.</summary>
    public readonly struct CampMember
    {
        public readonly SculptEnemy Species;

        /// <summary>Its number among its own kind, from 1. What it is called in the scene.</summary>
        public readonly int Ordinal;

        public CampMember(SculptEnemy species, int ordinal)
        {
            Species = species;
            Ordinal = ordinal;
        }

        public string Name => $"{Species.Name} {Ordinal:00}";
    }

    /// <summary>
    /// Turns "four crumpies and two aliens" into six creatures in the order they are placed.
    /// <para>
    /// Shared because both the castles and the camps place a roster, and the only thing they
    /// genuinely disagree about is the ground. Counting the roster twice in two files is how one
    /// of them ends up laying out five creatures into six positions.
    /// </para>
    /// </summary>
    public static class CampRosters
    {
        public static IReadOnlyList<CampMember> Expand(CampRoster[] roster)
        {
            var members = new List<CampMember>();
            if (roster == null) return members;

            foreach (CampRoster entry in roster)
                for (int index = 0; index < entry.Count; index++)
                    members.Add(new CampMember(entry.Species, index + 1));

            return members;
        }
    }

    /// <summary>One camp: a place, a size, and who is in it.</summary>
    public readonly struct EnemyCamp
    {
        /// <summary>What the camp's root object is called in the scene.</summary>
        public readonly string Name;

        /// <summary>
        /// Added to the world seed so the camps lay out differently. A number rather than a hash of
        /// the name, for the reason <see cref="CastleStand.SeedOffset"/> gives.
        /// </summary>
        public readonly int SeedOffset;

        /// <summary>Where it wants to be, in world metres on the XZ plane. The ground has a vote.</summary>
        public readonly Vector2 Centre;

        public readonly CampRoster[] Roster;

        public EnemyCamp(string name, int seedOffset, Vector2 centre, params CampRoster[] roster)
        {
            Name = name;
            SeedOffset = seedOffset;
            Centre = centre;
            Roster = roster;
        }

        /// <summary>How many creatures stand here, across every entry of the roster.</summary>
        public int Population
        {
            get
            {
                int total = 0;
                foreach (CampRoster entry in Roster) total += entry.Count;
                return total;
            }
        }

        /// <summary>Everyone in the camp, in the order they are placed around the ring.</summary>
        public IReadOnlyList<CampMember> Members => CampRosters.Expand(Roster);
    }
}
