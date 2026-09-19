using UnityEngine;

namespace SpaceGame.EditorTools
{
    /// <summary>
    /// One creature built from the sculpt base: which model it wears, what it is made of, and how
    /// it fights.
    /// <para>
    /// A table rather than numbers spread through a builder, for the reason
    /// <see cref="CastlePlacement"/> is one: these are design decisions the player meets head-on.
    /// Whether the crumpy is the fast frail one and the alien the slow heavy one is the whole
    /// difference between them, and it should be readable in one screen rather than reconstructed
    /// from a prefab diff.
    /// </para>
    /// <para>
    /// They differ in KIND, not in tier (<c>GDC-L1-BAL-0004</c>): the crumpy asks the player to
    /// keep moving and the alien asks them to wait for a gap, so neither is simply the other one
    /// with bigger numbers. The cost of that is real — asymmetry is only fair once it has been
    /// played (<c>GDC-L1-BAL-0003</c>), and none of these numbers have been through a fight yet.
    /// They are a starting point tuned against the goblin's, not a balanced roster.
    /// </para>
    /// </summary>
    public readonly struct SculptEnemy
    {
        /// <summary>What the prefab and its materials are called: Alien, Crumpy.</summary>
        public readonly string Name;

        /// <summary>The imported character model.</summary>
        public readonly string ModelPath;

        /// <summary>The dark blade it carries, by prefab name.</summary>
        public readonly string WeaponName;

        /// <summary>
        /// Skin tint over the sculpt's base map, and how wet the skin looks.
        /// <para>
        /// The map is the human's, and it is warm orange, so the tint MULTIPLIES against a red
        /// channel three times the blue one. Anything with red left in it comes out as human skin
        /// however cold the tint looks in the swatch — the only way to a creature that is not
        /// flesh-coloured is to crush red and let green and blue climb.
        /// </para>
        /// </summary>
        public readonly Color SkinTint;
        public readonly float SkinSmoothness;

        /// <summary>
        /// What its eyes give off. The one light on a creature of the dark, and it is there to be
        /// seen by: two points at a known height are how a player finds one before it is on them
        /// (<c>GDC-L1-FEEL-0004</c>). Kept dim enough to light nothing.
        /// </summary>
        public readonly Color EyeGlow;

        public readonly int Health;

        /// <summary>How tall it stands, in metres — its collider, its agent and where its eyes sit.</summary>
        public readonly float BodyHeight;
        public readonly float BodyRadius;

        /// <summary>What one swing does, and how long the player has to read it coming.</summary>
        public readonly int Damage;
        public readonly float Reach;
        public readonly float Windup;
        public readonly float HitWindow;

        /// <summary>How it closes and how often it commits.</summary>
        public readonly float AttackRange;
        public readonly float AttackCooldown;
        public readonly float WalkSpeed;
        public readonly float ChaseSpeed;

        /// <summary>
        /// How far off it picks the player out. Long on both, and deliberately: an enemy that only
        /// wakes at conversational range hands the player the choice of every fight, and the camps
        /// stand in open ground where being seen crossing it is the encounter
        /// (<c>GDC-L1-LEVEL-0002</c>). What still separates the two is WHEN, not whether — the
        /// alien sees furthest and is the one that starts walking first.
        /// </summary>
        public readonly float SightRange;

        public SculptEnemy(string name, string modelPath, string weaponName, Color skinTint,
                           float skinSmoothness, Color eyeGlow, int health, float bodyHeight,
                           float bodyRadius, int damage, float reach, float windup, float hitWindow,
                           float attackRange, float attackCooldown, float walkSpeed, float chaseSpeed,
                           float sightRange)
        {
            Name = name;
            ModelPath = modelPath;
            WeaponName = weaponName;
            SkinTint = skinTint;
            SkinSmoothness = skinSmoothness;
            EyeGlow = eyeGlow;
            Health = health;
            BodyHeight = bodyHeight;
            BodyRadius = bodyRadius;
            Damage = damage;
            Reach = reach;
            Windup = windup;
            HitWindow = hitWindow;
            AttackRange = attackRange;
            AttackCooldown = attackCooldown;
            WalkSpeed = walkSpeed;
            ChaseSpeed = chaseSpeed;
            SightRange = sightRange;
        }

        /// <summary>Where the swing is measured from, and where the eyes look from.</summary>
        public float ChestHeight => BodyHeight * 0.74f;
        public float EyeHeight => BodyHeight * 0.92f;
    }

    /// <summary>The two creatures the sculpt base has so far, beside the player's own body.</summary>
    public static class SculptEnemies
    {
        /// <summary>
        /// Long, gaunt and quick, with a drooping face and arms past its knees. The common enemy of
        /// the island: it carries the hooked khopesh, swings early and often, and dies fast. Reach
        /// out of proportion to its build, because those arms are what the player has to learn.
        /// </summary>
        public static readonly SculptEnemy Crumpy = new SculptEnemy(
            name: "Crumpy",
            modelPath: "Assets/Game/Art/Models/Characters/Crumpy/Crumpy.fbx",
            weaponName: "DarkKhopesh",
            skinTint: new Color(0.30f, 0.55f, 0.45f),
            skinSmoothness: 0.12f,
            eyeGlow: new Color(0.35f, 0.16f, 0.62f),
            health: 65,
            bodyHeight: 1.88f,
            bodyRadius: 0.32f,
            damage: 9,
            reach: 2.9f,
            windup: 0.22f,
            hitWindow: 0.14f,
            attackRange: 2.4f,
            attackCooldown: 1.05f,
            walkSpeed: 3.2f,
            chaseSpeed: 4.2f,
            sightRange: 45f);

        /// <summary>
        /// Broad, deliberate and hard to put down, with the eyes of something that saw you first.
        /// Rare, and the fight changes when one is there: it carries the axe, telegraphs a long
        /// windup and takes a third of a lantern to kill — but it is also the largest single
        /// payout of light on the island, because an enemy sheds orbs for what it is worth.
        /// </summary>
        public static readonly SculptEnemy Alien = new SculptEnemy(
            name: "Alien",
            modelPath: "Assets/Game/Art/Models/Characters/Alien/Alien.fbx",
            weaponName: "DarkAxe",
            skinTint: new Color(0.30f, 0.52f, 1f),
            skinSmoothness: 0.34f,
            eyeGlow: new Color(0.16f, 0.52f, 0.60f),
            health: 190,
            bodyHeight: 1.92f,
            bodyRadius: 0.38f,
            damage: 26,
            reach: 3.1f,
            windup: 0.52f,
            hitWindow: 0.22f,
            attackRange: 2.6f,
            attackCooldown: 2f,
            walkSpeed: 2.6f,
            chaseSpeed: 3.1f,
            sightRange: 60f);

        public static SculptEnemy[] All => new[] { Crumpy, Alien };
    }
}
