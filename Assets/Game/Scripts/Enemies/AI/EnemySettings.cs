// The numbers that make one enemy feel different from another.
//
// A struct rather than fields on the brain so that every tunable arrives from one place — the
// Inspector, via EnemyAgent — and a test can construct a deliberately extreme set (no aggro memory,
// a leash of nothing) without touching a prefab.
namespace SpaceGame.Enemies
{
    public struct EnemySettings
    {
        /// <summary>How close it has to be before it stops walking and starts swinging.</summary>
        public float AttackRange;

        /// <summary>Seconds between swings.</summary>
        public float AttackCooldown;

        /// <summary>
        /// How far from camp it will chase before giving up. This is what stops one goblin
        /// following you across the whole map and leaving its camp undefended.
        /// </summary>
        public float LeashRadius;

        /// <summary>
        /// Seconds it stays angry after losing sight. Not a flag, because a flag means breaking
        /// line of sight behind a rock switches it off mid-swing.
        /// </summary>
        public float AggroMemory;

        /// <summary>How far from camp it is willing to stroll while calm.</summary>
        public float WanderRadius;

        /// <summary>Seconds it stands still between strolls.</summary>
        public float WanderPause;

        /// <summary>Close enough to count as "back at camp".</summary>
        public float ArrivalRadius;

        /// <summary>Metres per second while strolling or walking back to camp.</summary>
        public float WalkSpeed;

        /// <summary>Metres per second while closing on the target. Also what full throttle means to the animator.</summary>
        public float ChaseSpeed;

        /// <summary>Sensible values for a melee goblin, and what EnemyAgent's Inspector defaults to.</summary>
        public static EnemySettings Default => new EnemySettings
        {
            WalkSpeed = 3.5f,
            ChaseSpeed = 3.5f,
            AttackRange = 2.2f,
            AttackCooldown = 1.4f,
            LeashRadius = 30f,
            AggroMemory = 5f,
            WanderRadius = 12f,
            WanderPause = 2f,
            ArrivalRadius = 1f,
        };
    }
}
